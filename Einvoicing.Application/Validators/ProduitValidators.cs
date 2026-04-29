using Einvoicing.Application.DTOs;
using FluentValidation;

namespace Einvoicing.Application.Validators;

public class CreerProduitValidator : AbstractValidator<CreerProduitRequest>
{
    public CreerProduitValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Libelle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PrixUnitaire).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TauxTva)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(100)
            .Must(t => t == 0 || t == 7 || t == 13 || t == 19)
            .WithMessage("Le taux de TVA doit être 0%, 7%, 13% ou 19% (taux tunisiens).");
        RuleFor(x => x.Unite).NotEmpty().MaximumLength(20);
    }
}

public class MettreAJourProduitValidator : AbstractValidator<MettreAJourProduitRequest>
{
    public MettreAJourProduitValidator()
    {
        RuleFor(x => x.Libelle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PrixUnitaire).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TauxTva)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(100);
        RuleFor(x => x.Unite).NotEmpty().MaximumLength(20);
    }
}
