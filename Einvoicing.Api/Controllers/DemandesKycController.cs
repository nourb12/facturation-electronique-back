using System.Text.Json;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/admin/demandes-kyc")]
[Authorize(Policy = "Admin")]
public sealed class DemandesKycController(IDemandeAccesService demandeService) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var demandes = await ChargerToutesDemandes(ct);
        return Ok(demandes.Select(MapToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var demandes = await ChargerToutesDemandes(ct);
        var demande = demandes.FirstOrDefault(d => d.EntrepriseId == id);
        return demande is null ? NotFound() : Ok(MapToDto(demande));
    }

    [HttpPost("{id:guid}/accepter")]
    public async Task<IActionResult> Accepter(Guid id, CancellationToken ct)
    {
        await demandeService.ValiderDemandeAsync(id, ct);
        return Ok(new { message = "Demande acceptee. Email envoye a l'entreprise." });
    }

    [HttpPost("{id:guid}/refuser")]
    public async Task<IActionResult> Refuser(Guid id, [FromBody] RefusRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Motif))
            return BadRequest(new { message = "Le motif de refus est obligatoire." });

        await demandeService.RejeterDemandeAsync(id, req.Motif, ct);
        return Ok(new { message = "Demande refusee. Email de notification envoye." });
    }

    [HttpPost("{id:guid}/demander-corrections")]
    public async Task<IActionResult> DemanderCorrections(Guid id, [FromBody] DemandeCorrectionsRequest req, CancellationToken ct)
    {
        var flagCodes = req.FlagCodes ?? [];
        if (flagCodes.Count == 0)
            return BadRequest(new { message = "Selectionnez au moins un point a corriger." });

        await demandeService.DemanderCorrectionsAsync(id, flagCodes, req.MessageAdmin, ct);
        return Ok(new { message = "Email de corrections envoye." });
    }
    // Mapping

    private async Task<List<DemandeAccesDto>> ChargerToutesDemandes(CancellationToken ct)
    {
        // DbContext n'est pas thread-safe : appels sequentiels pour eviter les erreurs 500.
        var statuts = new[] { "EnAttente", "Actif", "Supprime" };
        var result = new List<DemandeAccesDto>();

        foreach (var statut in statuts)
        {
            var items = await demandeService.ListerDemandesAsync(statut, ct);
            if (items.Count > 0) result.AddRange(items);
        }

        return result;
    }

    private static object MapToDto(DemandeAccesDto d)
    {
        var inscription = TryParse<InscriptionData>(d.DonneesInscription);
        var docs = TryParse<DocumentsData>(d.DocumentsUploades);
        var scoring = TryParse<ScoringData>(d.DonneesScoring);

        var profil = inscription?.ProfilEntreprise;
        var contact = inscription?.Contact;
        var resp = inscription?.ResponsableLegal;

        return new
        {
            id = d.EntrepriseId,
            raisonSociale = d.RaisonSociale,
            matriculeFiscal = d.MatriculeFiscal,
            formeJuridique = profil?.FormeJuridique ?? string.Empty,
            nomEntreprise = profil?.NomEntreprise ?? d.RaisonSociale,
            adresse = profil?.Adresse ?? d.Adresse,
            gouvernorat = profil?.Gouvernorat ?? d.Gouvernorat,
            codePostal = profil?.CodePostal ?? d.CodePostal,
            devisePrincipale = profil?.DevisePrincipale ?? d.DevisePrincipale,
            siteWeb = profil?.SiteWeb ?? d.SiteWeb ?? string.Empty,
            email = contact?.Email ?? d.Email,
            telephone = contact?.Telephone ?? d.Telephone ?? string.Empty,
            telEntreprise = profil?.TelEntreprise ?? d.Telephone ?? string.Empty,
            respPrenom = resp?.RespPrenom ?? d.RespPrenom,
            respNom = resp?.RespNom ?? d.RespNom,
            respEmail = resp?.RespEmail ?? d.RespEmail ?? contact?.Email ?? d.Email,
            respFonction = ResolveRespFonction(resp, d.RespFonction),
            statut = MapStatut(d.Statut),
            score = d.ScoreKyc,
            decision = scoring?.Decision ?? "RevisionManuelle",
            // Flags enrichis avec severity (retro-compatible : si c'est une string, on la convertit en objet).
            flags = scoring?.Flags ?? new List<object>(),

            scoreBreakdown = scoring?.Breakdown ?? new List<BreakdownData>(),
            comparisons = scoring?.Comparisons ?? new List<ComparisonData>(),
            ocr = scoring?.Ocr,
            createdAt = d.DateDemande,
            documents = new
            {
                registreCommerce = docs?.RegistreCommerce,
                patente = docs?.Patente,
                cinResponsable = docs?.CinResponsable,
                rib = docs?.Rib,
            }
        };
    }

    private static string MapStatut(string statut) => statut switch
    {
        "Actif" => "Accepte",
        "Supprime" => "Refuse",
        _ => "EnAttente"
    };

    private static string ResolveRespFonction(ResponsableLegal? resp, string? fallback)
    {
        if (resp is null) return fallback ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(resp.RespFonctionAutre)) return resp.RespFonctionAutre;
        return resp.RespFonction ?? fallback ?? string.Empty;
    }

    private static T? TryParse<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return default; }
    }
    // Records locaux

    public record RefusRequest(string Motif);
    public record DemandeCorrectionsRequest(List<string>? FlagCodes, string? MessageAdmin);

    private sealed class InscriptionData
    {
        public Contact? Contact { get; set; }
        public ProfilEntreprise? ProfilEntreprise { get; set; }
        public ResponsableLegal? ResponsableLegal { get; set; }
    }

    private sealed class Contact
    {
        public string? Email { get; set; }
        public string? Telephone { get; set; }
    }

    private sealed class ProfilEntreprise
    {
        public string? FormeJuridique { get; set; }
        public string? NomEntreprise { get; set; }
        public string? Adresse { get; set; }
        public string? Gouvernorat { get; set; }
        public string? CodePostal { get; set; }
        public string? SiteWeb { get; set; }
        public string? DevisePrincipale { get; set; }
        public string? TelEntreprise { get; set; }
    }

    private sealed class ResponsableLegal
    {
        public string? RespPrenom { get; set; }
        public string? RespNom { get; set; }
        public string? RespFonction { get; set; }
        public string? RespFonctionAutre { get; set; }
        public string? RespEmail { get; set; }
        public string? RespTel { get; set; }
    }

    private sealed class DocumentsData
    {
        public string? RegistreCommerce { get; set; }
        public string? Patente { get; set; }
        public string? CinResponsable { get; set; }
        public string? Rib { get; set; }
    }

    private sealed class ScoringData
    {
        public string? Decision { get; set; }
        // Flags peuvent etre une string (ancien format) ou un objet {code,message,severity} (nouveau).
        public List<object>? Flags { get; set; }
        public List<BreakdownData>? Breakdown { get; set; }
        public List<ComparisonData>? Comparisons { get; set; }
        public OcrData? Ocr { get; set; }
    }

    private sealed class BreakdownData
    {
        public string? Key { get; set; }
        public string? Label { get; set; }
        public int Points { get; set; }
        public int MaxPoints { get; set; }
        public bool Ok { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class ComparisonData
    {
        public string? Key { get; set; }
        public string? Label { get; set; }
        public string? FormValue { get; set; }
        public string? OcrValue { get; set; }
        public bool Available { get; set; }
        public bool Matched { get; set; }
        public bool Critical { get; set; }
    }

    private sealed class OcrData
    {
        public bool OcrSuccess { get; set; }
        public double ConfidenceScore { get; set; }
        public string? MatriculeFiscalExtrait { get; set; }
        public string? RaisonSocialeExtraite { get; set; }
        public string? NomGerantExtrait { get; set; }
        public string? NomExtrait { get; set; }
        public string? PrenomExtrait { get; set; }
        public string? CinExtrait { get; set; }
        public string? FormeJuridiqueExtraite { get; set; }
        public string? AdresseExtraite { get; set; }
        public string? TexteBrut { get; set; }
        public string? ErreurMessage { get; set; }
    }
}
