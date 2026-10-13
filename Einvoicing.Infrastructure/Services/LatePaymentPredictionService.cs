using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Einvoicing.Infrastructure.Services;

public sealed class LatePaymentPredictionService(
    ContextBaseDeDonnees db,
    IHttpClientFactory httpClientFactory,
    ILogger<LatePaymentPredictionService> logger) : ILatePaymentPredictionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PredictionRetardDto> PredireAsync(
        Guid factureId,
        Guid entrepriseId,
        CancellationToken ct = default)
    {
        var facture = await db.Factures
            .AsNoTracking()
            .SingleOrDefaultAsync(f => f.Id == factureId && f.EntrepriseId == entrepriseId, ct)
            ?? throw new NotFoundException("Facture introuvable.");

        var client = await db.Clients
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == facture.ClientId && c.EntrepriseId == entrepriseId, ct)
            ?? throw new NotFoundException("Client introuvable.");

        // Seules les factures antérieures déjà payées servent à construire l'historique.
        var historique = await db.Factures
            .AsNoTracking()
            .Where(f => f.EntrepriseId == entrepriseId
                && f.ClientId == facture.ClientId
                && f.Id != facture.Id
                && f.DateEmission < facture.DateEmission
                && f.Statut == StatutFacture.Payee
                && f.DatePaiement != null)
            .Select(f => new HistoriquePaiement(
                f.DateEcheance,
                f.DatePaiement!.Value))
            .ToListAsync(ct);

        var nombreRetards = historique.Count(h => h.DatePaiement.Date > h.DateEcheance.Date);
        var joursRetard = historique
            .Select(h => Math.Max(0, (h.DatePaiement.Date - h.DateEcheance.Date).Days))
            .ToList();

        var payload = new PredictionMlRequest(
            MontantTtc: (double)facture.TotalTtc,
            JoursDelai: Math.Max(0, (facture.DateEcheance.Date - facture.DateEmission.Date).Days),
            ModePaiement: NormaliserModePaiement(facture.ModePaiement),
            TypeClient: client.TypeClient.ToString(),
            Ville: string.IsNullOrWhiteSpace(client.Ville) ? "Inconnue" : client.Ville.Trim(),
            AncienneteMois: Math.Max(0, (int)((facture.DateEmission.Date - client.CreeLe.Date).TotalDays / 30)),
            NombreFacturesPrecedentes: historique.Count,
            NombreRetardsPrecedents: nombreRetards,
            TauxRetardPrecedent: historique.Count == 0 ? 0 : (double)nombreRetards / historique.Count,
            RetardMoyenJours: joursRetard.Count == 0 ? 0 : joursRetard.Average());

        var http = httpClientFactory.CreateClient("MachineLearning");
        using var response = await http.PostAsJsonAsync("predict/late-payment", payload, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var prediction = await response.Content.ReadFromJsonAsync<PredictionMlResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Le service ML a envoyé une réponse vide.");

        logger.LogInformation(
            "Prédiction de retard calculée pour la facture {FactureId}: {Risk}% ({Level})",
            factureId,
            prediction.ProbabiliteRetard,
            prediction.NiveauRisque);

        return new PredictionRetardDto(
            factureId,
            prediction.Prediction,
            (decimal)prediction.ProbabiliteRetard,
            prediction.NiveauRisque,
            historique.Count,
            nombreRetards);
    }

    public async Task<IReadOnlyList<PredictionRetardFactureDto>> ListerFacturesOuvertesAsync(
        Guid entrepriseId,
        CancellationToken ct = default)
    {
        // This list feeds the decision screen. Only invoices still awaiting payment are scored.
        var factures = await (
            from facture in db.Factures.AsNoTracking()
            join client in db.Clients.AsNoTracking() on facture.ClientId equals client.Id
            where facture.EntrepriseId == entrepriseId
                && facture.TypeFacture == TypeFacture.Facture
                && facture.Statut != StatutFacture.Brouillon
                && facture.Statut != StatutFacture.Payee
                && facture.Statut != StatutFacture.Annulee
                && facture.Statut != StatutFacture.Rejetee
            orderby facture.DateEcheance
            select new FactureOuverte(
                facture.Id,
                facture.Numero,
                facture.ClientId,
                client.Nom,
                client.TypeClient.ToString(),
                facture.DateEcheance,
                facture.TotalTtc,
                facture.Devise)
        ).Take(80).ToListAsync(ct);

        var resultats = new List<PredictionRetardFactureDto>(factures.Count);
        foreach (var facture in factures)
        {
            var prediction = await PredireAsync(facture.Id, entrepriseId, ct);
            resultats.Add(new PredictionRetardFactureDto(
                facture.Id,
                facture.Numero,
                facture.ClientId,
                facture.ClientNom,
                facture.TypeClient,
                facture.DateEcheance,
                facture.MontantTtc,
                facture.Devise,
                prediction.Prediction,
                prediction.ProbabiliteRetard,
                prediction.NiveauRisque,
                prediction.NombreFacturesPrecedentes,
                prediction.NombreRetardsPrecedents));
        }

        return resultats;
    }

    private static string NormaliserModePaiement(ModePaiement mode) => mode switch
    {
        ModePaiement.CarteBancaire => "Carte",
        _ => mode.ToString()
    };

    private sealed record HistoriquePaiement(DateTime DateEcheance, DateTime DatePaiement);

    private sealed record FactureOuverte(
        Guid Id,
        string Numero,
        Guid ClientId,
        string ClientNom,
        string TypeClient,
        DateTime DateEcheance,
        decimal MontantTtc,
        string Devise);

    private sealed record PredictionMlRequest(
        [property: JsonPropertyName("montant_ttc")] double MontantTtc,
        [property: JsonPropertyName("jours_delai")] int JoursDelai,
        [property: JsonPropertyName("mode_paiement")] string ModePaiement,
        [property: JsonPropertyName("type_client")] string TypeClient,
        [property: JsonPropertyName("ville")] string Ville,
        [property: JsonPropertyName("anciennete_mois")] int AncienneteMois,
        [property: JsonPropertyName("nombre_factures_precedentes")] int NombreFacturesPrecedentes,
        [property: JsonPropertyName("nombre_retards_precedents")] int NombreRetardsPrecedents,
        [property: JsonPropertyName("taux_retard_precedent")] double TauxRetardPrecedent,
        [property: JsonPropertyName("retard_moyen_jours")] double RetardMoyenJours);

    private sealed record PredictionMlResponse(
        [property: JsonPropertyName("prediction")] string Prediction,
        [property: JsonPropertyName("probabilite_retard")] double ProbabiliteRetard,
        [property: JsonPropertyName("niveau_risque")] string NiveauRisque);
}
