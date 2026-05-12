using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class Transaction
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public Guid CreePar { get; private set; }
    public Guid? ModifiePar { get; private set; }

    public DateTime Date { get; private set; }
    public string Libelle { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public string? TiersNom { get; private set; }
    public string? CategorieNom { get; private set; }
    public DocumentSource Source { get; private set; }
    public Guid? FournisseurId { get; private set; }
    public string? FournisseurMatriculeFiscal { get; private set; }
    public string? DocumentType { get; private set; }
    public int? OcrOverallConfidence { get; private set; }
    public string ReviewFieldsJson { get; private set; } = "[]";
    public string MissingFieldsJson { get; private set; } = "[]";
    public string AllocationsJson { get; private set; } = "[]";
    public string CommentsJson { get; private set; } = "[]";
    public string ActivitiesJson { get; private set; } = "[]";
    public string? BankMatchJson { get; private set; }
    public string? AccountingPeriodLabel { get; private set; }
    public decimal? RecoverableVatAmount { get; private set; }
    public decimal? RecoverableVatRate { get; private set; }

    public TypeTransaction Type { get; private set; }
    public StatutTransaction Statut { get; private set; }
    public StatutJustificatif? StatutJustificatif { get; private set; }

    public decimal Montant { get; private set; }
    public string Devise { get; private set; } = "TND";
    public string? Compte { get; private set; }

    public Guid? FactureId { get; private set; }

    public string? JustificatifChemin { get; private set; }
    public string? JustificatifNomFichier { get; private set; }
    public string? JustificatifContentType { get; private set; }
    public long? JustificatifTailleOctets { get; private set; }

    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private Transaction() { }

    public static Transaction Creer(
        Guid entrepriseId,
        Guid creePar,
        DateTime date,
        string libelle,
        decimal montant,
        TypeTransaction type,
        string devise = "TND",
        string? tiersNom = null,
        string? categorieNom = null,
        string? description = null,
        string? compte = null,
        Guid? factureId = null,
        DocumentSource source = DocumentSource.Web,
        StatutTransaction statut = StatutTransaction.NonJustifiee,
        StatutJustificatif? statutJustificatif = null)
    {
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ValidationMetierException("Le libellé est requis.");

        if (montant <= 0)
            throw new ValidationMetierException("Le montant doit être positif.");

        if (string.IsNullOrWhiteSpace(devise) || devise.Trim().Length != 3)
            throw new ValidationMetierException("La devise est invalide (ex: TND).");

        var now = DateTime.UtcNow;
        return new Transaction
        {
            Id = Guid.NewGuid(),
            EntrepriseId = entrepriseId,
            CreePar = creePar,
            Date = date,
            Libelle = libelle.Trim(),
            Montant = Math.Round(montant, 3),
            Type = type,
            Devise = devise.Trim().ToUpperInvariant(),
            TiersNom = string.IsNullOrWhiteSpace(tiersNom) ? null : tiersNom.Trim(),
            CategorieNom = string.IsNullOrWhiteSpace(categorieNom) ? null : categorieNom.Trim(),
            Source = source,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Compte = string.IsNullOrWhiteSpace(compte) ? null : compte.Trim(),
            FactureId = factureId,
            Statut = statut,
            StatutJustificatif = statutJustificatif,
            CreeLe = now,
            ModifieLe = now
        };
    }

    public void MettreAJour(
        Guid modifiePar,
        DateTime? date = null,
        string? libelle = null,
        decimal? montant = null,
        TypeTransaction? type = null,
        string? devise = null,
        string? tiersNom = null,
        string? categorieNom = null,
        string? description = null,
        string? compte = null,
        Guid? factureId = null,
        StatutTransaction? statut = null,
        StatutJustificatif? statutJustificatif = null)
    {
        ModifiePar = modifiePar;

        if (date is not null) Date = date.Value;

        if (libelle is not null)
        {
            if (string.IsNullOrWhiteSpace(libelle))
                throw new ValidationMetierException("Le libellé est requis.");
            Libelle = libelle.Trim();
        }

        if (montant is not null)
        {
            if (montant.Value <= 0)
                throw new ValidationMetierException("Le montant doit être positif.");
            Montant = Math.Round(montant.Value, 3);
        }

        if (type is not null) Type = type.Value;

        if (devise is not null)
        {
            if (string.IsNullOrWhiteSpace(devise) || devise.Trim().Length != 3)
                throw new ValidationMetierException("La devise est invalide (ex: TND).");
            Devise = devise.Trim().ToUpperInvariant();
        }

        if (tiersNom is not null) TiersNom = string.IsNullOrWhiteSpace(tiersNom) ? null : tiersNom.Trim();
        if (categorieNom is not null) CategorieNom = string.IsNullOrWhiteSpace(categorieNom) ? null : categorieNom.Trim();
        if (description is not null) Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (compte is not null) Compte = string.IsNullOrWhiteSpace(compte) ? null : compte.Trim();

        if (factureId is not null) FactureId = factureId;
        if (statut is not null) Statut = statut.Value;
        if (statutJustificatif is not null) StatutJustificatif = statutJustificatif;

        ModifieLe = DateTime.UtcNow;
    }

    public void DefinirJustificatif(
        Guid modifiePar,
        string chemin,
        string nomFichier,
        string? contentType,
        long? tailleOctets,
        StatutTransaction statutApresUpload = StatutTransaction.Justifiee)
    {
        if (string.IsNullOrWhiteSpace(chemin))
            throw new ValidationMetierException("Chemin justificatif invalide.");

        ModifiePar = modifiePar;
        JustificatifChemin = chemin.Trim();
        JustificatifNomFichier = string.IsNullOrWhiteSpace(nomFichier) ? null : nomFichier.Trim();
        JustificatifContentType = string.IsNullOrWhiteSpace(contentType) ? null : contentType.Trim();
        JustificatifTailleOctets = tailleOctets;
        StatutJustificatif = Enums.StatutJustificatif.Present;
        Statut = statutApresUpload;
        ModifieLe = DateTime.UtcNow;
    }

    public void LierFacture(Guid modifiePar, Guid factureId)
    {
        ModifiePar = modifiePar;
        FactureId = factureId;
        Statut = StatutTransaction.Justifiee;
        ModifieLe = DateTime.UtcNow;
    }

    public void EnregistrerContexteRevision(
        Guid modifiePar,
        string? documentType,
        int? ocrOverallConfidence,
        string? reviewFieldsJson,
        string? missingFieldsJson,
        Guid? fournisseurId,
        string? tiersNom,
        string? fournisseurMatriculeFiscal)
    {
        ModifiePar = modifiePar;
        DocumentType = string.IsNullOrWhiteSpace(documentType) ? DocumentType : documentType.Trim();
        OcrOverallConfidence = ocrOverallConfidence.HasValue
            ? Math.Clamp(ocrOverallConfidence.Value, 0, 100)
            : OcrOverallConfidence;
        ReviewFieldsJson = string.IsNullOrWhiteSpace(reviewFieldsJson) ? ReviewFieldsJson : reviewFieldsJson;
        MissingFieldsJson = string.IsNullOrWhiteSpace(missingFieldsJson) ? MissingFieldsJson : missingFieldsJson;
        FournisseurId = fournisseurId;
        FournisseurMatriculeFiscal = string.IsNullOrWhiteSpace(fournisseurMatriculeFiscal)
            ? FournisseurMatriculeFiscal
            : fournisseurMatriculeFiscal.Trim();
        TiersNom = string.IsNullOrWhiteSpace(tiersNom) ? TiersNom : tiersNom.Trim();
        ModifieLe = DateTime.UtcNow;
    }

    public void AppliquerCorrectionJustificatif(
        Guid modifiePar,
        DateTime? date,
        decimal? montant,
        string? libelle,
        string? tiersNom,
        Guid? fournisseurId,
        string? fournisseurMatriculeFiscal,
        string? reviewFieldsJson,
        string? missingFieldsJson)
    {
        MettreAJour(
            modifiePar: modifiePar,
            date: date,
            libelle: libelle,
            montant: montant,
            tiersNom: tiersNom);

        FournisseurId = fournisseurId;
        FournisseurMatriculeFiscal = string.IsNullOrWhiteSpace(fournisseurMatriculeFiscal)
            ? FournisseurMatriculeFiscal
            : fournisseurMatriculeFiscal.Trim();
        ReviewFieldsJson = string.IsNullOrWhiteSpace(reviewFieldsJson) ? ReviewFieldsJson : reviewFieldsJson;
        MissingFieldsJson = string.IsNullOrWhiteSpace(missingFieldsJson) ? MissingFieldsJson : missingFieldsJson;
        ModifieLe = DateTime.UtcNow;
    }

    public void DefinirAnalytique(
        Guid modifiePar,
        string? categorieNom,
        string allocationsJson,
        string? accountingPeriodLabel,
        decimal? recoverableVatAmount,
        decimal? recoverableVatRate)
    {
        MettreAJour(modifiePar, categorieNom: categorieNom);
        AllocationsJson = string.IsNullOrWhiteSpace(allocationsJson) ? "[]" : allocationsJson;
        AccountingPeriodLabel = string.IsNullOrWhiteSpace(accountingPeriodLabel) ? null : accountingPeriodLabel.Trim();
        RecoverableVatAmount = recoverableVatAmount.HasValue ? Math.Round(recoverableVatAmount.Value, 3) : null;
        RecoverableVatRate = recoverableVatRate.HasValue ? Math.Round(recoverableVatRate.Value, 2) : null;
        ModifieLe = DateTime.UtcNow;
    }

    public void DefinirCommentaires(Guid modifiePar, string commentsJson)
    {
        ModifiePar = modifiePar;
        CommentsJson = string.IsNullOrWhiteSpace(commentsJson) ? "[]" : commentsJson;
        ModifieLe = DateTime.UtcNow;
    }

    public void DefinirActivites(Guid modifiePar, string activitiesJson)
    {
        ModifiePar = modifiePar;
        ActivitiesJson = string.IsNullOrWhiteSpace(activitiesJson) ? "[]" : activitiesJson;
        ModifieLe = DateTime.UtcNow;
    }

    public void DefinirRapprochementBancaire(Guid modifiePar, string? bankMatchJson)
    {
        ModifiePar = modifiePar;
        BankMatchJson = string.IsNullOrWhiteSpace(bankMatchJson) ? null : bankMatchJson;
        ModifieLe = DateTime.UtcNow;
    }

    public void ValiderRevision(Guid modifiePar)
    {
        ModifiePar = modifiePar;
        Statut = StatutTransaction.Justifiee;
        if (JustificatifChemin is not null && StatutJustificatif is null)
            StatutJustificatif = Enums.StatutJustificatif.Present;
        ModifieLe = DateTime.UtcNow;
    }
}
