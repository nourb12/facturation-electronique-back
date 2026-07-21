using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class CalendrierService(ICalendrierRepository repo, IFactureRepository factureRepo) : ICalendrierService
{
    public async Task<IReadOnlyList<CalendrierEventDto>> ListerEventsAsync(Guid entrepriseId, CancellationToken ct = default)
    {
        var events = (await repo.ListerEventsAsync(entrepriseId, ct)).Select(MapEvent).ToList();
        var factures = await factureRepo.ListerAsync(
            entrepriseId,
            new FiltreFacturesRequest(Page: 1, ParPage: 500),
            ct);

        events.AddRange(factures.Items
            .Where(f => f.Statut is not StatutFacture.Payee and not StatutFacture.Annulee)
            .Select(f => new CalendrierEventDto(
                Guid.NewGuid(),
                entrepriseId,
                f.EstEnRetard ? $"{f.Numero} impayee" : $"Echeance {f.Numero}",
                DateOnly.FromDateTime(f.DateEcheance),
                f.EstEnRetard ? "overdue" : "invoice",
                null,
                null,
                null,
                f.MontantRestant,
                null,
                1440)));

        return events.OrderBy(e => e.Date).ToList();
    }

    public async Task<CalendrierEventDto> CreerEventAsync(Guid entrepriseId, CalendrierEventRequest request, CancellationToken ct = default)
    {
        var evt = CalendrierEvent.Creer(
            entrepriseId,
            request.Title,
            request.Date,
            request.Type,
            request.StartTime,
            request.EndTime,
            request.LinkedClientName,
            request.LinkedAmount,
            request.ZoomLink,
            request.ReminderMinutes);

        await repo.AjouterEventAsync(evt, ct);
        await repo.SauvegarderAsync(ct);
        return MapEvent(evt);
    }

    public async Task<CalendrierEventDto> MettreAJourEventAsync(Guid id, Guid entrepriseId, CalendrierEventRequest request, CancellationToken ct = default)
    {
        var evt = await repo.ObtenirEventAsync(id, ct) ?? throw new NotFoundException("Evenement calendrier introuvable.");
        if (evt.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        evt.MettreAJour(
            request.Title,
            request.Date,
            request.Type,
            request.StartTime,
            request.EndTime,
            request.LinkedClientName,
            request.LinkedAmount,
            request.ZoomLink,
            request.ReminderMinutes);

        repo.MettreAJourEvent(evt);
        await repo.SauvegarderAsync(ct);
        return MapEvent(evt);
    }

    public async Task SupprimerEventAsync(Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var evt = await repo.ObtenirEventAsync(id, ct) ?? throw new NotFoundException("Evenement calendrier introuvable.");
        if (evt.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        repo.SupprimerEvent(evt);
        await repo.SauvegarderAsync(ct);
    }

    public async Task<IReadOnlyList<CalendrierTaskDto>> ListerTasksAsync(Guid entrepriseId, CancellationToken ct = default)
        => (await repo.ListerTasksAsync(entrepriseId, ct)).Select(MapTask).ToList();

    public async Task<CalendrierTaskDto> CreerTaskAsync(Guid entrepriseId, CalendrierTaskRequest request, CancellationToken ct = default)
    {
        var task = CalendrierTask.Creer(entrepriseId, request.Title, request.Date, request.Priority);

        await repo.AjouterTaskAsync(task, ct);
        await repo.SauvegarderAsync(ct);
        return MapTask(task);
    }

    public async Task<CalendrierTaskDto> BasculerTaskAsync(Guid id, Guid entrepriseId, ToggleCalendrierTaskRequest request, CancellationToken ct = default)
    {
        var task = await repo.ObtenirTaskAsync(id, ct) ?? throw new NotFoundException("Tache calendrier introuvable.");
        if (task.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        task.Basculer(request.Done);
        repo.MettreAJourTask(task);
        await repo.SauvegarderAsync(ct);
        return MapTask(task);
    }

    public async Task SupprimerTaskAsync(Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var task = await repo.ObtenirTaskAsync(id, ct) ?? throw new NotFoundException("Tache calendrier introuvable.");
        if (task.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        repo.SupprimerTask(task);
        await repo.SauvegarderAsync(ct);
    }

    private static CalendrierEventDto MapEvent(CalendrierEvent evt) => new(
        evt.Id,
        evt.EntrepriseId,
        evt.Title,
        evt.Date,
        evt.Type,
        evt.StartTime,
        evt.EndTime,
        evt.LinkedClientName,
        evt.LinkedAmount,
        evt.ZoomLink,
        evt.ReminderMinutes);

    private static CalendrierTaskDto MapTask(CalendrierTask task) => new(
        task.Id,
        task.EntrepriseId,
        task.Title,
        task.Date,
        task.Done,
        task.Priority);
}
