using Einvoicing.Domain.Enums;

namespace Einvoicing.Application.DTOs;

public record CreerUtilisateurRequest(
    string Prenom,
    string Nom,
    string Email,
    string MotDePasse,
    string ConfirmationMotDePasse,
    RoleUtilisateur Role
);

public record MettreAJourUtilisateurRequest(
    string Prenom,
    string Nom,
    string? Telephone,
    string? Poste,
    string? Departement
);

public record UtilisateurListeDto(
    Guid Id,
    string Prenom,
    string Nom,
    string Email,
    string Role,
    string Statut,
    bool DeuxFAActif,
    string? Telephone,
    string? Poste,
    string? Departement,
    bool AlerteConnexion,
    DateTime CreeLe,
    DateTime? DerniereConnexion
);

public record ChangerMotDePasseRequest(
    string MotDePasseActuel,
    string NouveauMotDePasse,
    string ConfirmationMotDePasse
);

public record SessionActiveDto(
    Guid Id,
    string Appareil,
    string Ip,
    string UserAgent,
    DateTime CreeLe,
    DateTime DerniereActivite,
    bool EstCourante
);
