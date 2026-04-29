using System.Collections.Generic;
using System.Text;
using System.Threading;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.Factures;

public class FactureServiceTests
{
    private readonly Mock<IFactureRepository> _factureRepo = new();
    private readonly Mock<IClientRepository> _clientRepo = new();
    private readonly Mock<INumeroFactureService> _numeroService = new();
    private readonly Mock<IEntrepriseRepository> _entrepriseRepo = new();

    private FactureService CreateService() => new(
        _factureRepo.Object,
        _clientRepo.Object,
        _numeroService.Object,
        _entrepriseRepo.Object);

    [Fact]
    public async Task CreerAsync_WithLignes_CalculatesTotalsAndRestant()
    {
        
        var entrepriseId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var creePar = Guid.NewGuid();
        var client = Client.Creer(entrepriseId, "Client SA", "client@test.com", TypeClient.B2B, "1234567890123");

        _clientRepo.Setup(x => x.ObtenirParIdAsync(clientId, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        _numeroService.Setup(x => x.GenererNumeroAsync(entrepriseId, It.IsAny<CancellationToken>())).ReturnsAsync("FAC-001");
        _factureRepo.Setup(x => x.AjouterAsync(It.IsAny<Facture>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _factureRepo.Setup(x => x.SauvegarderAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var request = new CreerFactureRequest(
            clientId,
            TypeFacture.Facture,
            ModePaiement.Virement,
            DateTime.UtcNow.AddDays(10),
            new List<CreerLigneFactureRequest>
            {
                new("Audit", 2, 100, 19, null, null, "U", 0),
                new("Support", 1, 50, 7, null, null, "U", 10)
            },
            "REF-001",
            null,
            null,
            "TND");

        var service = CreateService();

        
        var dto = await service.CreerAsync(entrepriseId, creePar, request);

        
        dto.TotalHt.Should().Be(245m);
        dto.TotalTva.Should().Be(41.15m);
        dto.TotalTtc.Should().Be(286.15m);
        dto.MontantRestant.Should().Be(dto.TotalTtc);
        _factureRepo.Verify(x => x.AjouterAsync(It.Is<Facture>(f => f.TotalTtc == 286.15m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValiderAsync_BrouillonAvecLignes_ChangeStatutEtRetourneDto()
    {
        
        var entrepriseId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var validePar = Guid.NewGuid();
        var client = Client.Creer(entrepriseId, "Client SA", "client@test.com", TypeClient.B2B, "1234567890123");

        var facture = Facture.Creer(entrepriseId, clientId, validePar, "FAC-002", TypeFacture.Facture, ModePaiement.Virement, DateTime.UtcNow.AddDays(15));
        facture.AjouterLigne(LigneFacture.Creer(facture.Id, 1, "Service", 1, 120, 19));

        _factureRepo.Setup(x => x.ObtenirAvecDetailsAsync(facture.Id, It.IsAny<CancellationToken>())).ReturnsAsync(facture);
        _factureRepo.Setup(x => x.MettreAJour(facture));
        _factureRepo.Setup(x => x.SauvegarderAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _clientRepo.Setup(x => x.ObtenirParIdAsync(clientId, It.IsAny<CancellationToken>())).ReturnsAsync(client);

        var service = CreateService();

        
        var dto = await service.ValiderAsync(facture.Id, entrepriseId, validePar, CancellationToken.None);

        
        facture.Statut.Should().Be(StatutFacture.Validee);
        dto.Statut.Should().Be(StatutFacture.Validee.ToString());
        _factureRepo.Verify(x => x.MettreAJour(facture), Times.Once);
    }
    [Fact]
    public async Task GenererPdfAsync_FactureValide_ReturnsRealPdfBytes()
    {
        var entrepriseId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var creePar = Guid.NewGuid();

        var client = Client.Creer(
            entrepriseId,
            "Client SA",
            "client@test.com",
            TypeClient.B2B,
            "1234567ABM000");

        var entreprise = Entreprise.Creer(
            "TunisFlow Demo",
            "1234567ABM000",
            "1 rue de Tunis",
            "Tunis",
            "1000",
            "contact@tunisflow.tn",
            "TVA001",
            RegimeFiscal.Reel);

        var facture = Facture.Creer(
            entrepriseId,
            clientId,
            creePar,
            "FAC-PDF-001",
            TypeFacture.Facture,
            ModePaiement.Virement,
            DateTime.UtcNow.AddDays(15));

        facture.AjouterLigne(LigneFacture.Creer(facture.Id, 1, "Audit TEIF", 1, 120, 19));

        _factureRepo.Setup(x => x.ObtenirAvecDetailsAsync(facture.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(facture);
        _clientRepo.Setup(x => x.ObtenirParIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        _entrepriseRepo.Setup(x => x.ObtenirParIdAsync(entrepriseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entreprise);

        var service = CreateService();

        var pdfBytes = await service.GenererPdfAsync(facture.Id, entrepriseId, CancellationToken.None);

        pdfBytes.Should().NotBeEmpty();
        pdfBytes.Length.Should().BeGreaterThan(1000);
        Encoding.ASCII.GetString(pdfBytes, 0, 4).Should().Be("%PDF");
    }
}
