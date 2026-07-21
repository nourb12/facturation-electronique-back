using System.Globalization;
using System.Text.Json;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Einvoicing.Application.Services;

public sealed class ScanService(
    IOcrClient ocrClient,
    IFileStorageService fileStorage,
    IScannedDocumentRepository scannedDocumentRepo,
    ITransactionRepository transactionRepo,
    IFournisseurRepository fournisseurRepo) : IScanService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string[]> RequiredFieldsByType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["facture"] = ["document_number", "issue_date", "supplier_name", "client_name", "total_ttc", "trade_sense", "payment_status"],
        ["proforma"] = ["document_number", "issue_date", "supplier_name", "client_name", "total_ttc", "trade_sense"],
        ["avoir"] = ["document_number", "issue_date", "supplier_name", "total_ttc", "trade_sense", "payment_status"],
        ["devis"] = ["document_number", "issue_date", "supplier_name", "client_name", "total_ttc"],
        ["bon_commande"] = ["document_number", "issue_date", "supplier_name", "client_name"],
        ["bon_livraison"] = ["document_number", "issue_date", "supplier_name", "client_name"],
        ["bon_sortie"] = ["document_number", "issue_date"],
        ["paiement_recu"] = ["document_number", "issue_date", "amount", "payment_status"],
        ["paiement_emis"] = ["document_number", "issue_date", "amount", "payment_status"],
        ["ordre_fabrication"] = ["document_number", "issue_date"]
    };

    private static readonly Dictionary<string, string> DocumentTypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["supplier_invoice"] = "facture",
        ["cash_receipt"] = "facture",
        ["payment_receipt"] = "paiement_recu",
        ["delivery_note"] = "bon_livraison",
        ["expense_report"] = "facture",
        ["bank_statement"] = "paiement_emis",
        ["credit_note"] = "avoir",
        ["quote"] = "devis",
        ["purchase_order"] = "bon_commande",
        ["other"] = "facture"
    };

    public async Task<ScanUploadResponseDto> UploadAsync(
        Guid entrepriseId,
        Guid utilisateurId,
        IFormFile file,
        string documentType,
        CancellationToken ct = default)
    {
        if (file is null || file.Length <= 0)
            throw new ValidationMetierException("Le fichier du scan est requis.");

        var normalizedType = NormalizeDocumentType(documentType);
        var savedPath = await fileStorage.SaveAsync(file, "scan", ct);

        var document = ScannedDocument.Creer(
            entrepriseId,
            utilisateurId,
            normalizedType,
            DocumentSource.MobileApp,
            savedPath,
            file.FileName,
            file.ContentType,
            file.Length);

        var ocr = await ocrClient.ExtractAccountingAsync(file, normalizedType, ct)
            ?? new AccountingOcrResult
            {
                OcrSuccess = false,
                DocumentType = normalizedType,
                ErrorMessage = "Aucune réponse OCR n'a été reçue."
            };

        normalizedType = NormalizeDocumentType(ocr.DocumentType);
        if (!document.DocumentType.Equals(normalizedType, StringComparison.OrdinalIgnoreCase))
            document.ChangerTypeDocument(utilisateurId, normalizedType);

        var fields = NormalizeFields(normalizedType, ocr.Fields);
        var missingFields = fields
            .Where(f => f.Required && string.IsNullOrWhiteSpace(f.Value))
            .Select(f => f.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        document.EnregistrerExtraction(
            utilisateurId,
            ocr.OverallConfidence,
            JsonSerializer.Serialize(fields, JsonOpts),
            JsonSerializer.Serialize(missingFields, JsonOpts),
            ocr.RawText,
            ocr.ErrorMessage);

        await scannedDocumentRepo.AjouterAsync(document, ct);
        await scannedDocumentRepo.SauvegarderAsync(ct);

        return ToUploadDto(document, fields, missingFields, ocr);
    }

    public async Task<ValidateScanResponseDto> ValidateAsync(
        Guid entrepriseId,
        Guid utilisateurId,
        ValidateScanRequest request,
        CancellationToken ct = default)
    {
        var document = await scannedDocumentRepo.ObtenirParIdAsync(request.ScannedDocumentId, ct)
            ?? throw new NotFoundException("Document scanné introuvable.");

        if (document.EntrepriseId != entrepriseId)
            throw new AccesRefuseException();

        var normalizedType = NormalizeDocumentType(request.DocumentType ?? document.DocumentType);
        if (!document.DocumentType.Equals(normalizedType, StringComparison.OrdinalIgnoreCase))
            document.ChangerTypeDocument(utilisateurId, normalizedType);

        var originalFields = NormalizeFields(normalizedType, DeserializeFields(document.FieldsJson));
        var updatedFields = ApplyManualValues(originalFields, request.Fields);
        var missingFields = updatedFields
            .Where(f => f.Required && string.IsNullOrWhiteSpace(f.Value))
            .Select(f => f.Key)
            .ToList();

        document.MarquerCommeReluMobile(
            utilisateurId,
            JsonSerializer.Serialize(updatedFields, JsonOpts),
            JsonSerializer.Serialize(missingFields, JsonOpts));

        Guid? transactionId = null;
        string message;
        Fournisseur? supplier = null;

        if (request.SupplierId.HasValue)
        {
            supplier = await fournisseurRepo.ObtenirParIdAsync(request.SupplierId.Value, ct)
                ?? throw new NotFoundException("Fournisseur introuvable.");
            if (supplier.EntrepriseId != entrepriseId)
                throw new AccesRefuseException();
        }

        if (ShouldCreateTransaction(document.DocumentType))
        {
            var transaction = BuildPendingTransaction(
                document,
                utilisateurId,
                updatedFields,
                supplier?.Id,
                supplier?.Nom ?? request.SupplierName,
                request.SupplierTaxId ?? supplier?.MatriculeFiscal);
            transaction.DefinirJustificatif(
                utilisateurId,
                document.FilePath,
                document.FileName,
                document.ContentType,
                document.SizeBytes,
                StatutTransaction.EnAttente);
            transaction.EnregistrerContexteRevision(
                utilisateurId,
                document.DocumentType,
                document.OverallConfidence,
                JsonSerializer.Serialize(updatedFields, JsonOpts),
                JsonSerializer.Serialize(missingFields, JsonOpts),
                supplier?.Id,
                supplier?.Nom ?? request.SupplierName ?? transaction.TiersNom,
                request.SupplierTaxId ?? supplier?.MatriculeFiscal);
            transaction.DefinirActivites(utilisateurId, JsonSerializer.Serialize(new[]
            {
                ExpenseActivityLogger.Create(
                    "mobile-submitted",
                    "Document envoyé depuis l'application mobile.")
            }, JsonOpts));

            await transactionRepo.AjouterAsync(transaction, ct);
            transactionId = transaction.Id;
            document.AssocierTransaction(utilisateurId, transaction.Id);
            message = "Document transmis pour vérification web.";
        }
        else
        {
            message = "Document relu et enregistré. Le traitement web détaillé sera ajouté au prochain sprint.";
        }

        scannedDocumentRepo.MettreAJour(document);
        await scannedDocumentRepo.SauvegarderAsync(ct);

        return new ValidateScanResponseDto(
            document.Id,
            document.DocumentType,
            document.Status.ToString(),
            transactionId,
            message);
    }

    private static ScanUploadResponseDto ToUploadDto(
        ScannedDocument document,
        List<ExtractedFieldDto> fields,
        List<string> missingFields,
        AccountingOcrResult? ocr)
    {
        var imageUrl = "/" + document.FilePath.Replace("\\", "/").TrimStart('/');
        var schemaVersion = ocr?.SchemaVersionSnake ?? ocr?.SchemaVersion ?? "1.0";
        return new ScanUploadResponseDto(
            schemaVersion,
            schemaVersion,
            document.Id,
            document.DocumentType,
            document.OverallConfidence,
            imageUrl,
            fields,
            missingFields,
            fields.Any(f => f.RequiresReview),
            ocr?.Document,
            document.RawText,
            ocr?.SourceMode ?? "ocr",
            ocr?.ImageQuality,
            document.ErrorMessage);
    }

    private static string NormalizeDocumentType(string? documentType)
    {
        var value = (documentType ?? string.Empty).Trim().ToLowerInvariant();
        value = value.Replace("-", "_").Replace(" ", "_");
        if (DocumentTypeAliases.TryGetValue(value, out var alias))
            value = alias;
        return RequiredFieldsByType.ContainsKey(value)
            ? value
            : throw new ValidationMetierException("Type de document non supporté.");
    }

    private static List<ExtractedFieldDto> NormalizeFields(string documentType, IEnumerable<ExtractedFieldDto>? fields)
    {
        var required = RequiredFieldsByType[documentType];
        var list = (fields ?? [])
            .Select(f =>
            {
                var hasValue = !string.IsNullOrWhiteSpace(f.Value);
                var isRequired = required.Contains(f.Key, StringComparer.OrdinalIgnoreCase);
                var confidence = Math.Clamp(f.Confidence, 0, 100);
                return new ExtractedFieldDto(
                    f.Key,
                    string.IsNullOrWhiteSpace(f.Label) ? f.Key : f.Label,
                    f.Value?.Trim(),
                    confidence,
                    isRequired,
                    !hasValue || confidence < 70);
            })
            .ToList();

        foreach (var key in required)
        {
            if (list.Any(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
                continue;

            list.Add(new ExtractedFieldDto(
                key,
                BuildDefaultLabel(key),
                null,
                0,
                true,
                true));
        }

        return list;
    }

    private static List<ExtractedFieldDto> DeserializeFields(string json)
        => JsonSerializer.Deserialize<List<ExtractedFieldDto>>(json, JsonOpts) ?? [];

    private static List<ExtractedFieldDto> ApplyManualValues(
        List<ExtractedFieldDto> originalFields,
        IEnumerable<ValidateScanFieldRequest>? requestFields)
    {
        var requestedFields = (requestFields ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Key))
            .ToList();
        var overrides = requestedFields
            .GroupBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key.Trim(), g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var merged = originalFields
            .Select(f =>
            {
                if (!overrides.TryGetValue(f.Key, out var update))
                    return f;

                var clean = string.IsNullOrWhiteSpace(update.Value) ? null : update.Value.Trim();
                return f with
                {
                    Value = clean,
                    RequiresReview = string.IsNullOrWhiteSpace(clean)
                        ? f.Required
                        : false
                };
            })
            .ToList();

        foreach (var update in requestedFields)
        {
            if (merged.Any(field => field.Key.Equals(update.Key, StringComparison.OrdinalIgnoreCase)))
                continue;

            var clean = string.IsNullOrWhiteSpace(update.Value) ? null : update.Value.Trim();
            if (string.IsNullOrWhiteSpace(clean))
                continue;

            var key = update.Key.Trim();
            var label = string.IsNullOrWhiteSpace(update.Label)
                ? BuildDefaultLabel(key)
                : update.Label.Trim();

            merged.Add(new ExtractedFieldDto(
                key,
                label,
                clean,
                Math.Clamp(update.Confidence ?? 100, 0, 100),
                update.Required ?? false,
                false));
        }

        return merged;
    }

    private static bool ShouldCreateTransaction(string documentType)
        => documentType switch
        {
            "facture" => true,
            "avoir" => true,
            "paiement_recu" => true,
            "paiement_emis" => true,
            _ => false
        };

    private static Transaction BuildPendingTransaction(
        ScannedDocument document,
        Guid utilisateurId,
        IReadOnlyList<ExtractedFieldDto> fields,
        Guid? fournisseurId,
        string? fournisseurNom,
        string? fournisseurMatriculeFiscal)
    {
        var values = fields.ToDictionary(f => f.Key, f => f.Value, StringComparer.OrdinalIgnoreCase);
        var amount = ResolveAmount(document.DocumentType, values);
        if (amount <= 0)
            throw new ValidationMetierException("Le montant total doit être renseigné avant l'envoi.");

        var label = ResolveLabel(document.DocumentType, values);
        var tiersNom = fournisseurNom ?? ResolveCounterparty(document.DocumentType, values);
        var currency = Read(values, "currency") ?? "TND";
        var date = ResolveDate(values);
        var transactionType = ResolveTransactionType(document.DocumentType, values);

        var transaction = Transaction.Creer(
            entrepriseId: document.EntrepriseId,
            creePar: utilisateurId,
            date: date,
            libelle: label,
            montant: amount,
            type: transactionType,
            devise: currency,
            tiersNom: tiersNom,
            description: $"Document scanné ({document.DocumentType}) depuis l'application mobile.",
            compte: null,
            factureId: null,
            source: DocumentSource.MobileApp,
            statut: StatutTransaction.EnAttente,
            statutJustificatif: StatutJustificatif.Present);
        transaction.EnregistrerContexteRevision(
            utilisateurId,
            document.DocumentType,
            document.OverallConfidence,
            JsonSerializer.Serialize(fields, JsonOpts),
            JsonSerializer.Serialize(fields.Where(x => x.Required && string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Key).ToList(), JsonOpts),
            fournisseurId,
            tiersNom,
            fournisseurMatriculeFiscal);
        return transaction;
    }

    private static TypeTransaction ResolveTransactionType(string documentType, IReadOnlyDictionary<string, string?> values)
    {
        var sens = (Read(values, "trade_sense") ?? string.Empty).Trim().ToLowerInvariant();
        if (documentType.Equals("avoir", StringComparison.OrdinalIgnoreCase))
        {
            if (sens.Contains("vente"))
                return TypeTransaction.Sortie;
            if (sens.Contains("achat"))
                return TypeTransaction.Entree;
        }

        if (documentType.Equals("paiement_recu", StringComparison.OrdinalIgnoreCase) || sens.Contains("vente"))
            return TypeTransaction.Entree;

        return TypeTransaction.Sortie;
    }

    private static decimal ResolveAmount(string documentType, IReadOnlyDictionary<string, string?> values)
    {
        var candidateKeys = documentType.StartsWith("paiement_", StringComparison.OrdinalIgnoreCase)
            ? new[] { "amount", "total_ttc" }
            : new[] { "total_to_pay", "total_ttc", "amount", "net_ht", "total_ht" };

        foreach (var key in candidateKeys)
        {
            var parsed = ParseDecimal(Read(values, key));
            if (parsed > 0)
                return parsed;
        }

        return 0m;
    }

    private static DateTime ResolveDate(IReadOnlyDictionary<string, string?> values)
    {
        var raw = Read(values, "issue_date");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var formats = new[] { "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "dd.MM.yyyy" };
            if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
                return exact;
            if (DateTime.TryParse(raw, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out var flexible))
                return flexible;
        }

        return DateTime.Today;
    }

    private static string ResolveLabel(string documentType, IReadOnlyDictionary<string, string?> values)
    {
        var counterparty = ResolveCounterparty(documentType, values);
        var baseLabel = documentType switch
        {
            "facture" => "Facture",
            "proforma" => "Facture proforma",
            "avoir" => "Avoir",
            "devis" => "Devis",
            "bon_commande" => "Bon de commande",
            "bon_livraison" => "Bon de livraison",
            "bon_sortie" => "Bon de sortie",
            "paiement_recu" => "Paiement reçu",
            "paiement_emis" => "Paiement émis",
            "ordre_fabrication" => "Ordre de fabrication",
            "supplier_invoice" => "Facture fournisseur",
            "cash_receipt" => "Ticket de caisse",
            "payment_receipt" => "Reçu de paiement",
            "delivery_note" => "Bon de livraison",
            "expense_report" => "Note de frais",
            "bank_statement" => "Relevé bancaire",
            "credit_note" => "Avoir",
            "quote" => "Devis",
            "purchase_order" => "Bon de commande",
            _ => "Document scanné"
        };

        return string.IsNullOrWhiteSpace(counterparty)
            ? baseLabel
            : $"{baseLabel} - {counterparty}";
    }

    private static string? ResolveCounterparty(string documentType, IReadOnlyDictionary<string, string?> values)
        => documentType switch
        {
            "paiement_recu" => Read(values, "client_name") ?? Read(values, "supplier_name"),
            "paiement_emis" => Read(values, "supplier_name") ?? Read(values, "client_name"),
            "facture" or "proforma" or "avoir" or "devis" or "bon_commande" or "bon_livraison" or "bon_sortie" or "ordre_fabrication"
                => Read(values, "supplier_name") ?? Read(values, "client_name"),
            "supplier_invoice" => Read(values, "supplier_name"),
            "cash_receipt" => Read(values, "merchant_name"),
            "payment_receipt" => Read(values, "issuer_name") ?? Read(values, "beneficiary_name"),
            "delivery_note" => Read(values, "supplier_name"),
            "expense_report" => Read(values, "expense_owner") ?? Read(values, "expense_type"),
            "bank_statement" => Read(values, "iban"),
            "credit_note" => Read(values, "supplier_name"),
            "quote" => Read(values, "supplier_name") ?? Read(values, "client_name"),
            "purchase_order" => Read(values, "supplier_name") ?? Read(values, "client_name"),
            _ => null
        };

    private static string? Read(IReadOnlyDictionary<string, string?> values, string key)
        => values.TryGetValue(key, out var value) ? value : null;

    private static decimal ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0m;

        var cleaned = raw.Trim()
            .Replace("TND", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("€", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty);

        if (decimal.TryParse(cleaned.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            return Math.Round(value, 3);

        return 0m;
    }

    private static string BuildDefaultLabel(string key)
        => key.Replace("_", " ", StringComparison.Ordinal)
            .Trim()
            .Replace("ttc", "TTC", StringComparison.OrdinalIgnoreCase)
            .Replace("tva", "TVA", StringComparison.OrdinalIgnoreCase);
}
