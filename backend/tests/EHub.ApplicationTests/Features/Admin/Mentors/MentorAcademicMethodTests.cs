using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Admin.Mentors;
using EHub.Contracts.Mentors;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Admin.Mentors;

/// <summary>AI matches business (Enterprise) mentors only; IT (Academic) mentors are filled without AI.</summary>
public sealed class MentorAcademicMethodTests
{
    private static AllocationTeam[] Teams(int count) => Enumerable.Range(1, count)
        .Select(index => new AllocationTeam(Guid.NewGuid(), Guid.Empty, $"T{index}", $"Team {index}")).ToArray();

    private static AllocationMentor[] Mentors(MentorType type, int count) => Enumerable.Range(1, count)
        .Select(index => new AllocationMentor(Guid.NewGuid(), $"{type}{index}", "", type)).ToArray();

    // Rankings exist for business mentors only, exactly what the server sends to the model in this mode.
    private static Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>> BusinessRankings(
        AllocationTeam[] teams, AllocationMentor[] business) => teams.ToDictionary(team => team.Id,
        team => (IReadOnlyDictionary<Guid, MentorFitRanking>)business.ToDictionary(mentor => mentor.Id,
            mentor => new MentorFitRanking(70 + Math.Abs((team.Id.GetHashCode() ^ mentor.Id.GetHashCode()) % 20), ["Business evidence"])));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Engine_UsesAiForBusinessAndTheSimpleStrategyForIt(bool balancedIt)
    {
        var academic = balancedIt ? AllocationStrategy.Balanced : AllocationStrategy.Random;
        var teams = Teams(8);
        var business = Mentors(MentorType.Enterprise, 4);
        var it = Mentors(MentorType.Academic, 4);

        var outcome = MentorAllocationEngine.Allocate(teams, [.. business, .. it], [], 5, AllocationStrategy.Ai,
            BusinessRankings(teams, business), default, academic);

        outcome.Warnings.Should().BeEmpty("IT mentors need no AI ranking in this mode");
        outcome.Proposals.Should().HaveCount(16);
        var businessRows = outcome.Proposals.Where(row => row.Mentor.Type == MentorType.Enterprise).ToArray();
        var itRows = outcome.Proposals.Where(row => row.Mentor.Type == MentorType.Academic).ToArray();
        businessRows.Should().HaveCount(8).And.OnlyContain(row => row.Ranking != null && row.Ranking.Reasons.Contains("Business evidence"));
        itRows.Should().HaveCount(8).And.OnlyContain(row => row.Ranking == null);
        businessRows.Select(row => row.Team.Id).Should().OnlyHaveUniqueItems();
        itRows.Select(row => row.Team.Id).Should().OnlyHaveUniqueItems();
        if (academic == AllocationStrategy.Balanced)
            itRows.GroupBy(row => row.Mentor.Id).Select(group => group.Count()).Should().OnlyContain(count => count == 2);
    }

    [Fact]
    public void Engine_DefaultStillAsksAiForBothMentorTypes()
    {
        var teams = Teams(2);
        var business = Mentors(MentorType.Enterprise, 2);
        var it = Mentors(MentorType.Academic, 2);

        var outcome = MentorAllocationEngine.Allocate(teams, [.. business, .. it], [], 5, AllocationStrategy.Ai,
            BusinessRankings(teams, business));

        outcome.Proposals.Should().HaveCount(2, "IT mentors without AI rankings are not proposed in the all-AI mode");
        outcome.Warnings.Should().Contain(message => message.Contains("Academic"));
    }

    [Fact]
    public void Engine_KeepsAnExistingItMentor_AndIsRepeatableForTheSameSeed()
    {
        var teams = Teams(3);
        var business = Mentors(MentorType.Enterprise, 2);
        var it = Mentors(MentorType.Academic, 2);
        var existing = new[] { new AllocationExistingAssignment(teams[0].Id, it[0].Id, MentorType.Academic) };
        var rankings = BusinessRankings(teams, business);

        var first = MentorAllocationEngine.Allocate(teams, [.. business, .. it], existing, 9, AllocationStrategy.Ai,
            rankings, default, AllocationStrategy.Random);
        var second = MentorAllocationEngine.Allocate(teams, [.. business, .. it], existing, 9, AllocationStrategy.Ai,
            rankings, default, AllocationStrategy.Random);

        first.Proposals.Count(row => row.Mentor.Type == MentorType.Academic).Should().Be(2);
        first.Proposals.Should().NotContain(row => row.Team.Id == teams[0].Id && row.Mentor.Type == MentorType.Academic);
        first.Proposals.Select(row => (row.Team.Id, row.Mentor.Id)).Should().Equal(second.Proposals.Select(row => (row.Team.Id, row.Mentor.Id)));
    }

    [Fact]
    public void Engine_BusinessAssignmentsAreTheSameAsInTheAllAiMode()
    {
        var teams = Teams(6);
        var business = Mentors(MentorType.Enterprise, 3);
        var it = Mentors(MentorType.Academic, 3);
        var rankings = BusinessRankings(teams, business);
        // Give the all-AI mode rankings for IT as well so both modes can produce a complete result.
        var both = rankings.ToDictionary(item => item.Key, item => (IReadOnlyDictionary<Guid, MentorFitRanking>)item.Value
            .Concat(it.Select(mentor => new KeyValuePair<Guid, MentorFitRanking>(mentor.Id, new MentorFitRanking(50, ["IT evidence"]))))
            .ToDictionary(pair => pair.Key, pair => pair.Value));

        var hybrid = MentorAllocationEngine.Allocate(teams, [.. business, .. it], [], 31, AllocationStrategy.Ai, rankings, default, AllocationStrategy.Balanced);
        var allAi = MentorAllocationEngine.Allocate(teams, [.. business, .. it], [], 31, AllocationStrategy.Ai, both);

        hybrid.Proposals.Where(row => row.Mentor.Type == MentorType.Enterprise).Select(row => (row.Team.Id, row.Mentor.Id)).Should()
            .BeEquivalentTo(allAi.Proposals.Where(row => row.Mentor.Type == MentorType.Enterprise).Select(row => (row.Team.Id, row.Mentor.Id)));
    }

    [Fact]
    public void BalanceSummary_CountsTemporaryItMentors_OnlyWhenTheyAreNotMatchedByAi()
    {
        var mentors = new[]
        {
            new AllocationMentor(Guid.NewGuid(), "B1", "", MentorType.Enterprise), new AllocationMentor(Guid.NewGuid(), "B2", "", MentorType.Enterprise, true),
            new AllocationMentor(Guid.NewGuid(), "I1", "", MentorType.Academic), new AllocationMentor(Guid.NewGuid(), "I2", "", MentorType.Academic, true)
        };

        var allAi = MentorBalanceSummaryBuilder.Build(10, mentors, [], AllocationStrategy.Ai);
        var hybrid = MentorBalanceSummaryBuilder.Build(10, mentors, [], AllocationStrategy.Ai, AllocationStrategy.Balanced);

        allAi.Single(item => item.MentorType == "Academic").EligibleMentorCount.Should().Be(1);
        hybrid.Single(item => item.MentorType == "Academic").EligibleMentorCount.Should().Be(2);
        hybrid.Single(item => item.MentorType == "Enterprise").EligibleMentorCount.Should().Be(1);
    }

    [Theory]
    [InlineData("Ai", true)]
    [InlineData("balanced", true)]
    [InlineData(" Random ", true)]
    [InlineData("", false)]
    [InlineData("Gemini", false)]
    public void Options_AcceptOnlyKnownMethods(string value, bool valid) =>
        new MentorAllocationOptions { AcademicMethod = value }.IsValid().Should().Be(valid);

    [Fact]
    public void Options_ByDefaultMatchBusinessMentorsWithAiAndKeepItMentorLoadsEven()
    {
        var options = new MentorAllocationOptions();

        options.AcademicMethod.Should().Be("Balanced");
        options.IsValid().Should().BeTrue();
    }

    [Fact]
    public void Engine_GivesEveryItMentorTheSameNumberOfTeams_WhenTheyDivideEvenly()
    {
        var teams = Teams(40);
        var business = Mentors(MentorType.Enterprise, 10);
        var it = Mentors(MentorType.Academic, 10);

        var outcome = MentorAllocationEngine.Allocate(teams, [.. business, .. it], [], 11, AllocationStrategy.Ai,
            BusinessRankings(teams, business), default, AllocationStrategy.Balanced);

        outcome.Proposals.Where(row => row.Mentor.Type == MentorType.Academic).GroupBy(row => row.Mentor.Id)
            .Select(group => group.Count()).Should().HaveCount(10).And.OnlyContain(count => count == 4);
        outcome.Proposals.Where(row => row.Mentor.Type == MentorType.Enterprise).GroupBy(row => row.Mentor.Id)
            .Select(group => group.Count()).Should().HaveCount(10).And.OnlyContain(count => count == 4);
    }

    [Theory]
    [InlineData(-1, 65, false)]
    [InlineData(6, 65, false)]
    [InlineData(0, 0, true)]
    [InlineData(5, 300, true)]
    [InlineData(2, 301, false)]
    public void Options_BoundTheAutomaticRetries(int retries, int delaySeconds, bool valid) =>
        new MentorAllocationOptions { MaxProviderRetries = retries, ProviderRetryDelaySeconds = delaySeconds }.IsValid().Should().Be(valid);

    [Fact]
    public async Task Handler_SendsOnlyBusinessMentorsToTheModel_AndStillAssignsItMentors()
    {
        await using var db = Context();
        var (team, _, _) = await SeedAsync(db);
        var ranker = RecordingRanker(out var seenTypes);

        var result = await Handler(db, ranker, MentorAllocationOptions.Balanced).PreviewAllocationAsync(Request(team), default);

        result.IsSuccess.Should().BeTrue();
        seenTypes.Should().NotBeEmpty().And.OnlyContain(type => type == MentorType.Enterprise);
        var rows = result.Value!.Assignments;
        rows.Select(row => row.MentorType).Should().BeEquivalentTo(["Enterprise", "Academic"]);
        result.Value.CanCommit.Should().BeTrue();
        result.Value.UnfilledAcademicCount.Should().Be(0);
        result.Value.Warnings.Should().Contain(message => message.Contains("AI chỉ chấm mentor Business"));
    }

    [Fact]
    public async Task Handler_ByDefault_AiMatchesOnlyBusinessMentors_AndItMentorsAreEvenlyLoaded()
    {
        await using var db = Context();
        var (team, _, _) = await SeedAsync(db);
        var ranker = RecordingRanker(out var seenTypes);

        var result = await Handler(db, ranker, null).PreviewAllocationAsync(Request(team), default);

        result.IsSuccess.Should().BeTrue();
        seenTypes.Should().NotBeEmpty().And.OnlyContain(type => type == MentorType.Enterprise);
        result.Value!.Assignments.Select(row => row.MentorType).Should().BeEquivalentTo(["Enterprise", "Academic"]);
        result.Value.Warnings.Should().Contain(message => message.Contains("mentor IT được chia đều theo tải"));
    }

    [Fact]
    public async Task Handler_WhenConfiguredForAi_SendsBothMentorTypesToTheModel()
    {
        await using var db = Context();
        var (team, _, _) = await SeedAsync(db);
        var ranker = RecordingRanker(out var seenTypes);

        var result = await Handler(db, ranker, MentorAllocationOptions.Ai).PreviewAllocationAsync(Request(team), default);

        result.IsSuccess.Should().BeTrue();
        seenTypes.Should().Contain(MentorType.Enterprise).And.Contain(MentorType.Academic);
        result.Value!.Warnings.Should().NotContain(message => message.Contains("AI chỉ chấm mentor Business"));
    }

    [Fact]
    public async Task Handler_BalancedAndRandomStrategiesAreUnaffectedByTheSetting()
    {
        await using var db = Context();
        var (team, _, _) = await SeedAsync(db);
        var ranker = RecordingRanker(out var seenTypes);
        var request = new PreviewMentorAllocationRequest
        {
            SemesterId = team.Class.SemesterId, ClassIds = [team.ClassId], Strategy = MentorAllocationStrategies.Balanced, Seed = 5
        };

        var result = await Handler(db, ranker, MentorAllocationOptions.Random).PreviewAllocationAsync(request, default);

        result.IsSuccess.Should().BeTrue();
        seenTypes.Should().BeEmpty("non-AI strategies never call the model");
        result.Value!.Assignments.Should().HaveCount(2);
    }

    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Team Team, MentorProfile Business, MentorProfile It)> SeedAsync(AppDbContext db)
    {
        var semester = new Semester { Code = "AM", Name = "Synthetic semester", Year = 2026 };
        var team = new Team
        {
            TeamName = "Selected", TeamCode = "Selected", Description = "Marketing platform",
            Class = new Class { ClassCode = "Selected", Semester = semester, Course = new Course { Code = "EXE101", Name = "Startup" }, Status = ClassStatus.Active }
        };
        db.Teams.Add(team);
        var business = AddMentor(db, semester, "Business mentor", MentorType.Enterprise);
        var it = AddMentor(db, semester, "IT mentor", MentorType.Academic);
        await db.SaveChangesAsync();
        return (team, business, it);
    }

    private static MentorProfile AddMentor(AppDbContext db, Semester semester, string name, MentorType type)
    {
        var user = new User { FullName = name, Email = $"{Guid.NewGuid():N}@example.test" };
        var profile = new MentorProfile { User = user, UserId = user.Id, Type = type, Expertise = ["Marketing"] };
        db.MentorProfiles.Add(profile);
        db.SemesterStaffAssignments.Add(new SemesterStaffAssignment
        {
            Semester = semester, User = user, UserId = user.Id, Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active
        });
        return profile;
    }

    private static PreviewMentorAllocationRequest Request(Team team) => new()
    {
        SemesterId = team.Class.SemesterId, ClassIds = [team.ClassId], Strategy = MentorAllocationStrategies.Ai, Seed = 5
    };

    private static IMentorRecommendationRanker RecordingRanker(out List<MentorType> seenTypes)
    {
        var seen = new List<MentorType>();
        seenTypes = seen;
        var ranker = Substitute.For<IMentorRecommendationRanker>();
        ranker.RankForAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var profiles = call.ArgAt<IReadOnlyCollection<MentorProfile>>(1);
                seen.AddRange(profiles.Select(profile => profile.Type));
                return profiles.ToDictionary(profile => profile.Id, _ => new MentorFitRanking(80, ["Matching evidence"]));
            });
        return ranker;
    }

    private static MentorAdminHandler Handler(AppDbContext db, IMentorRecommendationRanker ranker, string? academicMethod)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(Guid.NewGuid());
        user.IsAuthenticated.Returns(true);
        user.Roles.Returns([SystemRoles.Admin]);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc));
        var options = academicMethod is null ? null : Options.Create(new MentorAllocationOptions { AcademicMethod = academicMethod });
        return new MentorAdminHandler(db, user, Substitute.For<IPasswordHasher>(), clock,
            Substitute.For<IUnitOfWork>(), NullLogger<MentorAdminHandler>.Instance, ranker, options);
    }
}
