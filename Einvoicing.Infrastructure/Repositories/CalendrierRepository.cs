using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class CalendrierRepository(ContextBaseDeDonnees db) : ICalendrierRepository
{
    public async Task<List<CalendrierEvent>> ListerEventsAsync(Guid entrepriseId, CancellationToken ct = default)
        => await db.CalendrierEvents
            .Where(e => e.EntrepriseId == entrepriseId)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.StartTime)
            .ToListAsync(ct);

    public async Task<CalendrierEvent?> ObtenirEventAsync(Guid id, CancellationToken ct = default)
        => await db.CalendrierEvents.FindAsync([id], ct);

    public async Task AjouterEventAsync(CalendrierEvent evt, CancellationToken ct = default)
        => await db.CalendrierEvents.AddAsync(evt, ct);

    public void MettreAJourEvent(CalendrierEvent evt)
        => db.CalendrierEvents.Update(evt);

    public void SupprimerEvent(CalendrierEvent evt)
        => db.CalendrierEvents.Remove(evt);

    public async Task<List<CalendrierTask>> ListerTasksAsync(Guid entrepriseId, CancellationToken ct = default)
        => await db.CalendrierTasks
            .Where(t => t.EntrepriseId == entrepriseId)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.CreeLe)
            .ToListAsync(ct);

    public async Task<CalendrierTask?> ObtenirTaskAsync(Guid id, CancellationToken ct = default)
        => await db.CalendrierTasks.FindAsync([id], ct);

    public async Task AjouterTaskAsync(CalendrierTask task, CancellationToken ct = default)
        => await db.CalendrierTasks.AddAsync(task, ct);

    public void MettreAJourTask(CalendrierTask task)
        => db.CalendrierTasks.Update(task);

    public void SupprimerTask(CalendrierTask task)
        => db.CalendrierTasks.Remove(task);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
