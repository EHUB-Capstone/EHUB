using EHub.Application.Features.Workspaces.CheckpointEvaluations;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointEvaluations;

public sealed class EvaluationVisibilityRulesTests
{
    [Theory]
    [InlineData(SystemRoles.Admin, EvaluationStatus.Submitted, true)]
    [InlineData(SystemRoles.Lecturer, EvaluationStatus.Submitted, true)]
    [InlineData(SystemRoles.Student, EvaluationStatus.Submitted, false)]
    [InlineData(SystemRoles.Mentor, EvaluationStatus.Submitted, false)]
    [InlineData(SystemRoles.Student, EvaluationStatus.Published, true)]
    [InlineData(SystemRoles.Mentor, EvaluationStatus.Published, true)]
    public void TeamScore_RequiresPublicationForExternalViewers(
        string role, EvaluationStatus status, bool expected)
    {
        EvaluationVisibilityRules.CanViewTeamScore(role, status).Should().Be(expected);
    }

    [Theory]
    [InlineData(SystemRoles.Admin, true)]
    [InlineData(SystemRoles.Lecturer, true)]
    [InlineData(SystemRoles.Student, false)]
    [InlineData(SystemRoles.Mentor, false)]
    public void FullMemberScores_AreInternalOnly(string role, bool expected)
    {
        EvaluationVisibilityRules.CanViewAllMemberScores(role).Should().Be(expected);
    }

    [Theory]
    [InlineData(SystemRoles.Admin, EvaluationStatus.Submitted, true)]
    [InlineData(SystemRoles.Lecturer, EvaluationStatus.Submitted, true)]
    [InlineData(SystemRoles.Student, EvaluationStatus.Submitted, false)]
    [InlineData(SystemRoles.Student, EvaluationStatus.Published, true)]
    [InlineData(SystemRoles.Mentor, EvaluationStatus.Submitted, false)]
    [InlineData(SystemRoles.Mentor, EvaluationStatus.Published, false)]
    public void CriterionScores_AreAvailableToStudentOnlyAfterPublication(
        string role, EvaluationStatus status, bool expected)
    {
        EvaluationVisibilityRules.CanViewCriterionScores(role, status).Should().Be(expected);
    }

    [Theory]
    [InlineData(SystemRoles.Student, EvaluationStatus.Submitted, false)]
    [InlineData(SystemRoles.Student, EvaluationStatus.Published, true)]
    [InlineData(SystemRoles.Mentor, EvaluationStatus.Published, false)]
    [InlineData(SystemRoles.Lecturer, EvaluationStatus.Published, false)]
    public void OwnMemberScore_IsOnlyAvailableToStudentAfterPublication(
        string role, EvaluationStatus status, bool expected)
    {
        EvaluationVisibilityRules.CanViewOwnMemberScore(role, status).Should().Be(expected);
    }

    [Theory]
    [InlineData(SystemRoles.Admin, false, true)]
    [InlineData(SystemRoles.Lecturer, false, true)]
    [InlineData(SystemRoles.Student, false, false)]
    [InlineData(SystemRoles.Student, true, true)]
    [InlineData(SystemRoles.Mentor, true, false)]
    public void CourseTotal_IsNeverAvailableToMentorAndRequiresAllPublishedForStudent(
        string role, bool allComponentsPublished, bool expected)
    {
        EvaluationVisibilityRules.CanViewCourseTotal(role, allComponentsPublished).Should().Be(expected);
    }
}
