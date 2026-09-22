namespace EHub.Application.Common.FeatureFlags;

public static class FeatureFlagValue
{
    public static bool IsEnabled(string? value) => value?.Trim().ToUpperInvariant() is
        "ON" or "TRUE" or "1" or "YES";
}
