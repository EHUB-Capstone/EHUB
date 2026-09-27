using System.Text;

namespace EHub.Application.Features.Classes.Common;

internal static class SemesterGroupColumn
{
    internal static string GetHeader(string? semesterCode)
    {
        var shortCode = GetShortCode(semesterCode);
        return string.IsNullOrWhiteSpace(shortCode) ? "Group" : $"Group {shortCode}";
    }

    internal static string GetShortCode(string? semesterCode)
    {
        if (string.IsNullOrWhiteSpace(semesterCode)) return string.Empty;

        var letters = new string(semesterCode.Where(char.IsLetter).ToArray()).ToUpperInvariant();
        var digits = new string(semesterCode.Where(char.IsDigit).ToArray());
        var year = digits.Length > 2 ? digits[^2..] : digits;
        return $"{letters}{year}";
    }

    internal static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character)) normalized.Append(char.ToUpperInvariant(character));
        }

        return normalized.ToString();
    }
}
