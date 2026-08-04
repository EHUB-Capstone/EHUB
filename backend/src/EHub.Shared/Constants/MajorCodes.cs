using System;
using System.Linq;

namespace EHub.Shared.Constants;

public static class MajorCodes
{
    public const string BBA_HM = "BBA_HM";
    public const string BBA_IB = "BBA_IB";
    public const string BBA_MC = "BBA_MC";
    public const string BBA_MKT = "BBA_MKT";
    public const string BEN = "BEN";
    public const string BBA_TM = "BBA_TM";
    public const string BBA_FIN = "BBA_FIN";
    public const string BBA_HRM = "BBA_HRM";
    public const string BBA_DM = "BBA_DM";
    public const string BBA_BA = "BBA_BA";
    public const string BBA_LOG = "BBA_LOG";

    public const string BIT_AI = "BIT_AI";
    public const string BIT_GD = "BIT_GD";
    public const string BIT_IA = "BIT_IA";
    public const string BIT_SE = "BIT_SE";
    public const string BIT_IS = "BIT_IS";
    public const string BIT_CS = "BIT_CS";
    public const string BIT_CY = "BIT_CY";
    public const string BIT_DS = "BIT_DS";

    public const string BLA_ELT = "BLA_ELT";
    public const string BLA_BC = "BLA_BC";
    public const string BLA_JP = "BLA_JP";
    public const string BLA_KR = "BLA_KR";
    public const string BLA_CN = "BLA_CN";

    public static readonly string[] All =
    [
        BBA_HM,
        BBA_IB,
        BBA_MC,
        BBA_MKT,
        BEN,
        BBA_TM,
        BBA_FIN,
        BBA_HRM,
        BBA_DM,
        BBA_BA,
        BBA_LOG,
        BIT_AI,
        BIT_GD,
        BIT_IA,
        BIT_SE,
        BIT_IS,
        BIT_CS,
        BIT_CY,
        BIT_DS,
        BLA_ELT,
        BLA_BC,
        BLA_JP,
        BLA_KR,
        BLA_CN
    ];

    public static bool IsValid(string? majorCode)
    {
        return !string.IsNullOrWhiteSpace(majorCode) &&
               All.Contains(majorCode.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
