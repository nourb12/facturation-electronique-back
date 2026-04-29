namespace Einvoicing.Application.DTOs;

public record TvaParTauxDto(
    decimal TauxTva,
    decimal BaseHt,
    decimal MontantTva,
    decimal MontantTtc
);

public record DelaiPaiementClientDto(
    Guid ClientId,
    string ClientNom,
    int NbFactures,
    double DelaiMoyenJours
);

public record RecapMensuelDto(
    string Mois,
    int NbFactures,
    decimal TotalHt,
    decimal TotalTva,
    decimal TotalTtc,
    decimal MontantPaye,
    decimal MontantImpaye,
    double TauxRecouvrement
);
