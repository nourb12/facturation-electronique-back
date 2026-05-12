using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class Fournisseur
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public string Nom { get; private set; } = string.Empty;
    public string? MatriculeFiscal { get; private set; }
    public string? Adresse { get; private set; }
    public string? Iban { get; private set; }
    public string? Email { get; private set; }
    public string? Telephone { get; private set; }
    public bool EstActif { get; private set; }
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private Fournisseur() { }

    public static Fournisseur Creer(
        Guid entrepriseId,
        string nom,
        string? matriculeFiscal = null,
        string? adresse = null,
        string? iban = null,
        string? email = null,
        string? telephone = null)
    {
        if (entrepriseId == Guid.Empty)
            throw new ValidationMetierException("Entreprise invalide.");
        if (string.IsNullOrWhiteSpace(nom))
            throw new ValidationMetierException("Le nom fournisseur est requis.");

        var now = DateTime.UtcNow;
        return new Fournisseur
        {
            Id = Guid.NewGuid(),
            EntrepriseId = entrepriseId,
            Nom = nom.Trim(),
            MatriculeFiscal = Clean(matriculeFiscal),
            Adresse = Clean(adresse),
            Iban = Clean(iban),
            Email = Clean(email)?.ToLowerInvariant(),
            Telephone = Clean(telephone),
            EstActif = true,
            CreeLe = now,
            ModifieLe = now
        };
    }

    public void MettreAJour(
        string nom,
        string? matriculeFiscal,
        string? adresse,
        string? iban,
        string? email,
        string? telephone)
    {
        if (string.IsNullOrWhiteSpace(nom))
            throw new ValidationMetierException("Le nom fournisseur est requis.");

        Nom = nom.Trim();
        MatriculeFiscal = Clean(matriculeFiscal);
        Adresse = Clean(adresse);
        Iban = Clean(iban);
        Email = Clean(email)?.ToLowerInvariant();
        Telephone = Clean(telephone);
        ModifieLe = DateTime.UtcNow;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
