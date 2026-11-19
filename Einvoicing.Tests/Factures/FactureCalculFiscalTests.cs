using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Einvoicing.Tests.Factures;

/// <summary>
/// Regles fiscales tunisiennes appliquees au calcul des factures :
/// remise globale au prorata des bases TVA, retenue a la source sur le TTC,
/// droit de timbre de 1,000 TND sur les factures (pas sur les avoirs), pas de surpaiement.
/// </summary>
public sealed class FactureCalculFiscalTests
{
    private static Facture NouvelleFacture(
        TypeFacture type = TypeFacture.Facture,
        decimal remiseGlobale = 0,
        bool appliquerTimbre = true,
        bool appliquerRS = false,
        decimal tauxRS = 0)
        => Facture.Creer(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "FAC-TEST-0001",
            type, ModePaiement.Virement, DateTime.UtcNow.AddDays(30),
            appliquerRS: appliquerRS, codeRS: appliquerRS ? "RS-1.5" : null, tauxRS: tauxRS,
            remiseGlobale: remiseGlobale, appliquerTimbre: appliquerTimbre);

    private static void AjouterLignes(Facture facture)
    {
        // 1 000,000 HT a 19 % + 500,000 HT a 7 %
        facture.AjouterLigne(LigneFacture.Creer(facture.Id, 1, "Service", 1, 1000m, 19m));
        facture.AjouterLigne(LigneFacture.Creer(facture.Id, 2, "Livre", 1, 500m, 7m));
    }

    [Fact]
    public void Facture_AjouteLeTimbreAuNetAPayer_SansLeMettreDansLeTtc()
    {
        var facture = NouvelleFacture();
        AjouterLignes(facture);

        facture.TotalHt.Should().Be(1500m);
        facture.TotalTva.Should().Be(225m);   // 190 + 35
        facture.TotalTtc.Should().Be(1725m);
        facture.TimbreFiscal.Should().Be(1.000m);
        facture.NetAPayer.Should().Be(1726m);
    }

    [Fact]
    public void Avoir_NePortePasDeTimbre()
    {
        var avoir = NouvelleFacture(TypeFacture.Avoir);
        AjouterLignes(avoir);

        avoir.TimbreFiscal.Should().Be(0);
        avoir.NetAPayer.Should().Be(avoir.TotalTtc);
    }

    [Fact]
    public void RemiseGlobale_EstRepartieSurLesBasesTva()
    {
        // Remise de 150 sur 1 500 HT = 10 % sur chaque base
        var facture = NouvelleFacture(remiseGlobale: 150m);
        AjouterLignes(facture);

        facture.TotalHt.Should().Be(1350m);
        facture.TotalTva.Should().Be(202.5m); // 171 + 31,5
        facture.TotalTtc.Should().Be(1552.5m);
        facture.NetAPayer.Should().Be(1553.5m);
    }

    [Fact]
    public void RemiseGlobale_SuperieureAuHt_EstRefusee()
    {
        var facture = NouvelleFacture(remiseGlobale: 5000m);
        AjouterLignes(facture);

        var act = () => facture.ControlerRemiseGlobale();
        act.Should().Throw<ValidationMetierException>();
    }

    [Fact]
    public void RetenueALaSource_EstCalculeeSurLeTtc()
    {
        var facture = NouvelleFacture(appliquerRS: true, tauxRS: 1.5m);
        AjouterLignes(facture);

        facture.BaseRS.Should().Be(1725m);
        facture.MontantRS.Should().Be(25.875m);
        facture.NetAPayer.Should().Be(1725m + 1m - 25.875m);
    }

    [Fact]
    public void Paiement_SuperieurAuReste_EstRefuse()
    {
        var facture = NouvelleFacture();
        AjouterLignes(facture);
        var user = Guid.NewGuid();
        facture.ValiderMetier(user);
        facture.MarquerConforme(user, "1.8.8");
        facture.MarquerTransmise(user);
        facture.MarquerAcceptee(user);

        var act = () => facture.EnregistrerPaiement(facture.NetAPayer + 10m, user);
        act.Should().Throw<ValidationMetierException>();

        facture.EnregistrerPaiement(facture.NetAPayer, user);
        facture.Statut.Should().Be(StatutFacture.Payee);
        facture.MontantRestant.Should().Be(0);
    }
}
