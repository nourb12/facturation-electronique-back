using System.Security.Claims;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Api.Services;

public static class ActiveSessionValidation
{
    public static async Task Valider(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirst("userId")?.Value, out var id)) { context.Fail("Utilisateur invalide."); return; }
        var db = context.HttpContext.RequestServices.GetRequiredService<ContextBaseDeDonnees>();
        var ct = context.HttpContext.RequestAborted;
        var user = await db.Utilisateurs.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        var jti = principal?.FindFirst("jti")?.Value;
        if (user is null || !user.PeutSeConnecter() || principal?.FindFirst(ClaimTypes.Role)?.Value != user.Role.ToString() ||
            principal?.FindFirst("entrepriseId")?.Value != (user.EntrepriseId?.ToString() ?? "") ||
            !await db.RefreshTokens.AnyAsync(t => t.UtilisateurId == id && t.JwtId == jti && !t.EstRevoque && t.ExpireLe > DateTime.UtcNow, ct))
        { context.Fail("Compte ou session revoque."); return; }

        if (principal?.FindFirst("supportActorId") is { } claim)
        {
            if (!Guid.TryParse(claim.Value, out var actorId)) { context.Fail("Session support invalide."); return; }
            var actorJti = principal.FindFirst("supportActorJti")?.Value;
            if (!await db.Utilisateurs.AnyAsync(u => u.Id == actorId && u.Role == RoleUtilisateur.SuperAdmin && u.Statut == StatutCompte.Actif && !u.EstSupprime, ct) ||
                !await db.RefreshTokens.AnyAsync(t => t.UtilisateurId == actorId && t.JwtId == actorJti && !t.EstRevoque && t.ExpireLe > DateTime.UtcNow, ct))
                context.Fail("Session administrateur revoquee.");
        }
    }
}

