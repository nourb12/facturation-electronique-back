





using Einvoicing.Application.DTOs;
using Einvoicing.Application.Helpers;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Options;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

using RegisterRequest = Einvoicing.Application.DTOs.RegisterRequest;
using LoginRequest = Einvoicing.Application.DTOs.LoginRequest;

namespace Einvoicing.Application.Services;

public sealed class AuthService(
    IUtilisateurRepository utilisateurRepo,
    IRefreshTokenRepository refreshTokenRepo,
    IOtpRepository otpRepo,
    IEntrepriseRepository entrepriseRepo,
    IJwtService jwtService,
    IPasswordHasher passwordHasher,
    ITotpService totpService,
    IEmailService emailService,
    IRateLimiter rateLimiter,
    IOptions<GoogleAuthOptions> googleAuthOptions
) : IAuthService
{
    private static readonly JsonSerializerOptions GoogleJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    
    
    

    public async Task<AuthResponse> InscrireAsync(
        RegisterRequest req, CancellationToken ct = default)
    {
        
        var email = req.Email.ToLower().Trim();
        var existant = await utilisateurRepo.ObtenirParEmailAsync(email, ct);
        if (existant is not null)
            throw new ValidationMetierException("Cet email est déjà utilisé.");

        if (req.MotDePasse != req.ConfirmationMotDePasse)
            throw new ValidationMetierException("Les mots de passe ne correspondent pas.");

        
        if (!string.IsNullOrWhiteSpace(req.MatriculeFiscal)
            && !MatriculeFiscalHelper.IsValid(req.MatriculeFiscal))
            throw new ValidationMetierException("Le matricule fiscal est invalide.");

        var matricule = MatriculeFiscalHelper.Normalize(req.MatriculeFiscal);
        if (!string.IsNullOrWhiteSpace(matricule))
        {
            var entrepriseExistante = await entrepriseRepo.ObtenirParMatriculeAsync(matricule, ct);
            if (entrepriseExistante is not null)
                throw new ValidationMetierException("Ce matricule fiscal est déjà utilisé.");
        }

        
        var hash = passwordHasher.Hacher(req.MotDePasse);

        var utilisateur = Utilisateur.Creer(
            prenom: req.Prenom,
            nom: req.Nom,
            email: email,
            motDePasseHash: hash,
            role: RoleUtilisateur.Admin
        );

        await utilisateurRepo.AjouterAsync(utilisateur, ct);
        await utilisateurRepo.SauvegarderAsync(ct);

        
        if (!string.IsNullOrWhiteSpace(req.NomEntreprise)
            && MatriculeFiscalHelper.IsValid(req.MatriculeFiscal))
        {
            var matriculeNormalise = MatriculeFiscalHelper.Normalize(req.MatriculeFiscal);
            var entreprise = Entreprise.Creer(
                nom: req.NomEntreprise,
                matriculeFiscal: matriculeNormalise,
                adresse: req.Adresse.Trim(),
                ville: req.Ville.Trim(),
                codePostal: req.CodePostal.Trim(),
                email: email,
                codeTva: string.Empty,
                regimeFiscal: RegimeFiscal.Reel,
                telephone: string.IsNullOrWhiteSpace(req.Telephone) ? null : req.Telephone.Trim(),
                siteWeb: string.IsNullOrWhiteSpace(req.SiteWeb) ? null : req.SiteWeb.Trim(),
                devisePrincipale: req.DevisePrincipale
            );

            await entrepriseRepo.AjouterAsync(entreprise, ct);
            await entrepriseRepo.SauvegarderAsync(ct);

            
            utilisateur.RattacherEntreprise(entreprise.Id);
            utilisateurRepo.MettreAJour(utilisateur);
            await utilisateurRepo.SauvegarderAsync(ct);
        }

        
        _ = emailService.EnvoyerBienvenueAsync(utilisateur.Email, utilisateur.Prenom, ct);

        
        return await GenererAuthResponse(utilisateur, null, null, ct);
    }

    
    
    

    public async Task<LoginResult> ConnecterAsync(
        LoginRequest req, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var email = req.Email.ToLower().Trim();

        var cleRate = req.AdminConsole && !string.IsNullOrWhiteSpace(ip)
            ? $"admin-login:{email}:{ip}"
            : $"login:{email}";
        if (rateLimiter.EstBloque(cleRate))
            throw new TropDeTentativesException(900);

        var utilisateur = await utilisateurRepo.ObtenirParEmailAsync(email, ct)?? throw new IdentifiantsInvalidesException();

        if (!passwordHasher.Verifier(req.MotDePasse, utilisateur.MotDePasseHash))
        {
            rateLimiter.EnregistrerEchec(cleRate);
            throw new IdentifiantsInvalidesException();
        }

        if (!utilisateur.PeutSeConnecter())
            throw new CompteInactifException();

        if (req.AdminConsole && utilisateur.Role != RoleUtilisateur.SuperAdmin)
            throw new AccesRefuseException("Acces reserve aux administrateurs systeme.");

        rateLimiter.Reinitialiser(cleRate);
        utilisateur.EnregistrerConnexion();
        await utilisateurRepo.SauvegarderAsync(ct);

        if (utilisateur.AlerteConnexion && ip is not null)
            await emailService.EnvoyerAlerteConnexionAsync(
                utilisateur.Email, utilisateur.Prenom, ip, userAgent ?? "Inconnu", ct);

        if (utilisateur.DeuxFAActif)
            return new DeuxFARequisResponse(utilisateur.Id);

        return await GenererAuthResponse(utilisateur, ip, userAgent, ct);
    }

    public async Task<AuthResponse> ConnecterAvecGoogleAsync(
        GoogleExchangeRequest req, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var google = googleAuthOptions.Value;
        if (string.IsNullOrWhiteSpace(google.ClientId) || string.IsNullOrWhiteSpace(google.ClientSecret))
            throw new ValidationMetierException("Connexion Google non configurée.");

        if (string.IsNullOrWhiteSpace(req.Code))
            throw new ValidationMetierException("Code Google manquant.");

        var roleHint = (req.RoleHint ?? string.Empty).Trim().ToLowerInvariant();
        if (roleHint is not ("admin" or "financier" or "entreprise"))
            throw new ValidationMetierException("Espace Google invalide.");

        using var http = new HttpClient();

        using var tokenResponse = await http.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = req.Code,
                ["client_id"] = google.ClientId,
                ["client_secret"] = google.ClientSecret,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = "postmessage",
            }),
            ct);

        var tokenPayload = await DeserializeGoogleResponseAsync<GoogleTokenResponse>(tokenResponse, ct);
        if (!tokenResponse.IsSuccessStatusCode || tokenPayload is null || string.IsNullOrWhiteSpace(tokenPayload.AccessToken))
            throw new ValidationMetierException("Authentification Google refusée.");

        using var userInfoRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "https://openidconnect.googleapis.com/v1/userinfo");
        userInfoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenPayload.AccessToken);

        using var userInfoResponse = await http.SendAsync(userInfoRequest, ct);
        var userInfo = await DeserializeGoogleResponseAsync<GoogleUserInfoResponse>(userInfoResponse, ct);
        if (!userInfoResponse.IsSuccessStatusCode || userInfo is null || string.IsNullOrWhiteSpace(userInfo.Email))
            throw new ValidationMetierException("Compte Google non valide.");

        if (!userInfo.EmailVerified)
            throw new ValidationMetierException("Adresse Google non vérifiée.");

        var utilisateur = await utilisateurRepo.ObtenirParEmailAsync(userInfo.Email.ToLower().Trim(), ct)
            ?? throw new AccesRefuseException("Aucun compte TuniFlow n'est rattaché à cette adresse Google.");

        if (!utilisateur.PeutSeConnecter())
            throw new CompteInactifException();

        if (!RoleCompatibleAvecEspace(roleHint, utilisateur.Role))
            throw new AccesRefuseException("Ce compte Google n'a pas accès à cet espace.");

        utilisateur.EnregistrerConnexion();
        await utilisateurRepo.SauvegarderAsync(ct);

        if (utilisateur.AlerteConnexion && ip is not null)
            await emailService.EnvoyerAlerteConnexionAsync(
                utilisateur.Email, utilisateur.Prenom, ip, userAgent ?? "Google OAuth", ct);

        return await GenererAuthResponse(utilisateur, ip, userAgent, ct);
    }

    public async Task<AuthResponse> Connecter2FAAsync(
        Login2FARequest req, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var utilisateur = await utilisateurRepo.ObtenirParIdAsync(req.UtilisateurId, ct)
            ?? throw new NotFoundException("Utilisateur introuvable.");

        if (!utilisateur.DeuxFAActif || utilisateur.DeuxFASecret is null)
            throw new ValidationMetierException("La 2FA n'est pas activée sur ce compte.");

        if (!totpService.ValiderCode(utilisateur.DeuxFASecret, req.Code))
            throw new OtpInvalideException();

        utilisateur.EnregistrerConnexion();
        await utilisateurRepo.SauvegarderAsync(ct);

        if (utilisateur.AlerteConnexion && ip is not null)
            await emailService.EnvoyerAlerteConnexionAsync(
                utilisateur.Email, utilisateur.Prenom, ip, userAgent ?? "Inconnu", ct);

        return await GenererAuthResponse(utilisateur, ip, userAgent, ct);
    }

    
    
    

    public async Task<AuthResponse> RafraichirTokenAsync(
        RefreshTokenRequest req, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var claims = jwtService.ExtraireClaimsTokenExpire(req.AccessToken)
            ?? throw new TokenInvalideException();

        var refreshToken = await refreshTokenRepo.ObtenirParTokenAsync(req.RefreshToken, ct)
            ?? throw new TokenInvalideException();

        if (!refreshToken.EstValide()) throw new TokenInvalideException();
        if (refreshToken.JwtId != claims.JwtId) throw new TokenInvalideException();
        if (refreshToken.UtilisateurId != claims.UtilisateurId) throw new TokenInvalideException();

        refreshToken.Utiliser();
        await refreshTokenRepo.SauvegarderAsync(ct);

        var utilisateur = await utilisateurRepo.ObtenirParIdAsync(claims.UtilisateurId, ct)
            ?? throw new NotFoundException("Utilisateur introuvable.");

        return await GenererAuthResponse(utilisateur, ip, userAgent, ct);
    }

    public async Task DeconnecterAsync(string refreshToken, CancellationToken ct = default)
    {
        var token = await refreshTokenRepo.ObtenirParTokenAsync(refreshToken, ct);
        if (token is null) return;
        token.Revoquer();
        await refreshTokenRepo.SauvegarderAsync(ct);
    }

    
    
    

    public async Task EnvoyerOtpAsync(
        MotDePasseOublieRequest req, CancellationToken ct = default)
    {
        var utilisateur = await utilisateurRepo.ObtenirParEmailAsync(req.Courriel.ToLower(), ct);
        if (utilisateur is null) return;

        await otpRepo.InvaliderTousAsync(utilisateur.Id, OtpType.ReinitialisationMotDePasse, ct);

        var code = GenererCodeOtp();
        var otp = OtpCode.Creer(utilisateur.Id, code, OtpType.ReinitialisationMotDePasse);
        await otpRepo.AjouterAsync(otp, ct);
        await otpRepo.SauvegarderAsync(ct);

        await emailService.EnvoyerOtpAsync(utilisateur.Email, utilisateur.Prenom, code, ct);
    }

    public async Task VerifierOtpAsync(
        VerifierOtpRequest req, CancellationToken ct = default)
    {
        var cleRate = $"otp:{req.Courriel.ToLower()}";
        if (rateLimiter.EstBloque(cleRate))
            throw new TropDeTentativesException(900);

        var utilisateur = await utilisateurRepo.ObtenirParEmailAsync(req.Courriel.ToLower(), ct)
            ?? throw new OtpInvalideException();

        var otp = await otpRepo.ObtenirDernierValideAsync(
            utilisateur.Id, OtpType.ReinitialisationMotDePasse, ct)
            ?? throw new OtpInvalideException();

        if (otp.Code != req.Otp || !otp.EstValide())
        {
            rateLimiter.EnregistrerEchec(cleRate);
            throw new OtpInvalideException();
        }

        rateLimiter.Reinitialiser(cleRate);
    }

    public async Task ReinitialiserMotDePasseAsync(
        ReinitialiserMotDePasseRequest req, CancellationToken ct = default)
    {
        if (req.NouveauMotDePasse != req.ConfirmationMotDePasse)
            throw new ValidationMetierException("Les mots de passe ne correspondent pas.");

        var utilisateur = await utilisateurRepo.ObtenirParEmailAsync(req.Courriel.ToLower(), ct)
            ?? throw new OtpInvalideException();

        var otp = await otpRepo.ObtenirDernierValideAsync(
            utilisateur.Id, OtpType.ReinitialisationMotDePasse, ct)
            ?? throw new OtpInvalideException();

        if (otp.Code != req.Otp || !otp.EstValide())
            throw new OtpInvalideException();

        utilisateur.ChangerMotDePasse(passwordHasher.Hacher(req.NouveauMotDePasse));
        otp.Utiliser();

        await refreshTokenRepo.RevoquerTousAsync(utilisateur.Id, ct);
        await utilisateurRepo.SauvegarderAsync(ct);
        await otpRepo.SauvegarderAsync(ct);

        await emailService.EnvoyerConfirmationReinitialisationAsync(
            utilisateur.Email, utilisateur.Prenom, ct);
    }

    
    
    

    public async Task<Activation2FAResponse> Activer2FAEtape1Async(
        Guid utilisateurId, CancellationToken ct = default)
    {
        var utilisateur = await utilisateurRepo.ObtenirParIdAsync(utilisateurId, ct)
            ?? throw new NotFoundException("Utilisateur introuvable.");

        var secret = totpService.GenererSecret();
        var qrUri = totpService.GenererQrCodeUri(secret, utilisateur.Email);

        return new Activation2FAResponse(secret, qrUri);
    }

    public async Task Activer2FAEtape2Async(
        Guid utilisateurId, Activer2FARequest req, CancellationToken ct = default)
    {
        var utilisateur = await utilisateurRepo.ObtenirParIdAsync(utilisateurId, ct)
            ?? throw new NotFoundException("Utilisateur introuvable.");

        if (!totpService.ValiderCode(req.Secret, req.Code))
            throw new OtpInvalideException();

        utilisateur.ActiverDeuxFA(req.Secret);
        await utilisateurRepo.SauvegarderAsync(ct);
    }

    public async Task Desactiver2FAAsync(
        Guid utilisateurId, CancellationToken ct = default)
    {
        var utilisateur = await utilisateurRepo.ObtenirParIdAsync(utilisateurId, ct)
            ?? throw new NotFoundException("Utilisateur introuvable.");

        utilisateur.DesactiverDeuxFA();
        await utilisateurRepo.SauvegarderAsync(ct);
    }

    
    
    

    private async Task<AuthResponse> GenererAuthResponse(
        Utilisateur utilisateur, string? ip, string? userAgent, CancellationToken ct)
    {
        var accessToken = jwtService.GenererAccessToken(utilisateur);
        var refreshToken = jwtService.GenererRefreshToken();

        var claims = jwtService.ExtraireClaimsTokenExpire(accessToken)
            ?? throw new TokenInvalideException();

        var rt = RefreshToken.Creer(
            utilisateurId: utilisateur.Id,
            token: refreshToken,
            jwtId: claims.JwtId,
            ip: ip,
            ua: userAgent
        );

        await refreshTokenRepo.AjouterAsync(rt, ct);
        await refreshTokenRepo.SauvegarderAsync(ct);

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpireA: DateTime.UtcNow.AddMinutes(60),
            Utilisateur: MapToDto(utilisateur)
        );
    }

    private static UtilisateurDto MapToDto(Utilisateur u) => new(
        Id: u.Id,
        Prenom: u.Prenom,
        Nom: u.Nom,
        Email: u.Email,
        Role: u.Role.ToString(),
        Statut: u.Statut.ToString(),
        DeuxFAActif: u.DeuxFAActif,
        EntrepriseId: u.EntrepriseId,
        DerniereConnexion: u.DerniereConnexion,
        Telephone: u.Telephone,
        Poste: u.Poste,
        Departement: u.Departement,
        AlerteConnexion: u.AlerteConnexion
    );

    private static string GenererCodeOtp()
        => System.Security.Cryptography.RandomNumberGenerator
               .GetInt32(100000, 999999).ToString();

    private static bool RoleCompatibleAvecEspace(string roleHint, RoleUtilisateur role) => roleHint switch
    {
        "admin" => role == RoleUtilisateur.SuperAdmin,
        "financier" => role is RoleUtilisateur.ResponsableFinancier or RoleUtilisateur.Admin or RoleUtilisateur.SuperAdmin,
        "entreprise" => role is RoleUtilisateur.ResponsableEntreprise or RoleUtilisateur.Admin or RoleUtilisateur.SuperAdmin,
        _ => false
    };

    private static async Task<T?> DeserializeGoogleResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, GoogleJsonOptions);
    }

    private sealed record GoogleTokenResponse(
        string AccessToken,
        string TokenType,
        string Scope,
        int ExpiresIn,
        string? IdToken
    );

    private sealed record GoogleUserInfoResponse(
        string Sub,
        string Email,
        bool EmailVerified,
        string? Name,
        string? GivenName,
        string? FamilyName,
        string? Picture
    );
}







