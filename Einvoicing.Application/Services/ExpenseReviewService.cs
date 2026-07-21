using System.Text.Json;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class ExpenseReviewService(
    ITransactionRepository transactionRepo,
    IFournisseurRepository fournisseurRepo,
    IUtilisateurRepository utilisateurRepo) : IExpenseReviewService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<ListeTransactionsDto> ListerPendingReviewAsync(
        Guid entrepriseId,
        int page,
        int parPage,
        string? recherche,
        CancellationToken ct = default)
    {
        var filtre = new FiltreTransactionsRequest(
            Page: page,
            ParPage: parPage,
            Statut: StatutTransaction.EnAttente,
            Recherche: recherche);

        var (items, total) = await transactionRepo.ListerAsync(entrepriseId, filtre, ct);
        return new ListeTransactionsDto(items.Select(TransactionDtoFactory.Create).ToList(), total, page, parPage);
    }

    public async Task<TransactionDto> SauvegarderJustificatifAsync(
        Guid transactionId,
        Guid entrepriseId,
        Guid utilisateurId,
        ReceiptReviewRequest request,
        CancellationToken ct = default)
    {
        var tx = await RequireTransactionAsync(transactionId, entrepriseId, ct);
        var actorName = await GetActorNameAsync(utilisateurId, ct);
        var mergedFields = MergeReviewFields(tx.ReviewFieldsJson, request.Fields);
        var missing = mergedFields.Where(x => x.Required && string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Key).ToList();

        Fournisseur? supplier = null;
        if (request.FournisseurId.HasValue)
        {
            supplier = await fournisseurRepo.ObtenirParIdAsync(request.FournisseurId.Value, ct)
                ?? throw new NotFoundException("Fournisseur introuvable.");
            if (supplier.EntrepriseId != entrepriseId)
                throw new AccesRefuseException();
        }

        var tiersNom = supplier?.Nom ?? request.TiersNom ?? tx.TiersNom;
        tx.AppliquerCorrectionJustificatif(
            utilisateurId,
            request.Date,
            request.Montant,
            request.Libelle,
            tiersNom,
            supplier?.Id,
            request.FournisseurMatriculeFiscal ?? supplier?.MatriculeFiscal,
            Serialize(mergedFields),
            Serialize(missing));

        var activities = Deserialize<List<TransactionActivityDto>>(tx.ActivitiesJson) ?? [];
        activities.Add(ExpenseActivityLogger.Create(
            "receipt-reviewed",
            $"Justificatif relu par {actorName}.",
            utilisateurId,
            actorName));
        if (supplier is not null)
        {
            activities.Add(ExpenseActivityLogger.Create(
                "supplier-linked",
                $"Fournisseur confirmé : {supplier.Nom}.",
                utilisateurId,
                actorName));
        }

        tx.DefinirActivites(utilisateurId, Serialize(activities));
        transactionRepo.MettreAJour(tx);
        await transactionRepo.SauvegarderAsync(ct);
        return TransactionDtoFactory.Create(tx);
    }

    public async Task<TransactionDto> CategoriserAsync(
        Guid transactionId,
        Guid entrepriseId,
        Guid utilisateurId,
        CategorizeExpenseRequest request,
        CancellationToken ct = default)
    {
        var tx = await RequireTransactionAsync(transactionId, entrepriseId, ct);
        var actorName = await GetActorNameAsync(utilisateurId, ct);

        var allocations = request.Allocations ?? [];
        if (allocations.Count > 0)
        {
            var totalPercent = Math.Round(allocations.Sum(x => x.Percentage), 2);
            if (Math.Abs(totalPercent - 100m) > 0.01m)
                throw new ValidationMetierException("La ventilation analytique doit totaliser 100%.");
        }

        var dtoAllocations = allocations.Select(x => new TransactionAllocationDto(
            Guid.NewGuid(),
            x.CategoryName.Trim(),
            Math.Round(x.Percentage, 2),
            Math.Round(x.Amount, 3))).ToList();

        tx.DefinirAnalytique(
            utilisateurId,
            request.CategorieNom,
            Serialize(dtoAllocations),
            request.AccountingPeriodLabel,
            request.RecoverableVatAmount,
            request.RecoverableVatRate);

        var activities = Deserialize<List<TransactionActivityDto>>(tx.ActivitiesJson) ?? [];
        activities.Add(ExpenseActivityLogger.Create(
            "categorized",
            $"Catégorisation enregistrée ({request.CategorieNom ?? "Sans catégorie"}).",
            utilisateurId,
            actorName));
        tx.DefinirActivites(utilisateurId, Serialize(activities));

        transactionRepo.MettreAJour(tx);
        await transactionRepo.SauvegarderAsync(ct);
        return TransactionDtoFactory.Create(tx);
    }

    public async Task<TransactionDto> EnregistrerRapprochementAsync(
        Guid transactionId,
        Guid entrepriseId,
        Guid utilisateurId,
        BankMatchRequest request,
        CancellationToken ct = default)
    {
        var tx = await RequireTransactionAsync(transactionId, entrepriseId, ct);
        var actorName = await GetActorNameAsync(utilisateurId, ct);
        var existing = Deserialize<TransactionBankMatchDto>(tx.BankMatchJson);
        var next = new TransactionBankMatchDto(
            request.Status,
            request.Reference,
            request.Date,
            request.Amount,
            request.Counterparty,
            existing?.Suggestions ?? []);

        tx.DefinirRapprochementBancaire(utilisateurId, Serialize(next));

        var activities = Deserialize<List<TransactionActivityDto>>(tx.ActivitiesJson) ?? [];
        activities.Add(ExpenseActivityLogger.Create(
            "bank-match",
            $"Rapprochement mis à jour ({request.Status ?? "manuel"}).",
            utilisateurId,
            actorName));
        tx.DefinirActivites(utilisateurId, Serialize(activities));

        transactionRepo.MettreAJour(tx);
        await transactionRepo.SauvegarderAsync(ct);
        return TransactionDtoFactory.Create(tx);
    }

    public async Task<TransactionDto> AjouterCommentaireAsync(
        Guid transactionId,
        Guid entrepriseId,
        Guid utilisateurId,
        AddExpenseCommentRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ValidationMetierException("Le commentaire ne peut pas être vide.");

        var tx = await RequireTransactionAsync(transactionId, entrepriseId, ct);
        var actorName = await GetActorNameAsync(utilisateurId, ct);
        var comments = Deserialize<List<TransactionCommentDto>>(tx.CommentsJson) ?? [];
        comments.Add(new TransactionCommentDto(
            Guid.NewGuid(),
            utilisateurId,
            actorName,
            request.Message.Trim(),
            DateTime.UtcNow));
        tx.DefinirCommentaires(utilisateurId, Serialize(comments));

        var activities = Deserialize<List<TransactionActivityDto>>(tx.ActivitiesJson) ?? [];
        activities.Add(ExpenseActivityLogger.Create(
            "comment-added",
            $"Commentaire ajouté par {actorName}.",
            utilisateurId,
            actorName));
        tx.DefinirActivites(utilisateurId, Serialize(activities));

        transactionRepo.MettreAJour(tx);
        await transactionRepo.SauvegarderAsync(ct);
        return TransactionDtoFactory.Create(tx);
    }

    public async Task<TransactionDto> ApprouverAsync(
        Guid transactionId,
        Guid entrepriseId,
        Guid utilisateurId,
        ApproveExpenseRequest request,
        CancellationToken ct = default)
    {
        var tx = await RequireTransactionAsync(transactionId, entrepriseId, ct);
        var actorName = await GetActorNameAsync(utilisateurId, ct);

        if (!string.IsNullOrWhiteSpace(request.Commentaire))
        {
            var comments = Deserialize<List<TransactionCommentDto>>(tx.CommentsJson) ?? [];
            comments.Add(new TransactionCommentDto(
                Guid.NewGuid(),
                utilisateurId,
                actorName,
                request.Commentaire.Trim(),
                DateTime.UtcNow));
            tx.DefinirCommentaires(utilisateurId, Serialize(comments));
        }

        tx.ValiderRevision(utilisateurId);

        var activities = Deserialize<List<TransactionActivityDto>>(tx.ActivitiesJson) ?? [];
        activities.Add(ExpenseActivityLogger.Create(
            "approved",
            $"Dépense validée par {actorName}.",
            utilisateurId,
            actorName));
        tx.DefinirActivites(utilisateurId, Serialize(activities));

        transactionRepo.MettreAJour(tx);
        await transactionRepo.SauvegarderAsync(ct);
        return TransactionDtoFactory.Create(tx);
    }

    private async Task<Transaction> RequireTransactionAsync(Guid transactionId, Guid entrepriseId, CancellationToken ct)
    {
        var tx = await transactionRepo.ObtenirParIdAsync(transactionId, ct)
            ?? throw new NotFoundException("Dépense introuvable.");
        if (tx.EntrepriseId != entrepriseId)
            throw new AccesRefuseException();
        return tx;
    }

    private async Task<string> GetActorNameAsync(Guid utilisateurId, CancellationToken ct)
    {
        var user = await utilisateurRepo.ObtenirParIdAsync(utilisateurId, ct);
        return user?.NomComplet ?? "Utilisateur";
    }

    private static List<ExtractedFieldDto> MergeReviewFields(string originalJson, IEnumerable<ValidateScanFieldRequest> updates)
    {
        var original = Deserialize<List<ExtractedFieldDto>>(originalJson) ?? [];
        var updateList = (updates ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToList();
        var overrideMap = updateList
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key.Trim(), g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var merged = original.Select(field =>
        {
            if (!overrideMap.TryGetValue(field.Key, out var update))
                return field;

            var clean = string.IsNullOrWhiteSpace(update.Value) ? null : update.Value.Trim();
            return field with
            {
                Value = clean,
                RequiresReview = string.IsNullOrWhiteSpace(clean) || field.Confidence < 70
            };
        }).ToList();

        foreach (var update in updateList)
        {
            if (merged.Any(field => field.Key.Equals(update.Key, StringComparison.OrdinalIgnoreCase)))
                continue;

            var clean = string.IsNullOrWhiteSpace(update.Value) ? null : update.Value.Trim();
            if (string.IsNullOrWhiteSpace(clean))
                continue;

            var key = update.Key.Trim();
            var label = string.IsNullOrWhiteSpace(update.Label)
                ? key.Replace("_", " ", StringComparison.Ordinal)
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

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOpts);
}
