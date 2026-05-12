using System.Globalization;
using System.Text;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class TransactionService(
    ITransactionRepository transactionRepo,
    IEntrepriseRepository entrepriseRepo,
    IFactureRepository factureRepo,
    IFileStorageService fileStorage
) : ITransactionService
{
    public async Task<ListeTransactionsDto> ListerAsync(
        Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default)
    {
        var (items, total) = await transactionRepo.ListerAsync(entrepriseId, filtre, ct);
        var dtos = items.Select(TransactionDtoFactory.Create).ToList();
        return new ListeTransactionsDto(dtos, total, filtre.Page, filtre.ParPage);
    }

    public async Task<TransactionDto> ObtenirParIdAsync(
        Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var tx = await transactionRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Transaction introuvable.");

        if (tx.EntrepriseId != entrepriseId)
            throw new AccesRefuseException();

        return TransactionDtoFactory.Create(tx);
    }

    public async Task<TransactionDto> CreerAsync(
        Guid entrepriseId, Guid utilisateurId, CreerTransactionRequest req, CancellationToken ct = default)
    {
        if (req.FactureId is not null)
        {
            var facture = await factureRepo.ObtenirParIdAsync(req.FactureId.Value, ct)
                ?? throw new ValidationMetierException("Facture introuvable.");
            if (facture.EntrepriseId != entrepriseId)
                throw new AccesRefuseException();
        }

        var tx = Transaction.Creer(
            entrepriseId: entrepriseId,
            creePar: utilisateurId,
            date: req.Date,
            libelle: req.Libelle,
            montant: req.Montant,
            type: req.Type,
            devise: req.Devise,
            tiersNom: req.TiersNom,
            categorieNom: req.CategorieNom,
            description: req.Description,
            compte: req.Compte,
            factureId: req.FactureId,
            source: DocumentSource.Web,
            statut: req.FactureId is null ? StatutTransaction.NonJustifiee : StatutTransaction.Justifiee,
            statutJustificatif: null
        );

        await transactionRepo.AjouterAsync(tx, ct);
        await transactionRepo.SauvegarderAsync(ct);
        return TransactionDtoFactory.Create(tx);
    }

    public async Task<TransactionDto> MettreAJourAsync(
        Guid id, Guid entrepriseId, Guid utilisateurId, MettreAJourTransactionRequest req, CancellationToken ct = default)
    {
        var tx = await transactionRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Transaction introuvable.");

        if (tx.EntrepriseId != entrepriseId)
            throw new AccesRefuseException();

        if (req.FactureId is not null)
        {
            var facture = await factureRepo.ObtenirParIdAsync(req.FactureId.Value, ct)
                ?? throw new ValidationMetierException("Facture introuvable.");
            if (facture.EntrepriseId != entrepriseId)
                throw new AccesRefuseException();
        }

        tx.MettreAJour(
            modifiePar: utilisateurId,
            date: req.Date,
            libelle: req.Libelle,
            montant: req.Montant,
            type: req.Type,
            devise: req.Devise,
            tiersNom: req.TiersNom,
            categorieNom: req.CategorieNom,
            description: req.Description,
            compte: req.Compte,
            factureId: req.FactureId,
            statut: req.Statut,
            statutJustificatif: req.StatutJustificatif
        );

        // Auto: if justificatif marked optional/lost/present, treat as justified unless explicitly overridden.
        if (req.Statut is null && req.StatutJustificatif is not null)
        {
            tx.MettreAJour(utilisateurId, statut: StatutTransaction.Justifiee);
        }

        transactionRepo.MettreAJour(tx);
        await transactionRepo.SauvegarderAsync(ct);
        return TransactionDtoFactory.Create(tx);
    }

    public async Task SupprimerAsync(Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var tx = await transactionRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Transaction introuvable.");

        if (tx.EntrepriseId != entrepriseId)
            throw new AccesRefuseException();

        transactionRepo.Supprimer(tx);
        await transactionRepo.SauvegarderAsync(ct);
    }

    public async Task<TransactionDto> UploadJustificatifAsync(
        Guid id, Guid entrepriseId, Guid utilisateurId, Microsoft.AspNetCore.Http.IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length <= 0)
            throw new ValidationMetierException("Fichier justificatif invalide.");

        var tx = await transactionRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Transaction introuvable.");

        if (tx.EntrepriseId != entrepriseId)
            throw new AccesRefuseException();

        var chemin = await fileStorage.SaveAsync(file, "transactions", ct);
        tx.DefinirJustificatif(utilisateurId, chemin, file.FileName, file.ContentType, file.Length);

        transactionRepo.MettreAJour(tx);
        await transactionRepo.SauvegarderAsync(ct);
        return TransactionDtoFactory.Create(tx);
    }

    public async Task<TransactionDto> LierFactureAsync(
        Guid id, Guid entrepriseId, Guid utilisateurId, Guid factureId, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirParIdAsync(factureId, ct)
            ?? throw new ValidationMetierException("Facture introuvable.");
        if (facture.EntrepriseId != entrepriseId)
            throw new AccesRefuseException();

        var tx = await transactionRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Transaction introuvable.");
        if (tx.EntrepriseId != entrepriseId)
            throw new AccesRefuseException();

        tx.LierFacture(utilisateurId, factureId);
        transactionRepo.MettreAJour(tx);
        await transactionRepo.SauvegarderAsync(ct);
        return TransactionDtoFactory.Create(tx);
    }

    public Task<TransactionCountersDto> CountersAsync(Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default)
        => transactionRepo.CountersAsync(entrepriseId, filtre, ct);

    public Task<List<TransactionCategorieResumeDto>> ResumeParCategorieAsync(Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default)
        => transactionRepo.ResumeParCategorieAsync(entrepriseId, filtre, ct);

    public async Task<byte[]> ExporterCsvAsync(Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default)
    {
        var items = await transactionRepo.ListerTousAsync(entrepriseId, filtre, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Date,Libelle,Tiers,Categorie,Type,Montant,Devise,Statut,Justificatif");

        foreach (var t in items.OrderByDescending(x => x.Date))
        {
            var signed = t.Type == TypeTransaction.Sortie ? -t.Montant : t.Montant;
            sb.AppendLine(string.Join(",",
                Csv(t.Date.ToString("yyyy-MM-dd")),
                Csv(t.Libelle),
                Csv(t.TiersNom ?? string.Empty),
                Csv(t.CategorieNom ?? string.Empty),
                Csv(t.Type.ToString()),
                Csv(signed.ToString("0.000", CultureInfo.InvariantCulture)),
                Csv(t.Devise),
                Csv(t.Statut.ToString()),
                Csv(t.JustificatifNomFichier ?? string.Empty)
            ));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExporterPdfAsync(Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default)
    {
        var entreprise = await entrepriseRepo.ObtenirParIdAsync(entrepriseId, ct)
            ?? throw new NotFoundException("Entreprise introuvable.");

        var items = await transactionRepo.ListerTousAsync(entrepriseId, filtre, ct);
        return TransactionJournalPdfBuilder.Generate(entreprise, items, filtre);
    }

    private static string Csv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

}
