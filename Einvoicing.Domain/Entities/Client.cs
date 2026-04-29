using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class Client
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public string Nom { get; private set; } = string.Empty;
    public string? MatriculeFiscal { get; private set; }
    public string? Adresse { get; private set; }
    public string? Ville { get; private set; }
    public string? CodePostal { get; private set; }
    public string Pays { get; private set; } = "TN";
    public string Email { get; private set; } = string.Empty;
    public string? Telephone { get; private set; }
    public TypeClient TypeClient { get; private set; }
    public bool EstActif { get; private set; }
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private Client() { }

    public static Client Creer(
        Guid entrepriseId,
        string nom,
        string email,
        TypeClient typeClient,
        string? matriculeFiscal = null,
        string? adresse = null,
        string? ville = null,
        string? codePostal = null,
        string? telephone = null,
        string pays = "TN")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nom);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (typeClient == TypeClient.B2B && string.IsNullOrWhiteSpace(matriculeFiscal))
            throw new ValidationMetierException("Le matricule fiscal est obligatoire pour un client B2B.");

        return new Client
        {
            Id = Guid.NewGuid(),
            EntrepriseId = entrepriseId,
            Nom = nom.Trim(),
            Email = email.ToLowerInvariant().Trim(),
            TypeClient = typeClient,
            MatriculeFiscal = matriculeFiscal?.ToUpperInvariant().Trim(),
            Adresse = adresse?.Trim(),
            Ville = ville?.Trim(),
            CodePostal = codePostal?.Trim(),
            Telephone = telephone?.Trim(),
            Pays = pays,
            EstActif = true,
            CreeLe = DateTime.UtcNow,
            ModifieLe = DateTime.UtcNow
        };
    }

    public void MettreAJour(
        string nom, string email, string? matriculeFiscal,
        string? adresse, string? ville, string? codePostal,
        string? telephone, TypeClient typeClient)
    {
        Nom = nom.Trim();
        Email = email.ToLowerInvariant().Trim();
        MatriculeFiscal = matriculeFiscal?.ToUpperInvariant().Trim();
        Adresse = adresse?.Trim();
        Ville = ville?.Trim();
        CodePostal = codePostal?.Trim();
        Telephone = telephone?.Trim();
        TypeClient = typeClient;
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
}
