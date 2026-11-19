namespace Einvoicing.Api.Middleware;

// Support tokens can inspect a tenant, never mutate it or mint another token.
public sealed class SupportReadOnlyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.HasClaim(c => c.Type == "supportActorId") &&
            !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) &&
            !(HttpMethods.IsPost(context.Request.Method) && context.Request.Path == "/api/support/terminer"))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { message = "La session support est en lecture seule." });
            return;
        }
        await next(context);
    }
}

