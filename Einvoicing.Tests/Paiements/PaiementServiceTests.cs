using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.Paiements;

public class PaiementServiceTests
{
    private readonly Mock<IPaiementRepository> _paiementRepo = new();
    private readonly Mock<IFactureRepository> _factureRepo = new();

    private PaiementService CreateService() => new(_paiementRepo.Object, _factureRepo.Object);

    [Fact]
    public async Task EnregistrerAsync_FactureNotFound_ThrowsNotFound()
    {
        var entrepriseId = Guid.NewGuid();
        var req = new EnregistrerPaiementRequest(Guid.NewGuid(), 10m, ModePaiement.Virement, DateTime.UtcNow, null, null, null);
        _factureRepo.Setup(r => r.ObtenirAvecDetailsAsync(req.FactureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Facture?)null);

        var service = CreateService();

        Func<Task> act = () => service.EnregistrerAsync(entrepriseId, Guid.NewGuid(), req);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task EnregistrerAsync_FactureNonAcceptee_ThrowsValidation()
    {
        var entrepriseId = Guid.NewGuid();
        var facture = Facture.Creer(
            entrepriseId, Guid.NewGuid(), Guid.NewGuid(),
            "FAC-1", TypeFacture.Facture, ModePaiement.Virement,
            DateTime.UtcNow.AddDays(5));

        _factureRepo.Setup(r => r.ObtenirAvecDetailsAsync(facture.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(facture);

        var req = new EnregistrerPaiementRequest(facture.Id, 10m, ModePaiement.Virement, DateTime.UtcNow, null, null, null);
        var service = CreateService();

        Func<Task> act = () => service.EnregistrerAsync(entrepriseId, Guid.NewGuid(), req);

        await act.Should().ThrowAsync<ValidationMetierException>();
    }
}