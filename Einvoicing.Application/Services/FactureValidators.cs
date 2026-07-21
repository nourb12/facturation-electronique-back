using Einvoicing.Application.DTOs;
using FluentValidation;

namespace Einvoicing.Application.Services;

public class CreerFactureValidator : AbstractValidator<CreerFactureRequest>
{
    public CreerFactureValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.DateEcheance)
            .GreaterThanOrEqualTo(DateTime.UtcNow.Date)
            .WithMessage("La date d'échéance ne peut pas être dans le passé.");
        RuleFor(x => x.Devise).NotEmpty().MaximumLength(3);
        RuleFor(x => x.Lignes)
            .NotEmpty().WithMessage("La facture doit contenir au moins une ligne.");
        RuleFor(x => x.TauxRS).InclusiveBetween(0, 100);
        RuleForEach(x => x.Lignes).SetValidator(new CreerLigneFactureValidator());
    }
}

public class CreerLigneFactureValidator : AbstractValidator<CreerLigneFactureRequest>
{
    public CreerLigneFactureValidator()
    {
        RuleFor(x => x.Designation).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantite).GreaterThan(0);
        RuleFor(x => x.PrixUnitaire).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TauxTva)
            .GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
        RuleFor(x => x.TauxRemise)
            .GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
        RuleFor(x => x.Unite).NotEmpty().MaximumLength(20);
    }
}

public class MettreAJourFactureValidator : AbstractValidator<MettreAJourFactureRequest>
{
    public MettreAJourFactureValidator()
    {
        RuleFor(x => x.DateEcheance).NotEmpty();
        RuleFor(x => x.Lignes).NotEmpty();
        RuleFor(x => x.TauxRS).InclusiveBetween(0, 100);
        RuleForEach(x => x.Lignes).SetValidator(new CreerLigneFactureValidator());
    }
}
