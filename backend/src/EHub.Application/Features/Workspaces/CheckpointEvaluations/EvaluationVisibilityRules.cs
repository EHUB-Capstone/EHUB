using EHub.Domain.Enums;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Workspaces.CheckpointEvaluations;

internal static class EvaluationVisibilityRules
{
    public static bool IsInternalViewer(string role) =>
        IsRole(role, SystemRoles.Admin) || IsRole(role, SystemRoles.Lecturer);

    public static bool CanViewTeamScore(string role, EvaluationStatus status) =>
        IsInternalViewer(role);

    public static bool CanViewCriterionScores(string role, EvaluationStatus status) =>
        IsInternalViewer(role);

    public static bool CanViewAllMemberScores(string role) => IsInternalViewer(role);

    public static bool CanViewOwnMemberScore(string role, EvaluationStatus status) =>
        IsRole(role, SystemRoles.Student) && status == EvaluationStatus.Published;

    public static bool CanViewCourseTotal(string role, bool allComponentsPublished) =>
        IsInternalViewer(role) || (IsRole(role, SystemRoles.Student) && allComponentsPublished);

    private static bool IsRole(string role, string expected) =>
        string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
}
