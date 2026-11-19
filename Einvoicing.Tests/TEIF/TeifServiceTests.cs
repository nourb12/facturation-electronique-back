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
        xml.HashIntegrite.Should().NotBeNullOrWhiteSpace();
        facture.XmlTeif.Should().NotBeNull();

        // Structure TEIF 1.8.8 (Tunisie TradeNet)
        var doc = System.Xml.Linq.XDocument.Parse(xml.XmlContent);
        var root = doc.Root!;
        root.Name.LocalName.Should().Be("TEIF");
        root.Attribute("version")!.Value.Should().Be("1.8.8");
        root.Attribute("controlingAgency")!.Value.Should().Be("TTN");
        root.Elements().Select(e => e.Name.LocalName).Should().Equal("InvoiceHeader", "InvoiceBody");
        root.Element("InvoiceBody")!.Elements().Select(e => e.Name.LocalName)
            .Should().Equal("Bgm", "Dtm", "PartnerSection", "PytSection", "LinSection", "InvoiceMoa", "InvoiceTax");
        root.Element("InvoiceHeader")!.Element("MessageSenderIdentifier")!.Attribute("type")!.Value.Should().Be("I-01");
        root.Descendants("DocumentType").Single().Attribute("code")!.Value.Should().Be("I-11");

        string Montant(System.Xml.Linq.XElement parent, string code) => parent.Descendants("Moa")
            .Single(m => m.Attribute("amountTypeCode")!.Value == code).Element("Amount")!.Value;
        var invoiceMoa = root.Descendants("InvoiceMoa").Single();
        Montant(invoiceMoa, "I-176").Should().Be("200.000");
        Montant(invoiceMoa, "I-181").Should().Be("38.000");
        Montant(invoiceMoa, "I-180").Should().Be("239.000"); // TTC 238,000 + timbre 1,000

        var taxes = root.Descendants("InvoiceTaxDetails").ToList();
        var tva = taxes.Single(t => t.Descendants("TaxTypeName").Single().Attribute("code")!.Value == "I-1602");
        Montant(tva, "I-177").Should().Be("200.000");
        Montant(tva, "I-178").Should().Be("38.000");
        var timbre = taxes.Single(t => t.Descendants("TaxTypeName").Single().Attribute("code")!.Value == "I-1601");
        Montant(timbre, "I-178").Should().Be("1.000");

        var lin = root.Descendants("Lin").Single();
        lin.Elements().Select(e => e.Name.LocalName).Should().Equal("ItemIdentifier", "LinImd", "LinQty", "LinTax", "LinMoa");
        Montant(lin, "I-171").Should().Be("200.000");
    }

    [Fact]
    public async Task GenererXmlAsync_Avoir_UtiliseLeCodeI12EtReferenceLaFacture()
    {
        var entrepriseId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var origine = BuildFacture(entrepriseId, clientId);
        var avoir = Facture.Creer(entrepriseId, clientId, Guid.NewGuid(), "AV-TEIF", TypeFacture.Avoir, ModePaiement.Especes,
            DateTime.UtcNow.AddDays(5), factureOrigineId: origine.Id, remiseGlobale: 20m);
        avoir.AjouterLigne(LigneFacture.Creer(avoir.Id, 1, "Retour", 1, 100, 19, tauxRemise: 10));
        avoir.ValiderMetier(Guid.NewGuid());
        var client = Client.Creer(entrepriseId, "Client B2C", "client@b2c.tn", TypeClient.B2C, "01234567");
        var entreprise = Entreprise.Creer("ACME", "1234567ABM000", "1 rue", "Tunis", "1000", "acme@test.tn", "TVA001", RegimeFiscal.Reel);
        _factureRepo.Setup(x => x.ObtenirAvecDetailsAsync(avoir.Id, It.IsAny<CancellationToken>())).ReturnsAsync(avoir);
        _factureRepo.Setup(x => x.ObtenirParIdAsync(origine.Id, It.IsAny<CancellationToken>())).ReturnsAsync(origine);
        _clientRepo.Setup(x => x.ObtenirParIdAsync(clientId, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        _entrepriseRepo.Setup(x => x.ObtenirParIdAsync(entrepriseId, It.IsAny<CancellationToken>())).ReturnsAsync(entreprise);

        var xml = await CreateService().GenererXmlAsync(avoir.Id, entrepriseId, CancellationToken.None);

        var root = System.Xml.Linq.XDocument.Parse(xml.XmlContent).Root!;
        root.Descendants("DocumentType").Single().Attribute("code")!.Value.Should().Be("I-12");
        root.Descendants("Reference").Single(r => r.Attribute("refID")!.Value == "I-89").Value.Should().Be(origine.Numero);
        root.Element("InvoiceHeader")!.Element("MessageRecieverIdentifier")!.Attribute("type")!.Value.Should().Be("I-02");
        root.Descendants("PaiMeansCode").Single().Value.Should().Be("I-131");
        root.Descendants("LinAlc").Should().ContainSingle();
        root.Descendants("TaxTypeName").Should().NotContain(t => t.Attribute("code")!.Value == "I-1601"); // pas de timbre sur un avoir
        root.Descendants("FreeTexts").Should().Contain(t => t.Value.Contains("Remise globale"));
    }

    private static Facture BuildFacture(Guid entrepriseId, Guid clientId)
    {
        var facture = Facture.Creer(entrepriseId, clientId, Guid.NewGuid(), "FAC-TEIF", TypeFacture.Facture, ModePaiement.Virement, DateTime.UtcNow.AddDays(5));
        facture.AjouterLigne(LigneFacture.Creer(facture.Id, 1, "Produit", 2, 100, 19));
        facture.ValiderMetier(Guid.NewGuid());
        return facture;
    }
}

