using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Produces("application/json")]
[Authorize(Policy = "SuperOuAdmin")]
public sealed class AdminOverviewController(
    ContextBaseDeDonnees db,
    ITeifService teifService,
    ISignatureService signatureService,
    IEchangeTtnService echangeService,
    ICurrentUserService currentUser,
    IEmailService emailService
) : ControllerBase
{
    [HttpGet("entreprises")]
    public async Task<IActionResult> Entreprises(CancellationToken ct)
    {
        var items = await db.Entreprises
            .Select(e => new
            {
                id = e.Id,
                raisonSociale = e.Nom,
                e.MatriculeFiscal,
                e.Ville,
                nbUtilisateurs = db.Utilisateurs.Count(u => u.EntrepriseId == e.Id),
                nbFactures = db.Factures.Count(f => f.EntrepriseId == e.Id),
                e.EstActive
            })
            .OrderBy(e => e.raisonSociale)
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPatch("entreprises/{id:guid}/statut")]
    public async Task<IActionResult> ChangerStatutEntreprise(Guid id, [FromBody] AdminStatutEntrepriseRequest req, CancellationToken ct)
    {
        var entreprise = await db.Entreprises.FindAsync([id], ct);
        if (entreprise is null) return NotFound();

        if (req.EstActive) entreprise.Reactiver();
        else entreprise.Desactiver();

        await db.SaveChangesAsync(ct);
        return Ok(new { entreprise.Id, entreprise.EstActive });
    }

    [HttpGet("factures")]
    public async Task<IActionResult> Factures(CancellationToken ct)
    {
        var rows = await db.Factures
            .Join(db.Entreprises, f => f.EntrepriseId, e => e.Id, (f, e) => new { f, e })
            .Join(db.Clients, x => x.f.ClientId, c => c.Id, (x, c) => new
            {
                id = x.f.Id,
                entrepriseId = x.e.Id,
                numero = x.f.Numero,
                entrepriseNom = x.e.Nom,
                clientNom = c.Nom,
                clientEmail = c.Email,
                x.f.DateEmission,
                totalTTC = x.f.TotalTtc,
                statut = x.f.EstEnRetard ? "EnRetard" : x.f.Statut.ToString(),
                xmlGenere = x.f.XmlTeif != null && x.f.XmlTeif != "",
                x.f.HashIntegrite,
                x.f.VersionTeif,
                signatureStatut = db.Signatures
                    .Where(s => s.FactureId == x.f.Id)
                    .OrderByDescending(s => s.CreeLe)
                    .Select(s => (int?)s.Statut)
                    .FirstOrDefault(),
                echangeStatut = db.Echanges
                    .Where(e => e.FactureId == x.f.Id)
                    .OrderByDescending(e => e.CreeLe)
                    .Select(e => (int?)e.Statut)
                    .FirstOrDefault()
            })
            .OrderByDescending(f => f.DateEmission)
            .Take(500)
            .ToListAsync(ct);

        var items = rows.Select(f => new
        {
            f.id,
            f.entrepriseId,
            f.numero,
            f.entrepriseNom,
            f.clientNom,
            f.clientEmail,
            f.DateEmission,
            f.totalTTC,
            f.statut,
            f.xmlGenere,
            f.HashIntegrite,
            f.VersionTeif,
            signatureStatut = f.signatureStatut.HasValue
                ? Enum.GetName(typeof(StatutSignature), f.signatureStatut.Value)
                : null,
            echangeStatut = f.echangeStatut.HasValue
                ? Enum.GetName(typeof(StatutEchange), f.echangeStatut.Value)
                : null
        });

        return Ok(items);
    }

    [HttpGet("factures/{id:guid}/xml")]
    public async Task<IActionResult> VoirXml(Guid id, CancellationToken ct)
    {
        var facture = await ChargerFactureAdminAsync(id, ct);
        if (facture is null) return NotFound();

        if (string.IsNullOrWhiteSpace(facture.XmlTeif))
        {
            var xml = await teifService.GenererXmlAsync(id, facture.EntrepriseId, ct);
            return Ok(xml);
        }

        return Ok(new XmlTeifDto(
            facture.Id,
            facture.Numero,
            facture.XmlTeif,
            facture.HashIntegrite ?? string.Empty,
            facture.VersionTeif ?? "v0",
            facture.ModifieLe));
    }

    [HttpPost("factures/{id:guid}/valider-v0")]
    public async Task<IActionResult> ValiderV0(Guid id, CancellationToken ct)
    {
        var userId = currentUser.UtilisateurId;
        if (userId is null) return Unauthorized();

        var facture = await ChargerFactureAdminAsync(id, ct);
        if (facture is null) return NotFound();

        if (facture.Statut is StatutFacture.Conforme or StatutFacture.Transmise or StatutFacture.Acceptee)
            return Ok(await ConstruireEtatFactureAsync(id, "Facture deja validee v0.", ct));

        if (facture.Statut != StatutFacture.Validee)
            return BadRequest(new
            {
                message = "La facture doit d'abord etre validee cote entreprise avant validation admin v0.",
                statut = facture.Statut.ToString()
            });

        var xml = await teifService.GenererXmlAsync(id, facture.EntrepriseId, ct);
        var validation = await teifService.ValiderConformiteAsync(id, facture.EntrepriseId, ct);
        if (!validation.EstConforme)
            return BadRequest(new
            {
                message = "XML non conforme : validation v0 impossible.",
                validation
            });

        var dto = await teifService.MarquerConformeAsync(id, facture.EntrepriseId, userId.Value, ct);
        await NotifierEntrepriseAsync(
            facture.EntrepriseId,
            "Facture validee v0",
            facture.Numero,
            "validee",
            facture.TotalTtc,
            ct);

        return Ok(new
        {
            message = "Facture validee v0. Email envoye au responsable.",
            facture = dto,
            xml,
            validation
        });
    }

    [HttpPost("factures/{id:guid}/envoyer-ttn")]
    public async Task<IActionResult> EnvoyerTtn(Guid id, CancellationToken ct)
    {
        var userId = currentUser.UtilisateurId;
        if (userId is null) return Unauthorized();

        var facture = await ChargerFactureAdminAsync(id, ct);
        if (facture is null) return NotFound();

        if (facture.Statut == StatutFacture.Validee)
        {
            var validation = await teifService.ValiderConformiteAsync(id, facture.EntrepriseId, ct);
            if (!validation.EstConforme)
                return BadRequest(new
                {
                    message = "XML non conforme : transmission TTN impossible.",
                    validation
                });

            await teifService.GenererXmlAsync(id, facture.EntrepriseId, ct);
            await teifService.MarquerConformeAsync(id, facture.EntrepriseId, userId.Value, ct);
        }

        facture = await ChargerFactureAdminAsync(id, ct);
        if (facture is null) return NotFound();

        if (facture.Statut is StatutFacture.Transmise or StatutFacture.Acceptee)
            return Ok(await ConstruireEtatFactureAsync(id, "Facture deja envoyee a TTN simulation.", ct));

        if (facture.Statut != StatutFacture.Conforme)
            return BadRequest(new
            {
                message = "La facture doit etre validee v0 avant l'envoi TTN.",
                statut = facture.Statut.ToString()
            });

        var signature = await db.Signatures
            .AsNoTracking()
            .Where(s => s.FactureId == id)
            .OrderByDescending(s => s.CreeLe)
            .FirstOrDefaultAsync(ct);

        SignatureDto? signatureDto = null;
        if (signature is null || signature.Statut != StatutSignature.Signee)
            signatureDto = await signatureService.DemanderSignatureAsync(id, facture.EntrepriseId, userId.Value, ct);

        var echange = await echangeService.EnvoyerAsync(id, facture.EntrepriseId, userId.Value, ct);
        await NotifierEntrepriseAsync(
            facture.EntrepriseId,
            "Facture envoyee a TTN simulation",
            facture.Numero,
            "validee",
            facture.TotalTtc,
            ct);

        return Ok(new
        {
            message = "Facture signee puis envoyee a TTN simulation.",
            signature = signatureDto,
            echange
        });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit(CancellationToken ct)
    {
        var items = await db.HistoriqueFactures
            .Join(db.Factures, h => h.FactureId, f => f.Id, (h, f) => new { h, f })
            .Join(db.Utilisateurs, x => x.h.EffectuePar, u => u.Id, (x, u) => new
            {
                date = x.h.CreeLe,
                utilisateur = u.Prenom + " " + u.Nom,
                action = x.h.Action,
                ressource = "Facture " + x.f.Numero,
                ip = "-",
                succes = true
            })
            .OrderByDescending(x => x.date)
            .Take(300)
            .ToListAsync(ct);

        return Ok(items);
    }

    private async Task<Einvoicing.Domain.Entities.Facture?> ChargerFactureAdminAsync(
        Guid factureId, CancellationToken ct)
        => await db.Factures
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == factureId, ct);

    private async Task<object> ConstruireEtatFactureAsync(
        Guid factureId, string message, CancellationToken ct)
    {
        var facture = await db.Factures.AsNoTracking().FirstAsync(f => f.Id == factureId, ct);
        var signature = await db.Signatures.AsNoTracking()
            .Where(s => s.FactureId == factureId)
            .OrderByDescending(s => s.CreeLe)
            .Select(s => (int?)s.Statut)
            .FirstOrDefaultAsync(ct);
        var echange = await db.Echanges.AsNoTracking()
            .Where(e => e.FactureId == factureId)
            .OrderByDescending(e => e.CreeLe)
            .Select(e => (int?)e.Statut)
            .FirstOrDefaultAsync(ct);

        return new
        {
            message,
            factureId,
            facture.Numero,
            statut = facture.Statut.ToString(),
            xmlGenere = !string.IsNullOrWhiteSpace(facture.XmlTeif),
            facture.HashIntegrite,
            facture.VersionTeif,
            signatureStatut = signature.HasValue
                ? Enum.GetName(typeof(StatutSignature), signature.Value)
                : null,
            echangeStatut = echange.HasValue
                ? Enum.GetName(typeof(StatutEchange), echange.Value)
                : null
        };
    }

    private async Task NotifierEntrepriseAsync(
        Guid entrepriseId,
        string sujet,
        string numeroFacture,
        string type,
        decimal montantTtc,
        CancellationToken ct)
    {
        var entreprise = await db.Entreprises.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entrepriseId, ct);
        if (entreprise is null || string.IsNullOrWhiteSpace(entreprise.Email))
            return;

        await emailService.EnvoyerNotifFactureAsync(
            entreprise.Email,
            entreprise.Nom,
            sujet,
            numeroFacture,
            type,
            montantTtc,
            ct);
    }
}

public sealed record AdminStatutEntrepriseRequest(bool EstActive);
