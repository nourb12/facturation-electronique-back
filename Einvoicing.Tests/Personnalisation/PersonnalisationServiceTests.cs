using System.Text.Json.Nodes;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.Personnalisations;

public class PersonnalisationServiceTests
{
    private readonly Mock<IPersonnalisationRepository> _repo = new();

    private PersonnalisationService CreateService() => new(_repo.Object);

    [Fact]
    public async Task EnregistrerAsync_NullData_ThrowsValidation()
    {
        var req = new EnregistrerPersonnalisationRequest(null);
        var service = CreateService();

        Func<Task> act = () => service.EnregistrerAsync(Guid.NewGuid(), req);

        await act.Should().ThrowAsync<ValidationMetierException>();
    }

    [Fact]
    public async Task EnregistrerAsync_New_CreatesAndReturnsDto()
    {
        var entrepriseId = Guid.NewGuid();
        _repo.Setup(r => r.ObtenirAsync(entrepriseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Personnalisation?)null);
        _repo.Setup(r => r.AjouterAsync(It.IsAny<Personnalisation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var req = new EnregistrerPersonnalisationRequest(JsonNode.Parse("{\"a\":1}"));
        var service = CreateService();

        var dto = await service.EnregistrerAsync(entrepriseId, req);

        dto.EntrepriseId.Should().Be(entrepriseId);
        _repo.Verify(r => r.AjouterAsync(It.IsAny<Personnalisation>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}