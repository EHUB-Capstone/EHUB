using EHub.Application.Common.Interfaces.Persistence;
using EHub.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.Common;

public static class RegisteredStudentMajorResolver
{
    public static async Task<IReadOnlyDictionary<string, string>> LoadByEmailAsync(
        IApplicationDbContext context,
        IEnumerable<string?> emails,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmails = emails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email!.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();

        if (normalizedEmails.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var registeredProfiles = await context.Students
            .AsNoTracking()
            .Where(student => student.UserId.HasValue &&
                student.Email != null &&
                normalizedEmails.Contains(student.Email.ToLower()))
            .Select(student => new { student.Email, student.MajorCode })
            .ToListAsync(cancellationToken);

        return registeredProfiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Email) && MajorCodes.IsValid(profile.MajorCode))
            .GroupBy(profile => profile.Email!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().MajorCode!.Trim().ToUpperInvariant(),
                StringComparer.OrdinalIgnoreCase);
    }
}
