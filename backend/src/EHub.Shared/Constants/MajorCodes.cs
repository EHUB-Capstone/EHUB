using System;
using System.Linq;

namespace EHub.Shared.Constants;

public static class MajorCodes
{
    // Import-only placeholder for source files that do not contain major data.
    // It is deliberately excluded from All so manual/API input still requires a real major.
    public const string Undeclared = "UNDECLARED";

    public const string BBA_HM = "BBA_HM";
    public const string BBA_IB = "BBA_IB";
    public const string BBA_MC = "BBA_MC";
    public const string BBA_MKT = "BBA_MKT";
    public const string BEN = "BEN";
    public const string BBA_TM = "BBA_TM";

    public const string BIT_AI = "BIT_AI";
    public const string BIT_GD = "BIT_GD";
    public const string BIT_IA = "BIT_IA";
    public const string BIT_SE = "BIT_SE";

    public static readonly string[] All =
    [
        BBA_HM,
        BBA_IB,
        BBA_MC,
        BBA_MKT,
        BEN,
        BBA_TM,
        BIT_AI,
        BIT_GD,
        BIT_IA,
        BIT_SE
    ];

    public static bool IsValid(string? majorCode)
    {
        return !string.IsNullOrWhiteSpace(majorCode) &&
               All.Contains(majorCode.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsUndeclared(string? majorCode) =>
        string.Equals(majorCode?.Trim(), Undeclared, StringComparison.OrdinalIgnoreCase);
}
