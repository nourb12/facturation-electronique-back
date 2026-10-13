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
}
