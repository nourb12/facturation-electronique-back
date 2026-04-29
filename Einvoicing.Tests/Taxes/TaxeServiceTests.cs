using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.Taxes;

public class TaxeServiceTests
{
    private readonly Mock<ITaxeRepository> _repo = new();

    private TaxeService CreateService() => new(_repo.Object);

    [Fact]
    public async Task CreerAsync_Valid_Persists()
    {
        var entrepriseId = Guid.NewGuid();
        var req = new CreerTaxeRequest("TVA", 19m, "Tva", null);
        _repo.Setup(r => r.AjouterAsync(It.IsAny<Taxe>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        var dto = await service.CreerAsync(entrepriseId, req);

        dto.Titre.Should().Be("TVA");
        _repo.Verify(r => r.AjouterAsync(It.IsAny<Taxe>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MettreAJourAsync_EntrepriseMismatch_ThrowAccesRefuse()
    {
        var entrepriseId = Guid.NewGuid();
        var taxe = Taxe.Creer(entrepriseId, "TVA", 19m, TypeTaxe.Tva, null);
        _repo.Setup(r => r.ObtenirParIdAsync(taxe.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(taxe);

        var req = new MettreAJourTaxeRequest("TVA", 19m, "Tva", null);
        var service = CreateService();

        Func<Task> act = () => service.MettreAJourAsync(taxe.Id, Guid.NewGuid(), req);

        await act.Should().ThrowAsync<AccesRefuseException>();
    }

    [Fact]
    public async Task CreerAsync_InvalidType_ThrowsValidation()
    {
        var req = new CreerTaxeRequest("TVA", 19m, "BadType", null);
        var service = CreateService();

        Func<Task> act = () => service.CreerAsync(Guid.NewGuid(), req);

        await act.Should().ThrowAsync<ValidationMetierException>();
    }
}