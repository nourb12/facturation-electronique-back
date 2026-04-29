using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IPersonnalisationService
{
    Task<PersonnalisationDto?> ObtenirAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<PersonnalisationDto> EnregistrerAsync(Guid entrepriseId, EnregistrerPersonnalisationRequest request, CancellationToken ct = default);
}