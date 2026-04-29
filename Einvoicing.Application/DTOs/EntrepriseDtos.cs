using Einvoicing.Domain.Enums;

namespace Einvoicing.Application.DTOs;

public record CreerEntrepriseRequest(
    string Nom,
    string MatriculeFiscal,
    string Adresse,
    string Ville,
    string CodePostal,
    string Email,
    string CodeTva,
    RegimeFiscal RegimeFiscal,
    string? Telephone,
    string? SiteWeb,
    string? DevisePrincipale
);

public record MettreAJourEntrepriseRequest(
    string? Nom = null,
    string? Adresse = null,
    string? Ville = null,
    string? CodePostal = null,
    string? Email = null,
    RegimeFiscal? RegimeFiscal = null,
    string? Telephone = null,
    string? SiteWeb = null,
    string? DevisePrincipale = null,
    string? RaisonSociale = null,
    string? NomCommercial = null,
    string? Forme = null,
    string? Capital = null,
    string? DateCreation = null,
    string? ActiviteCode = null,
    string? ActiviteLibelle = null,
    string? Gouvernorat = null,
    string? Pays = null,
    string? Fax = null,
    string? Tel = null,
    string? NumRne = null,
    string? RegimeTva = null,
    decimal? TauxTvaPrincipal = null,
    bool? TeifSignature = null,
    bool? TeifArchivage = null,
    bool? TeifHorodatage = null,
    bool? TeifSandbox = null,
    bool? TeifSurveille = null
);

public record ConfigurerTeifRequest(
    string ParametresTeif,
    string VersionTeif
);

public record EntrepriseDto(
    Guid Id,
    string Nom,
    string MatriculeFiscal,
    string Adresse,
    string Ville,
    string CodePostal,
    string Pays,
    string Email,
    string? Telephone,
    string? SiteWeb,
    string DevisePrincipale,
    string? LogoUrl,
    string RegimeFiscal,
    string CodeTva,
    string VersionTeif,
    bool EstActive,
    DateTime CreeLe,
    DateTime ModifieLe,
    string RaisonSociale,
    string NomCommercial,
    string Forme,
    string Capital,
    string DateCreation,
    string ActiviteCode,
    string ActiviteLibelle,
    string Gouvernorat,
    string Fax,
    string Tel,
    string NumRne,
    string RegimeTva,
    decimal TauxTvaPrincipal,
    bool TeifSignature,
    bool TeifArchivage,
    bool TeifHorodatage,
    bool TeifSandbox,
    bool TeifSurveille
);
