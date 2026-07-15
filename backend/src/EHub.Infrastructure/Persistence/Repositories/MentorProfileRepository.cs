using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Repositories;

public class MentorProfileRepository : IMentorProfileRepository
{
    private readonly AppDbContext _context;

    public MentorProfileRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MentorProfile?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.MentorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(
        MentorProfile mentorProfile,
        CancellationToken cancellationToken = default)
    {
        await _context.MentorProfiles.AddAsync(mentorProfile, cancellationToken);
    }

    public void Update(MentorProfile mentorProfile)
    {
        _context.MentorProfiles.Update(mentorProfile);
    }
}
