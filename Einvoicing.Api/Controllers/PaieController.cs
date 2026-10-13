using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/paie")]
[Authorize]
[Produces("application/json")]
public sealed class PaieController(ContextBaseDeDonnees db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] string? periode, CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return NotFound();
        var currentPeriod = string.IsNullOrWhiteSpace(periode) ? DateTime.UtcNow.ToString("yyyy-MM") : periode;
        var employes = await db.EmployesPaie.Where(e => e.EntrepriseId == entrepriseId).OrderBy(e => e.Nom).ToListAsync(ct);
        var fiches = await db.BulletinsPaie.Where(f => f.EntrepriseId == entrepriseId && f.Periode == currentPeriod).ToListAsync(ct);
        return Ok(new { periode = currentPeriod, employes = employes.Select(ToDto), fiches = fiches.Select(ToDto), declarations = Declarations(fiches) });
    }

    [HttpPost("employes")]
    public async Task<IActionResult> CreerEmploye([FromBody] CreerEmployeRequest request, CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return NotFound();
        if (await db.EmployesPaie.AnyAsync(e => e.EntrepriseId == entrepriseId && e.Matricule == request.Matricule.Trim(), ct))
            return Conflict(new { message = "Ce matricule existe déjà." });
        var employe = EmployePaie.Creer(entrepriseId.Value, request.Matricule, request.Nom, request.Poste, request.Departement, request.SalaireBase, request.Prime, request.Avantage, request.Cin, request.Cnss, request.Rib);
        db.EmployesPaie.Add(employe); await db.SaveChangesAsync(ct);
        return Created($"/api/paie/employes/{employe.Id}", ToDto(employe));
    }

    [HttpPost("fiches/generer")]
    public async Task<IActionResult> Generer([FromBody] GenererFichesRequest request, CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return NotFound();
        var employes = await db.EmployesPaie.Where(e => e.EntrepriseId == entrepriseId && e.EstActif).ToListAsync(ct);
        var existing = await db.BulletinsPaie.Where(f => f.EntrepriseId == entrepriseId && f.Periode == request.Periode).Select(f => f.EmployeId).ToListAsync(ct);
        var fiches = employes.Where(e => !existing.Contains(e.Id)).Select(e => BulletinPaie.Generer(e, request.Periode)).ToList();
        if (fiches.Count > 0) { db.BulletinsPaie.AddRange(fiches); await db.SaveChangesAsync(ct); }
        return Ok(new { generees = fiches.Count, fiches = fiches.Select(ToDto) });
    }

    [HttpPost("fiches/{id:guid}/valider")]
    public async Task<IActionResult> Valider(Guid id, CancellationToken ct)
    {
        var fiche = await FindFicheAsync(id, ct); if (fiche is null) return NotFound();
        fiche.Valider(); await db.SaveChangesAsync(ct); return Ok(ToDto(fiche));
    }

    [HttpPost("fiches/{id:guid}/payer")]
    public async Task<IActionResult> Payer(Guid id, [FromBody] PayerFicheRequest request, CancellationToken ct)
    {
        var fiche = await FindFicheAsync(id, ct); if (fiche is null) return NotFound();
        fiche.Payer(request.DatePaiement ?? DateTime.UtcNow, request.Reference);
        db.Transactions.Add(Transaction.Creer(fiche.EntrepriseId, currentUser.UtilisateurId ?? Guid.Empty, fiche.DatePaiement!.Value,
            $"Paiement salaire {fiche.Periode}", fiche.NetAPayer, TypeTransaction.Sortie,
            tiersNom: "Salariés", categorieNom: "Salaires", compte: "641100", source: DocumentSource.Web));
        await db.SaveChangesAsync(ct); return Ok(ToDto(fiche));
    }

    private async Task<BulletinPaie?> FindFicheAsync(Guid id, CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        return await db.BulletinsPaie.SingleOrDefaultAsync(f => f.Id == id && f.EntrepriseId == entrepriseId, ct);
    }
    private async Task<Guid?> EntrepriseIdAsync(CancellationToken ct) => currentUser.EntrepriseId ?? await db.Entreprises.Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);
    private static object ToDto(EmployePaie e) => new { id = e.Id, matricule = e.Matricule, nom = e.Nom, poste = e.Poste, departement = e.Departement, cin = e.Cin, cnss = e.NumeroCnss, rib = e.Rib, salaireBase = e.SalaireBase, prime = e.PrimeMensuelle, avantage = e.AvantageMensuel, actif = e.EstActif };
    private static object ToDto(BulletinPaie f) => new { id = f.Id, employeId = f.EmployeId, periode = f.Periode, statut = f.Statut.Replace("é", "e"), brut = f.Brut, cnssSalarie = f.CnssSalarie, irpp = f.Irpp, avances = f.Avances, net = f.NetAPayer, datePaiement = f.DatePaiement };
    private static object[] Declarations(IReadOnlyList<BulletinPaie> fiches) =>
    [
        new { code = "CNSS", montant = fiches.Sum(f => f.CnssSalarie), statut = "À déclarer" },
        new { code = "IRPP", montant = fiches.Sum(f => f.Irpp), statut = "À déclarer" },
        new { code = "PAY", montant = fiches.Sum(f => f.NetAPayer), statut = "Paiement" }
    ];
}

public sealed record CreerEmployeRequest(string Matricule, string Nom, string Poste, string Departement, decimal SalaireBase, decimal Prime, decimal Avantage, string? Cin, string? Cnss, string? Rib);
public sealed record GenererFichesRequest(string Periode);
public sealed record PayerFicheRequest(DateTime? DatePaiement, string? Reference);
