using System.Globalization;
using System.Threading;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.TEIF;

public class TeifServiceTests
{
    private readonly Mock<IFactureRepository> _factureRepo = new();
    private readonly Mock<IClientRepository> _clientRepo = new();
    private readonly Mock<IEntrepriseRepository> _entrepriseRepo = new();

    private TeifService CreateService() => new(
        _factureRepo.Object,
        _clientRepo.Object,
        _entrepriseRepo.Object);

    [Fact]
    public async Task ValiderConformiteAsync_ValidFacture_ReturnsNoErrors()
    {
        
        var entrepriseId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var facture = BuildFacture(entrepriseId, clientId);
        var client = Client.Creer(entrepriseId, "Client B2B", "client@b2b.tn", TypeClient.B2B, "1234567ABM000");
        var entreprise = Entreprise.Creer("ACME", "1234567ABM000", "1 rue", "Tunis", "1000", "acme@test.tn", "TVA001", RegimeFiscal.Reel);

        _factureRepo.Setup(x => x.ObtenirAvecDetailsAsync(facture.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(facture);
        _clientRepo.Setup(x => x.ObtenirParIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        _entrepriseRepo.Setup(x => x.ObtenirParIdAsync(entrepriseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entreprise);

        var service = CreateService();

        
        var validation = await service.ValiderConformiteAsync(facture.Id, entrepriseId, CancellationToken.None);

        
        validation.EstConforme.Should().BeTrue();
        validation.Erreurs.Should().BeEmpty();
    }

    [Fact]
    public async Task ValiderConformiteAsync_SansLignes_RetourneErreursBloquantes()
    {
        
        var entrepriseId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var facture = Facture.Creer(entrepriseId, clientId, Guid.NewGuid(), "FAC-EMPTY", TypeFacture.Facture, ModePaiement.Virement, DateTime.UtcNow.AddDays(5));
        var client = Client.Creer(entrepriseId, "Client B2C", "client@b2c.tn", TypeClient.B2C, null, null, null, null, null);
        var entreprise = Entreprise.Creer("ACME", "1234567ABM000", "1 rue", "Tunis", "1000", "acme@test.tn", "TVA001", RegimeFiscal.Reel);

        _factureRepo.Setup(x => x.ObtenirAvecDetailsAsync(facture.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(facture);
        _clientRepo.Setup(x => x.ObtenirParIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        _entrepriseRepo.Setup(x => x.ObtenirParIdAsync(entrepriseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entreprise);

        var service = CreateService();

        
        var validation = await service.ValiderConformiteAsync(facture.Id, entrepriseId, CancellationToken.None);

        
        validation.EstConforme.Should().BeFalse();
        validation.Erreurs.Should().Contain(e => e.Code == "R007");
        validation.Erreurs.Should().Contain(e => e.Code == "R008");
    }

    [Fact]
    public async Task GenererXmlAsync_FactureValidee_GenereXmlEtHash()
    {
        
        var entrepriseId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var facture = BuildFacture(entrepriseId, clientId);
        var client = Client.Creer(entrepriseId, "Client B2B", "client@b2b.tn", TypeClient.B2B, "1234567ABM000");
        var entreprise = Entreprise.Creer("ACME", "1234567ABM000", "1 rue", "Tunis", "1000", "acme@test.tn", "TVA001", RegimeFiscal.Reel);

        _factureRepo.Setup(x => x.ObtenirAvecDetailsAsync(facture.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(facture);
        _clientRepo.Setup(x => x.ObtenirParIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        _entrepriseRepo.Setup(x => x.ObtenirParIdAsync(entrepriseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entreprise);
        _factureRepo.Setup(x => x.MettreAJour(It.IsAny<Facture>()));
        _factureRepo.Setup(x => x.SauvegarderAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = CreateService();

        
        var xml = await service.GenererXmlAsync(facture.Id, entrepriseId, CancellationToken.None);

        
        xml.XmlContent.Should().Contain(facture.Numero);
        xml.XmlContent.Should().Contain(client.Nom);
        xml.XmlContent.Should().Contain(entreprise.MatriculeFiscal);
        xml.XmlContent.Should().Contain(facture.TotalTtc.ToString("F3"));
        xml.HashIntegrite.Should().NotBeNullOrWhiteSpace();
        facture.XmlTeif.Should().NotBeNull();
    }

    private static Facture BuildFacture(Guid entrepriseId, Guid clientId)
    {
        var facture = Facture.Creer(entrepriseId, clientId, Guid.NewGuid(), "FAC-TEIF", TypeFacture.Facture, ModePaiement.Virement, DateTime.UtcNow.AddDays(5));
        facture.AjouterLigne(LigneFacture.Creer(facture.Id, 1, "Produit", 2, 100, 19));
        facture.ValiderMetier(Guid.NewGuid());
        return facture;
    }
}

