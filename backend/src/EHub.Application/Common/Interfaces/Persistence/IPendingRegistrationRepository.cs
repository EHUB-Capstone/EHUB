using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Persistence;

public interface IPendingRegistrationRepository
{
    Task<PendingRegistration?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PendingRegistration?> GetByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        PendingRegistration registration,
        CancellationToken cancellationToken = default);
}
