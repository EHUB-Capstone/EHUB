using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Application.Features.Mentoring;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure;
using EHub.Infrastructure.Mentoring;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentorTrainedRecommendationTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("Ollama", false)]
    [InlineData("Trained", true)]
    [InlineData("trained", true)]
    [InlineData("Hybrid", true)]
    public void InfrastructureRegistersTheTrainedSingletonOnlyWhenSelected(string? provider, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mentoring:MatchingProvider"] = provider
        }).Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        var descriptors = services.Where(x => x.ServiceType == typeof(IMentorFitRanker)).ToArray();
        if (!expected)
        {
            descriptors.Should().BeEmpty();
            return;
        }
        descriptors.Should().ContainSingle();
        descriptors[0].ImplementationType.Should().Be(typeof(FileMentorFitRanker));
        descriptors[0].Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void InfrastructureRejectsUnknownMatchingProviders()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mentoring:MatchingProvider"] = "Unknown"
        }).Build();
        var services = new ServiceCollection();

        Action register = () => services.AddInfrastructure(configuration);

        register.Should().Throw<InvalidOperationException>().WithMessage("Mentoring:MatchingProvider must be Ollama, Trained or Hybrid.");
        services.Should().NotContain(x => x.ServiceType == typeof(IMentorFitRanker));
    }

    [Fact]
    public async Task TrainedRankingBypassesEmbeddingsAndKeepsWorkloadInformational()
    {
        await using var db = Context();
        var (team, mentor, lecturerId) = await SeedAsync(db);
        var other = AddMentor(db, team.Class.Semester, "Other", MentorType.Academic);
        db.MentorAssignments.Add(new MentorAssignment
        {
            MentorProfile = mentor, Team = new Team { TeamName = "Other team", TeamCode = "OTHER", Class = team.Class }
        });
        await db.SaveChangesAsync();
        var ranker = Substitute.For<IMentorFitRanker>();
        ranker.Rank(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyCollection<MentorProfile>>(1).ToDictionary(x => x.Id,
                x => new MentorFitRanking(x.Id == mentor.Id ? 90 : 20, ["Professional evidence"])));
        var embeddings = Substitute.For<IMentorEmbeddingSearch>();

        var result = await Handler(db, embeddings, ranker).RecommendAsync(team.Id, lecturerId, SystemRoles.Lecturer, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(x => x.Mentor.Id).Should().Equal(mentor.Id, other.Id);
        result.Value.First().FitScore.Should().Be(90);
        result.Value.First().ActiveTeamCount.Should().Be(1);
        result.Value.First().HasCapacity.Should().BeTrue();
        result.Value.First().Reasons.Should().Contain(["Professional evidence", "Đang phụ trách 1 nhóm"]);
        await embeddings.DidNotReceiveWithAnyArgs().SimilaritiesAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Mentor)]
    [InlineData(SystemRoles.Lecturer)]
    public async Task UnauthorizedResourceAccessStopsBeforeTheTrainedModel(string role)
    {
        await using var db = Context();
        var (team, _, _) = await SeedAsync(db);
        var ranker = Substitute.For<IMentorFitRanker>();

        var result = await Handler(db, Substitute.For<IMentorEmbeddingSearch>(), ranker)
            .RecommendAsync(team.Id, Guid.NewGuid(), role, default);

        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
        ranker.DidNotReceiveWithAnyArgs().Rank(default!, default!, default);
    }

    [Fact]
    public async Task ProjectNameAloneDoesNotInvokeTheTrainedModel()
    {
        await using var db = Context();
        var (team, _, lecturerId) = await SeedAsync(db);
        team.Description = "";
        team.TeamName = "Marketing finance project";
        await db.SaveChangesAsync();
        var ranker = Substitute.For<IMentorFitRanker>();

        var result = await Handler(db, Substitute.For<IMentorEmbeddingSearch>(), ranker)
            .RecommendAsync(team.Id, lecturerId, SystemRoles.Lecturer, default);

        result.Error.Code.Should().Be(ErrorCodes.CommonValidationError);
        ranker.DidNotReceiveWithAnyArgs().Rank(default!, default!, default);
    }

    [Fact]
    public async Task OnlyActiveProfilesAccountsAndSemesterMembersReachTheTrainedModel()
    {
        await using var db = Context();
        var (team, mentor, lecturerId) = await SeedAsync(db);
        AddMentor(db, team.Class.Semester, "Inactive profile").Status = MentorProfileStatus.Inactive;
        AddMentor(db, team.Class.Semester, "Inactive account").User.Status = UserStatus.Inactive;
        var inactiveMember = AddMentor(db, team.Class.Semester, "Inactive membership");
        db.ChangeTracker.Entries<SemesterStaffAssignment>().Single(x => x.Entity.UserId == inactiveMember.UserId)
            .Entity.Status = SemesterStaffStatus.Inactive;
        AddMentor(db, new Semester { Code = "OTHER", Name = "Other semester", Year = 2027 }, "Other semester");
        db.MentorProfiles.Add(new MentorProfile { User = new User { FullName = "Unlisted", Email = "unlisted@example.test" } });
        await db.SaveChangesAsync();
        var ranker = ConstantRanker();

        var result = await Handler(db, Substitute.For<IMentorEmbeddingSearch>(), ranker)
            .RecommendAsync(team.Id, lecturerId, SystemRoles.Lecturer, default);

        result.Value.Should().ContainSingle().Which.Mentor.Id.Should().Be(mentor.Id);
        ranker.Received(1).Rank(Arg.Any<string>(),
            Arg.Is<IReadOnlyCollection<MentorProfile>>(items => items != null && items.Count == 1 && items.Single().Id == mentor.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnavailableTrainedModelReturnsTheSafeServiceError()
    {
        await using var db = Context();
        var (team, _, lecturerId) = await SeedAsync(db);
        var ranker = Substitute.For<IMentorFitRanker>();
        ranker.Rank(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyDictionary<Guid, MentorFitRanking>>(_ => throw new MentorFitRankerUnavailableException());
        var embeddings = Substitute.For<IMentorEmbeddingSearch>();

        var result = await Handler(db, embeddings, ranker).RecommendAsync(team.Id, lecturerId, SystemRoles.Lecturer, default);

        result.Error.Code.Should().Be(ErrorCodes.MentorMatchingUnavailable);
        result.Error.Message.Should().Be("Mô hình gợi ý mentor tạm thời chưa sẵn sàng.");
        await embeddings.DidNotReceiveWithAnyArgs().SimilaritiesAsync(default!, default!, default);
    }

    [Fact]
    public async Task TemporaryAssignmentExcludesItsOccupiedMentorTypeBeforeRanking()
    {
        await using var db = Context();
        var (team, _, lecturerId) = await SeedAsync(db);
        var academic = AddMentor(db, team.Class.Semester, "Academic", MentorType.Academic);
        db.TemporaryMentorAssignments.Add(new TemporaryMentorAssignment
        {
            Team = team, Slot = MentorType.Enterprise,
            Draft = new MentorImportDraft { FullName = "Temporary", Type = MentorType.Enterprise }
        });
        await db.SaveChangesAsync();
        var ranker = ConstantRanker();

        var result = await Handler(db, Substitute.For<IMentorEmbeddingSearch>(), ranker)
            .RecommendAsync(team.Id, lecturerId, SystemRoles.Lecturer, default);

        result.Value.Should().ContainSingle().Which.Mentor.Id.Should().Be(academic.Id);
        ranker.Received(1).Rank(Arg.Any<string>(),
            Arg.Is<IReadOnlyCollection<MentorProfile>>(items => items != null && items.All(x => x.Type == MentorType.Academic)),
            Arg.Any<CancellationToken>());
    }

    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task OnlyTitleAndDescriptionReachTheRanker()
    {
        await using var db = Context();
        var (team, _, lecturerId) = await SeedAsync(db);
        team.Project = new Project
        {
            Name = "Project title", Description = "Project description",
            Technology = "EXCLUDED_TECHNOLOGY", StartupField = "EXCLUDED_FIELD",
            Problem = "EXCLUDED_PROBLEM", Solution = "EXCLUDED_SOLUTION"
        };
        team.Description = "EXCLUDED_TEAM_FALLBACK";
        db.Projects.Add(team.Project);
        await db.SaveChangesAsync();
        var ranker = ConstantRanker();

        var result = await Handler(db, Substitute.For<IMentorEmbeddingSearch>(), ranker)
            .RecommendAsync(team.Id, lecturerId, SystemRoles.Lecturer, default);

        result.IsSuccess.Should().BeTrue();
        ranker.Received(1).Rank("Project description\nProject title", Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProfilesWithoutMatchingEvidenceAreOmittedFromRecommendations()
    {
        await using var db = Context();
        var (team, _, lecturerId) = await SeedAsync(db);
        var ranker = Substitute.For<IMentorFitRanker>();
        ranker.Rank(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyCollection<MentorProfile>>(1).ToDictionary(x => x.Id,
                _ => new MentorFitRanking(0, ["No evidence"], false)));

        var result = await Handler(db, Substitute.For<IMentorEmbeddingSearch>(), ranker)
            .RecommendAsync(team.Id, lecturerId, SystemRoles.Lecturer, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task UnroundedModelValuesResolveDisplayedScoreTiesBeforeNames()
    {
        await using var db = Context();
        var (team, first, lecturerId) = await SeedAsync(db);
        first.User.FullName = "A weaker match";
        var stronger = AddMentor(db, team.Class.Semester, "Z stronger match");
        await db.SaveChangesAsync();
        var ranker = Substitute.For<IMentorFitRanker>();
        ranker.Rank(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, MentorFitRanking>
            {
                [first.Id] = new(80, [], true, 80.01), [stronger.Id] = new(80, [], true, 80.49)
            });
        var result = await Handler(db, Substitute.For<IMentorEmbeddingSearch>(), ranker)
            .RecommendAsync(team.Id, lecturerId, SystemRoles.Lecturer, default);
        result.Value.First().Mentor.Id.Should().Be(stronger.Id);
    }

    private static async Task<(Team Team, MentorProfile Mentor, Guid LecturerId)> SeedAsync(AppDbContext db)
    {
        var lecturerId = Guid.NewGuid();
        var semester = new Semester { Code = "TRAIN", Name = "Synthetic training semester", Year = 2026 };
        var team = new Team { TeamName = "Synthetic project", TeamCode = "TRAIN", Description = "Marketing finance product",
            Class = new Class { ClassCode = "TRAIN", PrimaryLecturerId = lecturerId, Semester = semester,
                Course = new Course { Code = "EXE101", Name = "Startup" }, Status = ClassStatus.Active } };
        db.Teams.Add(team);
        var mentor = AddMentor(db, semester, "Eligible");
        await db.SaveChangesAsync();
        return (team, mentor, lecturerId);
    }

    private static MentorProfile AddMentor(AppDbContext db, Semester semester, string name, MentorType type = MentorType.Enterprise)
    {
        var user = new User { FullName = name, Email = $"{Guid.NewGuid():N}@example.test" };
        var mentor = new MentorProfile { User = user, UserId = user.Id, Type = type, Expertise = ["Marketing"] };
        db.MentorProfiles.Add(mentor);
        db.SemesterStaffAssignments.Add(new SemesterStaffAssignment { Semester = semester, User = user, UserId = user.Id,
            Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active });
        return mentor;
    }

    private static IMentorFitRanker ConstantRanker()
    {
        var ranker = Substitute.For<IMentorFitRanker>();
        ranker.Rank(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyCollection<MentorProfile>>(1).ToDictionary(x => x.Id,
                _ => new MentorFitRanking(80, ["Professional evidence"])));
        return ranker;
    }

    private static MentorProfileHandler Handler(AppDbContext db, IMentorEmbeddingSearch embeddings, IMentorFitRanker ranker) =>
        new(db, Substitute.For<IMentorDocumentStorageService>(), embeddings, Substitute.For<IMentorCvTextReader>(), ranker);
}
