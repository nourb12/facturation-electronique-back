using Einvoicing.Application.DTOs;
using Einvoicing.Application.Helpers;
using FluentValidation;

namespace Einvoicing.Application.Validators;

public class CreerEntrepriseValidator : AbstractValidator<CreerEntrepriseRequest>
{
    public CreerEntrepriseValidator()
    {
        RuleFor(x => x.Nom).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MatriculeFiscal)
            .NotEmpty()
            .Must(MatriculeFiscalHelper.IsValid)
            .WithMessage("Le matricule fiscal doit respecter le format 1495908/S ou 1234567A/B/M/000.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Adresse).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Ville).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CodePostal).NotEmpty().MaximumLength(10);
        RuleFor(x => x.DevisePrincipale)
            .Length(3)
            .When(x => !string.IsNullOrWhiteSpace(x.DevisePrincipale));
        RuleFor(x => x.CodeTva).NotEmpty().MaximumLength(50);
    }
}

public class MettreAJourEntrepriseValidator : AbstractValidator<MettreAJourEntrepriseRequest>
{
    public MettreAJourEntrepriseValidator()
    {
        RuleFor(x => x.Nom).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Adresse).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Ville).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CodePostal).NotEmpty().MaximumLength(10);
        RuleFor(x => x.DevisePrincipale)
            .Length(3)
            .When(x => !string.IsNullOrWhiteSpace(x.DevisePrincipale));
    }
}