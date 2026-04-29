using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.Dashboard;

public class DashboardServiceTests
{
    private readonly Mock<IFactureRepository> _factureRepo = new();
    private readonly Mock<IEntrepriseRepository> _entrepriseRepo = new();

    private DashboardService CreateService() => new(_factureRepo.Object, _entrepriseRepo.Object);

    [Fact]
    public async Task ObtenirDashboardEntrepriseAsync_RetourneAlertesQuandRetard()
    {
        var entrepriseId = Guid.NewGuid();
        var stats = new StatistiquesFacturesDto(
            1, 2, 0, 0, 0, 0, 2,
            1000m, 700m, 300m
        );

        _factureRepo.Setup(r => r.ObtenirStatistiquesAsync(entrepriseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);
        _factureRepo.Setup(r => r.ObtenirVolumesMensuelsAsync(
                entrepriseId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VolumesMensuelDto>());
        _factureRepo.Setup(r => r.ListerAsync(entrepriseId, It.IsAny<FiltreFacturesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Facture>(), 0));

        var service = CreateService();

        var dto = await service.ObtenirDashboardEntrepriseAsync(entrepriseId);

        dto.Alertes.Should().NotBeEmpty();
        dto.KpisFinanciers.TauxEncaissement.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ObtenirDashboardAdminAsync_CalculeActives()
    {
        var e1 = Entreprise.Creer(
            "E1", "1495908/S", "Adr", "Ville", "1000",
            "a@b.c", "TVA", RegimeFiscal.Reel, null, null, "TND");
        var e2 = Entreprise.Creer(
            "E2", "1234567A/B/M/000", "Adr", "Ville", "1000",
            "b@c.d", "TVA", RegimeFiscal.Reel, null, null, "TND");
        e2.Desactiver();

        _entrepriseRepo.Setup(r => r.ListerToutesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Entreprise> { e1, e2 });
        _factureRepo.Setup(r => r.ObtenirStatistiquesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StatistiquesFacturesDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var service = CreateService();

        var dto = await service.ObtenirDashboardAdminAsync();

        dto.TotalEntreprises.Should().Be(2);
        dto.EntreprisesActives.Should().Be(1);
    }
}