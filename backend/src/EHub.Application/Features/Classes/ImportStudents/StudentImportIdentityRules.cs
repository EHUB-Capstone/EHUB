using EHub.Domain.Entities;

namespace EHub.Application.Features.Classes.ImportStudents;

internal static class StudentImportIdentityRules
{
    internal static bool HasConflict(
        int codeMatchCount,
        int emailMatchCount,
        Student? profileByCode,
        Student? profileByEmail,
        string studentCode,
        string email)
    {
        if (codeMatchCount > 1 || emailMatchCount > 1)
        {
            return true;
        }

        if (profileByCode != null && profileByEmail != null && profileByCode.Id != profileByEmail.Id)
        {
            return true;
        }

        if (profileByCode != null &&
            !string.IsNullOrWhiteSpace(profileByCode.Email) &&
            !string.Equals(profileByCode.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var existingStudentCode = GetStudentCode(profileByEmail);
        return !string.IsNullOrWhiteSpace(existingStudentCode) &&
               !string.Equals(existingStudentCode, studentCode, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool CompleteMissingIdentity(Student profile, string studentCode, string email)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(profile.RollNumber))
        {
            profile.RollNumber = studentCode;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(profile.NormalizedRollNumber))
        {
            profile.NormalizedRollNumber = studentCode;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(profile.Email))
        {
            profile.Email = email;
            changed = true;
        }

        return changed;
    }

    private static string? GetStudentCode(Student? profile)
    {
        if (profile == null)
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(profile.NormalizedRollNumber)
            ? profile.NormalizedRollNumber
            : profile.RollNumber;
    }
}
