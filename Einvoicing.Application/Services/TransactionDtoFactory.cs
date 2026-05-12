using System.Text.Json;
using Einvoicing.Application.DTOs;
using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Services;

internal static class TransactionDtoFactory
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static TransactionDto Create(Transaction t)
    {
        TransactionDocumentDto? doc = null;
        if (!string.IsNullOrWhiteSpace(t.JustificatifChemin))
        {
            var url = "/" + t.JustificatifChemin.Trim().Replace("\\", "/");
            doc = new TransactionDocumentDto(
                t.JustificatifNomFichier ?? "justificatif",
                url,
                t.JustificatifContentType,
                t.JustificatifTailleOctets
            );
        }

        return new TransactionDto(
            t.Id,
            t.EntrepriseId,
            t.Date,
            t.Libelle,
            t.Description,
            t.TiersNom,
            t.CategorieNom,
            t.Source.ToString(),
            t.FournisseurId,
            t.FournisseurMatriculeFiscal,
            t.DocumentType,
            t.OcrOverallConfidence,
            t.Type,
            t.Statut,
            t.StatutJustificatif,
            t.Montant,
            t.Devise,
            t.Compte,
            t.FactureId,
            doc,
            Deserialize<List<ExtractedFieldDto>>(t.ReviewFieldsJson) ?? [],
            Deserialize<List<string>>(t.MissingFieldsJson) ?? [],
            Deserialize<List<TransactionAllocationDto>>(t.AllocationsJson) ?? [],
            Deserialize<List<TransactionCommentDto>>(t.CommentsJson) ?? [],
            Deserialize<List<TransactionActivityDto>>(t.ActivitiesJson) ?? [],
            Deserialize<TransactionBankMatchDto>(t.BankMatchJson),
            t.AccountingPeriodLabel,
            t.RecoverableVatAmount,
            t.RecoverableVatRate,
            t.CreeLe,
            t.ModifieLe
        );
    }

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOpts);
        }
        catch
        {
            return default;
        }
    }
}
