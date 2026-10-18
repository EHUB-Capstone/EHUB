using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.ProjectData.GetProjectDataTrends;
using EHub.Contracts.ProjectData;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;

namespace EHub.ApplicationTests.Features.ProjectData;

public sealed class ProjectDataTrendsTests
{
    private static readonly Guid CheckpointOne = Guid.NewGuid();
    private static readonly Guid CheckpointTwo = Guid.NewGuid();
    private static readonly Guid Assessment = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private static readonly TrendComponent[] Components =
    [
        new(CheckpointOne, null, 30m),
        new(CheckpointTwo, null, 50m),
        new(null, Assessment, 20m),
    ];

    private static TrendEvaluation Evaluation(Guid? checkpoint, Guid? rubric, EvaluationStatus status, decimal score,
        decimal max = 10m, int minutes = 0) =>
        new(Guid.NewGuid(), rubric ?? Guid.NewGuid(), checkpoint, status, score, max, Now.AddMinutes(minutes));

    private static TrendEvaluation[] AllPublished() =>
    [
        Evaluation(CheckpointOne, null, EvaluationStatus.Published, 8m),
        Evaluation(CheckpointTwo, null, EvaluationStatus.Published, 6m),
        Evaluation(null, Assessment, EvaluationStatus.Published, 10m),
    ];

    [Fact]
    public void Score_WeightsCheckpointsAndAssessmentsToTenPointScale()
    {
        // 8*0.3 + 6*0.5 + 10*0.2 = 7.4
        OfficialCourseScore.Calculate(Components, AllPublished()).Should().Be(7.4m);
    }

    [Fact]
    public void Score_NormalizesEvaluationsThatAreNotOutOfTen()
    {
        var evaluations = AllPublished();
        evaluations[0] = Evaluation(CheckpointOne, null, EvaluationStatus.Published, 40m, max: 50m); // 8 / 10

        OfficialCourseScore.Calculate(Components, evaluations).Should().Be(7.4m);
    }

    [Theory]
    [InlineData(EvaluationStatus.Draft)]
    [InlineData(EvaluationStatus.Submitted)]
    public void Score_IsNullWhenAComponentIsNotPublished(EvaluationStatus status)
    {
        var evaluations = AllPublished();
        evaluations[1] = Evaluation(CheckpointTwo, null, status, 6m);

        OfficialCourseScore.Calculate(Components, evaluations).Should().BeNull();
    }

    [Fact]
    public void Score_IsNullWhenAComponentHasNoEvaluation()
    {
        OfficialCourseScore.Calculate(Components, AllPublished().Take(2).ToArray()).Should().BeNull();
    }

    [Fact]
    public void Score_UsesNothingWhenANewerRegradeIsStillUnpublished()
    {
        var evaluations = AllPublished().Append(
            Evaluation(CheckpointOne, null, EvaluationStatus.Submitted, 2m, minutes: 5)).ToArray();

        OfficialCourseScore.Calculate(Components, evaluations).Should().BeNull();
    }

    [Fact]
    public void Score_UsesTheNewestPublishedEvaluationOfAComponent()
    {
        var evaluations = AllPublished().Append(
            Evaluation(CheckpointOne, null, EvaluationStatus.Published, 10m, minutes: 5)).ToArray();

        // 10*0.3 + 6*0.5 + 10*0.2 = 8
        OfficialCourseScore.Calculate(Components, evaluations).Should().Be(8m);
    }

    [Fact]
    public void Score_IgnoresDraftRowsAndRowsOfOtherComponents()
    {
        var evaluations = AllPublished().Concat(
        [
            Evaluation(CheckpointOne, null, EvaluationStatus.Draft, 0m, minutes: 9),
            Evaluation(Guid.NewGuid(), null, EvaluationStatus.Published, 1m, minutes: 9),
        ]).ToArray();

        OfficialCourseScore.Calculate(Components, evaluations).Should().Be(7.4m);
    }

    [Fact]
    public void Score_IsNullWithoutWeightedComponents()
    {
        OfficialCourseScore.Calculate([], AllPublished()).Should().BeNull();
        OfficialCourseScore.Calculate([new TrendComponent(CheckpointOne, null, 0m)], AllPublished()).Should().BeNull();
    }

    private static TrendProject Project(string semester, int year, SemesterTerm term, string subject, string classCode,
        decimal? score, bool potential = false, bool internallyFunded = false, bool externallyFunded = false, bool awarded = false) =>
        new(Guid.NewGuid(), semester, year, term, subject, ClassId(semester, classCode), classCode, potential, internallyFunded, externallyFunded, awarded, score);

    // The same class in the same semester is one class, so its projects share an id.
    private static Guid ClassId(string semester, string classCode) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(semester + classCode)));

    [Fact]
    public void Builder_ComparesEachSemesterWithThePreviousOneOfTheSameSubject()
    {
        var response = ProjectDataTrendBuilder.Build(
        [
            Project("SU2026", 2026, SemesterTerm.Summer, "EXE201", "EXE201_1", 7m, potential: true),
            Project("SU2026", 2026, SemesterTerm.Summer, "EXE201", "EXE201_1", 8m),
            Project("SU2026", 2026, SemesterTerm.Summer, "EXE301", "EXE301_1", 6m),
            Project("FA2026", 2026, SemesterTerm.Fall, "EXE201", "EXE201_1", 9m, potential: true, internallyFunded: true, externallyFunded: true),
            Project("FA2026", 2026, SemesterTerm.Fall, "EXE201", "EXE201_1", null),
        ]);

        response.Semesters.Select(row => (row.SemesterCode, row.TotalGroups, row.GradedGroups, row.AverageScore))
            .Should().Equal(("SU2026", 3, 3, 7.0m), ("FA2026", 2, 1, 9.0m));
        response.Semesters.Last().AverageScoreChange.Should().Be(2.0m);
        response.Semesters.Last().PotentialChange.Should().Be(0);
        response.Semesters.Last().InternallyFundedChange.Should().Be(1);
        response.Semesters.Last().ExternallyFundedChange.Should().Be(1);
        response.Semesters.First().AverageScoreChange.Should().BeNull();

        var exe201 = response.Subjects.Where(row => row.SubjectCode == "EXE201").ToArray();
        exe201.Select(row => row.AverageScore).Should().Equal(7.5m, 9.0m);
        exe201.Last().AverageScoreChange.Should().Be(1.5m);
        response.Subjects.Single(row => row.SubjectCode == "EXE301").AverageScoreChange.Should().BeNull();

        response.Classes.Count.Should().Be(3);
    }

    [Fact]
    public void Builder_LeavesTheScoreChangeEmptyWhenASemesterHasNoGradedGroup()
    {
        var response = ProjectDataTrendBuilder.Build(
        [
            Project("SU2026", 2026, SemesterTerm.Summer, "EXE201", "EXE201_1", 7m),
            Project("FA2026", 2026, SemesterTerm.Fall, "EXE201", "EXE201_1", null),
        ]);

        var latest = response.Semesters.Last();
        latest.AverageScore.Should().BeNull();
        latest.AverageScoreChange.Should().BeNull();
        latest.PotentialChange.Should().Be(0);
    }

    [Fact]
    public void Builder_OrdersSemestersByYearThenSpringSummerFall()
    {
        var response = ProjectDataTrendBuilder.Build(
        [
            Project("FA2025", 2025, SemesterTerm.Fall, "EXE201", "A", 5m),
            Project("SP2026", 2026, SemesterTerm.Spring, "EXE201", "A", 5m),
            Project("SU2025", 2025, SemesterTerm.Summer, "EXE201", "A", 5m),
        ]);

        response.Semesters.Select(row => row.Term + row.Year).Should().Equal("SU2025", "FA2025", "SP2026");
    }

    private readonly IApplicationDbContext _context = Substitute.For<IApplicationDbContext>();

    [Theory]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Mentor)]
    [InlineData("")]
    public async Task Handler_DeniesRolesOutsideAdminAndLecturerBeforeTouchingData(string role)
    {
        var result = await new GetProjectDataTrendsQueryHandler(_context)
            .HandleAsync(new GetProjectDataRequest(), Guid.NewGuid(), role);

        result.Error.Code.Should().Be(ErrorCodes.ProjectDataAccessDenied);
        _ = _context.DidNotReceiveWithAnyArgs().Projects;
    }

    [Fact]
    public async Task Handler_RejectsOverlongSearchBeforeTouchingData()
    {
        var result = await new GetProjectDataTrendsQueryHandler(_context)
            .HandleAsync(new GetProjectDataRequest { Search = new string('a', 101) }, Guid.NewGuid(), SystemRoles.Admin);

        result.Error.Code.Should().Be(ErrorCodes.ProjectDataValidationError);
        _ = _context.DidNotReceiveWithAnyArgs().Projects;
    }
}
