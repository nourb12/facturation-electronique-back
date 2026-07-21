using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface ICalendrierRepository
{
    Task<List<CalendrierEvent>> ListerEventsAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<CalendrierEvent?> ObtenirEventAsync(Guid id, CancellationToken ct = default);
    Task AjouterEventAsync(CalendrierEvent evt, CancellationToken ct = default);
    void MettreAJourEvent(CalendrierEvent evt);
    void SupprimerEvent(CalendrierEvent evt);

    Task<List<CalendrierTask>> ListerTasksAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<CalendrierTask?> ObtenirTaskAsync(Guid id, CancellationToken ct = default);
    Task AjouterTaskAsync(CalendrierTask task, CancellationToken ct = default);
    void MettreAJourTask(CalendrierTask task);
    void SupprimerTask(CalendrierTask task);

    Task SauvegarderAsync(CancellationToken ct = default);
}
