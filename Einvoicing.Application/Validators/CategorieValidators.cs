using Einvoicing.Application.DTOs;
using FluentValidation;

namespace Einvoicing.Application.Validators;

public class CreerCategorieValidator : AbstractValidator<CreerCategorieRequest>
{
    public CreerCategorieValidator()
    {
        RuleFor(x => x.Nom).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
