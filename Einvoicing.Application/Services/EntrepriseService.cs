using Einvoicing.Application.DTOs;
using Einvoicing.Application.Helpers;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Einvoicing.Application.Services;

public sealed class EntrepriseService(
    IEntrepriseRepository entrepriseRepo,
    IUtilisateurRepository utilisateurRepo
) : IEntrepriseService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<EntrepriseDto> CreerAsync(
        CreerEntrepriseRequest req, Guid adminId, CancellationToken ct = default)
    {
        if (!MatriculeFiscalHelper.IsValid(req.MatriculeFiscal))
            throw new ValidationMetierException("Le matricule fiscal est invalide.");

        var matricule = MatriculeFiscalHelper.Normalize(req.MatriculeFiscal);
        var existant = await entrepriseRepo.ObtenirParMatriculeAsync(matricule, ct);
        if (existant is not null)
            throw new ConflitException("Ce matricule fiscal est déjà enregistré.");

        var entreprise = Entreprise.Creer(
            req.Nom, matricule, req.Adresse, req.Ville,
            req.CodePostal, req.Email, req.CodeTva, req.RegimeFiscal,
            req.Telephone, req.SiteWeb, req.DevisePrincipale);

        await entrepriseRepo.AjouterAsync(entreprise, ct);

        var admin = await utilisateurRepo.ObtenirParIdAsync(adminId, ct);
        if (admin is not null)
        {
            await entrepriseRepo.SauvegarderAsync(ct);
        }

        await entrepriseRepo.SauvegarderAsync(ct);
        return MapToDto(entreprise);
    }

    public async Task<EntrepriseDto> ObtenirParIdAsync(Guid id, CancellationToken ct = default)
    {
        var entreprise = await entrepriseRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Entreprise introuvable.");
        return MapToDto(entreprise);
    }

    public async Task<EntrepriseDto> MettreAJourAsync(
        Guid id, MettreAJourEntrepriseRequest req, CancellationToken ct = default)
    {
        var entreprise = await entrepriseRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Entreprise introuvable.");

        var profilActuel = LireProfil(entreprise.DonneesInscription);
        var teifActuel = LireTeif(entreprise.ParametresTeif);

        var nom = PremierTexteNonVide(req.Nom, req.RaisonSociale, entreprise.Nom);
        var adresse = PremierTexteNonVide(req.Adresse, entreprise.Adresse);
        var ville = PremierTexteNonVide(req.Ville, req.Gouvernorat, entreprise.Ville);
        var codePostal = PremierTexteNonVide(req.CodePostal, entreprise.CodePostal);
        var email = PremierTexteNonVide(req.Email, entreprise.Email);
        var telephone = PremierTexteNonVide(req.Telephone, req.Tel, entreprise.Telephone);
        var siteWeb = PremierTexteNonVide(req.SiteWeb, entreprise.SiteWeb);
        var devise = PremierTexteNonVide(req.DevisePrincipale, entreprise.DevisePrincipale);
        var regimeFiscal = ResoudreRegimeFiscal(req.RegimeFiscal, req.RegimeTva, entreprise.RegimeFiscal);

        entreprise.MettreAJour(
            nom,
            adresse,
            ville,
            codePostal,
            email,
            telephone,
            siteWeb,
            regimeFiscal,
            devise);

        var profil = profilActuel with
        {
            RaisonSociale = PremierTexteNonVide(req.RaisonSociale, req.Nom, entreprise.Nom),
            NomCommercial = PremierTexteNonVide(req.NomCommercial, profilActuel.NomCommercial, req.RaisonSociale, entreprise.Nom),
            Forme = PremierTexteNonVide(req.Forme, profilActuel.Forme),
            Capital = PremierTexteNonVide(req.Capital, profilActuel.Capital),
            DateCreation = PremierTexteNonVide(req.DateCreation, profilActuel.DateCreation),
            ActiviteCode = PremierTexteNonVide(req.ActiviteCode, profilActuel.ActiviteCode),
            ActiviteLibelle = PremierTexteNonVide(req.ActiviteLibelle, profilActuel.ActiviteLibelle),
            Gouvernorat = PremierTexteNonVide(req.Gouvernorat, req.Ville, profilActuel.Gouvernorat, entreprise.Ville),
            Pays = PremierTexteNonVide(req.Pays, profilActuel.Pays, entreprise.Pays),
            Fax = PremierTexteNonVide(req.Fax, profilActuel.Fax),
            NumRne = PremierTexteNonVide(req.NumRne, profilActuel.NumRne),
            RegimeTva = PremierTexteNonVide(req.RegimeTva, profilActuel.RegimeTva, ConvertirRegimeFiscal(regimeFiscal)),
            TauxTvaPrincipal = req.TauxTvaPrincipal ?? profilActuel.TauxTvaPrincipal ?? 19m
        };
        entreprise.DefinirDonneesInscription(JsonSerializer.Serialize(profil, JsonOptions));

        if (req.TeifSignature.HasValue
            || req.TeifArchivage.HasValue
            || req.TeifHorodatage.HasValue
            || req.TeifSandbox.HasValue
            || req.TeifSurveille.HasValue
            || string.IsNullOrWhiteSpace(entreprise.ParametresTeif))
        {
            var teif = teifActuel with
            {
                Signature = req.TeifSignature ?? teifActuel.Signature,
                Archivage = req.TeifArchivage ?? teifActuel.Archivage,
                Horodatage = req.TeifHorodatage ?? teifActuel.Horodatage,
                Sandbox = req.TeifSandbox ?? teifActuel.Sandbox,
                Surveille = req.TeifSurveille ?? teifActuel.Surveille
            };

            entreprise.ConfigurerTeif(
                JsonSerializer.Serialize(teif, JsonOptions),
                string.IsNullOrWhiteSpace(entreprise.VersionTeif) ? "2024" : entreprise.VersionTeif);
        }

        entrepriseRepo.MettreAJour(entreprise);
        await entrepriseRepo.SauvegarderAsync(ct);
        return MapToDto(entreprise);
    }

    public async Task ConfigurerTeifAsync(
        Guid id, ConfigurerTeifRequest req, CancellationToken ct = default)
    {
        var entreprise = await entrepriseRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Entreprise introuvable.");

        entreprise.ConfigurerTeif(req.ParametresTeif, req.VersionTeif);
        entrepriseRepo.MettreAJour(entreprise);
        await entrepriseRepo.SauvegarderAsync(ct);
    }

    public async Task DesactiverAsync(Guid id, CancellationToken ct = default)
    {
        var entreprise = await entrepriseRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Entreprise introuvable.");
        entreprise.Desactiver();
        entrepriseRepo.MettreAJour(entreprise);
        await entrepriseRepo.SauvegarderAsync(ct);
    }

    public async Task ReactiverAsync(Guid id, CancellationToken ct = default)
    {
        var entreprise = await entrepriseRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Entreprise introuvable.");
        entreprise.Reactiver();
        entrepriseRepo.MettreAJour(entreprise);
        await entrepriseRepo.SauvegarderAsync(ct);
    }

    public async Task<IReadOnlyList<EntrepriseDto>> ListerToutesAsync(CancellationToken ct = default)
    {
        var liste = await entrepriseRepo.ListerToutesAsync(ct);
        return liste.Select(MapToDto).ToList();
    }

    private static EntrepriseDto MapToDto(Entreprise e)
    {
        var profil = LireProfil(e.DonneesInscription);
        var teif = LireTeif(e.ParametresTeif);

        return new EntrepriseDto(
            e.Id,
            e.Nom,
            e.MatriculeFiscal,
            e.Adresse,
            e.Ville,
            e.CodePostal,
            e.Pays,
            e.Email,
            e.Telephone,
            e.SiteWeb,
            e.DevisePrincipale,
            e.LogoUrl,
            e.RegimeFiscal.ToString(),
            e.CodeTva,
            e.VersionTeif,
            e.EstActive,
            e.CreeLe,
            e.ModifieLe,
            PremierTexteNonVide(profil.RaisonSociale, e.Nom),
            PremierTexteNonVide(profil.NomCommercial, e.Nom),
            profil.Forme ?? string.Empty,
            profil.Capital ?? string.Empty,
            profil.DateCreation ?? string.Empty,
            profil.ActiviteCode ?? string.Empty,
            profil.ActiviteLibelle ?? string.Empty,
            PremierTexteNonVide(profil.Gouvernorat, e.Ville),
            profil.Fax ?? string.Empty,
            e.Telephone ?? string.Empty,
            profil.NumRne ?? string.Empty,
            PremierTexteNonVide(profil.RegimeTva, ConvertirRegimeFiscal(e.RegimeFiscal)),
            profil.TauxTvaPrincipal ?? 19m,
            teif.Signature,
            teif.Archivage,
            teif.Horodatage,
            teif.Sandbox,
            teif.Surveille
        );
    }

    private static string PremierTexteNonVide(params string?[] valeurs)
        => valeurs.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static RegimeFiscal ResoudreRegimeFiscal(
        RegimeFiscal? regimeFiscal,
        string? regimeTva,
        RegimeFiscal fallback)
    {
        if (regimeFiscal.HasValue)
            return regimeFiscal.Value;

        var normalise = (regimeTva ?? string.Empty).Trim().ToLowerInvariant();
        return normalise switch
        {
            "regime forfaitaire" or "forfait" => RegimeFiscal.Forfait,
            "exonere" or "non assujetti" => RegimeFiscal.NonAssujetti,
            "regime reel" or "reel" => RegimeFiscal.Reel,
            _ => fallback
        };
    }

    private static string ConvertirRegimeFiscal(RegimeFiscal regimeFiscal)
        => regimeFiscal switch
        {
            RegimeFiscal.Forfait => "Regime forfaitaire",
            RegimeFiscal.NonAssujetti => "Exonere",
            _ => "Regime reel"
        };

    private static EntrepriseProfilData LireProfil(string? json)
        => LireJson(json, new EntrepriseProfilData());

    private static EntrepriseTeifData LireTeif(string? json)
        => LireJson(json, new EntrepriseTeifData(
            Signature: true,
            Archivage: true,
            Horodatage: false,
            Sandbox: true,
            Surveille: false));

    private static T LireJson<T>(string? json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
            return fallback;

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}

public sealed record EntrepriseProfilData(
    string? RaisonSociale = null,
    string? NomCommercial = null,
    string? Forme = null,
    string? Capital = null,
    string? DateCreation = null,
    string? ActiviteCode = null,
    string? ActiviteLibelle = null,
    string? Gouvernorat = null,
    string? Pays = null,
    string? Fax = null,
    string? NumRne = null,
    string? RegimeTva = null,
    decimal? TauxTvaPrincipal = null
);

public sealed record EntrepriseTeifData(
    bool Signature,
    bool Archivage,
    bool Horodatage,
    bool Sandbox,
    bool Surveille
);
