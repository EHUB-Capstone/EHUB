using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EHub.Infrastructure.Persistence.Repositories;

public sealed class PendingRegistrationRepository : IPendingRegistrationRepository
{
    private readonly AppDbContext _context;

    public PendingRegistrationRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<PendingRegistration?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _context.PendingRegistrations
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<PendingRegistration?> GetByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        return _context.PendingRegistrations
            .FirstOrDefaultAsync(
                item => item.NormalizedEmail == normalizedEmail,
                cancellationToken);
    }

    public async Task AddAsync(
        PendingRegistration registration,
        CancellationToken cancellationToken = default)
    {
        await _context.PendingRegistrations.AddAsync(registration, cancellationToken);
    }
}
