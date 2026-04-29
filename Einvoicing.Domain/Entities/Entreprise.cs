using System.Text.RegularExpressions;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class Entreprise
{
    private static readonly Regex MatriculeFiscalPattern = new(@"^[0-9]{7}[A-Z]([A-Z]{2}[0-9]{3})?$", RegexOptions.Compiled);

    public Guid Id { get; private set; }
    public string Nom { get; private set; } = string.Empty;
    public string MatriculeFiscal { get; private set; } = string.Empty;
    public string Adresse { get; private set; } = string.Empty;
    public string Ville { get; private set; } = string.Empty;
    public string CodePostal { get; private set; } = string.Empty;
    public string Pays { get; private set; } = "TN";
    public string Email { get; private set; } = string.Empty;
    public string? Telephone { get; private set; }
    public string? SiteWeb { get; private set; }
    public string? LogoUrl { get; private set; }
    public RegimeFiscal RegimeFiscal { get; private set; }
    public string CodeTva { get; private set; } = string.Empty;
    public string DevisePrincipale { get; private set; } = "TND";
    public string? ParametresTeif { get; private set; }
    public string VersionTeif { get; private set; } = "2024";
    public int ScoreKyc { get; private set; }
    public string? DonneesScoring { get; private set; }
    public string? DocumentsUploades { get; private set; }
    public string? DonneesInscription { get; private set; }
    public bool EstActive { get; private set; }
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private readonly List<Utilisateur> _utilisateurs = [];
    public IReadOnlyCollection<Utilisateur> Utilisateurs => _utilisateurs.AsReadOnly();

    private Entreprise() { }

    public static Entreprise Creer(
        string nom,
        string matriculeFiscal,
        string adresse,
        string ville,
        string codePostal,
        string email,
        string codeTva,
        RegimeFiscal regimeFiscal,
        string? telephone = null,
        string? siteWeb = null,
        string? devisePrincipale = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nom);
        ArgumentException.ThrowIfNullOrWhiteSpace(matriculeFiscal);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var matriculeNormalise = Regex.Replace(matriculeFiscal.Trim().ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
        if (!MatriculeFiscalPattern.IsMatch(matriculeNormalise))
            throw new ValidationMetierException("Le matricule fiscal doit respecter le format 1495908/S ou 1234567A/B/M/000.");

        var devise = string.IsNullOrWhiteSpace(devisePrincipale)
            ? "TND"
            : devisePrincipale.Trim().ToUpperInvariant();
        if (devise.Length != 3)
            throw new ValidationMetierException("La devise principale doit contenir 3 caracteres.");

        return new Entreprise
        {
            Id = Guid.NewGuid(),
            Nom = nom.Trim(),
            MatriculeFiscal = matriculeNormalise,
            Adresse = adresse.Trim(),
            Ville = ville.Trim(),
            CodePostal = codePostal.Trim(),
            Email = email.ToLowerInvariant().Trim(),
            Telephone = telephone?.Trim(),
            CodeTva = codeTva.Trim(),
            SiteWeb = siteWeb?.Trim(),
            DevisePrincipale = devise,
            RegimeFiscal = regimeFiscal,
            EstActive = true,
            CreeLe = DateTime.UtcNow,
            ModifieLe = DateTime.UtcNow
        };
    }
    public void MettreAJour(
        string nom, string adresse, string ville,
        string codePostal, string email,
        string? telephone, string? siteWeb,
        RegimeFiscal regimeFiscal,
        string? devisePrincipale = null)
    {
        Nom = nom.Trim();
        Adresse = adresse.Trim();
        Ville = ville.Trim();
        CodePostal = codePostal.Trim();
        Email = email.ToLowerInvariant().Trim();
        Telephone = telephone?.Trim();
        SiteWeb = siteWeb?.Trim();
        if (!string.IsNullOrWhiteSpace(devisePrincipale))
        {
            var devise = devisePrincipale.Trim().ToUpperInvariant();
            if (devise.Length != 3)
                throw new ValidationMetierException("La devise principale doit contenir 3 caracteres.");
            DevisePrincipale = devise;
        }
        RegimeFiscal = regimeFiscal;
        ModifieLe = DateTime.UtcNow;
    }

    public void DefinirLogo(string logoUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logoUrl);
        LogoUrl = logoUrl.Trim();
        ModifieLe = DateTime.UtcNow;
    }

    public void ConfigurerTeif(string parametres, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parametres);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        ParametresTeif = parametres;
        VersionTeif = version;
        ModifieLe = DateTime.UtcNow;
    }
    public void DefinirDonneesInscription(string? donneesJson)
    {
        DonneesInscription = string.IsNullOrWhiteSpace(donneesJson) ? null : donneesJson;
        ModifieLe = DateTime.UtcNow;
    }

    public void DefinirDocumentsUploades(string? documentsJson)
    {
        DocumentsUploades = string.IsNullOrWhiteSpace(documentsJson) ? null : documentsJson;
        ModifieLe = DateTime.UtcNow;
    }

    public void DefinirScoring(int scoreKyc, string? donneesScoring)
    {
        if (scoreKyc < 0 || scoreKyc > 100)
            throw new ValidationMetierException("Le score KYC doit etre compris entre 0 et 100.");
        ScoreKyc = scoreKyc;
        DonneesScoring = donneesScoring;
        ModifieLe = DateTime.UtcNow;
    }
    public void Desactiver()
    {
        EstActive = false;
        ModifieLe = DateTime.UtcNow;
    }

    public void Reactiver()
    {
        EstActive = true;
        ModifieLe = DateTime.UtcNow;
    }
}
