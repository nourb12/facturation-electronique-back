using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.Signatures;

public class SignatureServiceTests
{
    private readonly Mock<ISignatureRepository> _signatureRepo = new();
    private readonly Mock<IFactureRepository> _factureRepo = new();
    private readonly Mock<ISignatureProvider> _signatureProvider = new();

    private SignatureService CreateService() => new(
        _signatureRepo.Object, _factureRepo.Object, _signatureProvider.Object);

    [Fact]
    public async Task DemanderSignatureAsync_FactureNotFound_ThrowsNotFound()
    {
        var factureId = Guid.NewGuid();
        _factureRepo.Setup(r => r.ObtenirAvecDetailsAsync(factureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Facture?)null);

        var service = CreateService();

        Func<Task> act = () => service.DemanderSignatureAsync(factureId, Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class EchangeTtnServiceTests
{
    private readonly Mock<IEchangeRepository> _echangeRepo = new();
    private readonly Mock<IFactureRepository> _factureRepo = new();
    private readonly Mock<ISignatureRepository> _signatureRepo = new();

    private EchangeTtnService CreateService() => new(
        _echangeRepo.Object, _factureRepo.Object, _signatureRepo.Object);

    [Fact]
    public async Task SimulerReponseAsync_ScenarioInconnu_ThrowsValidation()
    {
        var entrepriseId = Guid.NewGuid();
        var facture = Facture.Creer(
            entrepriseId, Guid.NewGuid(), Guid.NewGuid(),
            "FAC-1", TypeFacture.Facture, ModePaiement.Virement,
            DateTime.UtcNow.AddDays(5));

        var echange = ExternalExchange.Creer(facture.Id, entrepriseId, Guid.NewGuid());

        _echangeRepo.Setup(r => r.ObtenirParIdAsync(echange.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(echange);
        _factureRepo.Setup(r => r.ObtenirAvecDetailsAsync(facture.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(facture);

        var service = CreateService();

        Func<Task> act = () => service.SimulerReponseAsync(echange.Id, "inconnu");

        await act.Should().ThrowAsync<ValidationMetierException>();
    }
}