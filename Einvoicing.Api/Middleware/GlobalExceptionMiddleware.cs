




using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Einvoicing.Domain.Errors;
using Einvoicing.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Einvoicing.Api.Middleware;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOpts =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            await Handle(ctx, ex);
        }
    }

    private async Task Handle(HttpContext ctx, Exception ex)
    {
        (HttpStatusCode status, string code, string message, string[] erreurs, object? meta) = ex switch
        {
            ValidationException ve => (
                HttpStatusCode.BadRequest,
                "BUSINESS_ERROR",
                ErrorCodes.InvalidData,
                ve.Errors.Select(e => e.ErrorMessage).ToArray()
                , null
            ),
            ValidationMetierException e => (HttpStatusCode.BadRequest, "BUSINESS_ERROR", e.Message, Array.Empty<string>(), null),
            ConflitException e => (HttpStatusCode.Conflict, "BUSINESS_ERROR", e.Message, Array.Empty<string>(), null),
            DbUpdateException dbEx when EstViolationUnique(dbEx, out var msg) =>
                (HttpStatusCode.Conflict, "BUSINESS_ERROR", msg, Array.Empty<string>(), null),
            NotFoundException e => (HttpStatusCode.NotFound, "NOT_FOUND", e.Message, Array.Empty<string>(), null),
            IdentifiantsInvalidesException e => (HttpStatusCode.Unauthorized, "UNAUTHORIZED", e.Message, Array.Empty<string>(), null),
            CompteInactifException e => (HttpStatusCode.Unauthorized, "UNAUTHORIZED", e.Message, Array.Empty<string>(), null),
            TokenInvalideException e => (HttpStatusCode.Unauthorized, "UNAUTHORIZED", e.Message, Array.Empty<string>(), null),
            OtpInvalideException e => (HttpStatusCode.BadRequest, "BUSINESS_ERROR", e.Message, Array.Empty<string>(), null),
            AccesRefuseException e => (HttpStatusCode.Forbidden, "FORBIDDEN", e.Message, Array.Empty<string>(), null),
            TropDeTentativesException e => (HttpStatusCode.TooManyRequests, "RATE_LIMIT", e.Message, Array.Empty<string>(), new { secondsRemaining = e.SecondesRestantes }),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR", ErrorCodes.Generic, Array.Empty<string>(), null)
        };

        if ((int)status >= 500)
            logger.LogError(ex, "Erreur non gérée : {Message}", ex.Message);
        else
            logger.LogWarning(ex, "Erreur traitée : {Message}", ex.Message);

        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/json";

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            success = false,
            message,
            code,
            errors = erreurs.Length > 0 ? erreurs : null,
            meta
        }, JsonOpts));
    }

    private static bool EstViolationUnique(DbUpdateException ex, out string message)
    {
        message = ErrorCodes.ConflictData;
        if (ex.InnerException is not PostgresException pg || pg.SqlState != PostgresErrorCodes.UniqueViolation)
            return false;

        message = pg.ConstraintName switch
        {
            "IX_Entreprises_MatriculeFiscal" => ErrorCodes.MatriculeAlreadyUsed,
            "IX_Utilisateurs_Email" => ErrorCodes.EmailAlreadyUsed,
            _ => ErrorCodes.UniqueConstraintViolation
        };
        return true;
    }
}
