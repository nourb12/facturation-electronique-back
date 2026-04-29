using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.Clients;

public class ClientServiceTests
{
    private readonly Mock<IClientRepository> _clientRepo = new();

    private ClientService CreateService() => new(_clientRepo.Object);

    [Fact]
    public async Task CreerAsync_EmailExistant_ThrowConflit()
    {
        var entrepriseId = Guid.NewGuid();
        var existing = Client.Creer(entrepriseId, "Acme", "a@b.c", TypeClient.B2C);
        _clientRepo.Setup(r => r.ObtenirParEmailAsync(entrepriseId, "a@b.c", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var req = new CreerClientRequest("Acme", "a@b.c", TypeClient.B2C, null, null, null, null, null, "TN");
        var service = CreateService();

        Func<Task> act = () => service.CreerAsync(entrepriseId, req);

        await act.Should().ThrowAsync<ConflitException>();
    }

    [Fact]
    public async Task MettreAJourAsync_EntrepriseMismatch_ThrowAccesRefuse()
    {
        var entrepriseId = Guid.NewGuid();
        var client = Client.Creer(entrepriseId, "Acme", "a@b.c", TypeClient.B2C);
        _clientRepo.Setup(r => r.ObtenirParIdAsync(client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        var req = new MettreAJourClientRequest("New", "n@b.c", TypeClient.B2C, null, null, null, null, null);
        var service = CreateService();

        Func<Task> act = () => service.MettreAJourAsync(client.Id, Guid.NewGuid(), req);

        await act.Should().ThrowAsync<AccesRefuseException>();
    }

    [Fact]
    public async Task DesactiverAsync_SetsInactiveAndPersists()
    {
        var entrepriseId = Guid.NewGuid();
        var client = Client.Creer(entrepriseId, "Acme", "a@b.c", TypeClient.B2C);
        _clientRepo.Setup(r => r.ObtenirParIdAsync(client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        _clientRepo.Setup(r => r.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        await service.DesactiverAsync(client.Id, entrepriseId);

        client.EstActif.Should().BeFalse();
        _clientRepo.Verify(r => r.MettreAJour(client), Times.Once);
    }
}