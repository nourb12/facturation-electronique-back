using Einvoicing.Domain.Enums;

namespace Einvoicing.Application.DTOs;





public record CreerFactureRequest(
    Guid ClientId,
    TypeFacture TypeFacture,
    ModePaiement ModePaiement,
    DateTime DateEcheance,
    List<CreerLigneFactureRequest> Lignes,
    string? Reference,
    string? Notes,
    string? ConditionsPaiement,
    string Devise = "TND",
    Guid? FactureOrigineId = null
);

public record CreerLigneFactureRequest(
    string Designation,
    decimal Quantite,
    decimal PrixUnitaire,
    decimal TauxTva,
    Guid? ProduitId,
    string? Description,
    string Unite = "U",
    decimal TauxRemise = 0
);

public record MettreAJourFactureRequest(
    ModePaiement ModePaiement,
    DateTime DateEcheance,
    string? Reference,
    string? Notes,
    string? ConditionsPaiement,
    List<CreerLigneFactureRequest> Lignes
);

public record ValiderFactureRequest(string? Commentaire);

public record RejeterFactureRequest(string Motif);

public record AnnulerFactureRequest(string Motif);

public record FiltreFacturesRequest(
    int Page = 1,
    int ParPage = 20,
    Guid? ClientId = null,
    StatutFacture? Statut = null,
    DateTime? DateDebut = null,
    DateTime? DateFin = null,
    decimal? MontantMin = null,
    decimal? MontantMax = null,
    string? Recherche = null,
    TypeFacture? TypeFacture = null
);





public record FactureDto(
    Guid Id,
    Guid EntrepriseId,
    Guid ClientId,
    Guid? FactureOrigineId,
    string ClientNom,
    string? ClientMatriculeFiscal,
    string Numero,
    string? Reference,
    string Statut,
    string TypeFacture,
    string ModePaiement,
    string Devise,
    DateTime DateEmission,
    DateTime DateEcheance,
    DateTime? DatePaiement,
    decimal TotalHt,
    decimal TotalTva,
    decimal TotalTtc,
    decimal MontantPaye,
    decimal MontantRestant,
    bool EstEnRetard,
    string? Notes,
    string? ConditionsPaiement,
    bool XmlGenere,
    string? VersionTeif,
    List<LigneFactureDto> Lignes,
    List<HistoriqueFactureDto> Historique,
    DateTime CreeLe,
    DateTime ModifieLe
);

public record FactureListeDto(
    Guid Id,
    string Numero,
    string ClientNom,
    string Statut,
    string TypeFacture,
    DateTime DateEmission,
    DateTime DateEcheance,
    decimal TotalTtc,
    decimal MontantPaye,
    bool EstEnRetard,
    string Devise
);

public record LigneFactureDto(
    Guid Id,
    int Ordre,
    string Designation,
    string? Description,
    string Unite,
    decimal Quantite,
    decimal PrixUnitaire,
    decimal TauxRemise,
    decimal TauxTva,
    decimal MontantHt,
    decimal MontantRemise,
    decimal MontantTva,
    decimal MontantTtc,
    Guid? ProduitId
);

public record HistoriqueFactureDto(
    Guid Id,
    string Action,
    string Details,
    string? AncienneValeur,
    string? NouvelleValeur,
    DateTime CreeLe
);

public record ListeFacturesDto(
    List<FactureListeDto> Items,
    int Total,
    int Page,
    int ParPage
);

public record StatistiquesFacturesDto(
    int TotalBrouillons,
    int TotalValidees,
    int TotalTransmises,
    int TotalAcceptees,
    int TotalRejetees,
    int TotalPayees,
    int TotalEnRetard,
    decimal MontantTotalMois,
    decimal MontantEncaisseMois,
    decimal MontantEnAttente
);





public record XmlTeifDto(
    Guid FactureId,
    string Numero,
    string XmlContent,
    string HashIntegrite,
    string VersionTeif,
    DateTime GenereA
);

public record ValidationTeifDto(
    Guid FactureId,
    bool EstConforme,
    List<ErreurTeifDto> Erreurs,
    DateTime ValideeA
);

public record ErreurTeifDto(
    string Code,
    string Message,
    string? Champ,
    string Severite
);
