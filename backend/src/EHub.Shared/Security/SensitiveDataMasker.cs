using System;

namespace EHub.Shared.Security;

public static class SensitiveDataMasker
{
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "[empty]";
        }

        var parts = email.Split('@');

        if (parts.Length != 2)
        {
            return "[invalid-email]";
        }

        var name = parts[0];
        var domain = parts[1];

        if (name.Length <= 2)
        {
            return $"{name[0]}***@{domain}";
        }

        return $"{name[0]}***{name[^1]}@{domain}";
    }
}
