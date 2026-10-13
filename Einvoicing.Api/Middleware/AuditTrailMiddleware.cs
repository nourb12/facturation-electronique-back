using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;

namespace Einvoicing.Api.Middleware;

public sealed class AuditTrailMiddleware(RequestDelegate next, ILogger<AuditTrailMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ContextBaseDeDonnees db, ICurrentUserService currentUser)
    {
        await next(context);

        var request = context.Request;
        if (!request.Path.StartsWithSegments("/api") || HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            return;

        try
        {
            var path = request.Path.Value ?? "/api";
            var resource = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() ?? "API";
            db.EvenementsAudit.Add(EvenementAudit.Creer(
                currentUser.EntrepriseId,
                currentUser.UtilisateurId,
                request.Method,
                resource,
                context.TraceIdentifier,
                $"{request.Method} {path}",
                context.Connection.RemoteIpAddress?.ToString(),
                request.Headers.UserAgent.ToString(),
                context.Response.StatusCode));
            await db.SaveChangesAsync(context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible d'enregistrer l'evenement d'audit {Method} {Path}", request.Method, request.Path);
        }
    }
}
