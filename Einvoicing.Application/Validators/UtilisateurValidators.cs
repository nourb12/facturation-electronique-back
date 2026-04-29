using Einvoicing.Application.DTOs;
using FluentValidation;

namespace Einvoicing.Application.Validators;

public class CreerUtilisateurValidator : AbstractValidator<CreerUtilisateurRequest>
{
    public CreerUtilisateurValidator()
    {
        RuleFor(x => x.Prenom).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Nom).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.MotDePasse)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Au moins une majuscule.")
            .Matches("[0-9]").WithMessage("Au moins un chiffre.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Au moins un caractère spécial.");
        RuleFor(x => x.ConfirmationMotDePasse)
            .Equal(x => x.MotDePasse).WithMessage("Les mots de passe ne correspondent pas.");
    }
}
