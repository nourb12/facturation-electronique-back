




using Einvoicing.Application.DTOs;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;

using RegisterRequest = Einvoicing.Application.DTOs.RegisterRequest;
using LoginRequest = Einvoicing.Application.DTOs.LoginRequest;

namespace Einvoicing.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> InscrireAsync(RegisterRequest request, CancellationToken ct = default);
    Task<object> ConnecterAsync(LoginRequest request, string? ip, string? userAgent, CancellationToken ct = default);
    Task<AuthResponse> Connecter2FAAsync(Login2FARequest request, string? ip, string? userAgent, CancellationToken ct = default);
    Task<AuthResponse> RafraichirTokenAsync(RefreshTokenRequest request, string? ip, string? userAgent, CancellationToken ct = default);
    Task DeconnecterAsync(string refreshToken, CancellationToken ct = default);
    Task EnvoyerOtpAsync(MotDePasseOublieRequest request, CancellationToken ct = default);
    Task VerifierOtpAsync(VerifierOtpRequest request, CancellationToken ct = default);
    Task ReinitialiserMotDePasseAsync(ReinitialiserMotDePasseRequest request, CancellationToken ct = default);
    Task<Activation2FAResponse> Activer2FAEtape1Async(Guid utilisateurId, CancellationToken ct = default);
    Task Activer2FAEtape2Async(Guid utilisateurId, Activer2FARequest request, CancellationToken ct = default);
    Task Desactiver2FAAsync(Guid utilisateurId, CancellationToken ct = default);
}

public interface IJwtService
{
    string GenererAccessToken(Utilisateur utilisateur);
    string GenererRefreshToken();
    ClaimsResult? ExtraireClaimsTokenExpire(string token);
}

public interface IPasswordHasher
{
    string Hacher(string motDePasse);
    bool Verifier(string motDePasse, string hash);
}

public interface ITotpService
{
    string GenererSecret();
    string GenererQrCodeUri(string secret, string email, string issuer = "Einvoicing");
    bool ValiderCode(string secret, string code);
}

public interface IEmailService
{
    Task SendEmailAsync(string destinataire, string sujet, string corpsHtml, CancellationToken ct = default);
    Task EnvoyerOtpAsync(string email, string prenom, string otp, CancellationToken ct = default);
    Task EnvoyerAlerteConnexionAsync(string email, string prenom, string ip, string appareil, CancellationToken ct = default);
    Task EnvoyerConfirmationReinitialisationAsync(string email, string prenom, CancellationToken ct = default);
    Task EnvoyerBienvenueAsync(string email, string prenom, CancellationToken ct = default);
    Task EnvoyerBienvenueEntrepriseAsync(
    string email, string prenom, string nom,
    string nomEntreprise, string matriculeFiscal,
    string role, CancellationToken ct = default);

    Task EnvoyerNotificationNouvelleDemandeAsync(
        string adminEmail, string raisonSociale, string matriculeFiscal,
        string emailEntreprise, string telephone, Guid entrepriseId,
        List<string> documents, CancellationToken ct = default);

    Task EnvoyerConfirmationDemandeAsync(
        string email, string raisonSociale, string reference,
        string matriculeFiscal, string respPrenom, string respNom,
        List<string> documentsRecus,
        CancellationToken ct = default);

    Task EnvoyerAccesValideAsync(
        string email, string prenom, string raisonSociale,
        string loginEmail, string motDePasseTemp, CancellationToken ct = default);

    Task EnvoyerDemandeRejeteeAsync(
        string email, string prenom, string raisonSociale,
        string motif, CancellationToken ct = default);

    Task EnvoyerNotifFactureAsync(
        string email, string nomClient, string numeroFacture,
        string type, string telephone, decimal montantTtc,
        CancellationToken ct = default);
}

public interface ISmsService
{
    Task EnvoyerAsync(string telephone, string message, CancellationToken ct = default);
}

public interface IRateLimiter
{
    bool EstBloque(string cle);
    void EnregistrerEchec(string cle);
    void Reinitialiser(string cle);
}

public interface ICurrentUserService
{
    Guid? UtilisateurId { get; }
    Guid? EntrepriseId { get; }
    string? Email { get; }
    string? Role { get; }
    bool EstAuthentifie { get; }
}



public interface IUtilisateurRepository
{
    Task<Utilisateur?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<Utilisateur?> ObtenirParEmailAsync(string email, CancellationToken ct = default);
    Task AjouterAsync(Utilisateur utilisateur, CancellationToken ct = default);
    void MettreAJour(Utilisateur utilisateur);
    Task SauvegarderAsync(CancellationToken ct = default);
    Task<List<Utilisateur>> ListerParEntrepriseAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<List<Utilisateur>> ListerParStatutAsync(StatutCompte statut, CancellationToken ct = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> ObtenirParTokenAsync(string token, CancellationToken ct = default);
    Task AjouterAsync(RefreshToken refreshToken, CancellationToken ct = default);
    void MettreAJour(RefreshToken refreshToken);
    Task RevoquerTousAsync(Guid utilisateurId, CancellationToken ct = default);
    Task SauvegarderAsync(CancellationToken ct = default);
}

public interface IOtpRepository
{
    Task<OtpCode?> ObtenirDernierValideAsync(Guid utilisateurId, OtpType type, CancellationToken ct = default);
    Task AjouterAsync(OtpCode otp, CancellationToken ct = default);
    void MettreAJour(OtpCode otp);
    Task InvaliderTousAsync(Guid utilisateurId, OtpType type, CancellationToken ct = default);
    Task SauvegarderAsync(CancellationToken ct = default);
}

public interface ISessionRepository
{
    Task<List<SessionActive>> ListerAsync(Guid utilisateurId, CancellationToken ct = default);
    Task AjouterAsync(SessionActive session, CancellationToken ct = default);
    Task SupprimerAsync(Guid sessionId, CancellationToken ct = default);
    Task SauvegarderAsync(CancellationToken ct = default);
}
