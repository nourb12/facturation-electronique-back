using Einvoicing.Application.DTOs;
using Einvoicing.Domain.Enums;
using FluentValidation;

namespace Einvoicing.Application.Validators;

public class CreerTaxeValidator : AbstractValidator<CreerTaxeRequest>
{
    public CreerTaxeValidator()
    {
        RuleFor(x => x.Titre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Taux).GreaterThan(0);
        RuleFor(x => x.Type).NotEmpty().Must(BeType).WithMessage("Type invalide.");
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description != null);
    }

    private static bool BeType(string value) => Enum.TryParse<TypeTaxe>(value, true, out _);
}

public class MettreAJourTaxeValidator : AbstractValidator<MettreAJourTaxeRequest>
{
    public MettreAJourTaxeValidator()
    {
        RuleFor(x => x.Titre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Taux).GreaterThan(0);
        RuleFor(x => x.Type).NotEmpty().Must(BeType).WithMessage("Type invalide.");
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description != null);
    }

    private static bool BeType(string value) => Enum.TryParse<TypeTaxe>(value, true, out _);
}