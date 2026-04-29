using Einvoicing.Domain.Enums;

namespace Einvoicing.Application.DTOs;

public record CreerProduitRequest(
    string Code,
    string Libelle,
    decimal PrixUnitaire,
    decimal TauxTva,
    TypeProduit Type,
    Guid? CategorieId,
    string? Description,
    string Unite = "U"
);

public record MettreAJourProduitRequest(
    string Libelle,
    decimal PrixUnitaire,
    decimal TauxTva,
    TypeProduit Type,
    Guid? CategorieId,
    string? Description,
    string Unite = "U"
);

public record ProduitDto(
    Guid Id,
    Guid EntrepriseId,
    Guid? CategorieId,
    string? CategorieNom,
    string Code,
    string Libelle,
    string? Description,
    decimal PrixUnitaire,
    decimal TauxTva,
    string Unite,
    string Type,
    bool EstActif,
    DateTime CreeLe,
    DateTime ModifieLe
);

public record ListeProduitsDto(
    IReadOnlyList<ProduitDto> Items,
    int Total,
    int Page,
    int ParPage
);
