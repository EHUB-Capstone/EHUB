using EHub.Application.Features.Admin.Mentors;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Admin.Mentors;

public sealed class MentorManualEditPlannerTests
{
    private readonly AllocationTeam _team = new(Guid.NewGuid(), Guid.NewGuid(), "T1", "Team 1");
    private readonly AllocationMentor _enterprise = new(Guid.NewGuid(), "Enterprise A", "ea@example.com", MentorType.Enterprise);
    private readonly AllocationMentor _otherEnterprise = new(Guid.NewGuid(), "Enterprise B", "eb@example.com", MentorType.Enterprise);
    private readonly AllocationMentor _academic = new(Guid.NewGuid(), "Academic A", "aa@example.com", MentorType.Academic);

    private ManualPlan Plan(ManualEdit[] edits, params ExistingSlot[] existing) =>
        MentorManualEditPlanner.Plan(edits, [_team], [_enterprise, _otherEnterprise, _academic], existing);

    private ExistingSlot Occupant(AllocationMentor mentor, MentorType slot) =>
        new(Guid.NewGuid(), _team.Id, slot, mentor.Id, mentor.Name);

    [Fact]
    public void Plan_PutsTheChosenMentorIntoAnEmptySlot()
    {
        var plan = Plan([new ManualEdit(_team.Id, MentorType.Enterprise, _enterprise.Id, false, null)]);

        plan.Conflicts.Should().BeEmpty();
        var assignment = plan.Assignments.Should().ContainSingle().Subject;
        assignment.Mentor.Id.Should().Be(_enterprise.Id);
        assignment.Replaces.Should().BeNull();
    }

    [Fact]
    public void Plan_DoesNotOverwriteAnOccupiedSlotWithoutAnExplicitReplace()
    {
        var current = Occupant(_enterprise, MentorType.Enterprise);

        var plan = Plan([new ManualEdit(_team.Id, MentorType.Enterprise, _otherEnterprise.Id, false, null)], current);

        plan.Assignments.Should().BeEmpty();
        var conflict = plan.Conflicts.Should().ContainSingle().Subject;
        conflict.Kind.Should().Be(ManualConflictKind.SlotOccupied);
        conflict.Current!.AssignmentId.Should().Be(current.AssignmentId);
        conflict.Proposed!.Id.Should().Be(_otherEnterprise.Id);
    }

    [Fact]
    public void Plan_ReplacesTheCurrentMentorWhenAskedToWithAReason()
    {
        var current = Occupant(_enterprise, MentorType.Enterprise);

        var plan = Plan([new ManualEdit(_team.Id, MentorType.Enterprise, _otherEnterprise.Id, true, "  Mentor unavailable  ")], current);

        plan.Conflicts.Should().BeEmpty();
        var assignment = plan.Assignments.Should().ContainSingle().Subject;
        assignment.Replaces!.AssignmentId.Should().Be(current.AssignmentId);
        assignment.Mentor.Id.Should().Be(_otherEnterprise.Id);
        assignment.Reason.Should().Be("Mentor unavailable");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public void Plan_RejectsAReplaceWithoutAUsableReason(string? reason)
    {
        var current = Occupant(_enterprise, MentorType.Enterprise);

        var plan = Plan([new ManualEdit(_team.Id, MentorType.Enterprise, _otherEnterprise.Id, true, reason)], current);

        plan.Assignments.Should().BeEmpty();
        plan.Conflicts.Should().ContainSingle().Which.Kind.Should().Be(ManualConflictKind.EditRejected);
    }

    [Fact]
    public void Plan_RejectsAReplaceReasonThatIsTooLong()
    {
        var current = Occupant(_enterprise, MentorType.Enterprise);
        var reason = new string('x', MentorManualEditPlanner.MaximumReasonLength + 1);

        var plan = Plan([new ManualEdit(_team.Id, MentorType.Enterprise, _otherEnterprise.Id, true, reason)], current);

        plan.Assignments.Should().BeEmpty();
        plan.Conflicts.Should().ContainSingle().Which.Kind.Should().Be(ManualConflictKind.EditRejected);
    }

    [Fact]
    public void Plan_IgnoresAnEditThatAlreadyMatchesTheCurrentMentor()
    {
        var current = Occupant(_enterprise, MentorType.Enterprise);

        var plan = Plan([new ManualEdit(_team.Id, MentorType.Enterprise, _enterprise.Id, true, "no change needed")], current);

        plan.Assignments.Should().BeEmpty();
        plan.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public void Plan_RejectsAMentorOfTheWrongType()
    {
        var plan = Plan([new ManualEdit(_team.Id, MentorType.Enterprise, _academic.Id, false, null)]);

        plan.Assignments.Should().BeEmpty();
        plan.Conflicts.Should().ContainSingle().Which.Kind.Should().Be(ManualConflictKind.EditRejected);
    }

    [Fact]
    public void Plan_RejectsAMentorWhoIsNotActiveThisSemester()
    {
        var plan = Plan([new ManualEdit(_team.Id, MentorType.Enterprise, Guid.NewGuid(), false, null)]);

        plan.Assignments.Should().BeEmpty();
        plan.Conflicts.Should().ContainSingle().Which.Message.Should().Contain("not active");
    }

    [Fact]
    public void Plan_RejectsATeamOutsideTheScope()
    {
        var plan = Plan([new ManualEdit(Guid.NewGuid(), MentorType.Enterprise, _enterprise.Id, false, null)]);

        plan.Assignments.Should().BeEmpty();
        plan.Conflicts.Should().ContainSingle().Which.Kind.Should().Be(ManualConflictKind.EditRejected);
    }

    [Fact]
    public void Plan_AllowsOnlyOneEditPerTeamSlot()
    {
        var plan = Plan([
            new ManualEdit(_team.Id, MentorType.Enterprise, _enterprise.Id, false, null),
            new ManualEdit(_team.Id, MentorType.Enterprise, _otherEnterprise.Id, false, null)
        ]);

        plan.Assignments.Should().ContainSingle().Which.Mentor.Id.Should().Be(_enterprise.Id);
        plan.Conflicts.Should().ContainSingle().Which.Kind.Should().Be(ManualConflictKind.EditRejected);
    }

    [Fact]
    public void Plan_LetsTheAdminLeaveAnEmptySlotEmpty()
    {
        var plan = Plan([new ManualEdit(_team.Id, MentorType.Academic, null, false, null)]);

        plan.Exclusions.Should().ContainSingle().Which.Should().Be(new ManualExclusion(_team.Id, MentorType.Academic));
        plan.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public void Plan_DoesNotRemoveAnExistingMentorThroughAnEmptyEdit()
    {
        var current = Occupant(_academic, MentorType.Academic);

        var plan = Plan([new ManualEdit(_team.Id, MentorType.Academic, null, false, null)], current);

        plan.Exclusions.Should().BeEmpty();
        plan.Conflicts.Should().ContainSingle().Which.Kind.Should().Be(ManualConflictKind.EditRejected);
    }

    [Fact]
    public void Plan_HandlesTheTwoSlotsOfATeamIndependently()
    {
        var currentAcademic = Occupant(_academic, MentorType.Academic);

        var plan = Plan([
            new ManualEdit(_team.Id, MentorType.Enterprise, _enterprise.Id, false, null),
            new ManualEdit(_team.Id, MentorType.Academic, _academic.Id, false, null)
        ], currentAcademic);

        plan.Assignments.Should().ContainSingle().Which.Slot.Should().Be(MentorType.Enterprise);
        plan.Conflicts.Should().BeEmpty();
    }
}
