using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface ICalendrierService
{
    Task<IReadOnlyList<CalendrierEventDto>> ListerEventsAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<CalendrierEventDto> CreerEventAsync(Guid entrepriseId, CalendrierEventRequest request, CancellationToken ct = default);
    Task<CalendrierEventDto> MettreAJourEventAsync(Guid id, Guid entrepriseId, CalendrierEventRequest request, CancellationToken ct = default);
    Task SupprimerEventAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);

    Task<IReadOnlyList<CalendrierTaskDto>> ListerTasksAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<CalendrierTaskDto> CreerTaskAsync(Guid entrepriseId, CalendrierTaskRequest request, CancellationToken ct = default);
    Task<CalendrierTaskDto> BasculerTaskAsync(Guid id, Guid entrepriseId, ToggleCalendrierTaskRequest request, CancellationToken ct = default);
    Task SupprimerTaskAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
}
