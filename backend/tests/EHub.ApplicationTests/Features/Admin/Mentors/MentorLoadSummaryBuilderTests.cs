using EHub.Application.Features.Admin.Mentors;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Admin.Mentors;

public sealed class MentorLoadSummaryBuilderTests
{
    [Fact]
    public void Build_SplitsLoadBySubjectAndCountsBeforeAndAfter()
    {
        var mentor = new LoadMentor(Guid.NewGuid(), "Mentor A", "a@example.com", MentorType.Enterprise, "Thỉnh giảng");

        var summary = MentorLoadSummaryBuilder.Build(
            [mentor],
            existing: [new LoadAssignment(mentor.Id, "EXE101"), new LoadAssignment(mentor.Id, "EXE101")],
            proposed: [new LoadAssignment(mentor.Id, "EXE201"), new LoadAssignment(mentor.Id, "EXE101")]).Single();

        summary.TotalBefore.Should().Be(2);
        summary.TotalAfter.Should().Be(4);
        summary.Subjects.Should().Equal(new MentorSubjectLoad("EXE101", 2, 1), new MentorSubjectLoad("EXE201", 0, 1));
        summary.Mentor.ContractType.Should().Be("Thỉnh giảng");
    }

    [Fact]
    public void Build_ListsMentorsWithoutAnyTeamAsUnassigned()
    {
        var idle = new LoadMentor(Guid.NewGuid(), "Idle", "idle@example.com", MentorType.Academic, null);

        var summary = MentorLoadSummaryBuilder.Build([idle], [], []).Single();

        summary.Subjects.Should().BeEmpty();
        summary.TotalBefore.Should().Be(0);
        summary.TotalAfter.Should().Be(0);
    }

    [Fact]
    public void Build_OnlyCountsAssignmentsOfTheMentorItDescribes()
    {
        var first = new LoadMentor(Guid.NewGuid(), "B Mentor", "b@example.com", MentorType.Enterprise, null);
        var second = new LoadMentor(Guid.NewGuid(), "A Mentor", "a2@example.com", MentorType.Enterprise, null);

        var summaries = MentorLoadSummaryBuilder.Build(
            [first, second],
            [new LoadAssignment(first.Id, "EXE201")],
            [new LoadAssignment(second.Id, "EXE201")]);

        summaries.Select(item => item.Mentor.Name).Should().Equal("A Mentor", "B Mentor");
        summaries.Single(item => item.Mentor.Id == first.Id).TotalAfter.Should().Be(1);
        summaries.Single(item => item.Mentor.Id == second.Id).TotalBefore.Should().Be(0);
    }

    [Fact]
    public void Build_SubtractsAssignmentsThatEndBecauseAMentorIsReplaced()
    {
        var mentor = new LoadMentor(Guid.NewGuid(), "Replaced", "r@example.com", MentorType.Enterprise, null);

        var summary = MentorLoadSummaryBuilder.Build(
            [mentor],
            existing: [new LoadAssignment(mentor.Id, "EXE201"), new LoadAssignment(mentor.Id, "EXE201"), new LoadAssignment(mentor.Id, "EXE101")],
            proposed: [new LoadAssignment(mentor.Id, "EXE101")],
            ended: [new LoadAssignment(mentor.Id, "EXE201")]).Single();

        summary.TotalBefore.Should().Be(3);
        summary.TotalAfter.Should().Be(3);
        summary.Subjects.Should().Equal(new MentorSubjectLoad("EXE101", 1, 1, 0), new MentorSubjectLoad("EXE201", 2, 0, 1));
    }
}
