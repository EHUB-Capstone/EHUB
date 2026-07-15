using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Persistence;

public interface IMentorProfileRepository
{
    Task<MentorProfile?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        MentorProfile mentorProfile,
        CancellationToken cancellationToken = default);

    void Update(MentorProfile mentorProfile);
}
