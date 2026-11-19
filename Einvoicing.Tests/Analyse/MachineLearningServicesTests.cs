using System.Net;
using System.Text;
using System.Text.Json;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Einvoicing.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Einvoicing.Tests.Analyse;

public sealed class MachineLearningServicesTests
{
    private readonly Guid tenant = Guid.NewGuid();
    private readonly Guid user = Guid.NewGuid();
    private readonly DateTime today = DateTime.UtcNow.Date;

    [Theory]
    [InlineData(1, 4, 2, 0.6)]
    [InlineData(30, 26, -1, 0)]
    public async Task CashForecast_UsesPrudentFallbackAndFiltersCompanyData(
        int requestedWeeks, int expectedWeeks, double rate, double expectedRate)
    {
        await using var db = Store();
        var client = AddClient(db, tenant);
        var otherClient = AddClient(db, Guid.NewGuid());
        var invoice = AddInvoice(db, client, "FAC-OPEN", 5);
        AddInvoice(db, client, "FAC-DRAFT", 3, validate: false);
        AddInvoice(db, client, "FAC-FUTURE", 200);
        AddInvoice(db, otherClient, "FAC-OTHER", 3);
        db.Transactions.AddRange(
            Transaction.Creer(tenant, user, today, "Expense", 50, TypeTransaction.Sortie),
            Transaction.Creer(tenant, user, today, "Income", 900, TypeTransaction.Entree),
            Transaction.Creer(otherClient.EntrepriseId, user, today, "Other expense", 500, TypeTransaction.Sortie));
        await db.SaveChangesAsync();

        using var http = new MlEndpoint(_ => Response("{}", HttpStatusCode.ServiceUnavailable));
        var service = new CashForecastService(db, Predictor(invoice.Id).Object, http);

        var result = await service.ForecastAsync(tenant, new(100m, requestedWeeks, (decimal)rate));

        Assert.StartsWith("Repli prudent", result.Source);
        Assert.Equal((decimal)expectedRate, result.TauxRecouvrement);
        Assert.Equal(expectedWeeks, result.Semaines.Count);
        Assert.Equal(50m, result.Resume.TotalDecaissements);
        Assert.Equal(150m, result.Resume.FinOptimiste);
        Assert.Equal(50m, result.Resume.FinPessimiste);
        var forecast = Assert.Single(result.Factures);
        Assert.Equal(invoice.Id, forecast.FactureId);
        Assert.Equal(45m, forecast.MontantAPayerAEcheance);
        Assert.Equal(55m * (decimal)expectedRate, forecast.MontantReporteAttendu);
        Assert.Equal(21, forecast.DelaiEstimeJours);
    }

    [Fact]
    public async Task CashForecast_SendsInvoicesAndMapsTheModelOutput()
    {
        await using var db = Store();
        var invoice = AddInvoice(db, AddClient(db, tenant), "FAC-ML", 5);
        await db.SaveChangesAsync();
        using var http = new MlEndpoint(_ => Response($$"""
            {"source":"ml","assumption":{"recovery_rate":0.8,"delay_rule":"rule"},
             "summary":{"opening_balance":10,"invoices_total":100,"outflows_total":0,
             "deferred_expected":32,"final_optimistic":110,"final_realistic":102,
             "final_pessimistic":10,"alert":"none"},
             "weekly":[{"week_start":"2026-01-05","week_label":"Week 1","optimistic_inflows":100,
             "on_time_expected":60,"deferred_expected":32,"realistic_inflows":92,"outflows":0,
             "optimistic_balance":110,"realistic_balance":102,"pessimistic_balance":10}],
             "invoice_forecasts":[{"invoice_id":"{{invoice.Id}}","number":"FAC-ML","customer":"Demo",
             "due_date":"2026-01-07","amount":100,"late_probability":0.4,"on_time_amount":60,
             "deferred_amount":32,"estimated_delay_days":12,"estimated_payment_date":"2026-01-19"}]}
            """));
        var service = new CashForecastService(db, Predictor(invoice.Id).Object, http);

        var result = await service.ForecastAsync(tenant, new(10m, 8, .8m));

        Assert.Equal("/forecast/cash", http.Path);
        Assert.Equal(8, http.Payload.GetProperty("horizon_weeks").GetInt32());
        var sent = Assert.Single(http.Payload.GetProperty("invoices").EnumerateArray());
        Assert.Equal(invoice.Id.ToString(), sent.GetProperty("invoice_id").GetString());
        Assert.Equal(.4m, sent.GetProperty("late_probability").GetDecimal());
        Assert.Equal(102m, Assert.Single(result.Semaines).SoldeRealiste);
        Assert.Equal(invoice.Id, Assert.Single(result.Factures).FactureId);
        Assert.Equal("ml", result.Source);
    }

    [Fact]
    public async Task Prediction_ListsOnlyOpenInvoicesAndSendsNormalizedPaymentMode()
    {
        await using var db = Store();
        var client = AddClient(db, tenant);
        var first = AddInvoice(db, client, "FAC-1", 3, ModePaiement.CarteBancaire);
        var second = AddInvoice(db, client, "FAC-2", 5);
        AddInvoice(db, client, "FAC-DRAFT", 2, validate: false);
        AddInvoice(db, AddClient(db, Guid.NewGuid()), "FAC-OTHER", 2);
        await db.SaveChangesAsync();
        using var http = new MlEndpoint(_ => Response("""
            {"prediction":"retard","probabilite_retard":0.4,"niveau_risque":"moyen"}
            """));
        var service = new LatePaymentPredictionService(db, http, NullLogger<LatePaymentPredictionService>.Instance);

        var results = await service.ListerFacturesOuvertesAsync(tenant);

        Assert.Equal(new[] { first.Id, second.Id }, results.Select(x => x.FactureId));
        Assert.All(results, result => Assert.Equal(.4m, result.ProbabiliteRetard));
        Assert.Equal("/predict/late-payment", http.Path);
        Assert.Equal(new[] { "Carte", "Virement" }, http.Bodies.Select(x => x.GetProperty("mode_paiement").GetString()));
        Assert.All(http.Bodies, body => Assert.Equal(0, body.GetProperty("nombre_factures_precedentes").GetInt32()));
    }

    [Fact]
    public async Task OcrScore_ValidatesFieldsAndMapsReviewReasons()
    {
        using var http = new MlEndpoint(_ => Response("""
            {"model":"logistic","error_probability":0.72,"global_status":"review","requires_review":true,
             "fields_to_review":1,"field_assessments":[{"key":"total","label":"Total","value":"10",
             "confidence":0.6,"error_detected":"calculation","status":"review"}],
             "summary":{"character_errors":0,"missing_fields":0,"calculation_inconsistencies":1,"invalid_dates":0}}
            """));
        var service = new OcrErrorDetectionService(http);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ScoreAsync(new("invoice", [])));
        Assert.Empty(http.Bodies);

        var result = await service.ScoreAsync(new("invoice", [new("total", "Total", "10", .6m, true)]));

        Assert.Equal("/predict/ocr-error", http.Path);
        Assert.Equal("invoice", http.Payload.GetProperty("document_type").GetString());
        Assert.True(Assert.Single(http.Payload.GetProperty("fields").EnumerateArray()).GetProperty("required").GetBoolean());
        Assert.True(result.RequiresReview);
        Assert.Equal(.72m, result.ErrorProbability);
        Assert.Equal("calculation", Assert.Single(result.FieldAssessments).ErrorDetected);
        Assert.Equal(1, result.Summary.CalculationInconsistencies);
    }

    [Fact]
    public async Task AnomalyReport_RequiresHistoryAndMapsCompanyTransactions()
    {
        var repo = new Mock<ITransactionRepository>();
        var transactions = new List<Transaction>();
        repo.Setup(x => x.ListerTousAsync(tenant, It.IsAny<FiltreTransactionsRequest>(), default))
            .ReturnsAsync(transactions);
        using var http = new MlEndpoint(_ => Response("""
            {"source":"transactions_tuniflow","selected_model":"isolation_forest",
             "model_description":"Company history","review_note":"Manual review",
             "summary":{"transactions_analyzed":6,"suspected_transactions":1,"normal_transactions":5,
             "selected_model":"isolation_forest"},
             "top_suspicious":[{"transaction_id":"t1","date":"2026-01-05","amount":99,"label":"Charge",
             "counterparty":null,"anomaly_score":0.8,"anomaly_detected":true,"reason":"Unusual amount"}],
             "pca_points":[{"transaction_id":"t1","pca_x":1,"pca_y":2,"anomaly_score":0.8,"anomaly_detected":true}]}
            """));
        var service = new TransactionAnomalyService(http, repo.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetReportAsync(tenant));
        Assert.Empty(http.Bodies);

        transactions.AddRange(Enumerable.Range(1, 6).Select(i =>
            Transaction.Creer(tenant, user, today, "Charge", i, TypeTransaction.Sortie)));
        var result = await service.GetReportAsync(tenant);

        Assert.Equal("/anomaly/company-report", http.Path);
        Assert.Equal(6, http.Payload.GetProperty("transactions").GetArrayLength());
        Assert.Equal(1, result.Summary.SuspectedTransactions);
        Assert.Equal("Unusual amount", Assert.Single(result.TopSuspicious).Reason);
        Assert.True(Assert.Single(result.PcaPoints).AnomalyDetected);
    }

    private Client AddClient(ContextBaseDeDonnees db, Guid company)
    {
        var client = Client.Creer(company, "Demo", "demo@example.com", TypeClient.B2B, "1234567ABM000");
        db.Clients.Add(client);
        return client;
    }

    private Facture AddInvoice(ContextBaseDeDonnees db, Client client, string number, int dueDays,
        ModePaiement mode = ModePaiement.Virement, bool validate = true)
    {
        var invoice = Facture.Creer(client.EntrepriseId, client.Id, user, number,
            TypeFacture.Facture, mode, today.AddDays(dueDays), appliquerTimbre: false);
        invoice.AjouterLigne(LigneFacture.Creer(invoice.Id, 1, "Service", 1, 100, 0));
        if (validate) invoice.ValiderMetier(user);
        db.Factures.Add(invoice);
        return invoice;
    }

    private Mock<ILatePaymentPredictionService> Predictor(Guid id)
    {
        var service = new Mock<ILatePaymentPredictionService>(MockBehavior.Strict);
        service.Setup(x => x.PredireAsync(id, tenant, default))
            .ReturnsAsync(new PredictionRetardDto(id, "retard", .4m, "moyen", 0, 0));
        return service;
    }

    private static ContextBaseDeDonnees Store() => new(new DbContextOptionsBuilder<ContextBaseDeDonnees>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class MlEndpoint(Func<HttpRequestMessage, HttpResponseMessage> reply)
        : HttpMessageHandler, IHttpClientFactory
    {
        public string? Path { get; private set; }
        public List<JsonElement> Bodies { get; } = [];
        public JsonElement Payload => Bodies[^1];

        public HttpClient CreateClient(string name)
        {
            Assert.Equal("MachineLearning", name);
            return new HttpClient(this, disposeHandler: false) { BaseAddress = new Uri("http://ml.test/") };
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri!.AbsolutePath;
            Assert.Equal(HttpMethod.Post, request.Method);
            Bodies.Add(JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(ct)));
            return reply(request);
        }
    }
}

