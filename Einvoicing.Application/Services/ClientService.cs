




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class ClientService(IClientRepository clientRepo) : IClientService
{
    public async Task<ClientDto> CreerAsync(
        Guid entrepriseId, CreerClientRequest req, CancellationToken ct = default)
    {
        var existant = await clientRepo.ObtenirParEmailAsync(entrepriseId, req.Email.ToLower(), ct);
        if (existant is not null)
            throw new ConflitException("Un client avec cet email existe déjà.");

        var client = Client.Creer(
            entrepriseId, req.Nom, req.Email, req.TypeClient,
            req.MatriculeFiscal, req.Adresse, req.Ville,
            req.CodePostal, req.Telephone, req.Pays);

        await clientRepo.AjouterAsync(client, ct);
        await clientRepo.SauvegarderAsync(ct);
        return MapToDto(client);
    }

    public async Task<ClientDto> ObtenirParIdAsync(
        Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var client = await clientRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Client introuvable.");
        if (client.EntrepriseId != entrepriseId)
            throw new AccesRefuseException();
        return MapToDto(client);
    }

    public async Task<ListeClientsDto> ListerAsync(
        Guid entrepriseId, int page, int parPage, bool? actifSeulement, CancellationToken ct = default)
    {
        var (items, total) = await clientRepo.ListerAsync(entrepriseId, page, parPage, actifSeulement, ct);
        return new ListeClientsDto(items.Select(MapToDto).ToList(), total, page, parPage);
    }

    public async Task<ClientDto> MettreAJourAsync(
        Guid id, Guid entrepriseId, MettreAJourClientRequest req, CancellationToken ct = default)
    {
        var client = await clientRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Client introuvable.");
        if (client.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        client.MettreAJour(req.Nom, req.Email, req.MatriculeFiscal,
            req.Adresse, req.Ville, req.CodePostal, req.Telephone, req.TypeClient);

        clientRepo.MettreAJour(client);
        await clientRepo.SauvegarderAsync(ct);
        return MapToDto(client);
    }

    public async Task DesactiverAsync(Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var client = await clientRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Client introuvable.");
        if (client.EntrepriseId != entrepriseId) throw new AccesRefuseException();
        client.Desactiver();
        clientRepo.MettreAJour(client);
        await clientRepo.SauvegarderAsync(ct);
    }

    public async Task ReactiverAsync(Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var client = await clientRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Client introuvable.");
        if (client.EntrepriseId != entrepriseId) throw new AccesRefuseException();
        client.Reactiver();
        clientRepo.MettreAJour(client);
        await clientRepo.SauvegarderAsync(ct);
    }

    public async Task<IReadOnlyList<ClientDto>> RechercherAsync(
        Guid entrepriseId, string terme, CancellationToken ct = default)
    {
        var liste = await clientRepo.RechercherAsync(entrepriseId, terme, ct);
        return liste.Select(MapToDto).ToList();
    }

    private static ClientDto MapToDto(Client c) => new(
        c.Id, c.EntrepriseId, c.Nom, c.Email, c.TypeClient.ToString(),
        c.MatriculeFiscal, c.Adresse, c.Ville, c.CodePostal,
        c.Pays, c.Telephone, c.EstActif, c.CreeLe, c.ModifieLe);
}
