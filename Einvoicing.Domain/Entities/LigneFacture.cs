using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;





public sealed class LigneFacture
{
    public Guid Id { get; private set; }
    public Guid FactureId { get; private set; }
    public Guid? ProduitId { get; private set; }

    public int Ordre { get; private set; }
    public string Designation { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Unite { get; private set; } = "U";
    public decimal Quantite { get; private set; }
    public decimal PrixUnitaire { get; private set; }
    public decimal TauxRemise { get; private set; }
    public decimal TauxTva { get; private set; }

    public decimal MontantHt { get; private set; }
    public decimal MontantRemise { get; private set; }
    public decimal MontantTva { get; private set; }
    public decimal MontantTtc { get; private set; }

    private LigneFacture() { }

    public static LigneFacture Creer(
        Guid factureId,
        int ordre,
        string designation,
        decimal quantite,
        decimal prixUnitaire,
        decimal tauxTva,
        Guid? produitId = null,
        string? description = null,
        string unite = "U",
        decimal tauxRemise = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(designation);

        if (quantite <= 0)
            throw new ValidationMetierException("La quantité doit être positive.");
        if (prixUnitaire < 0)
            throw new ValidationMetierException("Le prix unitaire ne peut pas être négatif.");
        if (tauxRemise < 0 || tauxRemise > 100)
            throw new ValidationMetierException("Le taux de remise doit être entre 0 et 100.");

        var ligne = new LigneFacture
        {
            Id = Guid.NewGuid(),
            FactureId = factureId,
            ProduitId = produitId,
            Ordre = ordre,
            Designation = designation.Trim(),
            Description = description?.Trim(),
            Unite = unite.Trim(),
            Quantite = quantite,
            PrixUnitaire = Math.Round(prixUnitaire, 3),
            TauxRemise = tauxRemise,
            TauxTva = tauxTva
        };

        ligne.CalculerMontants();
        return ligne;
    }

    private void CalculerMontants()
    {
        var brut = Math.Round(Quantite * PrixUnitaire, 3);
        MontantRemise = Math.Round(brut * (TauxRemise / 100), 3);
        MontantHt = Math.Round(brut - MontantRemise, 3);
        MontantTva = Math.Round(MontantHt * (TauxTva / 100), 3);
        MontantTtc = Math.Round(MontantHt + MontantTva, 3);
    }
}





public sealed class HistoriqueFacture
{
    public Guid Id { get; private set; }
    public Guid FactureId { get; private set; }
    public Guid EffectuePar { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;
    public string? AncienneValeur { get; private set; }
    public string? NouvelleValeur { get; private set; }
    public DateTime CreeLe { get; private set; }

    private HistoriqueFacture() { }

    public static HistoriqueFacture Creer(
        Guid factureId, Guid effectuePar,
        string action, string details,
        string? ancienneValeur = null, string? nouvelleValeur = null)
    {
        return new HistoriqueFacture
        {
            Id = Guid.NewGuid(),
            FactureId = factureId,
            EffectuePar = effectuePar,
            Action = action,
            Details = details,
            AncienneValeur = ancienneValeur,
            NouvelleValeur = nouvelleValeur,
            CreeLe = DateTime.UtcNow
        };
    }
}





public sealed class CompteurFacture
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public int Annee { get; private set; }
    public int Mois { get; private set; }
    public int DernierNumero { get; private set; }
    public string Prefixe { get; private set; } = "FAC";

    private CompteurFacture() { }

    public static CompteurFacture Creer(Guid entrepriseId, int annee, int mois, string prefixe = "FAC")
    {
        return new CompteurFacture
        {
            Id = Guid.NewGuid(),
            EntrepriseId = entrepriseId,
            Annee = annee,
            Mois = mois,
            DernierNumero = 0,
            Prefixe = prefixe
        };
    }

    public string Incrementer()
    {
        DernierNumero++;
        return $"{Prefixe}-{Annee}{Mois:D2}-{DernierNumero:D4}";
    }
}
