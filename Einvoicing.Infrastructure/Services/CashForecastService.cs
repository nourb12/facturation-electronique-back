using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Services;

public sealed class CashForecastService(
    ContextBaseDeDonnees db,
    ILatePaymentPredictionService latePaymentPredictionService,
    IHttpClientFactory httpClientFactory) : ICashForecastService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<CashForecastDto> ForecastAsync(
        Guid entrepriseId,
        CashForecastRequestDto request,
        CancellationToken ct = default)
    {
        var horizon = Math.Clamp(request.HorizonSemaines, 4, 26);
        var recovery = Math.Clamp(request.TauxRecouvrement, 0, 1);
        var today = DateTime.UtcNow.Date;
        var until = today.AddDays(horizon * 7);

        var invoices = await (
            from facture in db.Factures.AsNoTracking()
            join client in db.Clients.AsNoTracking() on facture.ClientId equals client.Id
            where facture.EntrepriseId == entrepriseId
                && facture.TypeFacture == TypeFacture.Facture
                && facture.Statut != StatutFacture.Brouillon
                && facture.Statut != StatutFacture.Payee
                && facture.Statut != StatutFacture.Annulee
                && facture.Statut != StatutFacture.Rejetee
                && facture.NetAPayer - facture.MontantPaye > 0
                && facture.DateEcheance <= until
            select new InvoiceSource(
                facture.Id,
                facture.Numero,
                client.Nom,
                facture.DateEcheance,
                facture.NetAPayer - facture.MontantPaye))
            .ToListAsync(ct);

        var forecastInvoices = new List<ForecastInvoiceRequest>();
        // Transactions of type Sortie are the recorded supplier, payroll, tax and operating payments.
        var outflows = await db.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.EntrepriseId == entrepriseId
                && transaction.Type == TypeTransaction.Sortie
                && transaction.Date.Date >= today
                && transaction.Date.Date <= until)
            .OrderBy(transaction => transaction.Date)
            .Select(transaction => new ForecastOutflowRequest(
                transaction.Id.ToString(), transaction.Libelle, transaction.Date.Date,
                (double)transaction.Montant, transaction.CategorieNom ?? "Charge"))
            .ToListAsync(ct);

        try
        {
            foreach (var invoice in invoices)
            {
                var prediction = await latePaymentPredictionService.PredireAsync(invoice.Id, entrepriseId, ct);
                forecastInvoices.Add(new ForecastInvoiceRequest(
                    invoice.Id.ToString(), invoice.Numero, invoice.Client, invoice.DateEcheance.Date,
                    (double)invoice.Amount, (double)prediction.ProbabiliteRetard));
            }

            var payload = new ForecastRequest(
                (double)(request.SoldeInitial ?? 0), forecastInvoices, outflows, horizon, (double)recovery);
            var http = httpClientFactory.CreateClient("MachineLearning");
            using var response = await http.PostAsJsonAsync("forecast/cash", payload, JsonOptions, ct);
            response.EnsureSuccessStatusCode();
            var forecast = await response.Content.ReadFromJsonAsync<ForecastResponse>(JsonOptions, ct)
                ?? throw new InvalidOperationException("Le service IA a envoyé une prévision de trésorerie vide.");

            return ToDto(forecast);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsAiServiceFailure(exception))
        {
            return BuildPrudentFallback(
                request.SoldeInitial ?? 0,
                recovery,
                horizon,
                today,
                invoices,
                outflows);
        }
    }

    private static CashForecastDto ToDto(ForecastResponse forecast) => new(
        forecast.Source,
        (decimal)forecast.Assumption.RecoveryRate,
        forecast.Assumption.DelayRule,
        new CashForecastSummaryDto(
            (decimal)forecast.Summary.OpeningBalance,
            (decimal)forecast.Summary.InvoicesTotal,
            (decimal)forecast.Summary.OutflowsTotal,
            (decimal)forecast.Summary.DeferredExpected,
            (decimal)forecast.Summary.FinalOptimistic,
            (decimal)forecast.Summary.FinalRealistic,
            (decimal)forecast.Summary.FinalPessimistic,
            forecast.Summary.Alert),
        forecast.Weekly.Select(week => new CashForecastWeekDto(
            week.WeekStart, week.WeekLabel, (decimal)week.OptimisticInflows,
            (decimal)week.OnTimeExpected, (decimal)week.DeferredExpected,
            (decimal)week.RealisticInflows, (decimal)week.Outflows,
            (decimal)week.OptimisticBalance, (decimal)week.RealisticBalance,
            (decimal)week.PessimisticBalance)).ToList(),
        forecast.InvoiceForecasts.Select(invoice => new CashForecastInvoiceDto(
            Guid.Parse(invoice.InvoiceId), invoice.Number, invoice.Customer, invoice.DueDate,
            (decimal)invoice.Amount, (decimal)invoice.LateProbability,
            (decimal)invoice.OnTimeAmount, (decimal)invoice.DeferredAmount,
            invoice.EstimatedDelayDays, invoice.EstimatedPaymentDate)).ToList());

    // Keeps the forecast available from recorded accounting data if the IA endpoint cannot be reached.
    private static CashForecastDto BuildPrudentFallback(
        decimal openingBalance,
        decimal requestedRecoveryRate,
        int horizon,
        DateTime today,
        IReadOnlyList<InvoiceSource> invoices,
        IReadOnlyList<ForecastOutflowRequest> outflows)
    {
        var prudentRecoveryRate = Math.Min(requestedRecoveryRate, 0.60m);
        var invoiceForecasts = invoices.Select(invoice =>
        {
            var isOverdue = invoice.DateEcheance.Date < today;
            var lateProbability = isOverdue ? 0.75m : 0.55m;
            var onTimeAmount = RoundMoney(invoice.Amount * (1 - lateProbability));
            var deferredAmount = RoundMoney(invoice.Amount * lateProbability * prudentRecoveryRate);
            var delayDays = isOverdue ? 14 : 21;
            var dueCollectionDate = isOverdue ? today : invoice.DateEcheance.Date;
            var estimatedPaymentDate = dueCollectionDate.AddDays(delayDays);
            return new PrudentInvoiceForecast(
                invoice,
                lateProbability,
                onTimeAmount,
                deferredAmount,
                delayDays,
                dueCollectionDate,
                estimatedPaymentDate);
        }).ToList();

        var weeks = new List<CashForecastWeekDto>(horizon);
        var weekStart = StartOfWeek(today);
        var optimisticBalance = openingBalance;
        var realisticBalance = openingBalance;
        var pessimisticBalance = openingBalance;

        for (var weekIndex = 0; weekIndex < horizon; weekIndex += 1)
        {
            var start = weekStart.AddDays(weekIndex * 7);
            var end = start.AddDays(7);
            var optimisticInflows = invoiceForecasts
                .Where(item => IsWithinWeek(item.DueCollectionDate, start, end))
                .Sum(item => item.Invoice.Amount);
            var onTimeExpected = invoiceForecasts
                .Where(item => IsWithinWeek(item.DueCollectionDate, start, end))
                .Sum(item => item.OnTimeAmount);
            var deferredExpected = invoiceForecasts
                .Where(item => IsWithinWeek(item.EstimatedPaymentDate, start, end))
                .Sum(item => item.DeferredAmount);
            var outflowTotal = outflows
                .Where(item => IsWithinWeek(item.ScheduledDate, start, end))
                .Sum(item => (decimal)item.Amount);
            var realisticInflows = onTimeExpected + deferredExpected;

            optimisticBalance += optimisticInflows - outflowTotal;
            realisticBalance += realisticInflows - outflowTotal;
            pessimisticBalance -= outflowTotal;

            weeks.Add(new CashForecastWeekDto(
                start,
                $"Semaine du {start:dd/MM}",
                RoundMoney(optimisticInflows),
                RoundMoney(onTimeExpected),
                RoundMoney(deferredExpected),
                RoundMoney(realisticInflows),
                RoundMoney(outflowTotal),
                RoundMoney(optimisticBalance),
                RoundMoney(realisticBalance),
                RoundMoney(pessimisticBalance)));
        }

        var invoicesTotal = invoiceForecasts.Sum(item => item.Invoice.Amount);
        var outflowsTotal = outflows.Sum(item => (decimal)item.Amount);
        var deferredWithinHorizon = weeks.Sum(week => week.EncaissementsReportes);

        return new CashForecastDto(
            "Repli prudent sur les données réelles",
            prudentRecoveryRate,
            "Hypothèse prudente : 55 % de risque de retard, 75 % pour une facture déjà échue.",
            new CashForecastSummaryDto(
                RoundMoney(openingBalance),
                RoundMoney(invoicesTotal),
                RoundMoney(outflowsTotal),
                RoundMoney(deferredWithinHorizon),
                RoundMoney(optimisticBalance),
                RoundMoney(realisticBalance),
                RoundMoney(pessimisticBalance),
                "Le service IA est indisponible. Ce scénario prudent reste calculé avec vos factures ouvertes et vos sorties planifiées."),
            weeks,
            invoiceForecasts.Select(item => new CashForecastInvoiceDto(
                item.Invoice.Id,
                item.Invoice.Numero,
                item.Invoice.Client,
                item.Invoice.DateEcheance,
                RoundMoney(item.Invoice.Amount),
                item.LateProbability,
                item.OnTimeAmount,
                item.DeferredAmount,
                item.DelayDays,
                item.EstimatedPaymentDate)).ToList());
    }

    private static bool IsAiServiceFailure(Exception exception) => exception is HttpRequestException
        or JsonException
        or InvalidOperationException
        or TaskCanceledException;

    private static DateTime StartOfWeek(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static bool IsWithinWeek(DateTime date, DateTime start, DateTime end) =>
        date.Date >= start && date.Date < end;

    private static decimal RoundMoney(decimal amount) => Math.Round(amount, 3, MidpointRounding.AwayFromZero);

    private sealed record InvoiceSource(Guid Id, string Numero, string Client, DateTime DateEcheance, decimal Amount);
    private sealed record PrudentInvoiceForecast(
        InvoiceSource Invoice,
        decimal LateProbability,
        decimal OnTimeAmount,
        decimal DeferredAmount,
        int DelayDays,
        DateTime DueCollectionDate,
        DateTime EstimatedPaymentDate);
    private sealed record ForecastInvoiceRequest(
        [property: JsonPropertyName("invoice_id")] string InvoiceId,
        [property: JsonPropertyName("number")] string Number,
        [property: JsonPropertyName("customer")] string Customer,
        [property: JsonPropertyName("due_date")] DateTime DueDate,
        [property: JsonPropertyName("amount")] double Amount,
        [property: JsonPropertyName("late_probability")] double LateProbability);
    private sealed record ForecastOutflowRequest(
        [property: JsonPropertyName("outflow_id")] string OutflowId,
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("scheduled_date")] DateTime ScheduledDate,
        [property: JsonPropertyName("amount")] double Amount,
        [property: JsonPropertyName("category")] string Category);
    private sealed record ForecastRequest(
        [property: JsonPropertyName("opening_balance")] double OpeningBalance,
        [property: JsonPropertyName("invoices")] IReadOnlyList<ForecastInvoiceRequest> Invoices,
        [property: JsonPropertyName("outflows")] IReadOnlyList<ForecastOutflowRequest> Outflows,
        [property: JsonPropertyName("horizon_weeks")] int HorizonWeeks,
        [property: JsonPropertyName("recovery_rate")] double RecoveryRate);
    private sealed record ForecastResponse(
        string Source,
        ForecastAssumption Assumption,
        ForecastSummary Summary,
        List<ForecastWeek> Weekly,
        [property: JsonPropertyName("invoice_forecasts")] List<ForecastInvoice> InvoiceForecasts);
    private sealed record ForecastAssumption(
        [property: JsonPropertyName("recovery_rate")] double RecoveryRate,
        [property: JsonPropertyName("delay_rule")] string DelayRule);
    private sealed record ForecastSummary(
        [property: JsonPropertyName("opening_balance")] double OpeningBalance,
        [property: JsonPropertyName("invoices_total")] double InvoicesTotal,
        [property: JsonPropertyName("outflows_total")] double OutflowsTotal,
        [property: JsonPropertyName("deferred_expected")] double DeferredExpected,
        [property: JsonPropertyName("final_optimistic")] double FinalOptimistic,
        [property: JsonPropertyName("final_realistic")] double FinalRealistic,
        [property: JsonPropertyName("final_pessimistic")] double FinalPessimistic,
        string Alert);
    private sealed record ForecastWeek(
        [property: JsonPropertyName("week_start")] DateTime WeekStart,
        [property: JsonPropertyName("week_label")] string WeekLabel,
        [property: JsonPropertyName("optimistic_inflows")] double OptimisticInflows,
        [property: JsonPropertyName("on_time_expected")] double OnTimeExpected,
        [property: JsonPropertyName("deferred_expected")] double DeferredExpected,
        double Outflows,
        [property: JsonPropertyName("realistic_inflows")] double RealisticInflows,
        [property: JsonPropertyName("optimistic_balance")] double OptimisticBalance,
        [property: JsonPropertyName("realistic_balance")] double RealisticBalance,
        [property: JsonPropertyName("pessimistic_balance")] double PessimisticBalance);
    private sealed record ForecastInvoice(
        [property: JsonPropertyName("invoice_id")] string InvoiceId,
        string Number,
        string Customer,
        [property: JsonPropertyName("due_date")] DateTime DueDate,
        double Amount,
        [property: JsonPropertyName("late_probability")] double LateProbability,
        [property: JsonPropertyName("on_time_amount")] double OnTimeAmount,
        [property: JsonPropertyName("deferred_amount")] double DeferredAmount,
        [property: JsonPropertyName("estimated_delay_days")] int EstimatedDelayDays,
        [property: JsonPropertyName("estimated_payment_date")] DateTime EstimatedPaymentDate);
}
