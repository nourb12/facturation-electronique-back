namespace Einvoicing.Application.DTOs;

public record CreerParametreFiscalRequest(
    string Libelle,
    decimal Valeur,
    string Type,
    string Signe,
    string OrdreCalcul,
    string Utilisation,
    bool InclureRetenueSource,
    List<string> DocumentsCibles
);

public record MettreAJourParametreFiscalRequest(
    string Libelle,
    decimal Valeur,
    string Type,
    string Signe,
    string OrdreCalcul,
    string Utilisation,
    bool InclureRetenueSource,
    List<string> DocumentsCibles
);

public record ParametreFiscalDto(
    Guid Id,
    Guid EntrepriseId,
    string Libelle,
    decimal Valeur,
    string Type,
    string Signe,
    string OrdreCalcul,
    string Utilisation,
    bool InclureRetenueSource,
    List<string> DocumentsCibles,
    bool EstActif,
    DateTime CreeLe,
    DateTime ModifieLe
);