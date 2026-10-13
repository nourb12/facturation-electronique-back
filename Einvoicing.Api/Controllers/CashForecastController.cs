using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/analysis/cash-forecast")]
[Authorize(Policy = "Tous")]
[Produces("application/json")]
public sealed class CashForecastController(
    ICashForecastService cashForecastService,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CashForecastDto), 200)]
    public async Task<IActionResult> Forecast(
        [FromBody] CashForecastRequestDto request,
        CancellationToken ct)
    {
        if (currentUser.EntrepriseId is not Guid entrepriseId)
        {
            return Problem(
                title: "Entreprise non rattachée",
                detail: "Votre compte doit être rattaché à une entreprise pour générer une prévision.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        try
        {
            var forecast = await cashForecastService.ForecastAsync(entrepriseId, request, ct);
            return Ok(forecast);
        }
        catch (HttpRequestException)
        {
            return Problem(
                title: "Service de prévision indisponible",
                detail: "Le service Machine Learning ne répond pas. Réessayez dans quelques instants.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
