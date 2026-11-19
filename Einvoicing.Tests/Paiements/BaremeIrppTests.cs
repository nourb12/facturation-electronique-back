using Einvoicing.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Einvoicing.Tests.Paiements;

public sealed class BaremeIrppTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5000, 0)]
    [InlineData(10000, 750)]          // 5 000 x 15 %
    [InlineData(20000, 3250)]         // 750 + 10 000 x 25 %
    [InlineData(80000, 24750)]        // 750+2500+3000+3300+3600+7600 + 10 000 x 40 %
    public void ImpotAnnuel_AppliqueLeBaremeProgressif(decimal revenu, decimal attendu)
        => BaremeIrpp.ImpotAnnuel(revenu).Should().Be(attendu);

    [Fact]
    public void RetenueMensuelle_AnnualiseEtAppliqueLAbattement()
    {
        // Brut 1 500 : CNSS 137,700 ; net social annuel 16 347,600 ;
        // abattement 10 % = 1 634,760 ; imposable 14 712,840 ;
        // impôt = 750 + 4 712,840 x 25 % = 1 928,210 ; mensuel = 160,684
        BaremeIrpp.RetenueMensuelle(1500m, 137.7m).Should().Be(160.684m);
    }

    [Fact]
    public void RetenueMensuelle_PlafonneLesFraisProfessionnels()
    {
        // Brut 5 000 : net social annuel 54 492 ; abattement plafonné à 2 000 ; imposable 52 492
        var cnss = 459m;
        var attendu = Math.Round(BaremeIrpp.ImpotAnnuel((5000m - cnss) * 12m - 2000m) / 12m, 3, MidpointRounding.AwayFromZero);
        BaremeIrpp.RetenueMensuelle(5000m, cnss).Should().Be(attendu);
    }

    [Fact]
    public void PetitSalaire_NePaiePasDIrpp()
        => BaremeIrpp.RetenueMensuelle(450m, 41.31m).Should().Be(0);

    [Fact]
    public void DeductionsFamiliales_ChefDeFamilleEtEnfantsPlafonnesA4()
    {
        var situation = new SituationFamiliale(true, EnfantsACharge: 5, EnfantsEtudiants: 1, EnfantsHandicapes: 1);
        // 300 + 1 etudiant x 1 000 + 3 autres enfants x 100 (plafond 4) + 1 handicape x 2 000
        BaremeIrpp.DeductionsFamiliales(situation).Should().Be(3600m);
    }

    [Fact]
    public void DeductionsFamiliales_ReduisentLIrpp()
    {
        var sans = BaremeIrpp.RetenueMensuelle(1500m, 145.2m);
        var avec = BaremeIrpp.RetenueMensuelle(1500m, 145.2m, new SituationFamiliale(true, 2, 0, 0));
        // 500 TND de deductions a 25 % marginal = 125 / an, soit 10,417 par mois
        (sans - avec).Should().BeApproximately(10.417m, 0.001m);
    }

    [Fact]
    public void Css_EstCalculeeSurLeRevenuImposable_AvecExonerationSous5000()
    {
        BaremeIrpp.CssMensuelle(450m, 43.56m).Should().Be(0);
        var imposable = BaremeIrpp.RevenuImposableAnnuel(1500m, 145.2m);
        BaremeIrpp.CssMensuelle(1500m, 145.2m).Should().Be(Math.Round(imposable * 0.005m / 12m, 3, MidpointRounding.AwayFromZero));
        BaremeIrpp.CssMensuelle(1500m, 145.2m, tauxCss: 0).Should().Be(0);
    }

    [Fact]
    public void Bulletin_RetientCnssIrppEtCss()
    {
        var employe = EmployePaie.Creer(Guid.NewGuid(), "EMP-1", "Test", "Dev", "IT", 1500m, 0, 0, null, null, null,
            new SituationFamiliale(true, 1, 0, 0));
        var bulletin = BulletinPaie.Generer(employe, "2026-11");
        bulletin.CnssSalarie.Should().Be(145.2m);
        bulletin.Css.Should().BeGreaterThan(0);
        bulletin.NetAPayer.Should().Be(bulletin.Brut - bulletin.CnssSalarie - bulletin.Irpp - bulletin.Css);
    }
}
