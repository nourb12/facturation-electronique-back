using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;

namespace Einvoicing.Application.Interfaces;

public interface IDemoRequestRepository
{
    Task<DemoRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<DemoRequest>> GetAllAsync(CancellationToken ct = default);
    Task<List<DemoRequest>> GetByStatusAsync(DemoRequestStatus status, CancellationToken ct = default);
    Task AddAsync(DemoRequest request, CancellationToken ct = default);
    void Update(DemoRequest request);
    Task SaveChangesAsync(CancellationToken ct = default);
}
