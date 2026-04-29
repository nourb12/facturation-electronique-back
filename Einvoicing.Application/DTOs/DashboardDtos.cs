namespace Einvoicing.Application.DTOs;

public record DashboardDto(
    KpisFacturesDto KpisFactures,
    KpisFinanciersDto KpisFinanciers,
    KpisConformiteDto KpisConformite,
    List<FactureParStatutDto> FacturesParStatut,
    List<VolumesMensuelDto> VolumesMensuels,
    List<VolumesMensuelDto> EvolutionMensuelle,
    List<FactureListeDto> DernieresFactures,
    List<AlerteDto> Alertes
);

public record KpisFacturesDto(
    int TotalBrouillons,
    int TotalEnValidation,
    int TotalTransmises,
    int TotalAcceptees,
    int TotalRejetees,
    int TotalEnRetard,
    int TotalMois
);

public record KpisFinanciersDto(
    decimal MontantTotalMois,
    decimal MontantEncaisseMois,
    decimal MontantEnAttente,
    decimal MontantEnRetard,
    double DsoJours,
    decimal TauxEncaissement
);

public record KpisConformiteDto(
    int TotalValidees,
    int TotalConformes,
    int TotalNonConformes,
    double TauxConformite,
    List<string> ReglesLesPlusViolees
);

public record FactureParStatutDto(string Statut, int Nombre, decimal Montant);

public record VolumesMensuelDto(string Mois, int NbFactures, decimal MontantHt, decimal MontantTtc);

public record AlerteDto(
    string Type,
    string Message,
    string Severite,
    Guid? FactureId,
    DateTime CreeLe
);

public record DashboardAdminDto(
    int TotalEntreprises,
    int EntreprisesActives,
    int TotalUtilisateurs,
    int TotalFacturesPlateforme,
    decimal VolumeTotal,
    List<EntrepriseActiviteDto> TopEntreprises
);

public record EntrepriseActiviteDto(
    Guid Id,
    string Nom,
    int NbFactures,
    decimal Volume,
    double TauxConformite
);

