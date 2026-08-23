using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;

namespace EHub.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailWithRolesAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public async Task<User?> GetByIdWithRolesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public void Update(User user)
    {
        _context.Users.Update(user);
    }

    public async Task<IReadOnlyCollection<User>> GetPendingApprovalUsersAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .Where(user => user.Status == UserStatus.PendingApproval)
            .Where(user => user.IsEmailVerified)
            .Where(user => user.UserRoles.Any(userRole =>
                userRole.Role.Name == SystemRoles.Lecturer ||
                userRole.Role.Name == SystemRoles.Mentor))
            .Where(user => !user.IsDeleted)
            .OrderByDescending(user => user.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
