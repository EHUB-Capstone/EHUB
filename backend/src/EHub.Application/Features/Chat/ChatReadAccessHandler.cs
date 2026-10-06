using EHub.Application.Common.Interfaces.Persistence;
using EHub.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Chat;

public sealed class ChatReadAccessHandler(IApplicationDbContext context)
{
    public async Task<bool> CanReadAsync(Guid groupId, Guid userId, IEnumerable<string> roles,
        CancellationToken cancellationToken)
    {
        var isAdmin = roles.Contains(SystemRoles.Admin, StringComparer.OrdinalIgnoreCase);
        var isLecturer = roles.Contains(SystemRoles.Lecturer, StringComparer.OrdinalIgnoreCase);
        return await context.ChatGroups.AsNoTracking().AnyAsync(group => group.Id == groupId &&
            (isAdmin || (isLecturer && (group.Class.PrimaryLecturerId == userId ||
                group.Class.ClassLecturers.Any(item => item.LecturerId == userId))) ||
             !isLecturer && group.Members.Any(member => member.IsActive && !member.IsDeleted &&
                (member.UserId == userId || member.Student != null && member.Student.UserId == userId))),
            cancellationToken);
    }
}
