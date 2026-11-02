using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Admin.Mentors;
using EHub.Contracts.Mentors;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Admin.Mentors;

public sealed class MentorAiAllocationTests
{
    [Fact]
    public void Engine_PicksEvidenceForEachTeamAndType_WithoutReplacingExistingSlots()
    {
        var first = new AllocationTeam(Guid.NewGuid(), Guid.NewGuid(), "A", "First");
        var second = new AllocationTeam(Guid.NewGuid(), first.ClassId, "B", "Second");
        var business = new AllocationMentor(Guid.NewGuid(), "Business", "", MentorType.Enterprise);
        var alternative = new AllocationMentor(Guid.NewGuid(), "Alternative", "", MentorType.Enterprise);
        var academic = new AllocationMentor(Guid.NewGuid(), "Academic", "", MentorType.Academic);
        var rankings = new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>
        {
            [first.Id] = new Dictionary<Guid, MentorFitRanking>
            {
                [business.Id] = new(90, ["Business evidence"]), [alternative.Id] = new(20, []), [academic.Id] = new(99, [])
            },
            [second.Id] = new Dictionary<Guid, MentorFitRanking>
            {
                [business.Id] = new(30, []), [alternative.Id] = new(95, ["Other evidence"]), [academic.Id] = new(99, [])
            }
        };

        var result = MentorAllocationEngine.Allocate([first, second], [business, alternative, academic],
            [new(first.Id, academic.Id, MentorType.Academic)], 3, AllocationStrategy.Ai, rankings);

        result.Proposals.Should().HaveCount(3);
        result.Proposals.Single(row => row.Team.Id == first.Id).Mentor.Id.Should().Be(business.Id);
        result.Proposals.Single(row => row.Team.Id == second.Id && row.Mentor.Type == MentorType.Enterprise)
            .Mentor.Id.Should().Be(alternative.Id);
        result.Proposals.Single(row => row.Team.Id == first.Id).Ranking!.Reasons.Should().Contain("Business evidence");
    }

    [Fact]
    public void Engine_ProposesUnprovenProfilesForReview_ButNeverAllocatesTemporaryProfiles()
    {
        var team = new AllocationTeam(Guid.NewGuid(), Guid.NewGuid(), "A", "First");
        var unproven = new AllocationMentor(Guid.NewGuid(), "Unproven", "", MentorType.Enterprise);
        var temporary = new AllocationMentor(Guid.NewGuid(), "Temporary", "", MentorType.Academic, true);
        var rankings = new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>
        {
            [team.Id] = new Dictionary<Guid, MentorFitRanking>
            {
                [unproven.Id] = new(100, [], false), [temporary.Id] = new(100, [])
            }
        };

        var result = MentorAllocationEngine.Allocate([team], [unproven, temporary], [], 5, AllocationStrategy.Ai, rankings);

        var proposal = result.Proposals.Should().ContainSingle().Subject;
        proposal.Mentor.Id.Should().Be(unproven.Id);
        proposal.Ranking!.Score.Should().Be(0);
        proposal.Ranking.RankingValue.Should().Be(0);
        proposal.Ranking.HasEvidence.Should().BeFalse();
        proposal.Ranking.Reasons.Should().Contain(reason => reason.Contains("cần admin rà soát"));
        result.Warnings.Should().HaveCount(2);
        result.Warnings.Should().Contain(message => message.Contains("chưa có bằng chứng chuyên môn phù hợp"));
    }

    [Fact]
    public void Engine_FillsEveryUnprovenSlotAndBalancesFallbackLoad_WithRepeatableSeed()
    {
        var classId = Guid.NewGuid();
        var teams = Enumerable.Range(1, 8).Select(index =>
            new AllocationTeam(Guid.NewGuid(), classId, $"P{index}", $"Project {index}")).ToArray();
        var mentors = Enumerable.Range(1, 3).Select(index =>
            new AllocationMentor(Guid.NewGuid(), $"Mentor {index}", "", MentorType.Enterprise)).ToArray();
        var rankings = teams.ToDictionary(team => team.Id, _ =>
            (IReadOnlyDictionary<Guid, MentorFitRanking>)mentors.ToDictionary(mentor => mentor.Id,
                _ => new MentorFitRanking(0, ["No matching evidence"], false, 0)));

        var first = MentorAllocationEngine.Allocate(teams, mentors, [], 19, AllocationStrategy.Ai, rankings);
        var second = MentorAllocationEngine.Allocate(teams, mentors, [], 19, AllocationStrategy.Ai, rankings);

        first.Proposals.Should().HaveCount(8);
        first.Proposals.Select(proposal => proposal.Team.Id).Should().OnlyHaveUniqueItems();
        first.Proposals.Should().OnlyContain(proposal => proposal.Ranking != null &&
            proposal.Ranking.Score == 0 && !proposal.Ranking.HasEvidence);
        var loads = first.Proposals.GroupBy(proposal => proposal.Mentor.Id).Select(group => group.Count()).ToArray();
        loads.Should().HaveCount(3);
        (loads.Max() - loads.Min()).Should().BeLessThanOrEqualTo(1);
        first.Proposals.Select(proposal => (proposal.Team.Id, proposal.Mentor.Id))
            .Should().Equal(second.Proposals.Select(proposal => (proposal.Team.Id, proposal.Mentor.Id)));
    }

    [Fact]
    public void Engine_PrefersGenuineEvidenceEvenAtZeroScore_ToAnyUnprovenCandidate()
    {
        var team = new AllocationTeam(Guid.NewGuid(), Guid.NewGuid(), "A", "Project");
        var evidenced = new AllocationMentor(Guid.NewGuid(), "Evidenced", "", MentorType.Enterprise);
        var unproven = new AllocationMentor(Guid.NewGuid(), "Unproven", "", MentorType.Enterprise);
        var rankings = new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>
        {
            [team.Id] = new Dictionary<Guid, MentorFitRanking>
            {
                [evidenced.Id] = new(0, ["Specific work evidence"], true, 0),
                [unproven.Id] = new(100, [], false, 100)
            }
        };
        var existing = new[] { new AllocationExistingAssignment(Guid.NewGuid(), evidenced.Id, MentorType.Enterprise) };

        var result = MentorAllocationEngine.Allocate([team], [evidenced, unproven], existing, 9, AllocationStrategy.Ai, rankings);

        var proposal = result.Proposals.Should().ContainSingle().Subject;
        proposal.Mentor.Id.Should().Be(evidenced.Id);
        proposal.Ranking!.HasEvidence.Should().BeTrue();
        proposal.Ranking.Reasons.Should().Equal("Specific work evidence");
        result.Warnings.Should().NotContain(message => message.Contains("cần rà soát"));
    }

    [Fact]
    public void Engine_FallbackUsesSemesterLoad_AndPreservesAnExistingSlot()
    {
        var occupied = new AllocationTeam(Guid.NewGuid(), Guid.NewGuid(), "Existing", "Existing project");
        var missing = new AllocationTeam(Guid.NewGuid(), occupied.ClassId, "Missing", "New project");
        var busy = new AllocationMentor(Guid.NewGuid(), "Busy", "", MentorType.Enterprise);
        var free = new AllocationMentor(Guid.NewGuid(), "Free", "", MentorType.Enterprise);
        var rankings = new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>
        {
            [missing.Id] = new Dictionary<Guid, MentorFitRanking>
            {
                [busy.Id] = new(0, [], false), [free.Id] = new(0, [], false)
            }
        };
        var existing = new[]
        {
            new AllocationExistingAssignment(occupied.Id, busy.Id, MentorType.Enterprise),
            new AllocationExistingAssignment(Guid.NewGuid(), busy.Id, MentorType.Enterprise)
        };

        var result = MentorAllocationEngine.Allocate([occupied, missing], [busy, free], existing, 11, AllocationStrategy.Ai, rankings);

        var proposal = result.Proposals.Should().ContainSingle().Subject;
        proposal.Team.Id.Should().Be(missing.Id);
        proposal.Mentor.Id.Should().Be(free.Id);
        proposal.ResultingLoad.Should().Be(1);
    }

    [Theory]
    [InlineData("missing-team")]
    [InlineData("missing-candidate")]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("invalid-score")]
    public void Engine_DoesNotUseFallbackForIncompleteOrInvalidModelResults(string invalid)
    {
        var team = new AllocationTeam(Guid.NewGuid(), Guid.NewGuid(), "A", "Project");
        var first = new AllocationMentor(Guid.NewGuid(), "First", "", MentorType.Enterprise);
        var second = new AllocationMentor(Guid.NewGuid(), "Second", "", MentorType.Enterprise);
        var scores = new Dictionary<Guid, MentorFitRanking> { [first.Id] = new(0, [], false) };
        if (invalid != "missing-candidate") scores[second.Id] = invalid switch
        {
            "nan" => new(0, [], false, double.NaN),
            "infinity" => new(0, [], false, double.PositiveInfinity),
            "invalid-score" => new(101, [], false),
            _ => new(0, [], false)
        };
        var rankings = new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>();
        if (invalid != "missing-team") rankings[team.Id] = scores;

        var result = MentorAllocationEngine.Allocate([team], [first, second], [], 17, AllocationStrategy.Ai, rankings);

        result.Proposals.Should().BeEmpty();
    }

    [Fact]
    public void Engine_LeavesSlotsWithoutEligibleProfilesUnfilled_InsteadOfBorrowingAnotherType()
    {
        var team = new AllocationTeam(Guid.NewGuid(), Guid.NewGuid(), "A", "Project");
        var enterprise = new AllocationMentor(Guid.NewGuid(), "Enterprise", "", MentorType.Enterprise);
        var temporaryAcademic = new AllocationMentor(Guid.NewGuid(), "Temporary", "", MentorType.Academic, true);
        var rankings = new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>
        {
            [team.Id] = new Dictionary<Guid, MentorFitRanking> { [enterprise.Id] = new(0, [], false) }
        };

        var result = MentorAllocationEngine.Allocate([team], [enterprise, temporaryAcademic], [], 17, AllocationStrategy.Ai, rankings);

        result.Proposals.Should().ContainSingle().Which.Mentor.Type.Should().Be(MentorType.Enterprise);
        result.Proposals.Should().NotContain(proposal => proposal.Mentor.IsTemporary);
    }

    [Fact]
    public void Engine_UsesPreciseFitBeforeWorkload_ThenBalancesTiesAcrossOtherClasses()
    {
        var team = new AllocationTeam(Guid.NewGuid(), Guid.NewGuid(), "A", "First");
        var busy = new AllocationMentor(Guid.NewGuid(), "Busy", "", MentorType.Enterprise);
        var free = new AllocationMentor(Guid.NewGuid(), "Free", "", MentorType.Enterprise);
        var existing = new[] { new AllocationExistingAssignment(Guid.NewGuid(), busy.Id, MentorType.Enterprise) };
        var fits = new Dictionary<Guid, MentorFitRanking>
        {
            [busy.Id] = new(80, [], true, 80.49), [free.Id] = new(80, [], true, 80.01)
        };
        var rankings = new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>> { [team.Id] = fits };

        var precise = MentorAllocationEngine.Allocate([team], [busy, free], existing, 7, AllocationStrategy.Ai, rankings);
        precise.Proposals.Single().Mentor.Id.Should().Be(busy.Id);
        fits[free.Id] = new(80, [], true, 80.49);
        var tied = MentorAllocationEngine.Allocate([team], [busy, free], existing, 7, AllocationStrategy.Ai, rankings);
        tied.Proposals.Single().Mentor.Id.Should().Be(free.Id);
    }

    [Fact]
    public async Task ClassPreview_RestrictsTeamsAndCandidates_ButCountsSemesterWorkload()
    {
        await using var db = Context();
        var (selected, busy, free) = await SeedAsync(db);
        var outside = AddTeam(db, selected.Class.Semester, "Other class");
        db.MentorAssignments.Add(new MentorAssignment { Team = outside, MentorProfile = busy, Slot = MentorType.Enterprise });
        AddMentor(db, new Semester { Code = "OTHER", Name = "Other semester", Year = 2025 }, "Wrong semester");
        var inactive = AddMentor(db, selected.Class.Semester, "Inactive");
        inactive.Status = MentorProfileStatus.Inactive;
        await db.SaveChangesAsync();
        var ranker = ConstantRanker();

        var result = await Handler(db, ranker).PreviewAllocationAsync(Request(selected));

        result.IsSuccess.Should().BeTrue();
        result.Value.Strategy.Should().Be("Ai");
        result.Value.Assignments.Should().ContainSingle();
        var row = result.Value.Assignments.Single();
        row.TeamId.Should().Be(selected.Id);
        row.MentorProfileId.Should().Be(free.Id);
        row.FitScore.Should().Be(80);
        row.Reasons.Should().Contain("Matching evidence");
        result.Value.MentorLoads.Single(item => item.MentorProfileId == busy.Id).TotalBefore.Should().Be(1);
        db.MentorAllocationSessions.Should().ContainSingle();
        await ranker.Received(1).RankForAllocationAsync("Marketing platform\nSelected", Arg.Is<IReadOnlyCollection<MentorProfile>>(
            profiles => profiles != null && profiles.Count == 2 && profiles.All(profile => profile.Id == busy.Id || profile.Id == free.Id)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SemesterPreview_RanksEveryActiveClass_AndPreservesExistingAssignments()
    {
        await using var db = Context();
        var (selected, busy, _) = await SeedAsync(db);
        var outside = AddTeam(db, selected.Class.Semester, "Other class");
        var occupied = AddTeam(db, selected.Class.Semester, "Occupied");
        db.MentorAssignments.Add(new MentorAssignment { Team = occupied, MentorProfile = busy, Slot = MentorType.Enterprise });
        await db.SaveChangesAsync();
        var ranker = ConstantRanker();

        var result = await Handler(db, ranker).PreviewAllocationAsync(new PreviewMentorAllocationRequest
        {
            SemesterId = selected.Class.SemesterId, Strategy = MentorAllocationStrategies.Ai, Seed = 4
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Assignments.Select(row => row.TeamId).Should().BeEquivalentTo([selected.Id, outside.Id]);
        result.Value.ExistingAssignments.Should().ContainSingle(row => row.TeamId == occupied.Id);
        await ranker.Received(2).RankForAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preview_AppliesManualThenInheritanceBeforeRankingTheRemainingTeams()
    {
        await using var db = Context();
        var (target, previousMentor, aiMentor) = await SeedAsync(db);
        target.Class.Course.Code = "EXE201";
        var previousSemester = new Semester { Code = "PAST", Name = "Previous semester", Year = 2025 };
        var source = AddTeam(db, previousSemester, "Source");
        target.PreviousTeamId = source.Id;
        db.MentorAssignments.Add(new MentorAssignment { Team = source, MentorProfile = previousMentor, Slot = MentorType.Enterprise });
        var manualTeam = new Team { Class = target.Class, TeamCode = "MANUAL", TeamName = "Manual", Description = "Manual description" };
        var aiTeam = new Team { Class = target.Class, TeamCode = "AI", TeamName = "Automatic", Description = "Automatic description" };
        db.Teams.AddRange(manualTeam, aiTeam);
        await db.SaveChangesAsync();
        var ranker = Substitute.For<IMentorRecommendationRanker>();
        ranker.RankForAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, MentorFitRanking> { [aiMentor.Id] = new(95, ["Project evidence"]) });

        var result = await Handler(db, ranker).PreviewAllocationAsync(new PreviewMentorAllocationRequest
        {
            SemesterId = target.Class.SemesterId, ClassIds = [target.ClassId], Strategy = MentorAllocationStrategies.Ai,
            Edits = [new MentorAllocationEdit { TeamId = manualTeam.Id, MentorType = "Enterprise", MentorProfileId = previousMentor.Id }]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Assignments.Should().HaveCount(3);
        result.Value.Assignments.Single(row => row.TeamId == target.Id).Source.Should().Be(MentorAllocationSources.Retained);
        result.Value.Assignments.Single(row => row.TeamId == target.Id).MentorProfileId.Should().Be(previousMentor.Id);
        result.Value.Assignments.Single(row => row.TeamId == manualTeam.Id).Source.Should().Be(MentorAllocationSources.Manual);
        result.Value.Assignments.Single(row => row.TeamId == aiTeam.Id).MentorProfileId.Should().Be(aiMentor.Id);
        await ranker.Received(1).RankForAllocationAsync("Automatic description\nAutomatic", Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MissingEvidence_ProposesForReviewWithoutInventingAFitScore()
    {
        await using var db = Context();
        var (selected, _, _) = await SeedAsync(db);
        var ranker = Substitute.For<IMentorRecommendationRanker>();
        ranker.RankForAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyCollection<MentorProfile>>(1).ToDictionary(profile => profile.Id,
                _ => new MentorFitRanking(100, [], false)));

        var noEvidence = await Handler(db, ranker).PreviewAllocationAsync(Request(selected));

        noEvidence.IsSuccess.Should().BeTrue();
        noEvidence.Value.CanCommit.Should().BeTrue();
        var row = noEvidence.Value.Assignments.Should().ContainSingle().Subject;
        row.FitScore.Should().Be(0);
        row.Reasons.Should().Contain(reason => reason.Contains("cần admin rà soát"));
        noEvidence.Value.UnfilledEnterpriseCount.Should().Be(0);
        db.MentorAllocationSessions.Should().ContainSingle();
        db.MentorAssignments.Should().BeEmpty("preview still requires admin acceptance");
    }

    [Fact]
    public async Task MissingDescription_RanksTheAvailableTitleAndProposesForReview()
    {
        await using var db = Context();
        var (selected, _, _) = await SeedAsync(db);
        var ranker = Substitute.For<IMentorRecommendationRanker>();
        ranker.RankForAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyCollection<MentorProfile>>(1).ToDictionary(profile => profile.Id,
                _ => new MentorFitRanking(0, [], false)));
        selected.Description = "";
        await db.SaveChangesAsync();
        var noDescription = await Handler(db, ranker).PreviewAllocationAsync(Request(selected));

        noDescription.IsSuccess.Should().BeTrue();
        noDescription.Value.CanCommit.Should().BeTrue();
        noDescription.Value.UnfilledEnterpriseCount.Should().Be(0);
        var row = noDescription.Value.Assignments.Should().ContainSingle().Subject;
        row.FitScore.Should().Be(0);
        row.Reasons.Should().Contain(reason => reason.Contains("cần admin rà soát"));
        db.MentorAllocationSessions.Should().ContainSingle();
        db.MentorAssignments.Should().BeEmpty("admin must still accept the preview");
        await ranker.Received(1).RankForAllocationAsync("\nSelected", Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("trained")]
    [InlineData("embedding")]
    [InlineData("timeout")]
    [InlineData("unconfigured")]
    public async Task ProviderUnavailable_DoesNotPersistAPartialOrRandomPreview(string failure)
    {
        await using var db = Context();
        var (selected, _, _) = await SeedAsync(db);
        var ranker = Substitute.For<IMentorRecommendationRanker>();
        Exception error = failure switch
        {
            "embedding" => new MentorEmbeddingUnavailableException(),
            "timeout" => new OperationCanceledException(),
            _ => new MentorFitRankerUnavailableException()
        };
        ranker.RankForAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyDictionary<Guid, MentorFitRanking>>(error));

        var result = await Handler(db, failure == "unconfigured" ? null : ranker).PreviewAllocationAsync(Request(selected));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.MentorMatchingUnavailable);
        db.MentorAllocationSessions.Should().BeEmpty();
        db.MentorAssignments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(SystemRoles.Lecturer)]
    [InlineData(SystemRoles.Student)]
    public async Task NonAdmin_CannotInvokeAllocationRanking(string? role)
    {
        await using var db = Context();
        var (selected, _, _) = await SeedAsync(db);
        var ranker = ConstantRanker();

        var result = await Handler(db, ranker, role).PreviewAllocationAsync(Request(selected));

        result.Error.Code.Should().Be(ErrorCodes.CommonUnauthorizedError);
        await ranker.DidNotReceiveWithAnyArgs().RankForAllocationAsync(default!, default!, default);
    }

    [Fact]
    public async Task ClassOutsideSemester_IsRejectedBeforeRanking()
    {
        await using var db = Context();
        var (selected, _, _) = await SeedAsync(db);
        var ranker = ConstantRanker();

        var result = await Handler(db, ranker).PreviewAllocationAsync(new PreviewMentorAllocationRequest
        {
            SemesterId = selected.Class.SemesterId, ClassIds = [Guid.NewGuid()], Strategy = MentorAllocationStrategies.Ai
        });

        result.Error.Code.Should().Be(ErrorCodes.MentorAllocationInvalid);
        await ranker.DidNotReceiveWithAnyArgs().RankForAllocationAsync(default!, default!, default);
    }

    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Team Team, MentorProfile First, MentorProfile Second)> SeedAsync(AppDbContext db)
    {
        var semester = new Semester { Code = "AI", Name = "Synthetic AI semester", Year = 2026 };
        var team = AddTeam(db, semester, "Selected");
        var first = AddMentor(db, semester, "First");
        var second = AddMentor(db, semester, "Second");
        await db.SaveChangesAsync();
        return (team, first, second);
    }

    private static Team AddTeam(AppDbContext db, Semester semester, string name)
    {
        var team = new Team { TeamName = name, TeamCode = name, Description = "Marketing platform",
            Class = new Class { ClassCode = name, Semester = semester,
                Course = new Course { Code = "EXE101", Name = "Startup" }, Status = ClassStatus.Active } };
        db.Teams.Add(team);
        return team;
    }

    private static MentorProfile AddMentor(AppDbContext db, Semester semester, string name)
    {
        var user = new User { FullName = name, Email = $"{Guid.NewGuid():N}@example.test" };
        var profile = new MentorProfile { User = user, UserId = user.Id, Type = MentorType.Enterprise, Expertise = ["Marketing"] };
        db.MentorProfiles.Add(profile);
        db.SemesterStaffAssignments.Add(new SemesterStaffAssignment { Semester = semester, User = user, UserId = user.Id,
            Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active });
        return profile;
    }

    private static PreviewMentorAllocationRequest Request(Team team) => new()
    {
        SemesterId = team.Class.SemesterId, ClassIds = [team.ClassId], Strategy = MentorAllocationStrategies.Ai, Seed = 5
    };

    private static IMentorRecommendationRanker ConstantRanker()
    {
        var ranker = Substitute.For<IMentorRecommendationRanker>();
        ranker.RankForAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<IReadOnlyCollection<MentorProfile>>(1).ToDictionary(profile => profile.Id,
                _ => new MentorFitRanking(80, ["Matching evidence"])));
        return ranker;
    }

    private static MentorAdminHandler Handler(AppDbContext db, IMentorRecommendationRanker? ranker, string? role = SystemRoles.Admin)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(Guid.NewGuid());
        user.IsAuthenticated.Returns(role is not null);
        user.Roles.Returns(role is null ? Array.Empty<string>() : [role]);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc));
        return new MentorAdminHandler(db, user, Substitute.For<IPasswordHasher>(), clock,
            Substitute.For<IUnitOfWork>(), NullLogger<MentorAdminHandler>.Instance, ranker);
    }
}
