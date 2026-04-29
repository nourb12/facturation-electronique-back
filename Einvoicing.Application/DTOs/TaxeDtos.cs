namespace Einvoicing.Application.DTOs;

public record CreerTaxeRequest(
    string Titre,
    decimal Taux,
    string Type,
    string? Description
);

public record MettreAJourTaxeRequest(
    string Titre,
    decimal Taux,
    string Type,
    string? Description
);

public record TaxeDto(
    Guid Id,
    Guid EntrepriseId,
    string Titre,
    decimal Taux,
    string Type,
    string? Description,
    DateTime CreeLe,
    DateTime ModifieLe
);