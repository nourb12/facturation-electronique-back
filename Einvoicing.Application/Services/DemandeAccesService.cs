using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Einvoicing.Application.Configuration;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Helpers;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using Microsoft.Extensions.Logging;

using Einvoicing.Domain;

namespace Einvoicing.Application.Services;

public sealed class DemandeAccesService(
    IEntrepriseRepository entrepriseRepo,
    IUtilisateurRepository utilisateurRepo,
    IEmailService emailService,
    IPasswordHasher passwordHasher,
    IFileStorageService fileStorage,
    KycScoringService kycScoring,
    IOcrClient ocrClient,
    ISmsService smsService,
    IPlatformConfigurationService platformConfiguration,
    ILogger<DemandeAccesService> logger
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
        EnsureDistinctDocuments(req);
        var cheminRc = req.RegistreCommerce is null ? null : await fileStorage.SaveAsync(req.RegistreCommerce, "kyc", ct);
        var cheminPat = req.Patente is null ? null : await fileStorage.SaveAsync(req.Patente, "kyc", ct);
        var cheminCin = req.CinResponsable is null ? null : await fileStorage.SaveAsync(req.CinResponsable, "kyc", ct);
        var cheminRib = req.Rib is null ? null : await fileStorage.SaveAsync(req.Rib, "kyc", ct);
        // OCR par piece : l'analyse d'un lot ne permet pas de prouver que chaque zone contient le bon document.
        OcrExtractionResult? ocrResult = null;
        Dictionary<string, OcrExtractionResult?> ocrByDocument = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            ocrByDocument = await ExtractKycDocumentsAsync(req, ct);
            ocrResult = MergeKycOcrResults(ocrByDocument);
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
        var autoApprovalThreshold = await platformConfiguration.GetIntAsync(
            PlatformConfigurationKeys.KycAutoApprovalThreshold, 80, ct);
        var manualReviewThreshold = await platformConfiguration.GetIntAsync(
            PlatformConfigurationKeys.KycManualReviewThreshold, 50, ct);
        var scoring = kycScoring.Calculate(
            req,
            cheminRc,
            cheminCin,
            cheminPat,
            cheminRib,
            ocrResult,
            autoApprovalThreshold,
            manualReviewThreshold);

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
            ocr = ocrResult,
            ocrByDocument = ocrByDocument.ToDictionary(
                x => x.Key,
                x => new { x.Value?.OcrSuccess, x.Value?.ConfidenceScore, x.Value?.TypeDocument, x.Value?.ErreurMessage })
        };
        entreprise.DefinirScoring(scoring.Score, JsonSerializer.Serialize(scoringPayload, JsonOptions));

        var inscription = new
        {
            contact = new { email, telephone = contactTelephone },
            profilEntreprise = new
            {
                profileType = req.ProfileType,
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

        var smsEnvoye = await EnvoyerSmsAsync(
            contactTelephone,
            $"[TuniFlow] Demande {reference} recue. Votre dossier est en cours d'examen.",
            ct);

        return new DemandeAccesConfirmationDto(reference, "Demande recue. Vous serez contacte sous 48h ouvrables.", smsEnvoye);
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
                Adresse: ent.Adresse,
                Gouvernorat: ent.Ville,
                CodePostal: ent.CodePostal,
                SiteWeb: ent.SiteWeb,
                DevisePrincipale: ent.DevisePrincipale,
                RespPrenom: u.Prenom,
                RespNom: u.Nom,
                RespEmail: u.Email,
                RespTelephone: u.Telephone,
                RespFonction: u.Poste,
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

        if (utilisateur.Statut == StatutCompte.Actif)
            throw new ValidationMetierException("Cette demande est deja acceptee. Le mot de passe existant reste inchange.");

        var tempPassword = GenererMotDePasseTemp();
        utilisateur.ChangerMotDePasse(passwordHasher.Hacher(tempPassword));
        utilisateur.Reactiver();
        utilisateurRepo.MettreAJour(utilisateur);

        entreprise.Reactiver();
        entrepriseRepo.MettreAJour(entreprise);

        RecordKycEvent(entreprise, "Acceptee", null, null);

        await utilisateurRepo.SauvegarderAsync(ct);
        await entrepriseRepo.SauvegarderAsync(ct);

        await emailService.EnvoyerAccesValideAsync(
            utilisateur.Email, utilisateur.Prenom,
            entreprise.Nom, utilisateur.Email, tempPassword, ct);
        await EnvoyerSmsAsync(
            utilisateur.Telephone,
            $"[TuniFlow] Votre acces pour {entreprise.Nom} est valide. Connectez-vous a votre espace.",
            ct);
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

        RecordKycEvent(entreprise, "Refusee", motif, null);

        await utilisateurRepo.SauvegarderAsync(ct);
        await entrepriseRepo.SauvegarderAsync(ct);

        await emailService.EnvoyerDemandeRejeteeAsync(
            utilisateur.Email, utilisateur.Prenom, entreprise.Nom, motif, ct);
        await EnvoyerSmsAsync(
            utilisateur.Telephone,
            $"[TuniFlow] Votre demande pour {entreprise.Nom} n'a pas ete acceptee. Consultez votre email.",
            ct);
    }

    public async Task DemanderCorrectionsAsync(
        Guid entrepriseId,
        IReadOnlyCollection<string> flagCodes,
        string? messageAdmin,
        CancellationToken ct = default)
    {
        if (flagCodes.Count == 0)
            throw new ValidationMetierException("Selectionnez au moins un point a corriger.");

        var entreprise = await entrepriseRepo.ObtenirParIdAsync(entrepriseId, ct)
            ?? throw new NotFoundException("Entreprise introuvable.");

        var utilisateurs = await utilisateurRepo.ListerParEntrepriseAsync(entrepriseId, ct);
        var utilisateur = utilisateurs.OrderBy(u => u.CreeLe).FirstOrDefault()
            ?? throw new NotFoundException("Utilisateur introuvable.");

        if (utilisateur.Statut != StatutCompte.EnAttente)
            throw new ValidationMetierException("Seules les demandes en attente peuvent recevoir une demande de correction.");

        var corrections = ResolveCorrections(entreprise.DonneesScoring, flagCodes);
        RecordKycEvent(entreprise, "CorrectionsDemandees", messageAdmin, flagCodes);
        entrepriseRepo.MettreAJour(entreprise);
        await entrepriseRepo.SauvegarderAsync(ct);

        await emailService.EnvoyerDemandeCorrectionsAsync(
            utilisateur.Email,
            utilisateur.Prenom,
            entreprise.Nom,
            corrections,
            messageAdmin,
            ct);
        await EnvoyerSmsAsync(
            utilisateur.Telephone,
            $"[TuniFlow] Des corrections sont requises pour votre demande {entreprise.Nom}. Consultez votre email.",
            ct);
    }

    public async Task AjouterNoteInterneAsync(Guid entrepriseId, string note, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new ValidationMetierException("La note interne ne peut pas être vide.");

        var entreprise = await entrepriseRepo.ObtenirParIdAsync(entrepriseId, ct)
            ?? throw new NotFoundException("Entreprise introuvable.");

        RecordKycEvent(entreprise, "NoteInterne", note, null);
        entrepriseRepo.MettreAJour(entreprise);
        await entrepriseRepo.SauvegarderAsync(ct);
    }

    private async Task<bool> EnvoyerSmsAsync(string? telephone, string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(telephone)) return false;
        try
        {
            await smsService.EnvoyerAsync(telephone, message, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SMS non envoye pour une demande d'acces vers {Telephone}", telephone);
            return false;
        }
    }

    private async Task<Dictionary<string, OcrExtractionResult?>> ExtractKycDocumentsAsync(
        SoumettreDemandeAccesRequest request,
        CancellationToken ct)
    {
        var documents = new[]
        {
            (Key: "registreCommerce", ExpectedType: "registre", File: request.RegistreCommerce),
            (Key: "patente", ExpectedType: "patente", File: request.Patente),
            (Key: "cinResponsable", ExpectedType: "cin", File: request.CinResponsable),
            (Key: "rib", ExpectedType: "rib", File: request.Rib)
        };
        var results = new Dictionary<string, OcrExtractionResult?>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in documents)
        {
            var result = await ocrClient.ExtractAsync([document.File], ct);
            if (result?.OcrSuccess == true
                && (string.IsNullOrWhiteSpace(result.TypeDocument)
                    || !result.TypeDocument.Contains(document.ExpectedType, StringComparison.OrdinalIgnoreCase)))
            {
                result.OcrSuccess = false;
                result.ErreurMessage = $"Le document analyse dans la zone {document.Key} ne correspond pas au type attendu.";
            }
            results[document.Key] = result;
        }
        return results;
    }

    private static OcrExtractionResult MergeKycOcrResults(
        IReadOnlyDictionary<string, OcrExtractionResult?> results)
    {
        results.TryGetValue("registreCommerce", out var registre);
        results.TryGetValue("patente", out var patente);
        results.TryGetValue("cinResponsable", out var cin);
        results.TryGetValue("rib", out var rib);
        var all = results.Values.Where(value => value is not null).Cast<OcrExtractionResult>().ToArray();
        var errors = all.Where(value => !value.OcrSuccess || value.ConfidenceScore < .55d)
            .Select(value => value.ErreurMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message));

        return new OcrExtractionResult
        {
            OcrSuccess = all.Length == 4 && all.All(value => value.OcrSuccess && value.ConfidenceScore >= .55d),
            ConfidenceScore = all.Length == 0 ? 0 : all.Average(value => value.ConfidenceScore),
            MatriculeFiscalExtrait = registre?.MatriculeFiscalExtrait ?? patente?.MatriculeFiscalExtrait,
            RaisonSocialeExtraite = registre?.RaisonSocialeExtraite,
            FormeJuridiqueExtraite = registre?.FormeJuridiqueExtraite,
            NomGerantExtrait = registre?.NomGerantExtrait ?? cin?.NomGerantExtrait,
            NomExtrait = cin?.NomExtrait,
            PrenomExtrait = cin?.PrenomExtrait,
            CinExtrait = cin?.CinExtrait,
            RibExtrait = rib?.RibExtrait,
            AdresseExtraite = registre?.AdresseExtraite ?? patente?.AdresseExtraite,
            ErreurMessage = string.Join(" ", errors.DefaultIfEmpty("Analyse OCR incomplete : revue manuelle obligatoire.")),
            TexteBrut = string.Join(Environment.NewLine, all.Select(value => value.TexteBrut))
        };
    }

    private static void RecordKycEvent(
        Entreprise entreprise,
        string type,
        string? message,
        IReadOnlyCollection<string>? flagCodes)
    {
        var history = new List<object>();
        Dictionary<string, JsonElement> existing = new(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(entreprise.DonneesScoring))
        {
            try
            {
                using var doc = JsonDocument.Parse(entreprise.DonneesScoring);
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("history"))
                    {
                        if (property.Value.ValueKind == JsonValueKind.Array)
                            history.AddRange(property.Value.EnumerateArray().Select(x => (object)x.Clone()));
                        continue;
                    }
                    existing[property.Name] = property.Value.Clone();
                }
            }
            catch (JsonException)
            {
                // Keep a new auditable payload if legacy scoring data is malformed.
            }
        }

        history.Add(new
        {
            type,
            createdAt = DateTime.UtcNow,
            actor = "Administrateur TuniFlow",
            message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            flagCodes = flagCodes?.Where(code => !string.IsNullOrWhiteSpace(code)).Distinct().ToArray() ?? []
        });
        existing["history"] = JsonSerializer.SerializeToElement(history, JsonOptions);
        entreprise.DefinirScoring(entreprise.ScoreKyc, JsonSerializer.Serialize(existing, JsonOptions));
    }

    private static void EnsureDistinctDocuments(SoumettreDemandeAccesRequest request)
    {
        var files = new[]
        {
            ("Registre de commerce", request.RegistreCommerce),
            ("Patente", request.Patente),
            ("CIN du responsable", request.CinResponsable),
            ("RIB bancaire", request.Rib)
        };
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (label, file) in files)
        {
            if (file is null) continue;
            using var stream = file.OpenReadStream();
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            if (hashes.TryGetValue(hash, out var existing))
                throw new ValidationMetierException($"Le document '{label}' est identique a '{existing}'. Chaque emplacement exige sa propre piece.");
            hashes.Add(hash, label);
        }
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

    private static IReadOnlyCollection<string> ResolveCorrections(string? donneesScoring, IReadOnlyCollection<string> flagCodes)
    {
        var requested = new HashSet<string>(flagCodes.Where(c => !string.IsNullOrWhiteSpace(c)), StringComparer.OrdinalIgnoreCase);
        if (requested.Count == 0)
            return Array.Empty<string>();

        var labels = new List<string>();
        var resolvedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(donneesScoring))
        {
            try
            {
                using var doc = JsonDocument.Parse(donneesScoring);
                if (doc.RootElement.TryGetProperty("flags", out var flags) && flags.ValueKind == JsonValueKind.Array)
                {
                    foreach (var flag in flags.EnumerateArray())
                    {
                        if (flag.ValueKind == JsonValueKind.String)
                        {
                            var stringCode = flag.GetString();
                            if (!string.IsNullOrWhiteSpace(stringCode) && requested.Contains(stringCode))
                            {
                                labels.Add(FormatCorrection(stringCode, null));
                                resolvedCodes.Add(stringCode);
                            }
                            continue;
                        }

                        if (flag.ValueKind != JsonValueKind.Object)
                            continue;

                        var code = flag.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : null;
                        if (string.IsNullOrWhiteSpace(code) || !requested.Contains(code))
                            continue;

                        var message = flag.TryGetProperty("message", out var messageProp) ? messageProp.GetString() : null;
                        labels.Add(FormatCorrection(code, message));
                        resolvedCodes.Add(code);
                    }
                }
            }
            catch
            {
                // Fallback below: keep sending a useful email even if scoring JSON is legacy/corrupted.
            }
        }

        foreach (var code in requested)
        {
            if (!resolvedCodes.Contains(code))
                labels.Add(FormatCorrection(code, null));
        }

        return labels;
    }

    private static string FormatCorrection(string code, string? message)
    {
        var readableCode = Regex
            .Replace(code.Replace('_', ' ').Replace('-', ' '), "([a-z])([A-Z])", "$1 $2", RegexOptions.None, RegexMatchPolicy.Timeout)
            .Trim();
        var title = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(readableCode);
        return string.IsNullOrWhiteSpace(message)
            ? title
            : $"{title} : {message.Trim()}";
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
