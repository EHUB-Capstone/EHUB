using System.Net.Mail;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Classes.Common;

public sealed record NormalizedStudentEnrollmentInput(
    string StudentCode,
    string FullName,
    string Email,
    string MajorCode);

public static class StudentEnrollmentRules
{
    public static string? ValidateAndNormalize(
        string? studentCodeValue,
        string? fullNameValue,
        string? emailValue,
        string? majorCodeValue,
        out NormalizedStudentEnrollmentInput input,
        bool allowUndeclaredMajor = false)
    {
        input = new NormalizedStudentEnrollmentInput(
            studentCodeValue?.Trim().ToUpperInvariant() ?? string.Empty,
            fullNameValue?.Trim() ?? string.Empty,
            emailValue?.Trim().ToLowerInvariant() ?? string.Empty,
            majorCodeValue?.Trim().ToUpperInvariant() ?? string.Empty);

        if (string.IsNullOrWhiteSpace(input.StudentCode) || input.StudentCode.Length > 20)
        {
            return "Student code is required and must not exceed 20 characters.";
        }

        if (string.IsNullOrWhiteSpace(input.FullName) || input.FullName.Length > 150)
        {
            return "Student full name is required and must not exceed 150 characters.";
        }

        if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 150 || !MailAddress.TryCreate(input.Email, out _))
        {
            return "A valid student email address is required.";
        }

        if (!MajorCodes.IsValid(input.MajorCode) &&
            !(allowUndeclaredMajor && MajorCodes.IsUndeclared(input.MajorCode)))
        {
            return $"Major code '{majorCodeValue}' is invalid.";
        }

        return null;
    }
}
