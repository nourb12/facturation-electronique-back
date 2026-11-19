using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using FluentAssertions;
using Moq;
using Xunit;

namespace Einvoicing.Tests.TEIF;

/// <summary>
/// Valide le XML produit par TeifService contre le schéma officiel TEIF 1.8.8 de Tunisie TradeNet.
/// </summary>
public sealed class TeifXsdValidationTests
{
    private static readonly XmlSchemaSet Schema = ChargerSchema();

    private static XmlSchemaSet ChargerSchema()
    {
        var chemin = Path.Combine(AppContext.BaseDirectory, "TEIF", "Schemas", "teif-1.8.8-withoutSig.xsd");
        var set = new XmlSchemaSet();
        using var reader = XmlReader.Create(chemin, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        set.Add(null, reader);
        set.Compile();
        return set;
    }

    private static List<string> Valider(string xml)
    {
        var erreurs = new List<string>();
        XDocument.Parse(xml).Validate(Schema, (_, e) => erreurs.Add($"{e.Severity}: {e.Message}"));
        return erreurs;
    }

    private static async Task<string> GenererAsync(Facture facture, Client client, Entreprise entreprise, Facture? origine = null)
    {
        var factureRepo = new Mock<IFactureRepository>();
        var clientRepo = new Mock<IClientRepository>();
        var entrepriseRepo = new Mock<IEntrepriseRepository>();
        factureRepo.Setup(x => x.ObtenirAvecDetailsAsync(facture.Id, It.IsAny<CancellationToken>())).ReturnsAsync(facture);
        if (origine is not null)
            factureRepo.Setup(x => x.ObtenirParIdAsync(origine.Id, It.IsAny<CancellationToken>())).ReturnsAsync(origine);
        clientRepo.Setup(x => x.ObtenirParIdAsync(facture.ClientId, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        entrepriseRepo.Setup(x => x.ObtenirParIdAsync(facture.EntrepriseId, It.IsAny<CancellationToken>())).ReturnsAsync(entreprise);
        var service = new TeifService(factureRepo.Object, clientRepo.Object, entrepriseRepo.Object);
        return (await service.GenererXmlAsync(facture.Id, facture.EntrepriseId, CancellationToken.None)).XmlContent;
    }

    private static Entreprise Entreprise()
        => Einvoicing.Domain.Entities.Entreprise.Creer("ACME & Fils", "1234567ABM000", "12 rue de l'Industrie", "Tunis", "1000", "acme@test.tn", "TVA001", RegimeFiscal.Reel);

    [Fact]
    public void LeSchemaOfficiel_EstCharge()
        => Schema.GlobalElements.Names.OfType<XmlQualifiedName>().Select(n => n.Name).Should().Contain("TEIF");

    [Fact]
    public async Task FactureComplete_RespecteLeSchemaTeif()
    {
        var entrepriseId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var facture = Facture.Creer(entrepriseId, clientId, Guid.NewGuid(), "FAC-202611-0001", TypeFacture.Facture, ModePaiement.Virement,
            DateTime.UtcNow.AddDays(30), notes: "Merci pour votre confiance", conditionsPaiement: "Virement à 30 jours",
            appliquerRS: true, codeRS: "RS-1.5", tauxRS: 1.5m, remiseGlobale: 150m);
        facture.AjouterLigne(LigneFacture.Creer(facture.Id, 1, "Prestation de conseil", 1, 1000m, 19m, unite: "Jour"));
        facture.AjouterLigne(LigneFacture.Creer(facture.Id, 2, "Livre technique", 2, 250m, 7m, tauxRemise: 10m));
        facture.AjouterLigne(LigneFacture.Creer(facture.Id, 3, "Formation exonérée", 1, 300m, 0m, produitId: Guid.NewGuid()));
        facture.ValiderMetier(Guid.NewGuid());
        var client = Client.Creer(entrepriseId, "Société Atlas <SARL>", "atlas@client.tn", TypeClient.B2B, "7654321BAM000",
            "Avenue Habib Bourguiba", "Sfax", "3000");

        var xml = await GenererAsync(facture, client, Entreprise());

        Valider(xml).Should().BeEmpty(xml);
    }

    [Fact]
    public async Task AvoirParticulier_RespecteLeSchemaTeif()
    {
        var entrepriseId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var origine = Facture.Creer(entrepriseId, clientId, Guid.NewGuid(), "FAC-202611-0002", TypeFacture.Facture, ModePaiement.Especes, DateTime.UtcNow.AddDays(10));
        origine.AjouterLigne(LigneFacture.Creer(origine.Id, 1, "Article", 1, 100m, 19m));
        var avoir = Facture.Creer(entrepriseId, clientId, Guid.NewGuid(), "AV-202611-0001", TypeFacture.Avoir, ModePaiement.Cheque,
            DateTime.UtcNow.AddDays(10), factureOrigineId: origine.Id);
        avoir.AjouterLigne(LigneFacture.Creer(avoir.Id, 1, "Retour article", 1, 100m, 19m));
        avoir.ValiderMetier(Guid.NewGuid());
        var client = Client.Creer(entrepriseId, "Nour Particulier", "nour@client.tn", TypeClient.B2C, "01234567");

        var xml = await GenererAsync(avoir, client, Entreprise(), origine);

        Valider(xml).Should().BeEmpty(xml);
    }

    [Fact]
    public void UnXmlNonConforme_EstBienRejeteParLeSchema()
        => Valider("<TEIF version=\"2.0\" controlingAgency=\"TTN\"><InvoiceHeader/></TEIF>").Should().NotBeEmpty();
}
