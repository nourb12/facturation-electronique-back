using Einvoicing.Application.DTOs;
using Einvoicing.Domain.Enums;
using FluentValidation;

namespace Einvoicing.Application.Validators;

public class CreerParametreFiscalValidator : AbstractValidator<CreerParametreFiscalRequest>
{
    public CreerParametreFiscalValidator()
    {
        RuleFor(x => x.Libelle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Valeur).GreaterThan(0);
        RuleFor(x => x.Type).NotEmpty().Must(BeType).WithMessage("Type invalide.");
        RuleFor(x => x.Signe).NotEmpty().Must(BeSigne).WithMessage("Signe invalide.");
        RuleFor(x => x.OrdreCalcul).NotEmpty().Must(BeOrdre).WithMessage("Ordre de calcul invalide.");
        RuleFor(x => x.Utilisation).NotEmpty().Must(BeUtilisation).WithMessage("Utilisation invalide.");
        RuleForEach(x => x.DocumentsCibles)
            .NotEmpty()
            .MaximumLength(100)
            .When(x => x.DocumentsCibles != null);
    }

    private static bool BeType(string value) => Enum.TryParse<TypeParametreFiscal>(value, true, out _);
    private static bool BeSigne(string value) => Enum.TryParse<SigneParametreFiscal>(value, true, out _);
    private static bool BeOrdre(string value) => Enum.TryParse<OrdreCalcul>(value, true, out _);
    private static bool BeUtilisation(string value) => Enum.TryParse<UtilisationParametreFiscal>(value, true, out _);
}

public class MettreAJourParametreFiscalValidator : AbstractValidator<MettreAJourParametreFiscalRequest>
{
    public MettreAJourParametreFiscalValidator()
    {
        RuleFor(x => x.Libelle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Valeur).GreaterThan(0);
        RuleFor(x => x.Type).NotEmpty().Must(BeType).WithMessage("Type invalide.");
        RuleFor(x => x.Signe).NotEmpty().Must(BeSigne).WithMessage("Signe invalide.");
        RuleFor(x => x.OrdreCalcul).NotEmpty().Must(BeOrdre).WithMessage("Ordre de calcul invalide.");
        RuleFor(x => x.Utilisation).NotEmpty().Must(BeUtilisation).WithMessage("Utilisation invalide.");
        RuleForEach(x => x.DocumentsCibles)
            .NotEmpty()
            .MaximumLength(100)
            .When(x => x.DocumentsCibles != null);
    }

    private static bool BeType(string value) => Enum.TryParse<TypeParametreFiscal>(value, true, out _);
    private static bool BeSigne(string value) => Enum.TryParse<SigneParametreFiscal>(value, true, out _);
    private static bool BeOrdre(string value) => Enum.TryParse<OrdreCalcul>(value, true, out _);
    private static bool BeUtilisation(string value) => Enum.TryParse<UtilisationParametreFiscal>(value, true, out _);
}