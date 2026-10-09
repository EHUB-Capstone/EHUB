using EHub.Application.Features.Admin.Mentors;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Admin.Mentors;

public sealed class MentorRetentionPlannerTests
{
    private static readonly Guid TargetSemester = Guid.NewGuid();
    private static readonly Guid PreviousSemester = Guid.NewGuid();
    private static readonly DateTime Completed = new(2026, 12, 20, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Plan_KeepsBothMentorsForAContinuingTeamAndIgnoresTheTeamName()
    {
        var f = new Fixture();
        var enterprise = f.Mentor(MentorType.Enterprise);
        var academic = f.Mentor(MentorType.Academic);
        var source = f.Source("EXE101_7G5");
        var target = f.Target("RenamedTeam", previous: source);
        f.EndedAtCompletion(source, enterprise);
        f.EndedAtCompletion(source, academic);

        var plan = f.Plan();

        plan.Skipped.Should().BeEmpty();
        plan.Retained.Should().HaveCount(2);
        plan.Retained.Should().Contain(item => item.Team.Id == target.Id && item.Slot == MentorType.Enterprise && item.Mentor.Id == enterprise.Id);
        plan.Retained.Should().Contain(item => item.Team.Id == target.Id && item.Slot == MentorType.Academic && item.Mentor.Id == academic.Id);
    }

    [Fact]
    public void Plan_DoesNotMatchTeamsWithoutALineageLinkEvenWhenTheNamesMatch()
    {
        var f = new Fixture();
        var mentor = f.Mentor(MentorType.Enterprise);
        var source = f.Source("SameName");
        f.Target("SameName", previous: null);
        f.EndedAtCompletion(source, mentor);

        var plan = f.Plan();

        plan.Retained.Should().BeEmpty();
    }

    [Fact]
    public void Plan_KeepsAnActiveAssignmentOfAClassThatIsNotCompletedYet()
    {
        var f = new Fixture();
        var mentor = f.Mentor(MentorType.Enterprise);
        var source = f.Source("EXE101_1G1", completedAt: null);
        f.Target("EXE201_1G1", previous: source);
        f.Assignment(source, mentor, MentorAssignmentStatus.Active, endedAt: null);

        f.Plan().Retained.Should().ContainSingle().Which.Mentor.Id.Should().Be(mentor.Id);
    }

    [Fact]
    public void Plan_IgnoresAMentorWhoseAssignmentEndedBeforeTheClassWasCompleted()
    {
        var f = new Fixture();
        var removed = f.Mentor(MentorType.Enterprise);
        var source = f.Source("EXE101_2G1");
        f.Target("EXE201_2G1", previous: source);
        f.Assignment(source, removed, MentorAssignmentStatus.Ended, endedAt: Completed.AddDays(-30));

        var plan = f.Plan();

        plan.Retained.Should().BeEmpty();
        plan.Skipped.Should().BeEmpty();
    }

    [Fact]
    public void Plan_UsesTheReplacementMentorInsteadOfTheOneEndedEarlier()
    {
        var f = new Fixture();
        var first = f.Mentor(MentorType.Enterprise);
        var replacement = f.Mentor(MentorType.Enterprise);
        var source = f.Source("EXE101_3G1");
        f.Target("EXE201_3G1", previous: source);
        f.Assignment(source, first, MentorAssignmentStatus.Ended, endedAt: Completed.AddDays(-30), assignedAt: Completed.AddDays(-90));
        f.Assignment(source, replacement, MentorAssignmentStatus.Ended, endedAt: Completed, assignedAt: Completed.AddDays(-29));

        f.Plan().Retained.Should().ContainSingle().Which.Mentor.Id.Should().Be(replacement.Id);
    }

    [Fact]
    public void Plan_ShowsAMentorWhoIsNotActiveThisSemesterAsSkippedAndKeepsTheSlotFree()
    {
        var f = new Fixture();
        var inactive = f.Mentor(MentorType.Enterprise, activeInSemester: false);
        var academic = f.Mentor(MentorType.Academic);
        var source = f.Source("EXE101_4G1");
        var target = f.Target("EXE201_4G1", previous: source);
        f.EndedAtCompletion(source, inactive);
        f.EndedAtCompletion(source, academic);

        var plan = f.Plan();

        plan.Retained.Should().ContainSingle().Which.Slot.Should().Be(MentorType.Academic);
        var skip = plan.Skipped.Should().ContainSingle().Subject;
        skip.Reason.Should().Be(MentorRetentionSkipReason.MentorNotActiveInSemester);
        skip.Team!.Id.Should().Be(target.Id);
        skip.Slot.Should().Be(MentorType.Enterprise);
        skip.MentorProfileId.Should().Be(inactive.Id);
    }

    [Fact]
    public void Plan_SkipsAMentorWhoseAccountIsNoLongerUsable()
    {
        var f = new Fixture();
        var disabled = f.Mentor(MentorType.Academic, accountActive: false);
        var source = f.Source("EXE101_5G1");
        f.Target("EXE201_5G1", previous: source);
        f.EndedAtCompletion(source, disabled);

        var plan = f.Plan();

        plan.Retained.Should().BeEmpty();
        plan.Skipped.Should().ContainSingle().Which.Reason.Should().Be(MentorRetentionSkipReason.MentorUnavailable);
    }

    [Fact]
    public void Plan_NeverOverwritesADifferentMentorAlreadyInTheSlot()
    {
        var f = new Fixture();
        var previousMentor = f.Mentor(MentorType.Enterprise);
        var currentMentor = f.Mentor(MentorType.Enterprise);
        var source = f.Source("EXE101_6G1");
        var target = f.Target("EXE201_6G1", previous: source);
        f.EndedAtCompletion(source, previousMentor);
        f.Existing(target, currentMentor, MentorType.Enterprise);

        var plan = f.Plan();

        plan.Retained.Should().BeEmpty();
        plan.Skipped.Should().ContainSingle().Which.Reason.Should().Be(MentorRetentionSkipReason.SlotAlreadyFilled);
    }

    [Fact]
    public void Plan_IsSilentWhenTheSameMentorIsAlreadyInTheSlot()
    {
        var f = new Fixture();
        var mentor = f.Mentor(MentorType.Enterprise);
        var source = f.Source("EXE101_7G1");
        var target = f.Target("EXE201_7G1", previous: source);
        f.EndedAtCompletion(source, mentor);
        f.Existing(target, mentor, MentorType.Enterprise);

        var plan = f.Plan();

        plan.Retained.Should().BeEmpty();
        plan.Skipped.Should().BeEmpty();
    }

    [Fact]
    public void Plan_KeepsOnlyTheContinuingTeamsOfAMentorWithSeveralTeams()
    {
        var f = new Fixture();
        var mentor = f.Mentor(MentorType.Enterprise);
        var continuing = f.Source("EXE101_8G1");
        var notContinuing = f.Source("EXE101_8G2");
        var finishedExe201 = f.Source("EXE201_8G3", courseCode: "EXE201");
        f.Target("EXE201_8G1", previous: continuing);
        f.EndedAtCompletion(continuing, mentor);
        f.EndedAtCompletion(notContinuing, mentor);
        f.EndedAtCompletion(finishedExe201, mentor);

        var plan = f.Plan();

        plan.Retained.Should().ContainSingle().Which.SourceTeamCode.Should().Be("EXE101_8G1");
        plan.Skipped.Should().ContainSingle()
            .Which.Should().Match<RetentionSkip>(item => item.Reason == MentorRetentionSkipReason.NoContinuedTeam && item.SourceTeamCode == "EXE101_8G2");
    }

    [Fact]
    public void Plan_NeverCarriesAnExe201TeamForward()
    {
        var f = new Fixture();
        var mentor = f.Mentor(MentorType.Enterprise);
        var finished = f.Source("EXE201_9G1", courseCode: "EXE201");
        f.Target("EXE201_9G9", previous: finished);
        f.EndedAtCompletion(finished, mentor);

        var plan = f.Plan();

        plan.Retained.Should().BeEmpty();
        plan.Skipped.Should().BeEmpty();
    }

    [Fact]
    public void Plan_OnlyContinuesIntoAnExe201Team()
    {
        var f = new Fixture();
        var mentor = f.Mentor(MentorType.Enterprise);
        var source = f.Source("EXE101_10G1");
        f.Target("EXE101_NEW", previous: source, courseCode: "EXE101");
        f.EndedAtCompletion(source, mentor);

        f.Plan().Retained.Should().BeEmpty();
    }

    [Fact]
    public void Plan_IgnoresCancelledAndPendingAssignments()
    {
        var f = new Fixture();
        var mentor = f.Mentor(MentorType.Enterprise);
        var source = f.Source("EXE101_11G1");
        f.Target("EXE201_11G1", previous: source);
        f.Assignment(source, mentor, MentorAssignmentStatus.Cancelled, endedAt: Completed);
        f.Assignment(source, mentor, MentorAssignmentStatus.Pending, endedAt: null);

        var plan = f.Plan();

        plan.Retained.Should().BeEmpty();
        plan.Skipped.Should().BeEmpty();
    }

    private sealed class Fixture
    {
        private readonly List<RetentionTargetTeam> _targets = [];
        private readonly List<RetentionSourceTeam> _sources = [];
        private readonly List<RetentionSourceAssignment> _assignments = [];
        private readonly List<RetentionMentor> _mentors = [];
        private readonly List<AllocationExistingAssignment> _existing = [];

        public RetentionMentor Mentor(MentorType type, bool activeInSemester = true, bool accountActive = true)
        {
            var mentor = new RetentionMentor(Guid.NewGuid(), $"Mentor {_mentors.Count + 1}", $"m{_mentors.Count + 1}@example.com", type, accountActive, activeInSemester);
            _mentors.Add(mentor);
            return mentor;
        }

        public RetentionSourceTeam Source(string code, string courseCode = "EXE101", DateTime? completedAt = default)
        {
            var source = new RetentionSourceTeam(
                Guid.NewGuid(), code, code, code.Split('G')[0], courseCode, PreviousSemester,
                completedAt == default ? Completed : completedAt);
            _sources.Add(source);
            return source;
        }

        public RetentionTargetTeam Target(string name, RetentionSourceTeam? previous, string courseCode = "EXE201")
        {
            var target = new RetentionTargetTeam(Guid.NewGuid(), name, name, "EXE201_X", courseCode, previous?.Id);
            _targets.Add(target);
            return target;
        }

        public void EndedAtCompletion(RetentionSourceTeam source, RetentionMentor mentor) =>
            Assignment(source, mentor, MentorAssignmentStatus.Ended, endedAt: Completed);

        public void Assignment(
            RetentionSourceTeam source, RetentionMentor mentor, MentorAssignmentStatus status, DateTime? endedAt, DateTime? assignedAt = null) =>
            _assignments.Add(new RetentionSourceAssignment(
                source.Id, mentor.Type, mentor.Id, assignedAt ?? Completed.AddDays(-60), status, endedAt));

        public void Existing(RetentionTargetTeam team, RetentionMentor mentor, MentorType slot) =>
            _existing.Add(new AllocationExistingAssignment(team.Id, mentor.Id, slot));

        public RetentionPlan Plan()
        {
            var continued = _targets.Where(item => item.PreviousTeamId != null).Select(item => item.PreviousTeamId!.Value).ToArray();
            return MentorRetentionPlanner.Plan(new RetentionInput(
                TargetSemester, _targets, continued, _sources, _assignments, _mentors, _existing));
        }
    }
}
