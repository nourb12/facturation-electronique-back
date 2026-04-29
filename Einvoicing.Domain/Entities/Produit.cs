using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;





public sealed class CategorieProduit
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public string Nom { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool EstActive { get; private set; }
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private readonly List<Produit> _produits = [];
    public IReadOnlyCollection<Produit> Produits => _produits.AsReadOnly();

    private CategorieProduit() { }

    public static CategorieProduit Creer(Guid entrepriseId, string nom, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nom);

        return new CategorieProduit
        {
            Id = Guid.NewGuid(),
            EntrepriseId = entrepriseId,
            Nom = nom.Trim(),
            Description = description?.Trim(),
            EstActive = true,
            CreeLe = DateTime.UtcNow,
            ModifieLe = DateTime.UtcNow
        };
    }

    public void MettreAJour(string nom, string? description)
    {
        Nom = nom.Trim();
        Description = description?.Trim();
        ModifieLe = DateTime.UtcNow;
    }

    public void Desactiver()
    {
        EstActive = false;
        ModifieLe = DateTime.UtcNow;
    }
}





public sealed class Produit
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public Guid? CategorieId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Libelle { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal PrixUnitaire { get; private set; }
    public decimal TauxTva { get; private set; }
    public string Unite { get; private set; } = "U";
    public TypeProduit Type { get; private set; }
    public bool EstActif { get; private set; }
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private Produit() { }

    public static Produit Creer(
        Guid entrepriseId,
        string code,
        string libelle,
        decimal prixUnitaire,
        decimal tauxTva,
        TypeProduit type,
        Guid? categorieId = null,
        string? description = null,
        string unite = "U")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(libelle);

        if (prixUnitaire < 0)
            throw new ValidationMetierException("Le prix unitaire ne peut pas être négatif.");

        if (tauxTva < 0 || tauxTva > 100)
            throw new ValidationMetierException("Le taux de TVA doit être compris entre 0 et 100.");

        return new Produit
        {
            Id = Guid.NewGuid(),
            EntrepriseId = entrepriseId,
            CategorieId = categorieId,
            Code = code.ToUpperInvariant().Trim(),
            Libelle = libelle.Trim(),
            Description = description?.Trim(),
            PrixUnitaire = Math.Round(prixUnitaire, 3),
            TauxTva = tauxTva,
            Unite = unite.Trim(),
            Type = type,
            EstActif = true,
            CreeLe = DateTime.UtcNow,
            ModifieLe = DateTime.UtcNow
        };
    }

    public void MettreAJour(
        string libelle, decimal prixUnitaire, decimal tauxTva,
        string? description, string unite, Guid? categorieId, TypeProduit type)
    {
        if (prixUnitaire < 0)
            throw new ValidationMetierException("Le prix unitaire ne peut pas être négatif.");

        Libelle = libelle.Trim();
        PrixUnitaire = Math.Round(prixUnitaire, 3);
        TauxTva = tauxTva;
        Description = description?.Trim();
        Unite = unite.Trim();
        CategorieId = categorieId;
        Type = type;
        ModifieLe = DateTime.UtcNow;
    }

    public void Desactiver()
    {
        EstActif = false;
        ModifieLe = DateTime.UtcNow;
    }

    public void Reactiver()
    {
        EstActif = true;
        ModifieLe = DateTime.UtcNow;
    }

    public decimal CalculerMontantTtc(decimal quantite)
    {
        var ht = PrixUnitaire * quantite;
        var tva = ht * (TauxTva / 100);
        return Math.Round(ht + tva, 3);
    }
}
