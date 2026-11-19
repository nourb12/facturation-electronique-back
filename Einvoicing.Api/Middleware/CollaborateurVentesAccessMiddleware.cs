namespace Einvoicing.Api.Middleware;

/// <summary>
/// The sales workspace is protected server-side; hidden navigation is not an authorization mechanism.
/// </summary>
public sealed class CollaborateurVentesAccessMiddleware(RequestDelegate next)
{
    private static readonly string[] AllowedPrefixes =
    [
        "/api/auth",
        "/api/clients",
        "/api/produits",
        "/api/categories",
        "/api/documents-commerciaux",
        "/api/factures",
        "/api/notifications",
        "/api/scan",
        "/api/utilisateurs/me",
        "/api/documents/dashboard",
        "/api/dashboard"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var isSalesUser = context.User.Identity?.IsAuthenticated == true &&
            context.User.IsInRole("CollaborateurVentes");
        var path = context.Request.Path;
        var isDocumentConversion = path.StartsWithSegments("/api/documents", out var remainder)
            && remainder.Value?.EndsWith("/convertir", StringComparison.OrdinalIgnoreCase) == true
            && HttpMethods.IsPost(context.Request.Method);

        if (isSalesUser && !isDocumentConversion && !AllowedPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Cette action n'est pas autorisée pour le rôle Collaborateur ventes." });
            return;
        }

        await next(context);
    }
}
