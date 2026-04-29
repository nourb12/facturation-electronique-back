using System.Text.Json;
using System.Text.Json.Nodes;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class PersonnalisationService(IPersonnalisationRepository repo) : IPersonnalisationService
{
    public async Task<PersonnalisationDto?> ObtenirAsync(Guid entrepriseId, CancellationToken ct = default)
    {
        var perso = await repo.ObtenirAsync(entrepriseId, ct);
        return perso is null ? null : MapToDto(perso);
    }

    public async Task<PersonnalisationDto> EnregistrerAsync(
        Guid entrepriseId, EnregistrerPersonnalisationRequest request, CancellationToken ct = default)
    {
        var json = NormaliserJson(request.Donnees);
        var perso = await repo.ObtenirAsync(entrepriseId, ct);

        if (perso is null)
        {
            perso = Personnalisation.Creer(entrepriseId, json);
            await repo.AjouterAsync(perso, ct);
        }
        else
        {
            perso.MettreAJour(json);
            repo.MettreAJour(perso);
        }

        await repo.SauvegarderAsync(ct);
        return MapToDto(perso);
    }

    private static string NormaliserJson(JsonNode? node)
    {
        if (node is null)
            throw new ValidationMetierException("Les données de personnalisation sont obligatoires.");

        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static PersonnalisationDto MapToDto(Personnalisation p)
    {
        var node = JsonNode.Parse(p.DonneesJson) ?? new JsonObject();
        return new PersonnalisationDto(
            p.Id,
            p.EntrepriseId,
            node,
            p.CreeLe,
            p.ModifieLe
        );
    }
}