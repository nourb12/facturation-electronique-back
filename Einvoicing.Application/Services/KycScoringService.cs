using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Helpers;

namespace Einvoicing.Application.Services;

// ─── Résultats du scoring ────────────────────────────────────────────────────

public sealed class KycScoringResult
{
    public int Score { get; init; }
    public KycDecision Decision { get; init; }
    public List<KycFlag> Flags { get; init; } = new();
    public List<KycBreakdownItem> Breakdown { get; init; } = new();
    public List<KycComparisonItem> Comparisons { get; init; } = new();
}

public enum KycDecision
{
    AutoApprovalCandidate,  // score >= 80, aucun flag critique
    RevisionManuelle,       // score 50-79
    RejetAutomatique        // score < 50 OU flag critique
}

public sealed class KycFlag
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public FlagSeverity Severity { get; init; }
}

public enum FlagSeverity { Info, Warning, Error, Critical }

public sealed class KycBreakdownItem
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Points { get; init; }
    public int MaxPoints { get; init; }
    public bool Ok { get; init; }
    public string? Reason { get; init; }
}

public sealed class KycComparisonItem
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? FormValue { get; init; }
    public string? OcrValue { get; init; }
    public bool Available { get; init; }
    public bool Matched { get; init; }
    public bool Critical { get; init; }
}

// ─── OCR result (inchangé pour compatibilité avec OcrClient) ─────────────────
public sealed class OcrExtractionResult
{
    public bool OcrSuccess { get; set; }
    public double ConfidenceScore { get; set; }
    public string? TypeDocument { get; set; }
    public string? MatriculeFiscalExtrait { get; set; }
    public string? RaisonSocialeExtraite { get; set; }
    public string? NomGerantExtrait { get; set; }
    public string? NomExtrait { get; set; }
    public string? PrenomExtrait { get; set; }
    public string? CinExtrait { get; set; }
    public string? FormeJuridiqueExtraite { get; set; }
    public string? AdresseExtraite { get; set; }
    public string? DateCreationExtraite { get; set; }
    public int KycScore { get; set; }
    public string TexteBrut { get; set; } = string.Empty;
    public string? ErreurMessage { get; set; }
}

// ─── Service de scoring ───────────────────────────────────────────────────────

public sealed class KycScoringService
{
    // Plages de codes postaux par gouvernorat (données tunisiennes réelles)
    private static readonly Dictionary<string, (int Min, int Max)[]> CodePostalRanges = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tunis"] = [(1000, 1099)],
        ["Ariana"] = [(2000, 2099)],
        ["Ben Arous"] = [(2013, 2097)],
        ["Manouba"] = [(2010, 2098)],
        ["Nabeul"] = [(8000, 8099)],
        ["Zaghouan"] = [(1100, 1199)],
        ["Bizerte"] = [(7000, 7099)],
        ["Béja"] = [(9000, 9099)],
        ["Jendouba"] = [(8100, 8199)],
        ["Le Kef"] = [(7100, 7199)],
        ["Siliana"] = [(6100, 6199)],
        ["Sousse"] = [(4000, 4099)],
        ["Monastir"] = [(5000, 5099)],
        ["Mahdia"] = [(5100, 5199)],
        ["Sfax"] = [(3000, 3099)],
        ["Kairouan"] = [(3100, 3199)],
        ["Kasserine"] = [(1200, 1299)],
        ["Sidi Bouzid"] = [(9100, 9199)],
        ["Gabès"] = [(6000, 6099)],
        ["Médenine"] = [(4100, 4199)],
        ["Tataouine"] = [(3200, 3299)],
        ["Gafsa"] = [(2100, 2199)],
        ["Tozeur"] = [(2200, 2299)],
        ["Kébili"] = [(4200, 4299)],
    };

    public KycScoringResult Calculate(
        SoumettreDemandeAccesRequest request,
        string? cheminRc,
        string? cheminCin,
        string? cheminPatente,
        string? cheminRib,
        OcrExtractionResult? ocr = null)
    {
        var flags = new List<KycFlag>();
        var breakdown = new List<KycBreakdownItem>();

        // ── 1. Matricule fiscal — validation format (20 pts) ─────────────────
        var mfValide = MatriculeFiscalHelper.IsValid(request.MatriculeFiscal);
        breakdown.Add(new KycBreakdownItem
        {
            Key = "mf_valide",
            Label = "Matricule fiscal valide",
            Points = mfValide ? 20 : 0,
            MaxPoints = 20,
            Ok = mfValide,
            Reason = mfValide ? null : "Format matricule fiscal non conforme (attendu : 1495908/S ou 1234567A/B/M/000)."
        });
        if (!mfValide)
            flags.Add(new KycFlag
            {
                Code = "mf_invalide",
                Message = $"Matricule fiscal '{request.MatriculeFiscal}' non conforme au format tunisien.",
                Severity = FlagSeverity.Critical
            });

        // ── 2. Documents présents (20 pts) ───────────────────────────────────
        var docsPresents = new Dictionary<string, string?>
        {
            ["Registre de Commerce"] = cheminRc,
            ["Patente"] = cheminPatente,
            ["CIN responsable"] = cheminCin,
            ["RIB bancaire"] = cheminRib,
        };
        var manquants = docsPresents.Where(kv => string.IsNullOrWhiteSpace(kv.Value))
                                    .Select(kv => kv.Key).ToList();
        var documentsComplets = manquants.Count == 0;
        breakdown.Add(new KycBreakdownItem
        {
            Key = "documents_complets",
            Label = "4 documents obligatoires présents",
            Points = documentsComplets ? 20 : 0,
            MaxPoints = 20,
            Ok = documentsComplets,
            Reason = documentsComplets ? null : $"Documents manquants : {string.Join(", ", manquants)}."
        });
        if (!documentsComplets)
            flags.Add(new KycFlag
            {
                Code = "docs_incomplets",
                Message = $"Documents manquants : {string.Join(", ", manquants)}.",
                Severity = FlagSeverity.Error
            });

        // ── 3. Validation croisée champs formulaire (20 pts) ─────────────────
        var validationsCroisees = ValidationsChamps(request, flags);
        var toutesCroisees = validationsCroisees.All(v => v);
        breakdown.Add(new KycBreakdownItem
        {
            Key = "validation_croisee",
            Label = "Validation croisée champs (CP/gouvernorat, adresse, noms)",
            Points = toutesCroisees ? 20 : (validationsCroisees.Count(v => v) * 4),
            MaxPoints = 20,
            Ok = toutesCroisees,
            Reason = toutesCroisees ? null : "Des incohérences ont été détectées dans les données saisies."
        });

        // ── 4. Cohérence OCR / formulaire (30 pts) ───────────────────────────
        var comparisons = BuildComparisons(request, ocr);
        int ocrPoints = 0;
        string? ocrReason = null;

        if (ocr?.OcrSuccess == true && ocr.ConfidenceScore >= 0.55d)
        {
            var critiques = comparisons.Where(c => c.Critical && c.Available).ToList();
            var matches = critiques.Where(c => c.Matched).ToList();
            var mismatches = critiques.Where(c => !c.Matched).ToList();

            ocrPoints = critiques.Count == 0
                ? 10  // OCR disponible mais peu de champs extraits — bonus partiel
                : (int)Math.Round(30.0 * matches.Count / Math.Max(1, critiques.Count));

            foreach (var mm in mismatches)
            {
                flags.Add(new KycFlag
                {
                    Code = $"{mm.Key}_incoherent",
                    Message = $"Incohérence {mm.Label} : formulaire = '{mm.FormValue}' / OCR = '{mm.OcrValue}'.",
                    Severity = FlagSeverity.Error
                });
            }

            if (mismatches.Count == 0 && critiques.Count > 0)
                ocrPoints = 30;
            else if (mismatches.Count > 0)
                ocrReason = $"Écart(s) détecté(s) : {string.Join(", ", mismatches.Select(m => m.Label.ToLowerInvariant()))}.";
        }
        else if (ocr is not null)
        {
            // OCR appelé mais a échoué (microservice indispo ou confidence trop faible)
            flags.Add(new KycFlag
            {
                Code = "ocr_echoue",
                Message = ocr.ErreurMessage ?? "Microservice OCR indisponible — analyse manuelle requise.",
                Severity = FlagSeverity.Warning
            });
            flags.Add(new KycFlag
            {
                Code = "analyse_manuelle_requise",
                Message = "Le dossier doit être vérifié manuellement (OCR non exploitable).",
                Severity = FlagSeverity.Warning
            });
            ocrReason = "Aucune donnée OCR exploitable — comparaison impossible.";
        }
        else
        {
            // Pas d'OCR du tout (service non configuré) — scoring sans comparaison
            ocrReason = "Service OCR non configuré. Vérification manuelle des documents requise.";
            flags.Add(new KycFlag
            {
                Code = "analyse_manuelle_requise",
                Message = "Service OCR non configuré — validation manuelle obligatoire.",
                Severity = FlagSeverity.Info
            });
        }

        breakdown.Add(new KycBreakdownItem
        {
            Key = "coherence_ocr",
            Label = "Cohérence OCR / données formulaire",
            Points = ocrPoints,
            MaxPoints = 30,
            Ok = ocrPoints >= 25,
            Reason = ocrReason
        });

        // ── 5. RIB checksum (10 pts) ─────────────────────────────────────────
        // Validation locale, aucune API externe
        var ribPoints = 0;
        if (!string.IsNullOrWhiteSpace(request.RespTel))
        {
            // Placeholder — le RIB est un fichier uploadé, pas un champ texte
            // Dans une v2 : ajouter champ RibNumero au formulaire et valider ISO 7064
            ribPoints = documentsComplets ? 10 : 0;
        }
        else
        {
            ribPoints = documentsComplets ? 10 : 0;
        }
        breakdown.Add(new KycBreakdownItem
        {
            Key = "rib_present",
            Label = "RIB bancaire fourni",
            Points = ribPoints,
            MaxPoints = 10,
            Ok = ribPoints > 0,
            Reason = ribPoints > 0 ? null : "RIB bancaire absent."
        });

        // ── Calcul score final + décision ─────────────────────────────────────
        var score = Math.Clamp(breakdown.Sum(b => b.Points), 0, 100);
        var hasCritical = flags.Any(f => f.Severity == FlagSeverity.Critical);

        var decision = (score, hasCritical) switch
        {
            (_, true) => KycDecision.RejetAutomatique,
            ( >= 80, false) => KycDecision.AutoApprovalCandidate,
            ( >= 50, false) => KycDecision.RevisionManuelle,
            _ => KycDecision.RejetAutomatique
        };

        return new KycScoringResult
        {
            Score = score,
            Decision = decision,
            Flags = flags.DistinctBy(f => f.Code).ToList(),
            Breakdown = breakdown,
            Comparisons = comparisons
        };
    }

    // ── Validations croisées champs formulaire ────────────────────────────────

    private static List<bool> ValidationsChamps(SoumettreDemandeAccesRequest req, List<KycFlag> flags)
    {
        var results = new List<bool>();

        // Code postal × gouvernorat
        if (!string.IsNullOrWhiteSpace(req.CodePostal) && !string.IsNullOrWhiteSpace(req.Gouvernorat))
        {
            var cpOk = IsCodePostalValid(req.CodePostal, req.Gouvernorat);
            results.Add(cpOk);
            if (!cpOk)
                flags.Add(new KycFlag
                {
                    Code = "cp_gouvernorat_incoherent",
                    Message = $"Code postal {req.CodePostal} ne correspond pas au gouvernorat {req.Gouvernorat}.",
                    Severity = FlagSeverity.Warning
                });
        }

        // Prénom/Nom — pas de chiffres, min 3 chars
        var prenomOk = !string.IsNullOrWhiteSpace(req.RespPrenom)
            && req.RespPrenom.Trim().Length >= 3
            && !Regex.IsMatch(req.RespPrenom, @"\d");
        results.Add(prenomOk);
        if (!prenomOk)
            flags.Add(new KycFlag
            {
                Code = "prenom_invalide",
                Message = $"Prénom '{req.RespPrenom}' invalide (min 3 caractères, sans chiffres).",
                Severity = FlagSeverity.Warning
            });

        var nomOk = !string.IsNullOrWhiteSpace(req.RespNom)
            && req.RespNom.Trim().Length >= 3
            && !Regex.IsMatch(req.RespNom, @"\d");
        results.Add(nomOk);
        if (!nomOk)
            flags.Add(new KycFlag
            {
                Code = "nom_invalide",
                Message = $"Nom '{req.RespNom}' invalide (min 3 caractères, sans chiffres).",
                Severity = FlagSeverity.Warning
            });

        // Adresse — min 10 chars, doit contenir un chiffre
        var adresseOk = !string.IsNullOrWhiteSpace(req.Adresse)
            && req.Adresse.Trim().Length >= 10
            && Regex.IsMatch(req.Adresse, @"\d");
        results.Add(adresseOk);
        if (!adresseOk)
            flags.Add(new KycFlag
            {
                Code = "adresse_invalide",
                Message = "L'adresse doit contenir au moins 10 caractères et un numéro de rue.",
                Severity = FlagSeverity.Warning
            });

        // Raison sociale — pas de chaîne gibberish (5+ consonnes consécutives)
        if (!string.IsNullOrWhiteSpace(req.RaisonSociale))
        {
            var rsGibberish = LooksLikeGibberish(req.RaisonSociale);
            results.Add(!rsGibberish);
            if (rsGibberish)
                flags.Add(new KycFlag
                {
                    Code = "raison_sociale_suspecte",
                    Message = $"Raison sociale '{req.RaisonSociale}' semble invalide.",
                    Severity = FlagSeverity.Warning
                });
        }

        return results;
    }

    // ── Comparaisons OCR / formulaire ─────────────────────────────────────────

    private static List<KycComparisonItem> BuildComparisons(
        SoumettreDemandeAccesRequest request,
        OcrExtractionResult? ocr)
    {
        if (ocr?.OcrSuccess != true)
            return new List<KycComparisonItem>();

        var responsableoorm = JoinValues(request.RespPrenom, request.RespNom);
        var responsableOcr = !string.IsNullOrWhiteSpace(ocr.NomGerantExtrait)
            ? ocr.NomGerantExtrait
            : JoinValues(ocr.PrenomExtrait, ocr.NomExtrait);

        return new List<KycComparisonItem>
        {
            BuildComparison("matriculeFiscal", "Matricule fiscal",
                request.MatriculeFiscal, ocr.MatriculeFiscalExtrait,
                ValuesMatchForMatricule, critical: true),

            BuildComparison("raisonSociale", "Raison sociale",
                request.RaisonSociale, ocr.RaisonSocialeExtraite,
                ValuesMatchForText, critical: true),

            BuildComparison("formeJuridique", "Forme juridique",
                request.FormeJuridique, ocr.FormeJuridiqueExtraite,
                ValuesMatchForText, critical: true),

            BuildComparison("responsableLegal", "Responsable légal",
                responsableoorm, responsableOcr,
                ValuesMatchForText, critical: true),

            BuildComparison("adresse", "Adresse",
                request.Adresse, ocr.AdresseExtraite,
                ValuesMatchForText, critical: false),
        };
    }

    private static KycComparisonItem BuildComparison(
        string key, string label,
        string? formValue, string? docValue,
        Func<string?, string?, bool> comparer,
        bool critical)
    {
        var available = !string.IsNullOrWhiteSpace(formValue) && !string.IsNullOrWhiteSpace(docValue);
        return new KycComparisonItem
        {
            Key = key,
            Label = label,
            FormValue = formValue?.Trim(),
            OcrValue = docValue?.Trim(),
            Available = available,
            Matched = available && comparer(formValue, docValue),
            Critical = critical
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsCodePostalValid(string cp, string gouvernorat)
    {
        if (!int.TryParse(cp.Trim(), out var num)) return false;
        if (!CodePostalRanges.TryGetValue(gouvernorat, out var ranges)) return true; // inconnu → pas bloquant
        return ranges.Any(r => num >= r.Min && num <= r.Max);
    }

    private static bool LooksLikeGibberish(string value)
    {
        var v = value.ToLowerInvariant();
        var latin = Regex.Replace(v, @"[^a-z]", "");
        return Regex.IsMatch(latin, @"[bcdfghjklmnpqrstvwxyz]{5,}");
    }

    private static bool ValuesMatchForMatricule(string? a, string? b)
        => MatriculeFiscalHelper.Normalize(a) == MatriculeFiscalHelper.Normalize(b);

    private static bool ValuesMatchForText(string? a, string? b)
    {
        var na = NormalizeText(a);
        var nb = NormalizeText(b);
        if (string.IsNullOrWhiteSpace(na) || string.IsNullOrWhiteSpace(nb)) return false;
        if (na.Contains(nb, StringComparison.Ordinal) || nb.Contains(na, StringComparison.Ordinal)) return true;

        var tokA = na.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tokB = nb.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var overlap = tokA.Intersect(tokB).Count();
        return overlap >= Math.Max(1, Math.Min(tokA.Length, tokB.Length) - 1);
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        var s = sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        s = Regex.Replace(s, @"\b(STE|STE\.|SOCIETE|SOCIETE\.|SARL|SUARL|SA|SNC)\b", " ");
        s = Regex.Replace(s, @"[^A-Z0-9]", " ");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    private static string JoinValues(params string?[] values)
        => string.Join(' ', values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));
}