using System.Text.Json;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/mobile/document-updates")]
[Authorize]
public sealed class MobileDocumentUpdatesController(ContextBaseDeDonnees db, ICurrentUserService user) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (user.UtilisateurId is not Guid actor) return Unauthorized();
        if (user.EntrepriseId is not Guid enterprise) return Forbid();
        // Only the connected user's mobile documents, never a client-supplied tenant.
        var rows = await (from document in db.ScannedDocuments.AsNoTracking()
                          join transaction in db.Transactions.AsNoTracking() on document.TransactionId equals (Guid?)transaction.Id
                          where document.EntrepriseId == enterprise && document.CreePar == actor
                              && document.Source == DocumentSource.MobileApp && transaction.EntrepriseId == enterprise
                          orderby transaction.ModifieLe descending, transaction.Id
                          select new { transaction.Id, transaction.ModifieLe, transaction.Statut, transaction.MissingFieldsJson })
            .Take(100).ToListAsync(ct);
        return Ok(rows.Select(row => new MobileDocumentUpdate(
            $"{row.Id:N}:{row.ModifieLe.Ticks}", row.Id, row.ModifieLe,
            row.Statut == StatutTransaction.Justifiee ? "reviewed" :
            row.Statut == StatutTransaction.NonJustifiee || HasMissingFields(row.MissingFieldsJson) ? "correction" : "pending")));
    }

    private static bool HasMissingFields(string json)
    {
        try { return (JsonSerializer.Deserialize<string[]>(json)?.Length ?? 0) > 0; }
        catch (JsonException) { return false; }
    }
}

public sealed record MobileDocumentUpdate(string Id, Guid TransactionId, DateTime UpdatedAt, string Kind);
