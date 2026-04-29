using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class Taxe
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public string Titre { get; private set; } = string.Empty;
    public decimal Taux { get; private set; }
    public TypeTaxe Type { get; private set; }
    public string? Description { get; private set; }
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private Taxe() { }

    public static Taxe Creer(Guid entrepriseId, string titre, decimal taux, TypeTaxe type, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titre);
        if (taux <= 0)
            throw new ValidationMetierException("Le taux doit être supérieur à 0.");

        return new Taxe
        {
            Id = Guid.NewGuid(),
            EntrepriseId = entrepriseId,
            Titre = titre.Trim(),
            Taux = taux,
            Type = type,
            Description = NettoyerDescription(description),
            CreeLe = DateTime.UtcNow,
            ModifieLe = DateTime.UtcNow
        };
    }

    public void MettreAJour(string titre, decimal taux, TypeTaxe type, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titre);
        if (taux <= 0)
            throw new ValidationMetierException("Le taux doit être supérieur à 0.");

        Titre = titre.Trim();
        Taux = taux;
        Type = type;
        Description = NettoyerDescription(description);
        ModifieLe = DateTime.UtcNow;
    }

    private static string? NettoyerDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        return description.Trim();
    }
}