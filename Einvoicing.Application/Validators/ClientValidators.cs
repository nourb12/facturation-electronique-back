using Einvoicing.Application.DTOs;
using FluentValidation;

namespace Einvoicing.Application.Validators;

public class CreerClientValidator : AbstractValidator<CreerClientRequest>
{
    public CreerClientValidator()
    {
        RuleFor(x => x.Nom).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Pays).NotEmpty().Length(2);
    }
}

public class MettreAJourClientValidator : AbstractValidator<MettreAJourClientRequest>
{
    public MettreAJourClientValidator()
    {
        RuleFor(x => x.Nom).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
