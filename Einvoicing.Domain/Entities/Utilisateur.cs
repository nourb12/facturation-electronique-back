




using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class Utilisateur
{
    
    public Guid Id { get; private set; }
    public string Prenom { get; private set; } = string.Empty;
    public string Nom { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string MotDePasseHash { get; private set; } = string.Empty;
    public string? Telephone { get; private set; }
    public string? Poste { get; private set; }
    public string? Departement { get; private set; }

    
    public RoleUtilisateur Role { get; private set; }
    public StatutCompte Statut { get; private set; }
    public Guid? EntrepriseId { get; private set; }

    
    public bool DeuxFAActif { get; private set; }
    public string? DeuxFASecret { get; private set; }
    public bool AlerteConnexion { get; private set; }

    
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }
    public DateTime? DerniereConnexion { get; private set; }
    public bool EstSupprime { get; private set; }

    
    private readonly List<RefreshToken> _refreshTokens = [];
    private readonly List<OtpCode> _otpCodes = [];
    private readonly List<SessionActive> _sessions = [];

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();
    public IReadOnlyCollection<OtpCode> OtpCodes => _otpCodes.AsReadOnly();
    public IReadOnlyCollection<SessionActive> Sessions => _sessions.AsReadOnly();

    private Utilisateur() { }

    

    public static Utilisateur Creer(
        string prenom, string nom, string email,
        string motDePasseHash, RoleUtilisateur role,
        Guid? entrepriseId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prenom, nameof(prenom));
        ArgumentException.ThrowIfNullOrWhiteSpace(nom, nameof(nom));
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(motDePasseHash, nameof(motDePasseHash));

        return new Utilisateur
        {
            Id = Guid.NewGuid(),
            Prenom = prenom.Trim(),
            Nom = nom.Trim(),
            Email = email.ToLowerInvariant().Trim(),
            MotDePasseHash = motDePasseHash,
            Role = role,
            Statut = StatutCompte.Actif,
            EntrepriseId = entrepriseId,
            AlerteConnexion = true,
            CreeLe = DateTime.UtcNow,
            ModifieLe = DateTime.UtcNow
        };
    }

    

    
    public void RattacherEntreprise(Guid entrepriseId)
    {
        if (entrepriseId == Guid.Empty)
            throw new ValidationMetierException("L'identifiant de l'entreprise est invalide.");
        EntrepriseId = entrepriseId;
        ModifieLe = DateTime.UtcNow;
    }

    public void MettreAJourProfil(
        string prenom, string nom,
        string? telephone, string? poste, string? departement)
    {
        Prenom = prenom.Trim();
        Nom = nom.Trim();
        Telephone = telephone?.Trim();
        Poste = poste?.Trim();
        Departement = departement?.Trim();
        ModifieLe = DateTime.UtcNow;
    }

    public void ChangerMotDePasse(string nouveauHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nouveauHash);
        MotDePasseHash = nouveauHash;
        ModifieLe = DateTime.UtcNow;
    }

    public void ActiverDeuxFA(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        DeuxFASecret = secret;
        DeuxFAActif = true;
        ModifieLe = DateTime.UtcNow;
    }

    public void DesactiverDeuxFA()
    {
        DeuxFASecret = null;
        DeuxFAActif = false;
        ModifieLe = DateTime.UtcNow;
    }

    public void EnregistrerConnexion()
    {
        DerniereConnexion = DateTime.UtcNow;
        ModifieLe = DateTime.UtcNow;
    }

    public void Suspendre()
    {
        if (Statut == StatutCompte.Supprime)
            throw new ValidationMetierException("Impossible de suspendre un compte supprimé.");
        Statut = StatutCompte.Suspendu;
        ModifieLe = DateTime.UtcNow;
    }

    public void Reactiver()
    {
        if (Statut == StatutCompte.Supprime)
            throw new ValidationMetierException("Impossible de réactiver un compte supprimé.");
        Statut = StatutCompte.Actif;
        ModifieLe = DateTime.UtcNow;
    }

    public void MettreEnAttente()
    {
        if (Statut == StatutCompte.Supprime)
            throw new ValidationMetierException("Impossible de mettre en attente un compte supprime.");
        Statut = StatutCompte.EnAttente;
        ModifieLe = DateTime.UtcNow;
    }
    public void Supprimer()
    {
        EstSupprime = true;
        Statut = StatutCompte.Supprime;
        ModifieLe = DateTime.UtcNow;
    }

    public void ConfigurerSecurite(bool alerteConnexion)
    {
        AlerteConnexion = alerteConnexion;
        ModifieLe = DateTime.UtcNow;
    }

    public bool PeutSeConnecter() =>
        Statut == StatutCompte.Actif && !EstSupprime;

    public string NomComplet => $"{Prenom} {Nom}";
}