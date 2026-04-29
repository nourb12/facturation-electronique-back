using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Helpers;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class DemandeAccesService(
    IEntrepriseRepository entrepriseRepo,
    IUtilisateurRepository utilisateurRepo,
    IEmailService emailService,
    IPasswordHasher passwordHasher,
    IFileStorageService fileStorage,
    KycScoringService kycScoring,
    IOcrClient ocrClient
) : IDemandeAccesService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DemandeAccesConfirmationDto> SoumettreDemandeAsync(
        SoumettreDemandeAccesRequest req, CancellationToken ct = default)
    {
        var email = ((string.IsNullOrWhiteSpace(req.Email) ? req.RespEmail : req.Email) ?? string.Empty)
            .Trim().ToLowerInvariant();
        var responsableEmail = string.IsNullOrWhiteSpace(req.RespEmail)
            ? email
            : req.RespEmail.Trim().ToLowerInvariant();
        var contactTelephone = string.IsNullOrWhiteSpace(req.Telephone)
            ? req.RespTel?.Trim() ?? string.Empty
            : req.Telephone.Trim();

        var existant = await utilisateurRepo.ObtenirParEmailAsync(email, ct);
        if (existant is not null)
            throw new ValidationMetierException("Cet email est déjà utilisé.");

        if (!MatriculeFiscalHelper.IsValid(req.MatriculeFiscal))
            throw new ValidationMetierException("Le matricule fiscal est invalide.");

        var matricule = MatriculeFiscalHelper.Normalize(req.MatriculeFiscal);
        var entrepriseExistante = await entrepriseRepo.ObtenirParMatriculeAsync(matricule, ct);
        if (entrepriseExistante is not null)
            throw new ValidationMetierException("Ce matricule fiscal est déjà utilisé.");

        var entreprise = Entreprise.Creer(
            nom: req.RaisonSociale,
            matriculeFiscal: matricule,
            adresse: req.Adresse,
            ville: req.Gouvernorat,
            codePostal: req.CodePostal,
            email: email,
            codeTva: string.Empty,
            regimeFiscal: RegimeFiscal.Reel,
            telephone: string.IsNullOrWhiteSpace(req.TelEntreprise) ? null : req.TelEntreprise.Trim(),
            siteWeb: string.IsNullOrWhiteSpace(req.SiteWeb) ? null : req.SiteWeb.Trim(),
            devisePrincipale: req.DevisePrincipale
        );
        entreprise.Desactiver();
        // Logo retire du flow principal (gere dans les parametres apres validation)
        var cheminRc = req.RegistreCommerce is null ? null : await fileStorage.SaveAsync(req.RegistreCommerce, "kyc", ct);
        var cheminPat = req.Patente is null ? null : await fileStorage.SaveAsync(req.Patente, "kyc", ct);
        var cheminCin = req.CinResponsable is null ? null : await fileStorage.SaveAsync(req.CinResponsable, "kyc", ct);
        var cheminRib = req.Rib is null ? null : await fileStorage.SaveAsync(req.Rib, "kyc", ct);
        // OCR local (degradation gracieuse si non configure)
        OcrExtractionResult? ocrResult = null;
        try
        {
            ocrResult = await ocrClient.ExtractAsync(new[]
            {
                req.RegistreCommerce,
                req.CinResponsable,
                req.Patente,
                req.Rib
            }, ct);
        }
        catch (Exception ex)
        {
            ocrResult = new OcrExtractionResult
            {
                OcrSuccess = false,
                ConfidenceScore = 0d,
                ErreurMessage = ex.Message,
                TexteBrut = string.Empty
            };
        }
        // Scoring KYC
        var scoring = kycScoring.Calculate(req, cheminRc, cheminCin, cheminPat, cheminRib, ocrResult);

        var scoringPayload = new
        {
            score = scoring.Score,
            decision = scoring.Decision.ToString(),
            flags = scoring.Flags.Select(f => new
            {
                code = f.Code,
                message = f.Message,
                severity = f.Severity.ToString()
            }),
            breakdown = scoring.Breakdown,
            comparisons = scoring.Comparisons.Select(c => new
            {
                c.Key,
                c.Label,
                c.FormValue,
                ocrValue = c.OcrValue,
                c.Available,
                c.Matched,
                c.Critical
            }),
            ocr = ocrResult
        };
        entreprise.DefinirScoring(scoring.Score, JsonSerializer.Serialize(scoringPayload, JsonOptions));

        var inscription = new
        {
            contact = new { email, telephone = contactTelephone },
            profilEntreprise = new
            {
                req.FormeJuridique,
                req.NomEntreprise,
                req.Adresse,
                req.Gouvernorat,
                req.CodePostal,
                req.SiteWeb,
                req.DevisePrincipale,
                req.TelEntreprise
            },
            responsableLegal = new
            {
                req.RespPrenom,
                req.RespNom,
                req.RespFonction,
                req.RespFonctionAutre,
                respEmail = responsableEmail,
                req.RespTel
            }
        };
        entreprise.DefinirDonneesInscription(JsonSerializer.Serialize(inscription, JsonOptions));

        var documents = new
        {
            registreCommerce = cheminRc,
            patente = cheminPat,
            cinResponsable = cheminCin,
            rib = cheminRib,
        };
        entreprise.DefinirDocumentsUploades(JsonSerializer.Serialize(documents, JsonOptions));

        await entrepriseRepo.AjouterAsync(entreprise, ct);
        await entrepriseRepo.SauvegarderAsync(ct);

        var tempPassword = GenererMotDePasseTemp();
        var hash = passwordHasher.Hacher(tempPassword);

        var poste = req.RespFonction == "Autre" && !string.IsNullOrWhiteSpace(req.RespFonctionAutre)
            ? req.RespFonctionAutre
            : req.RespFonction;
        var roleResponsable = DeterminerRoleResponsable(poste);
        var departement = DeterminerDepartement(roleResponsable);

        var utilisateur = Utilisateur.Creer(
            prenom: req.RespPrenom,
            nom: req.RespNom,
            email: email,
            motDePasseHash: hash,
            role: roleResponsable,
            entrepriseId: entreprise.Id
        );
        utilisateur.MettreEnAttente();
        utilisateur.MettreAJourProfil(req.RespPrenom, req.RespNom, req.RespTel, poste, departement);
        await utilisateurRepo.AjouterAsync(utilisateur, ct);
        await utilisateurRepo.SauvegarderAsync(ct);

        var reference = $"MZN-{matricule[..7]}-{DateTime.UtcNow:yyyy}";
        var documentsRecus = new List<string>();
        if (req.RegistreCommerce is not null) documentsRecus.Add("Registre de commerce");
        if (req.Patente is not null) documentsRecus.Add("Patente");
        if (req.CinResponsable is not null) documentsRecus.Add("CIN du responsable légal");
        if (req.Rib is not null) documentsRecus.Add("RIB bancaire");

        await emailService.EnvoyerConfirmationDemandeAsync(
            email,
            req.RaisonSociale,
            reference,
            req.MatriculeFiscal,
            req.RespPrenom,
            req.RespNom,
            documentsRecus,
            ct);

        return new DemandeAccesConfirmationDto(reference, "Demande recue. Vous serez contacte sous 48h ouvrables.");
    }

    public async Task<StatutCompteDto> VerifierStatutAsync(
        Guid utilisateurId, CancellationToken ct = default)
    {
        var utilisateur = await utilisateurRepo.ObtenirParIdAsync(utilisateurId, ct)
            ?? throw new NotFoundException("Utilisateur introuvable.");
        return new StatutCompteDto(utilisateur.Statut.ToString());
    }

    public async Task<IReadOnlyList<DemandeAccesDto>> ListerDemandesAsync(
        string? statut, CancellationToken ct = default)
    {
        var statutValue = Enum.TryParse<StatutCompte>(statut ?? "EnAttente", true, out var s)
            ? s : StatutCompte.EnAttente;

        var utilisateurs = await utilisateurRepo.ListerParStatutAsync(statutValue, ct);
        var result = new List<DemandeAccesDto>();

        foreach (var u in utilisateurs)
        {
            if (u.EntrepriseId is null) continue;
            var ent = await entrepriseRepo.ObtenirParIdAsync(u.EntrepriseId.Value, ct);
            if (ent is null) continue;

            result.Add(new DemandeAccesDto(
                EntrepriseId: ent.Id,
                RaisonSociale: ent.Nom,
                MatriculeFiscal: ent.MatriculeFiscal,
                Email: ent.Email,
                Telephone: ent.Telephone,
                DateDemande: u.CreeLe,
                Statut: u.Statut.ToString(),
                ScoreKyc: ent.ScoreKyc,
                DonneesScoring: ent.DonneesScoring,
                DonneesInscription: ent.DonneesInscription,
                DocumentsUploades: ent.DocumentsUploades
            ));
        }

        return result;
    }

    public async Task ValiderDemandeAsync(Guid entrepriseId, CancellationToken ct = default)
    {
        var entreprise = await entrepriseRepo.ObtenirParIdAsync(entrepriseId, ct)
            ?? throw new NotFoundException("Entreprise introuvable.");

        var utilisateurs = await utilisateurRepo.ListerParEntrepriseAsync(entrepriseId, ct);
        var utilisateur = utilisateurs.OrderBy(u => u.CreeLe).FirstOrDefault()
            ?? throw new NotFoundException("Utilisateur introuvable.");

        if (utilisateur.Statut == StatutCompte.Supprime)
            throw new ValidationMetierException("Ce compte a déjà été supprimé.");

        var tempPassword = GenererMotDePasseTemp();
        utilisateur.ChangerMotDePasse(passwordHasher.Hacher(tempPassword));
        utilisateur.Reactiver();
        utilisateurRepo.MettreAJour(utilisateur);

        entreprise.Reactiver();
        entrepriseRepo.MettreAJour(entreprise);

        await utilisateurRepo.SauvegarderAsync(ct);
        await entrepriseRepo.SauvegarderAsync(ct);

        await emailService.EnvoyerAccesValideAsync(
            utilisateur.Email, utilisateur.Prenom,
            entreprise.Nom, utilisateur.Email, tempPassword, ct);
    }

    public async Task RejeterDemandeAsync(Guid entrepriseId, string motif, CancellationToken ct = default)
    {
        var entreprise = await entrepriseRepo.ObtenirParIdAsync(entrepriseId, ct)
            ?? throw new NotFoundException("Entreprise introuvable.");

        var utilisateurs = await utilisateurRepo.ListerParEntrepriseAsync(entrepriseId, ct);
        var utilisateur = utilisateurs.OrderBy(u => u.CreeLe).FirstOrDefault()
            ?? throw new NotFoundException("Utilisateur introuvable.");

        utilisateur.Supprimer();
        utilisateurRepo.MettreAJour(utilisateur);

        entreprise.Desactiver();
        entrepriseRepo.MettreAJour(entreprise);

        await utilisateurRepo.SauvegarderAsync(ct);
        await entrepriseRepo.SauvegarderAsync(ct);

        await emailService.EnvoyerDemandeRejeteeAsync(
            utilisateur.Email, utilisateur.Prenom, entreprise.Nom, motif, ct);
    }

    private static string GenererMotDePasseTemp(int length = 10)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var buffer = new char[length];
        for (var i = 0; i < length; i++)
            buffer[i] = chars[bytes[i] % chars.Length];
        return new string(buffer);
    }

    private static RoleUtilisateur DeterminerRoleResponsable(string? poste)
    {
        var valeur = Normaliser(poste);
        if (string.IsNullOrWhiteSpace(valeur))
            return RoleUtilisateur.Admin;

        if (ContientUnMotCle(valeur,
                "responsable comptabilite",
                "responsable administratif et financier",
                "responsable administratif financier",
                "comptabilite",
                "financier",
                "finance",
                "daf",
                "raf",
                "tresorier",
                "controleur de gestion"))
            return RoleUtilisateur.ResponsableFinancier;

        if (ContientUnMotCle(valeur,
                "gerant",
                "directeur general",
                "responsable entreprise",
                "representant legal",
                "legal",
                "ceo",
                "manager"))
            return RoleUtilisateur.ResponsableEntreprise;

        if (ContientUnMotCle(valeur,
                "administrateur systeme",
                "administrateur",
                "sysadmin",
                "informatique",
                "systeme",
                "it"))
            return RoleUtilisateur.Admin;

        return RoleUtilisateur.Admin;
    }

    private static string? DeterminerDepartement(RoleUtilisateur role) => role switch
    {
        RoleUtilisateur.ResponsableFinancier => "Finance",
        RoleUtilisateur.ResponsableEntreprise => "Direction",
        RoleUtilisateur.Admin => "Administration",
        _ => null
    };

    private static bool ContientUnMotCle(string source, params string[] motsCles)
        => motsCles.Any(source.Contains);

    private static string Normaliser(string? valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
            return string.Empty;

        var formD = valeur.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
