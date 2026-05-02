using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class DemoRequestRepository(ContextBaseDeDonnees db) : IDemoRequestRepository
{
    public async Task<DemoRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.DemoRequests.FindAsync([id], ct);

    public async Task<List<DemoRequest>> GetAllAsync(CancellationToken ct = default)
        => await db.DemoRequests
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

    public async Task<List<DemoRequest>> GetByStatusAsync(DemoRequestStatus status, CancellationToken ct = default)
        => await db.DemoRequests
            .Where(d => d.Status == status)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(DemoRequest request, CancellationToken ct = default)
        => await db.DemoRequests.AddAsync(request, ct);

    public void Update(DemoRequest request)
        => db.DemoRequests.Update(request);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
