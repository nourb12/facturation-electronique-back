using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.Entreprises;

public class EntrepriseServiceTests
{
    private readonly Mock<IEntrepriseRepository> _entrepriseRepo = new();
    private readonly Mock<IUtilisateurRepository> _utilisateurRepo = new();

    private EntrepriseService CreateService() => new(_entrepriseRepo.Object, _utilisateurRepo.Object);

    [Fact]
    public async Task CreerAsync_MatriculeExists_ThrowConflit()
    {
        var req = new CreerEntrepriseRequest(
            "Acme", "1234567ABM000", "Adr", "Ville", "1000",
            "a@b.c", "TVA", RegimeFiscal.Reel, null, null, "TND");
        var existing = Entreprise.Creer(
            "Acme", "1234567ABM000", "Adr", "Ville", "1000",
            "a@b.c", "TVA", RegimeFiscal.Reel, null, null, "TND");
        _entrepriseRepo.Setup(r => r.ObtenirParMatriculeAsync(req.MatriculeFiscal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = CreateService();

        Func<Task> act = () => service.CreerAsync(req, Guid.NewGuid());

        await act.Should().ThrowAsync<ConflitException>();
    }

    [Fact]
    public async Task MettreAJourAsync_UpdatesAndPersists()
    {
        var entreprise = Entreprise.Creer(
            "Acme", "1234567ABM000", "Adr", "Ville", "1000",
            "a@b.c", "TVA", RegimeFiscal.Reel, null, null, "TND");
        _entrepriseRepo.Setup(r => r.ObtenirParIdAsync(entreprise.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entreprise);
        _entrepriseRepo.Setup(r => r.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var req = new MettreAJourEntrepriseRequest(
            "NewName", "Adr", "Ville", "1000", "new@b.c", RegimeFiscal.Reel, null, null, "TND");
        var service = CreateService();

        var dto = await service.MettreAJourAsync(entreprise.Id, req);

        dto.Nom.Should().Be("NewName");
        _entrepriseRepo.Verify(r => r.MettreAJour(entreprise), Times.Once);
    }
}
