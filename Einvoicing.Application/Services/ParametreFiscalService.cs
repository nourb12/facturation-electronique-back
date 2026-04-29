using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class ParametreFiscalService(IParametreFiscalRepository repo) : IParametreFiscalService
{
    public async Task<ParametreFiscalDto> CreerAsync(
        Guid entrepriseId, CreerParametreFiscalRequest req, CancellationToken ct = default)
    {
        var parametre = ParametreFiscal.Creer(
            entrepriseId,
            req.Libelle,
            req.Valeur,
            Parse<TypeParametreFiscal>(req.Type, "Type"),
            Parse<SigneParametreFiscal>(req.Signe, "Signe"),
            Parse<OrdreCalcul>(req.OrdreCalcul, "Ordre de calcul"),
            Parse<UtilisationParametreFiscal>(req.Utilisation, "Utilisation"),
            req.InclureRetenueSource,
            req.DocumentsCibles ?? []);

        await repo.AjouterAsync(parametre, ct);
        await repo.SauvegarderAsync(ct);
        return MapToDto(parametre);
    }

    public async Task<IReadOnlyList<ParametreFiscalDto>> ListerAsync(
        Guid entrepriseId, CancellationToken ct = default)
    {
        var liste = await repo.ListerAsync(entrepriseId, ct);
        return liste.Select(MapToDto).ToList();
    }

    public async Task<ParametreFiscalDto> MettreAJourAsync(
        Guid id, Guid entrepriseId, MettreAJourParametreFiscalRequest req, CancellationToken ct = default)
    {
        var param = await repo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Paramètre fiscal introuvable.");
        if (param.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        param.MettreAJour(
            req.Libelle,
            req.Valeur,
            Parse<TypeParametreFiscal>(req.Type, "Type"),
            Parse<SigneParametreFiscal>(req.Signe, "Signe"),
            Parse<OrdreCalcul>(req.OrdreCalcul, "Ordre de calcul"),
            Parse<UtilisationParametreFiscal>(req.Utilisation, "Utilisation"),
            req.InclureRetenueSource,
            req.DocumentsCibles ?? []);

        repo.MettreAJour(param);
        await repo.SauvegarderAsync(ct);
        return MapToDto(param);
    }

    public async Task SupprimerAsync(Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var param = await repo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Paramètre fiscal introuvable.");
        if (param.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        repo.Supprimer(param);
        await repo.SauvegarderAsync(ct);
    }

    private static T Parse<T>(string value, string label) where T : struct, Enum
    {
        if (!Enum.TryParse<T>(value, true, out var result))
            throw new ValidationMetierException($"{label} invalide.");
        return result;
    }

    private static ParametreFiscalDto MapToDto(ParametreFiscal p) => new(
        p.Id,
        p.EntrepriseId,
        p.Libelle,
        p.Valeur,
        p.Type.ToString(),
        p.Signe.ToString(),
        p.OrdreCalcul.ToString(),
        p.Utilisation.ToString(),
        p.InclureRetenueSource,
        p.DocumentsCibles,
        p.EstActif,
        p.CreeLe,
        p.ModifieLe);
}