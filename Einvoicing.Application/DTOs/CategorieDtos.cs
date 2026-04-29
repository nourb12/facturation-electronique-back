namespace Einvoicing.Application.DTOs;

public record CreerCategorieRequest(
    string Nom,
    string? Description
);

public record MettreAJourCategorieRequest(
    string Nom,
    string? Description
);

public record CategorieDto(
    Guid Id,
    Guid EntrepriseId,
    string Nom,
    string? Description,
    bool EstActive,
    int NbProduits,
    DateTime CreeLe
);
