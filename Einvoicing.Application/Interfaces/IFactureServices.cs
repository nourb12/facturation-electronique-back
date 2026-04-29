using Einvoicing.Application.DTOs;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;

namespace Einvoicing.Application.Interfaces;

public interface IFactureService
{
    Task<FactureDto> CreerAsync(Guid entrepriseId, Guid creePar, CreerFactureRequest request, CancellationToken ct = default);
    Task<FactureDto> ObtenirParIdAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
    Task<ListeFacturesDto> ListerAsync(Guid entrepriseId, FiltreFacturesRequest filtre, CancellationToken ct = default);
    Task<FactureDto> MettreAJourAsync(Guid id, Guid entrepriseId, Guid modifiePar, MettreAJourFactureRequest request, CancellationToken ct = default);
    Task<FactureDto> ValiderAsync(Guid id, Guid entrepriseId, Guid validePar, CancellationToken ct = default);
    Task<FactureDto> RejeterAsync(Guid id, Guid entrepriseId, Guid rejetePar, RejeterFactureRequest request, CancellationToken ct = default);
    Task<FactureDto> AnnulerAsync(Guid id, Guid entrepriseId, Guid annulerPar, AnnulerFactureRequest request, CancellationToken ct = default);
    Task<FactureDto> RemettreBrouillonAsync(Guid id, Guid entrepriseId, Guid remisePar, CancellationToken ct = default);
    Task<StatistiquesFacturesDto> ObtenirStatistiquesAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<byte[]> GenererPdfAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
    Task<IReadOnlyList<HistoriqueFactureDto>> ObtenirHistoriqueAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
}

public interface ITeifService
{
    Task<XmlTeifDto> GenererXmlAsync(Guid factureId, Guid entrepriseId, CancellationToken ct = default);
    Task<ValidationTeifDto> ValiderConformiteAsync(Guid factureId, Guid entrepriseId, CancellationToken ct = default);
    Task<FactureDto> MarquerConformeAsync(Guid factureId, Guid entrepriseId, Guid validePar, CancellationToken ct = default);
}

public interface INumeroFactureService
{
    Task<string> GenererNumeroAsync(Guid entrepriseId, CancellationToken ct = default);
}

public interface IFactureRepository
{
    Task<Facture?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<Facture?> ObtenirAvecDetailsAsync(Guid id, CancellationToken ct = default);
    Task<(List<Facture> Items, int Total)> ListerAsync(Guid entrepriseId, FiltreFacturesRequest filtre, CancellationToken ct = default);
    Task<List<Facture>> ListerEnRetardAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<bool> RelanceDejaEnvoyeeAsync(Guid factureId, string action, CancellationToken ct = default);
    Task EnregistrerRelanceAsync(Guid factureId, Guid effectuePar, string action, string details, CancellationToken ct = default);
    Task<StatistiquesFacturesDto> ObtenirStatistiquesAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<List<VolumesMensuelDto>> ObtenirVolumesMensuelsAsync(Guid entrepriseId, DateTime debut, DateTime fin, CancellationToken ct = default);
    Task<List<TvaParTauxDto>> ObtenirRepartitionTvaAsync(Guid entrepriseId, DateTime debut, DateTime fin, CancellationToken ct = default);
    Task<List<DelaiPaiementClientDto>> ObtenirDelaisPaiementAsync(Guid entrepriseId, DateTime debut, DateTime fin, int top, CancellationToken ct = default);
    Task<List<RecapMensuelDto>> ObtenirRecapMensuelAsync(Guid entrepriseId, DateTime debut, DateTime fin, CancellationToken ct = default);
    Task AjouterAsync(Facture facture, CancellationToken ct = default);
    void MettreAJour(Facture facture);
    Task SauvegarderAsync(CancellationToken ct = default);
}

public interface ICompteurFactureRepository
{
    Task<CompteurFacture?> ObtenirAsync(Guid entrepriseId, int annee, int mois, CancellationToken ct = default);
    Task AjouterAsync(CompteurFacture compteur, CancellationToken ct = default);
    void MettreAJour(CompteurFacture compteur);
    Task SauvegarderAsync(CancellationToken ct = default);
}