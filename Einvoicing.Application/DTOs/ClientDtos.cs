using Einvoicing.Domain.Enums;

namespace Einvoicing.Application.DTOs;

public record CreerClientRequest(
    string Nom,
    string Email,
    TypeClient TypeClient,
    string? MatriculeFiscal,
    string? Adresse,
    string? Ville,
    string? CodePostal,
    string? Telephone,
    string Pays = "TN"
);

public record MettreAJourClientRequest(
    string Nom,
    string Email,
    TypeClient TypeClient,
    string? MatriculeFiscal,
    string? Adresse,
    string? Ville,
    string? CodePostal,
    string? Telephone
);

public record ClientDto(
    Guid Id,
    Guid EntrepriseId,
    string Nom,
    string Email,
    string TypeClient,
    string? MatriculeFiscal,
    string? Adresse,
    string? Ville,
    string? CodePostal,
    string Pays,
    string? Telephone,
    bool EstActif,
    DateTime CreeLe,
    DateTime ModifieLe
);

public record ListeClientsDto(
    IReadOnlyList<ClientDto> Items,
    int Total,
    int Page,
    int ParPage
);
