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
        ["supplier_invoice"] = ["supplier_name", "invoice_number", "issue_date", "total_ttc"],
        ["cash_receipt"] = ["merchant_name", "issue_date", "total_ttc"],
        ["payment_receipt"] = ["reference", "issue_date", "amount"],
        ["delivery_note"] = ["delivery_note_number", "issue_date", "supplier_name"],
        ["expense_report"] = ["expense_type", "issue_date", "total_ttc"],
        ["bank_statement"] = ["iban", "period_label"]
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

        return ToUploadDto(document, fields, missingFields);
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

        var originalFields = DeserializeFields(document.FieldsJson);
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
        List<string> missingFields)
    {
        var imageUrl = "/" + document.FilePath.Replace("\\", "/").TrimStart('/');
        return new ScanUploadResponseDto(
            document.Id,
            document.DocumentType,
            document.OverallConfidence,
            imageUrl,
            fields,
            missingFields,
            fields.Any(f => f.RequiresReview),
            document.RawText,
            document.ErrorMessage);
    }

    private static string NormalizeDocumentType(string? documentType)
    {
        var value = (documentType ?? string.Empty).Trim().ToLowerInvariant();
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
        var overrides = (requestFields ?? [])
            .GroupBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);

        return originalFields
            .Select(f =>
            {
                if (!overrides.TryGetValue(f.Key, out var value))
                    return f;

                var clean = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                return f with
                {
                    Value = clean,
                    RequiresReview = string.IsNullOrWhiteSpace(clean)
                        ? f.Required
                        : false
                };
            })
            .ToList();
    }

    private static bool ShouldCreateTransaction(string documentType)
        => documentType switch
        {
            "supplier_invoice" => true,
            "cash_receipt" => true,
            "payment_receipt" => true,
            "expense_report" => true,
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

        var transaction = Transaction.Creer(
            entrepriseId: document.EntrepriseId,
            creePar: utilisateurId,
            date: date,
            libelle: label,
            montant: amount,
            type: TypeTransaction.Sortie,
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

    private static decimal ResolveAmount(string documentType, IReadOnlyDictionary<string, string?> values)
    {
        var candidateKeys = documentType.Equals("payment_receipt", StringComparison.OrdinalIgnoreCase)
            ? new[] { "amount", "total_ttc" }
            : new[] { "total_ttc", "amount", "total_ht" };

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
            "supplier_invoice" => "Facture fournisseur",
            "cash_receipt" => "Ticket de caisse",
            "payment_receipt" => "Reçu de paiement",
            "delivery_note" => "Bon de livraison",
            "expense_report" => "Note de frais",
            "bank_statement" => "Relevé bancaire",
            _ => "Document scanné"
        };

        return string.IsNullOrWhiteSpace(counterparty)
            ? baseLabel
            : $"{baseLabel} - {counterparty}";
    }

    private static string? ResolveCounterparty(string documentType, IReadOnlyDictionary<string, string?> values)
        => documentType switch
        {
            "supplier_invoice" => Read(values, "supplier_name"),
            "cash_receipt" => Read(values, "merchant_name"),
            "payment_receipt" => Read(values, "issuer_name") ?? Read(values, "beneficiary_name"),
            "delivery_note" => Read(values, "supplier_name"),
            "expense_report" => Read(values, "expense_owner") ?? Read(values, "expense_type"),
            "bank_statement" => Read(values, "iban"),
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
