




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LoginRequest = Einvoicing.Application.DTOs.LoginRequest;
using RegisterRequest = Einvoicing.Application.DTOs.RegisterRequest;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    IAuthService authService,
    ICurrentUserService currentUser,
    IUtilisateurRepository utilisateurRepo,
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepo
) : ControllerBase
{
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? Ua => Request.Headers.UserAgent.ToString();

    
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest req, CancellationToken ct)
    {
        var result = await authService.InscrireAsync(req, ct);
        return Ok(result);
    }
    
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest req, CancellationToken ct)
    {
        var result = await authService.ConnecterAsync(req, Ip, Ua, ct);
        return Ok(result);
    }

    
    [HttpPost("2fa/login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<IActionResult> Login2FA(
        [FromBody] Login2FARequest req, CancellationToken ct)
    {
        var result = await authService.Connecter2FAAsync(req, Ip, Ua, ct);
        return Ok(result);
    }

    
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest req, CancellationToken ct)
    {
        var result = await authService.RafraichirTokenAsync(req, Ip, Ua, ct);
        return Ok(result);
    }

    
    
    
    [HttpPost("refresh/entreprise")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<IActionResult> RefreshTokenEntreprise(CancellationToken ct)
    {
        var utilisateurId = currentUser.UtilisateurId;
        if (utilisateurId is null)
            return Unauthorized();

        
        var utilisateur = await utilisateurRepo.ObtenirParIdAsync(utilisateurId.Value, ct);
        if (utilisateur is null)
            return NotFound();

        if (utilisateur.EntrepriseId is null)
            return BadRequest(new { message = "Aucune entreprise rattachée à ce compte." });

        
        var accessToken = jwtService.GenererAccessToken(utilisateur);
        var refreshToken = jwtService.GenererRefreshToken();

        var claims = jwtService.ExtraireClaimsTokenExpire(accessToken);
        if (claims is null)
            return StatusCode(500);

        var rt = Einvoicing.Domain.Entities.RefreshToken.Creer(
            utilisateurId: utilisateur.Id,
            token: refreshToken,
            jwtId: claims.JwtId,
            ip: Ip,
            ua: Ua
        );

        await refreshTokenRepo.AjouterAsync(rt, ct);
        await refreshTokenRepo.SauvegarderAsync(ct);

        return Ok(new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpireA: DateTime.UtcNow.AddMinutes(60),
            Utilisateur: new UtilisateurDto(
                Id: utilisateur.Id,
                Prenom: utilisateur.Prenom,
                Nom: utilisateur.Nom,
                Email: utilisateur.Email,
                Role: utilisateur.Role.ToString(),
                Statut: utilisateur.Statut.ToString(),
                DeuxFAActif: utilisateur.DeuxFAActif,
                EntrepriseId: utilisateur.EntrepriseId,
                DerniereConnexion: utilisateur.DerniereConnexion,
                Telephone: utilisateur.Telephone,
                Poste: utilisateur.Poste,
                Departement: utilisateur.Departement,
                AlerteConnexion: utilisateur.AlerteConnexion
            )
        ));
    }

    
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SuccesResponse), 200)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest req, CancellationToken ct)
    {
        await authService.DeconnecterAsync(req.RefreshToken, ct);
        return Ok(new SuccesResponse("Déconnexion effectuée."));
    }

    
    [HttpPost("mot-de-passe-oublie")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SuccesResponse), 200)]
    public async Task<IActionResult> MotDePasseOublie(
        [FromBody] MotDePasseOublieRequest req, CancellationToken ct)
    {
        await authService.EnvoyerOtpAsync(req, ct);
        return Ok(new SuccesResponse(
            "Si cet email est associé à un compte, un code vous a été envoyé."));
    }

    
    [HttpPost("verifier-otp")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SuccesResponse), 200)]
    public async Task<IActionResult> VerifierOtp(
        [FromBody] VerifierOtpRequest req, CancellationToken ct)
    {
        await authService.VerifierOtpAsync(req, ct);
        return Ok(new SuccesResponse("Code vérifié. Vous pouvez définir un nouveau mot de passe."));
    }

    
    [HttpPost("reinitialiser-mot-de-passe")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SuccesResponse), 200)]
    public async Task<IActionResult> ReinitialiserMotDePasse(
        [FromBody] ReinitialiserMotDePasseRequest req, CancellationToken ct)
    {
        await authService.ReinitialiserMotDePasseAsync(req, ct);
        return Ok(new SuccesResponse(
            "Mot de passe réinitialisé. Toutes vos sessions ont été révoquées."));
    }

    
    [HttpPost("2fa/activer")]
    [Authorize]
    [ProducesResponseType(typeof(Activation2FAResponse), 200)]
    public async Task<IActionResult> Activer2FAEtape1(CancellationToken ct)
    {
        var result = await authService.Activer2FAEtape1Async(currentUser.UtilisateurId!.Value, ct);
        return Ok(result);
    }

    
    [HttpPost("2fa/confirmer")]
    [Authorize]
    [ProducesResponseType(typeof(SuccesResponse), 200)]
    public async Task<IActionResult> Activer2FAEtape2(
        [FromBody] Activer2FARequest req, CancellationToken ct)
    {
        await authService.Activer2FAEtape2Async(currentUser.UtilisateurId!.Value, req, ct);
        return Ok(new SuccesResponse("Double authentification activée."));
    }

    
    [HttpDelete("2fa")]
    [Authorize]
    [ProducesResponseType(typeof(SuccesResponse), 200)]
    public async Task<IActionResult> Desactiver2FA(CancellationToken ct)
    {
        await authService.Desactiver2FAAsync(currentUser.UtilisateurId!.Value, ct);
        return Ok(new SuccesResponse("Double authentification désactivée."));
    }
}
