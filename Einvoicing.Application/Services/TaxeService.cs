using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class TaxeService(ITaxeRepository repo) : ITaxeService
{
    public async Task<TaxeDto> CreerAsync(
        Guid entrepriseId, CreerTaxeRequest req, CancellationToken ct = default)
    {
        var taxe = Taxe.Creer(
            entrepriseId,
            req.Titre,
            req.Taux,
            Parse<TypeTaxe>(req.Type, "Type"),
            req.Description);

        await repo.AjouterAsync(taxe, ct);
        await repo.SauvegarderAsync(ct);
        return MapToDto(taxe);
    }

    public async Task<IReadOnlyList<TaxeDto>> ListerAsync(Guid entrepriseId, CancellationToken ct = default)
    {
        var liste = await repo.ListerAsync(entrepriseId, ct);
        return liste.Select(MapToDto).ToList();
    }

    public async Task<TaxeDto> MettreAJourAsync(
        Guid id, Guid entrepriseId, MettreAJourTaxeRequest req, CancellationToken ct = default)
    {
        var taxe = await repo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Taxe introuvable.");
        if (taxe.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        taxe.MettreAJour(
            req.Titre,
            req.Taux,
            Parse<TypeTaxe>(req.Type, "Type"),
            req.Description);

        repo.MettreAJour(taxe);
        await repo.SauvegarderAsync(ct);
        return MapToDto(taxe);
    }

    public async Task SupprimerAsync(Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var taxe = await repo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Taxe introuvable.");
        if (taxe.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        repo.Supprimer(taxe);
        await repo.SauvegarderAsync(ct);
    }

    private static T Parse<T>(string value, string label) where T : struct, Enum
    {
        if (!Enum.TryParse<T>(value, true, out var result))
            throw new ValidationMetierException($"{label} invalide.");
        return result;
    }

    private static TaxeDto MapToDto(Taxe t) => new(
        t.Id,
        t.EntrepriseId,
        t.Titre,
        t.Taux,
        t.Type.ToString(),
        t.Description,
        t.CreeLe,
        t.ModifieLe);
}