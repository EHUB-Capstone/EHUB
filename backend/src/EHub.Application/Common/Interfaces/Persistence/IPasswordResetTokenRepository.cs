using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Persistence;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default);
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task MarkActiveTokensAsUsedByUserIdAsync(Guid userId, DateTime usedAt, CancellationToken cancellationToken = default);
}
