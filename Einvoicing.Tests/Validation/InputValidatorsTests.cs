using Einvoicing.Application.DTOs;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Enums;
using FluentAssertions;

namespace Einvoicing.Tests.Validation;

public sealed class InputValidatorsTests
{
    [Fact]
    public void RegisterValidator_AcceptsCompleteRequest()
    {
        var request = new RegisterRequest(
            "Nour", "Ben Ali", "nour@example.com", "Secure!123", "Secure!123",
            "TuniFlow", "1934567/E", "Rue de Tunis", "Tunis", "1000", "22123456",
            "https://tuniflow.local", "TND");

        var result = new RegisterRequestValidator().Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RegisterValidator_RejectsWeakPasswordAndInvalidCompanyData()
    {
        var request = new RegisterRequest(
            "", "", "not-an-email", "weak", "different", "", "bad", "", "", "12", "", null, "EU");

        var result = new RegisterRequestValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(x => x.PropertyName)
            .Should().Contain(new[] { "Prenom", "Nom", "Email", "MotDePasse", "ConfirmationMotDePasse", "NomEntreprise", "MatriculeFiscal", "CodePostal", "DevisePrincipale" });
    }

    [Fact]
    public void LoginValidator_RejectsInvalidEmailAndShortPassword()
    {
        var result = new LoginRequestValidator().Validate(new LoginRequest("bad", "123"));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(x => x.PropertyName).Should().Contain(new[] { "Email", "MotDePasse" });
    }

    [Fact]
    public void FactureValidator_AcceptsValidInvoiceWithLine()
    {
        var request = new CreerFactureRequest(
            Guid.NewGuid(), TypeFacture.Facture, ModePaiement.Virement,
            DateTime.UtcNow.Date.AddDays(30),
            [new CreerLigneFactureRequest("Abonnement", 2, 150, 19, null, null)],
            "REF-001", null, null, "TND", false, null, 0, null, 0, true);

        var result = new CreerFactureValidator().Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FactureValidator_RejectsPastDueDateAndInvalidLine()
    {
        var request = new CreerFactureRequest(
            Guid.Empty, (TypeFacture)99, (ModePaiement)99, DateTime.UtcNow.Date.AddDays(-1),
            [new CreerLigneFactureRequest("", 0, -1, 101, null, null, "", 101)],
            null, null, null, "", false, null, 101, null, 0, true);

        var result = new CreerFactureValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void PasswordResetValidator_RequiresSixDigitOtpAndMatchingPassword()
    {
        var request = new ReinitialiserMotDePasseRequest(
            "bad", "12", "NoDigit!", "different");

        var result = new ReinitialiserMotDePasseRequestValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(x => x.PropertyName)
            .Should().Contain(new[] { "Courriel", "Otp", "NouveauMotDePasse", "ConfirmationMotDePasse" });
    }
}
