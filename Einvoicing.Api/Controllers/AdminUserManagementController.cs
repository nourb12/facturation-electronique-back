using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Net;
using System.Security.Cryptography;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Api.Controllers;

public record PreuveAdmin([Required, MaxLength(200)] string MotDePasse, [MaxLength(10)] string? Code2FA, [Required, MinLength(10), MaxLength(500)] string Motif);
public record InvitationRequest([Required, MaxLength(100)] string Prenom, [Required, MaxLength(100)] string Nom,
    [Required, EmailAddress, MaxLength(254)] string Email, [Required] string Role, Guid? EntrepriseId, [Required] PreuveAdmin Preuve);
public record RoleAdminRequest([Required] string Role, [Required] PreuveAdmin Preuve);
public record AccepterInvitationRequest(Guid Id, [Required, MinLength(32), MaxLength(100)] string Token,
    [Required, MinLength(12), MaxLength(200)] string MotDePasse, [Required] string ConfirmationMotDePasse);

[ApiController]
[Route("api/admin/gestion-utilisateurs")]
[Authorize(Roles = "SuperAdmin")]
public sealed class AdminUserManagementController(ContextBaseDeDonnees db, ICurrentUserService current,
    IPasswordHasher hasher, ITotpService totp, IJwtService jwt, IEmailService email, IRateLimiter limiter,
    IConfiguration config, ILogger<AdminUserManagementController> logger) : ControllerBase
{
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private async Task<Utilisateur> VerifierAdmin(PreuveAdmin preuve, CancellationToken ct)
    {
        var actor = await db.Utilisateurs.SingleOrDefaultAsync(u => u.Id == current.UtilisateurId, ct)
            ?? throw new AccesRefuseException("Administrateur introuvable.");
        if (!actor.PeutSeConnecter() || actor.Role != RoleUtilisateur.SuperAdmin || User.HasClaim(c => c.Type == "supportActorId"))
            throw new AccesRefuseException("Action reservee a un SuperAdmin actif.");
        var key = "admin-action:" + actor.Id;
        if (limiter.EstBloque(key)) throw new TropDeTentativesException(900);
        if (!hasher.Verifier(preuve.MotDePasse, actor.MotDePasseHash) ||
            actor.DeuxFAActif && (actor.DeuxFASecret is null || !totp.ValiderCode(actor.DeuxFASecret, preuve.Code2FA ?? "")))
        {
            limiter.EnregistrerEchec(key);
            throw new ValidationMetierException("Mot de passe administrateur ou code 2FA incorrect.");
        }
        limiter.Reinitialiser(key);
        return actor;
    }
    private async Task<Utilisateur> Cible(Guid id, CancellationToken ct)
    {
        var user = await db.Utilisateurs.SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("Utilisateur introuvable.");
        if (user.Id == current.UtilisateurId || user.EstSupprime)
            throw new AccesRefuseException("Votre propre compte et les comptes archives sont proteges.");
        return user;
    }
    private async Task Revoquer(Guid id, CancellationToken ct)
    {
        foreach (var token in await db.RefreshTokens.Where(t => t.UtilisateurId == id && !t.EstRevoque).ToListAsync(ct))
            token.Revoquer();
        db.Sessions.RemoveRange(await db.Sessions.Where(s => s.UtilisateurId == id).ToListAsync(ct));
        foreach (var otp in await db.OtpCodes.Where(o => o.UtilisateurId == id && !o.EstUtilise).ToListAsync(ct)) otp.Utiliser();
    }
    private void Audit(Utilisateur user, string action, string motif)
        => db.EvenementsAudit.Add(EvenementAudit.Creer(user.EntrepriseId, current.UtilisateurId, action, "Utilisateur",
            user.Id.ToString(), motif, Ip, Request.Headers.UserAgent.ToString(), 200));
    private async Task<bool> Notifier(Utilisateur user, string sujet, string message, CancellationToken ct)
    {
        try { await email.SendSecurityEmailAsync(user.Email, sujet, "<p>" + WebUtility.HtmlEncode(message) + "</p>", ct); return true; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { logger.LogWarning(ex, "Notification de securite non envoyee pour {UserId}", user.Id); return false; }
    }
    private static RoleUtilisateur LireRole(string value)
    {
        if (!Enum.TryParse<RoleUtilisateur>(value, false, out var role) || !Enum.IsDefined(role))
            throw new ValidationMetierException("Role invalide.");
        return role;
    }

    [HttpPost("invitations")]
    public async Task<IActionResult> Inviter(InvitationRequest req, CancellationToken ct)
    {
        var actor = await VerifierAdmin(req.Preuve, ct);
        var role = LireRole(req.Role);
        var address = req.Email.Trim().ToLowerInvariant();
        if (await db.Utilisateurs.AnyAsync(u => u.Email == address, ct)) throw new ConflitException("Cette adresse possede deja un compte.");
        var team = role is RoleUtilisateur.SuperAdmin or RoleUtilisateur.Admin;
        if (team && req.EntrepriseId is not null) throw new ValidationMetierException("L'equipe ne doit pas etre rattachee a une entreprise.");
        if (!team && (req.EntrepriseId is null || !await db.Entreprises.AnyAsync(e => e.Id == req.EntrepriseId && e.EstActive, ct)))
            throw new ValidationMetierException("Selectionnez une entreprise active.");
        var user = Utilisateur.Creer(req.Prenom, req.Nom, address, hasher.Hacher(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))), role, req.EntrepriseId);
        user.MettreEnAttente();
        db.Utilisateurs.Add(user);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var invite = InvitationUtilisateur.Creer(user.Id, actor.Id, token);
        db.InvitationsUtilisateurs.Add(invite);
        Audit(user, "INVITER", req.Preuve.Motif);
        await db.SaveChangesAsync(ct);
        var sent = await EnvoyerInvitation(user, invite, token, ct);
        return StatusCode(201, new { id = user.Id, emailEnvoye = sent, message = sent ? "Invitation envoyee." : "Compte invite cree, mais l'e-mail n'a pas ete envoye. Configurez SMTP puis renvoyez l'invitation." });
    }

    private async Task<bool> EnvoyerInvitation(Utilisateur user, InvitationUtilisateur invite, string token, CancellationToken ct)
    {
        var frontend = (config["Frontend:BaseUrl"] ?? "http://localhost:4200").TrimEnd('/');
        var link = frontend + "/invitation#id=" + invite.Id + "&token=" + token;
        try
        {
            await email.SendSecurityEmailAsync(user.Email, "Votre invitation TuniFlow",
                "<p>Vous avez ete invite sur TuniFlow. Choisissez votre mot de passe. Le lien expire dans 48 heures et ne fonctionne qu'une fois.</p><p><a href=\"" + WebUtility.HtmlEncode(link) + "\">Activer mon compte</a></p>", ct);
            invite.MarquerEmailEnvoye(); await db.SaveChangesAsync(ct); return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { logger.LogWarning(ex, "Invitation non envoyee pour {UserId}", user.Id); return false; }
    }

    [HttpPost("{id:guid}/invitation/renvoyer")]
    public async Task<IActionResult> Renvoyer(Guid id, PreuveAdmin req, CancellationToken ct)
    {
        var actor = await VerifierAdmin(req, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await Cible(id, ct);
        if (user.Statut != StatutCompte.EnAttente || !await db.InvitationsUtilisateurs.AnyAsync(i => i.UtilisateurId == id && i.AccepteeLe == null, ct))
            throw new ValidationMetierException("Ce compte n'a pas d'invitation en attente.");
        if (await db.InvitationsUtilisateurs.AnyAsync(i => i.UtilisateurId == id && i.CreeLe > DateTime.UtcNow.AddMinutes(-1), ct))
            throw new ValidationMetierException("Patientez une minute avant de renvoyer.");
        foreach (var old in await db.InvitationsUtilisateurs.Where(i => i.UtilisateurId == id && i.AccepteeLe == null).ToListAsync(ct)) old.Annuler();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var invitation = InvitationUtilisateur.Creer(id, actor.Id, token); db.InvitationsUtilisateurs.Add(invitation);
        Audit(user, "RENVOYER", req.Motif); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        var sent = await EnvoyerInvitation(user, invitation, token, ct);
        return Ok(new { emailEnvoye = sent, message = sent ? "Nouvelle invitation envoyee. L'ancien lien est invalide." : "Invitation renouvelee, mais l'envoi SMTP a echoue." });
    }

    [HttpPost("/api/invitations/accepter")]
    [AllowAnonymous]
    public async Task<IActionResult> Accepter(AccepterInvitationRequest req, CancellationToken ct)
    {
        var key = "invitation:" + Ip;
        if (limiter.EstBloque(key)) throw new TropDeTentativesException(900);
        if (req.MotDePasse != req.ConfirmationMotDePasse || !req.MotDePasse.Any(char.IsUpper) || !req.MotDePasse.Any(char.IsLower) || !req.MotDePasse.Any(char.IsDigit))
            throw new ValidationMetierException("Utilisez au moins 12 caracteres, une majuscule, une minuscule et un chiffre. Les mots de passe doivent correspondre.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var invite = await db.InvitationsUtilisateurs.SingleOrDefaultAsync(i => i.Id == req.Id, ct);
        if (invite is null || !invite.EstValide(req.Token))
        { limiter.EnregistrerEchec(key); throw new ValidationMetierException("Invitation expiree, invalide ou deja utilisee."); }
        var user = await db.Utilisateurs.SingleAsync(u => u.Id == invite.UtilisateurId, ct);
        if (user.EstSupprime || user.Statut != StatutCompte.EnAttente) throw new AccesRefuseException("Ce compte ne peut pas etre active.");
        invite.Accepter(req.Token); user.ChangerMotDePasse(hasher.Hacher(req.MotDePasse)); user.Reactiver();
        db.EvenementsAudit.Add(EvenementAudit.Creer(user.EntrepriseId, user.Id, "ACTIVER", "Utilisateur", user.Id.ToString(), "Invitation acceptee", Ip, Request.Headers.UserAgent.ToString(), 200));
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); limiter.Reinitialiser(key);
        return Ok(new { message = "Compte active. Vous pouvez vous connecter.", role = user.Role.ToString() });
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> ChangerRole(Guid id, RoleAdminRequest req, CancellationToken ct)
    {
        await VerifierAdmin(req.Preuve, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await Cible(id, ct); var role = LireRole(req.Role); var previous = user.Role;
        if (previous == role) throw new ValidationMetierException("Le role est deja attribue.");
        if (previous == RoleUtilisateur.SuperAdmin && user.Statut == StatutCompte.Actif &&
            await db.Utilisateurs.CountAsync(u => u.Role == RoleUtilisateur.SuperAdmin && u.Statut == StatutCompte.Actif && !u.EstSupprime, ct) <= 1)
            throw new AccesRefuseException("Le dernier SuperAdmin actif doit etre conserve.");
        user.ChangerRole(role); await Revoquer(id, ct); Audit(user, "ROLE", previous + " -> " + role + ". " + req.Preuve.Motif);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        var sent = await Notifier(user, "Changement de role TuniFlow", "Votre role a ete modifie en " + role + ". Reconnectez-vous.", ct);
        return Ok(new { message = "Role modifie. Sessions revoquees.", emailEnvoye = sent });
    }

    [HttpPost("{id:guid}/2fa/reinitialiser")]
    public async Task<IActionResult> ResetMfa(Guid id, PreuveAdmin req, CancellationToken ct)
    {
        await VerifierAdmin(req, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await Cible(id, ct);
        if (!user.DeuxFAActif) throw new ValidationMetierException("La 2FA est deja inactive.");
        user.DesactiverDeuxFA(); await Revoquer(id, ct); Audit(user, "RESET_2FA", req.Motif);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        var sent = await Notifier(user, "Reinitialisation 2FA TuniFlow", "Votre double authentification a ete reinitialisee par un administrateur. Vos sessions sont fermees. Reconfigurez la 2FA dans les parametres de securite.", ct);
        return Ok(new { message = "2FA reinitialisee. Sessions revoquees.", emailEnvoye = sent });
    }

    [HttpPost("{id:guid}/sessions/revoquer")]
    public async Task<IActionResult> ForcerDeconnexion(Guid id, PreuveAdmin req, CancellationToken ct)
    {
        await VerifierAdmin(req, ct); var user = await Cible(id, ct);
        await Revoquer(id, ct); Audit(user, "DECONNECTER", req.Motif); await db.SaveChangesAsync(ct);
        return Ok(new { message = "Toutes les sessions sont revoquees. La prochaine requete sera refusee." });
    }

    [HttpPost("{id:guid}/impersonation")]
    public async Task<IActionResult> Impersonation(Guid id, PreuveAdmin req, CancellationToken ct)
    {
        var actor = await VerifierAdmin(req, ct); var user = await Cible(id, ct);
        if (user.Role is RoleUtilisateur.SuperAdmin or RoleUtilisateur.Admin || !user.PeutSeConnecter() ||
            user.EntrepriseId is null || !await db.Entreprises.AnyAsync(e => e.Id == user.EntrepriseId && e.EstActive, ct))
            throw new AccesRefuseException("Seuls les comptes clients actifs peuvent etre consultes.");
        var actorJti = User.FindFirst("jti")?.Value ?? throw new TokenInvalideException();
        var accessToken = jwt.GenererTokenSupport(user, actor.Id, actorJti);
        var claims = jwt.ExtraireClaimsTokenExpire(accessToken) ?? throw new TokenInvalideException();
        var rt = RefreshToken.Creer(user.Id, jwt.GenererRefreshToken(), claims.JwtId, Ip, Request.Headers.UserAgent.ToString());
        rt.Utiliser(); db.RefreshTokens.Add(rt);
        Audit(user, "SUPPORT", req.Motif); await db.SaveChangesAsync(ct);
        return Ok(new { accessToken, expireA = DateTime.UtcNow.AddMinutes(10),
            utilisateur = new UtilisateurDto(user.Id, user.Prenom, user.Nom, user.Email, user.Role.ToString(), user.Statut.ToString(),
                user.DeuxFAActif, user.EntrepriseId, user.DerniereConnexion, user.Telephone, user.Poste, user.Departement, user.AlerteConnexion) });
    }

    [HttpPost("/api/support/terminer")]
    [AllowAnonymous]
    public async Task<IActionResult> Terminer(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        var actorId = User.FindFirst("supportActorId")?.Value;
        if (!Guid.TryParse(actorId, out var actor)) return BadRequest(new { message = "Aucune session support." });
        var jti = User.FindFirst("jti")?.Value;
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.JwtId == jti, ct);
        token?.Revoquer();
        db.EvenementsAudit.Add(EvenementAudit.Creer(current.EntrepriseId, actor, "FIN_SUPPORT", "Utilisateur", current.UtilisateurId?.ToString(), "Session support terminee", Ip, Request.Headers.UserAgent.ToString(), 200));
        await db.SaveChangesAsync(ct); return Ok(new { message = "Session support terminee." });
    }

    [HttpGet("{id:guid}/historique")]
    public async Task<IActionResult> Historique(Guid id, CancellationToken ct) => Ok(await db.EvenementsAudit.AsNoTracking()
        .Where(e => e.Ressource == "Utilisateur" && e.RessourceId == id.ToString()).OrderByDescending(e => e.CreeLe).Take(20)
        .Select(e => new { e.Action, e.Details, e.CreeLe, acteurId = e.UtilisateurId }).ToListAsync(ct));
}
