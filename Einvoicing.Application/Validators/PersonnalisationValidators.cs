using Einvoicing.Application.DTOs;
using FluentValidation;

namespace Einvoicing.Application.Validators;

public class EnregistrerPersonnalisationValidator : AbstractValidator<EnregistrerPersonnalisationRequest>
{
    public EnregistrerPersonnalisationValidator()
    {
        RuleFor(x => x.Donnees)
            .NotNull()
            .WithMessage("Les données de personnalisation sont obligatoires.");
    }
}