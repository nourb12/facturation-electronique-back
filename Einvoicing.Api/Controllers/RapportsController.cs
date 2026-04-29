using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/rapports")]
[Produces("application/json")]
[Authorize]
public sealed class RapportsController(
    IRapportService rapportService,
    ICurrentUserService currentUser
) : ControllerBase
{
    private IActionResult? EntrepriseRequise(out Guid entrepriseId)
    {
        entrepriseId = Guid.Empty;
        if (currentUser.EntrepriseId is null)
            return Problem(
                title: "Entreprise non rattachee",
                detail: "Votre compte n'est pas encore rattache a une entreprise. Reconnectez-vous.",
                statusCode: 403);
        entrepriseId = currentUser.EntrepriseId.Value;
        return null;
    }

    private static (DateTime Debut, DateTime Fin) NormaliserPeriode(
        DateTime? dateDebut, DateTime? dateFin, int moisDefaut)
    {
        if (dateDebut is null && dateFin is null)
        {
            var maintenant = DateTime.UtcNow;
            var debutDefaut = new DateTime(maintenant.Year, maintenant.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMonths(-(moisDefaut - 1));
            var finDefaut = new DateTime(maintenant.Year, maintenant.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMonths(1);
            return (debutDefaut, finDefaut);
        }

        if (dateDebut is null)
        {
            var finRaw = dateFin!.Value;
            var debut = finRaw.AddMonths(-moisDefaut);
            var fin = NormaliserFin(finRaw);
            return AssurerPeriodeValide(debut, fin);
        }

        if (dateFin is null)
        {
            var debut = dateDebut.Value;
            var fin = NormaliserFin(debut.AddMonths(moisDefaut));
            return AssurerPeriodeValide(debut, fin);
        }

        return AssurerPeriodeValide(dateDebut.Value, NormaliserFin(dateFin.Value));
    }

    private static DateTime NormaliserFin(DateTime fin)
        => fin.TimeOfDay == TimeSpan.Zero ? fin.AddDays(1) : fin;

    private static (DateTime Debut, DateTime Fin) AssurerPeriodeValide(DateTime debut, DateTime fin)
    {
        if (fin <= debut)
            fin = debut.AddDays(1);
        return (debut, fin);
    }

    [HttpGet("tva-par-taux")]
    [ProducesResponseType(typeof(List<TvaParTauxDto>), 200)]
    public async Task<IActionResult> TvaParTaux(
        [FromQuery] DateTime? dateDebut = null,
        [FromQuery] DateTime? dateFin = null,
        CancellationToken ct = default)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;

        var (debut, fin) = NormaliserPeriode(dateDebut, dateFin, 1);
        var result = await rapportService.ObtenirTvaParTauxAsync(eId, debut, fin, ct);
        return Ok(result);
    }

    [HttpGet("delais-paiement")]
    [ProducesResponseType(typeof(List<DelaiPaiementClientDto>), 200)]
    public async Task<IActionResult> DelaisPaiement(
        [FromQuery] DateTime? dateDebut = null,
        [FromQuery] DateTime? dateFin = null,
        [FromQuery] int top = 5,
        CancellationToken ct = default)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;

        var (debut, fin) = NormaliserPeriode(dateDebut, dateFin, 1);
        var result = await rapportService.ObtenirDelaisPaiementAsync(eId, debut, fin, top, ct);
        return Ok(result);
    }

    [HttpGet("recap-mensuel")]
    [ProducesResponseType(typeof(List<RecapMensuelDto>), 200)]
    public async Task<IActionResult> RecapMensuel(
        [FromQuery] DateTime? dateDebut = null,
        [FromQuery] DateTime? dateFin = null,
        CancellationToken ct = default)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;

        var (debut, fin) = NormaliserPeriode(dateDebut, dateFin, 6);
        var result = await rapportService.ObtenirRecapMensuelAsync(eId, debut, fin, ct);
        return Ok(result);
    }
}