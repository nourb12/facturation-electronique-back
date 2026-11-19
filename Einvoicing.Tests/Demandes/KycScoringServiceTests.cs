using Einvoicing.Application.DTOs;
using Einvoicing.Application.Services;

namespace Einvoicing.Tests.Demandes;

public sealed class KycScoringServiceTests
{
    [Fact]
    public void CriticalOcrMismatch_RequiresManualReview_EvenWithHighScore()
    {
        var request = new SoumettreDemandeAccesRequest
        {
            MatriculeFiscal = "1495908S",
            RaisonSociale = "TuniFlow Services",
            FormeJuridique = "SARL",
            RespPrenom = "Nour",
            RespNom = "Ben Mna",
            Adresse = "12 Rue du Lac 2",
            Gouvernorat = "Tunis",
            CodePostal = "1053"
        };
        var ocr = new OcrExtractionResult
        {
            OcrSuccess = true,
            ConfidenceScore = .98,
            MatriculeFiscalExtrait = "1495908S",
            RaisonSocialeExtraite = "TuniFlow Services",
            FormeJuridiqueExtraite = "SA",
            NomGerantExtrait = "Nour Ben Mna"
        };

        var result = new KycScoringService().Calculate(
            request, "rc.pdf", "cin.pdf", "patente.pdf", "rib.pdf", ocr);

        Assert.Equal(KycDecision.RevisionManuelle, result.Decision);
        Assert.Contains(result.Flags, f => f.Code == "formeJuridique_incoherent" && f.Severity == FlagSeverity.Critical);
    }

    [Theory]
    [InlineData("12345678901234567890")]
    [InlineData("12 345 6789012345678 90")]
    public void ConfiguredThresholds_AreUsedForDecision(string rib)
    {
        var request = new SoumettreDemandeAccesRequest
        {
            MatriculeFiscal = "1495908S",
            RaisonSociale = "TuniFlow Services",
            FormeJuridique = "SARL",
            RespPrenom = "Nour",
            RespNom = "Ben Mna",
            Adresse = "12 Rue du Lac 2",
            Gouvernorat = "Tunis",
            CodePostal = "1053"
        };
        var ocr = new OcrExtractionResult
        {
            OcrSuccess = true,
            ConfidenceScore = .98,
            MatriculeFiscalExtrait = "1495908S",
            RaisonSocialeExtraite = "TuniFlow Services",
            FormeJuridiqueExtraite = "SARL",
            NomGerantExtrait = "Nour Ben Mna",
            RibExtrait = rib
        };

        var result = new KycScoringService().Calculate(
            request, "rc.pdf", "cin.pdf", "patente.pdf", "rib.pdf", ocr,
            autoApprovalThreshold: 101 - 1,
            manualReviewThreshold: 95);

        Assert.Equal(KycDecision.AutoApprovalCandidate, result.Decision);
        Assert.Equal(100, result.Score);
    }
}
