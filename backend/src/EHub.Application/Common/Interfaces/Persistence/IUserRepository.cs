using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Persistence;

public interface IUserRepository
{
    Task<User?> GetByEmailWithRolesAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    Task<User?> GetByIdWithRolesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        User user,
        CancellationToken cancellationToken = default);

    void Update(User user);

    Task<IReadOnlyCollection<User>> GetPendingApprovalUsersAsync(
        CancellationToken cancellationToken = default);
}
