using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class ParametreFiscal
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public string Libelle { get; private set; } = string.Empty;
    public decimal Valeur { get; private set; }
    public TypeParametreFiscal Type { get; private set; }
    public SigneParametreFiscal Signe { get; private set; }
    public OrdreCalcul OrdreCalcul { get; private set; }
    public UtilisationParametreFiscal Utilisation { get; private set; }
    public bool InclureRetenueSource { get; private set; }
    public List<string> DocumentsCibles { get; private set; } = [];
    public bool EstActif { get; private set; }
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private ParametreFiscal() { }

    public static ParametreFiscal Creer(
        Guid entrepriseId,
        string libelle,
        decimal valeur,
        TypeParametreFiscal type,
        SigneParametreFiscal signe,
        OrdreCalcul ordreCalcul,
        UtilisationParametreFiscal utilisation,
        bool inclureRetenueSource,
        List<string>? documentsCibles = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libelle);
        if (valeur <= 0)
            throw new ValidationMetierException("La valeur doit être supérieure à 0.");

        return new ParametreFiscal
        {
            Id = Guid.NewGuid(),
            EntrepriseId = entrepriseId,
            Libelle = libelle.Trim(),
            Valeur = valeur,
            Type = type,
            Signe = signe,
            OrdreCalcul = ordreCalcul,
            Utilisation = utilisation,
            InclureRetenueSource = inclureRetenueSource,
            DocumentsCibles = NettoyerDocuments(documentsCibles),
            EstActif = true,
            CreeLe = DateTime.UtcNow,
            ModifieLe = DateTime.UtcNow
        };
    }

    public void MettreAJour(
        string libelle,
        decimal valeur,
        TypeParametreFiscal type,
        SigneParametreFiscal signe,
        OrdreCalcul ordreCalcul,
        UtilisationParametreFiscal utilisation,
        bool inclureRetenueSource,
        List<string>? documentsCibles = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libelle);
        if (valeur <= 0)
            throw new ValidationMetierException("La valeur doit être supérieure à 0.");

        Libelle = libelle.Trim();
        Valeur = valeur;
        Type = type;
        Signe = signe;
        OrdreCalcul = ordreCalcul;
        Utilisation = utilisation;
        InclureRetenueSource = inclureRetenueSource;
        DocumentsCibles = NettoyerDocuments(documentsCibles);
        ModifieLe = DateTime.UtcNow;
    }

    private static List<string> NettoyerDocuments(List<string>? docs)
    {
        if (docs is null || docs.Count == 0) return [];
        return docs
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}