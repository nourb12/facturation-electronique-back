using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> ObtenirDashboardEntrepriseAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<DashboardAdminDto> ObtenirDashboardAdminAsync(CancellationToken ct = default);
}
