




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Helpers;
using Einvoicing.Domain.Errors;
using FluentValidation;


using RegisterRequest = Einvoicing.Application.DTOs.RegisterRequest;
using LoginRequest = Einvoicing.Application.DTOs.LoginRequest;

namespace Einvoicing.Application.Services;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Prenom)
            .NotEmpty().WithMessage(ErrorCodes.FirstNameRequired)
            .MaximumLength(50);

        RuleFor(x => x.Nom)
            .NotEmpty().WithMessage(ErrorCodes.LastNameRequired)
            .MaximumLength(50);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(ErrorCodes.EmailRequired)
            .EmailAddress().WithMessage(ErrorCodes.EmailInvalid);

        RuleFor(x => x.MotDePasse)
            .NotEmpty().WithMessage(ErrorCodes.PasswordRequired)
            .MinimumLength(8).WithMessage(ErrorCodes.PasswordMin8)
            .Matches("[A-Z]").WithMessage(ErrorCodes.PasswordNeedsUpper)
            .Matches("[0-9]").WithMessage(ErrorCodes.PasswordNeedsDigit)
            .Matches("[^a-zA-Z0-9]").WithMessage(ErrorCodes.PasswordNeedsSpecial);

        RuleFor(x => x.ConfirmationMotDePasse)
            .Equal(x => x.MotDePasse).WithMessage(ErrorCodes.PasswordsMismatch);

        RuleFor(x => x.NomEntreprise)
            .NotEmpty().WithMessage(ErrorCodes.CompanyNameRequired)
            .MaximumLength(100);

        RuleFor(x => x.MatriculeFiscal)
            .NotEmpty().WithMessage(ErrorCodes.MatriculeRequired)
            .Must(MatriculeFiscalHelper.IsValid)
            .WithMessage(ErrorCodes.MatriculeInvalid);

        RuleFor(x => x.Adresse)
            .NotEmpty().WithMessage(ErrorCodes.AddressRequired)
            .MaximumLength(300);

        RuleFor(x => x.Ville)
            .NotEmpty().WithMessage(ErrorCodes.GovernorateRequired)
            .MaximumLength(100);

        RuleFor(x => x.CodePostal)
            .NotEmpty().WithMessage(ErrorCodes.PostalRequired)
            .Matches(@"^\d{4}$").WithMessage(ErrorCodes.PostalInvalid);

        RuleFor(x => x.Telephone)
            .NotEmpty().WithMessage(ErrorCodes.PhoneRequired)
            .MaximumLength(20);

        RuleFor(x => x.SiteWeb)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.SiteWeb));

        RuleFor(x => x.DevisePrincipale)
            .NotEmpty().WithMessage(ErrorCodes.CurrencyRequired)
            .Length(3).WithMessage(ErrorCodes.CurrencyInvalid);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(ErrorCodes.EmailRequired)
            .EmailAddress().WithMessage(ErrorCodes.EmailInvalid);

        RuleFor(x => x.MotDePasse)
            .NotEmpty().WithMessage(ErrorCodes.PasswordRequired)
            .MinimumLength(6).WithMessage(ErrorCodes.PasswordMin6);
    }
}

public class ReinitialiserMotDePasseRequestValidator
    : AbstractValidator<ReinitialiserMotDePasseRequest>
{
    public ReinitialiserMotDePasseRequestValidator()
    {
        RuleFor(x => x.Courriel)
            .NotEmpty().WithMessage(ErrorCodes.EmailRequired)
            .EmailAddress().WithMessage(ErrorCodes.EmailInvalid);

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage(ErrorCodes.InvalidOtp)
            .Length(6).WithMessage(ErrorCodes.InvalidOtp);

        RuleFor(x => x.NouveauMotDePasse)
            .NotEmpty().WithMessage(ErrorCodes.PasswordRequired)
            .MinimumLength(8).WithMessage(ErrorCodes.PasswordMin8)
            .Matches("[A-Z]").WithMessage(ErrorCodes.PasswordNeedsUpper)
            .Matches("[0-9]").WithMessage(ErrorCodes.PasswordNeedsDigit);

        RuleFor(x => x.ConfirmationMotDePasse)
            .Equal(x => x.NouveauMotDePasse)
            .WithMessage(ErrorCodes.PasswordsMismatch);
    }
}




