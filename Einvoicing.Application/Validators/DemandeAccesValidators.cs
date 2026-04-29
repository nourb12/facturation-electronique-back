using Einvoicing.Application.DTOs;
using Einvoicing.Application.Helpers;
using Einvoicing.Domain.Errors;
using FluentValidation;

namespace Einvoicing.Application.Validators;

public sealed class SoumettreDemandeAccesValidator : AbstractValidator<SoumettreDemandeAccesRequest>
{
    public SoumettreDemandeAccesValidator()
    {
        RuleFor(x => x.RaisonSociale)
            .NotEmpty().WithMessage(ErrorCodes.CompanyNameRequired);
        RuleFor(x => x.MatriculeFiscal)
            .NotEmpty().WithMessage(ErrorCodes.MatriculeRequired)
            .Must(MatriculeFiscalHelper.IsValid)
            .WithMessage(ErrorCodes.MatriculeInvalid);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(ErrorCodes.EmailRequired)
            .EmailAddress().WithMessage(ErrorCodes.EmailInvalid);

        RuleFor(x => x.Telephone)
            .NotEmpty().WithMessage(ErrorCodes.PhoneRequired)
            .MinimumLength(8).WithMessage(ErrorCodes.PhoneRequired);

        RuleFor(x => x.FormeJuridique)
            .NotEmpty().WithMessage(ErrorCodes.LegalFormRequired);

        RuleFor(x => x.NomEntreprise)
            .NotEmpty().WithMessage(ErrorCodes.CompanyNameRequired)
            .MinimumLength(3).WithMessage(ErrorCodes.CompanyNameRequired);

        RuleFor(x => x.Adresse)
            .NotEmpty().WithMessage(ErrorCodes.AddressRequired)
            .MinimumLength(6).WithMessage(ErrorCodes.AddressRequired);

        RuleFor(x => x.Gouvernorat)
            .NotEmpty().WithMessage(ErrorCodes.GovernorateRequired);

        RuleFor(x => x.CodePostal)
            .NotEmpty().WithMessage(ErrorCodes.PostalRequired)
            .Matches(@"^\d{4}$").WithMessage(ErrorCodes.PostalInvalid);

        RuleFor(x => x.DevisePrincipale)
            .NotEmpty().WithMessage(ErrorCodes.CurrencyRequired)
            .Length(3).WithMessage(ErrorCodes.CurrencyInvalid);

        RuleFor(x => x.TelEntreprise)
            .NotEmpty().WithMessage(ErrorCodes.PhoneRequired)
            .MinimumLength(8).WithMessage(ErrorCodes.PhoneRequired);

        RuleFor(x => x.RespPrenom)
            .NotEmpty().WithMessage(ErrorCodes.FirstNameRequired)
            .MinimumLength(2).WithMessage(ErrorCodes.FirstNameRequired);

        RuleFor(x => x.RespNom)
            .NotEmpty().WithMessage(ErrorCodes.LastNameRequired)
            .MinimumLength(2).WithMessage(ErrorCodes.LastNameRequired);

        RuleFor(x => x.RespFonction)
            .NotEmpty().WithMessage(ErrorCodes.FunctionRequired);
        RuleFor(x => x.RespFonctionAutre)
            .NotEmpty().WithMessage(ErrorCodes.FunctionRequired)
            .When(x => x.RespFonction == "Autre");
        RuleFor(x => x.RespEmail)
            .NotEmpty().WithMessage(ErrorCodes.EmailRequired)
            .EmailAddress().WithMessage(ErrorCodes.EmailInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.RespEmail));

        RuleFor(x => x.RespTel)
            .NotEmpty().WithMessage(ErrorCodes.PhoneRequired)
            .MinimumLength(8).WithMessage(ErrorCodes.PhoneRequired);

        RuleFor(x => x.RegistreCommerce).NotNull().WithMessage(ErrorCodes.RegistreCommerceRequired);
        RuleFor(x => x.Patente).NotNull().WithMessage(ErrorCodes.PatenteRequired);
        RuleFor(x => x.CinResponsable).NotNull().WithMessage(ErrorCodes.CinResponsableRequired);
        RuleFor(x => x.Rib).NotNull().WithMessage(ErrorCodes.RibRequired);
    }
}
