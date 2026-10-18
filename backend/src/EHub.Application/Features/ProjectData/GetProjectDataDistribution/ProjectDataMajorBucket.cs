using EHub.Application.Features.Classes.Common;
using EHub.Contracts.ProjectData;
using EHub.Shared.Constants;

namespace EHub.Application.Features.ProjectData.GetProjectDataDistribution;

internal static class ProjectDataMajorBucket
{
    /// <summary>
    /// The major a member counts under: the enrollment major, else the profile major. Missing, UNDECLARED
    /// and codes outside <see cref="MajorCodes.All"/> fall into the unspecified bucket.
    /// </summary>
    internal static string Resolve(string? enrollmentMajorCode, string? profileMajorCode)
    {
        var effective = StudentEnrollmentRules.ResolveEffectiveMajorCode(enrollmentMajorCode, profileMajorCode);
        return !string.IsNullOrWhiteSpace(effective) && MajorCodes.IsValid(effective)
            ? effective
            : ProjectDataDistributionRules.UnspecifiedMajor;
    }
}
