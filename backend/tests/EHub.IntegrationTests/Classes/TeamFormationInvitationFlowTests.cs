using EHub.Application.Features.Teams.TeamFormations;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Classes;

/// <summary>
/// Team formation = Proposal Creator invites students; only accepted members become official
/// team members, and only when the creator finalizes.
/// Students (from CreateSeedAsync): [0] BBA (creator), [1..3] BIT, [4..5] added on demand (BIT).
/// </summary>
public sealed partial class TeamWorkflowIntegrationTests
{
    private sealed record FormationArrange(
        AppDbContext Context, ITeamFormationHandler Handler, WorkflowSeed Seed, Guid[] Students, TeamFormationDto Formation);

    private async Task<FormationArrange> ArrangeFormationAsync(
        IServiceScope scope, int studentCount, int proposedLeaderIndex = 1)
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: false);
        var students = seed.StudentIds.ToList();
        if (studentCount > students.Count)
            students.AddRange(await AddStudentsAsync(context, seed, studentCount - students.Count, MajorCodes.BIT_SE));
        var members = students.Take(studentCount).ToArray();
        context.ChangeTracker.Clear();
        var handler = scope.ServiceProvider.GetRequiredService<ITeamFormationHandler>();
        var created = await handler.CreateAsync(seed.ClassId, new CreateTeamFormationRequest
        {
            TeamName = $"Flow {Guid.NewGuid():N}"[..20],
            InviteeStudentIds = members.Skip(1).ToArray(),
            LeaderStudentId = members[proposedLeaderIndex]
        }, seed.ProposerUserId, SystemRoles.Student);
        created.IsSuccess.Should().BeTrue(created.IsFailure ? created.Error.Message : "");
        return new FormationArrange(context, handler, seed, members, created.Value);
    }

    private static async Task<Guid[]> AddStudentsAsync(AppDbContext context, WorkflowSeed seed, int count, string major)
    {
        var template = await context.ClassStudents.AsNoTracking().FirstAsync(item => item.ClassId == seed.ClassId);
        var ids = new List<Guid>();
        for (var index = 0; index < count; index++)
        {
            var user = await CreateUserAsync(context, SystemRoles.Student, $"extra-{index}");
            var roll = $"SE{Guid.NewGuid():N}"[..10].ToUpperInvariant();
            var student = new Student
            {
                UserId = user.Id, User = user, RollNumber = roll, NormalizedRollNumber = roll,
                FullName = user.FullName, Email = user.Email, MajorCode = major,
                Status = StudentStatus.Active, CreatedBy = seed.AdminId
            };
            context.Students.Add(student);
            context.ClassStudents.Add(new ClassStudent
            {
                ClassId = seed.ClassId, StudentId = student.Id, Student = student,
                SemesterId = template.SemesterId, CourseId = template.CourseId,
                EnrollmentStatus = EnrollmentStatus.Active, CountsTowardCourseSemesterLimit = true,
                MajorCodeAtEnrollment = major
            });
            ids.Add(student.Id);
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return ids.ToArray();
    }

    private static async Task SetMajorAsync(AppDbContext context, Guid classId, Guid studentId, string major)
    {
        await context.Students.Where(item => item.Id == studentId)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.MajorCode, major));
        await context.ClassStudents.Where(item => item.ClassId == classId && item.StudentId == studentId)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.MajorCodeAtEnrollment, major));
        context.ChangeTracker.Clear();
    }

    private static async Task<Guid> UserIdOfAsync(AppDbContext context, Guid studentId) =>
        (await context.Students.AsNoTracking().Where(item => item.Id == studentId)
            .Select(item => item.UserId).SingleAsync())!.Value;

    private static async Task AcceptAllAsync(FormationArrange arrange, params int[] indexes)
    {
        foreach (var index in indexes)
        {
            var result = await arrange.Handler.AcceptAsync(arrange.Formation.Id,
                await UserIdOfAsync(arrange.Context, arrange.Students[index]), SystemRoles.Student);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
        }
    }

    /// <summary>Makes the student's pending invitation overdue, then runs the expiry job.</summary>
    private static async Task ExpireAsync(IServiceScope scope, FormationArrange arrange, params int[] indexes)
    {
        foreach (var index in indexes)
        {
            var studentId = arrange.Students[index];
            await arrange.Context.TeamFormationInvitations
                .Where(item => item.FormationId == arrange.Formation.Id && item.StudentId == studentId &&
                    item.ReservationReleasedAtUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(item => item.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-5)));
        }
        arrange.Context.ChangeTracker.Clear();
        await scope.ServiceProvider.GetRequiredService<ITeamFormationExpirationService>().ExpireDueAsync();
        arrange.Context.ChangeTracker.Clear();
    }

    private static async Task<EHub.Shared.Results.Result<TeamFormationDto>> FinalizeFormationAsync(
        FormationArrange arrange, Guid? leader = null, bool confirmPending = false) =>
        await arrange.Handler.FinalizeAsync(arrange.Formation.Id, new FinalizeTeamFormationRequest
        {
            LeaderStudentId = leader, ConfirmPendingInvitations = confirmPending
        }, arrange.Seed.ProposerUserId, SystemRoles.Student);

    private static async Task<Team> TheTeamAsync(FormationArrange arrange) =>
        await arrange.Context.Teams.AsNoTracking().Include(item => item.TeamMembers)
            .SingleAsync(item => item.ClassId == arrange.Seed.ClassId);

    [Fact]
    public async Task Finalize_AllInviteesAccepted_CreatesOfficialTeamWithOneLeader()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 6);
        await AcceptAllAsync(arrange, 1, 2, 3, 4, 5);

        var result = await FinalizeFormationAsync(arrange);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
        arrange.Context.ChangeTracker.Clear();
        var team = await TheTeamAsync(arrange);
        team.TeamMembers.Should().HaveCount(6);
        team.TeamMembers.Where(item => item.RoleInTeam == TeamMemberRole.Leader).Should().ContainSingle()
            .Which.StudentId.Should().Be(arrange.Students[1]);
    }

    [Fact]
    public async Task Finalize_FiveAcceptedOneExpired_Succeeds()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 6);
        await AcceptAllAsync(arrange, 1, 2, 3, 4);
        await ExpireAsync(scope, arrange, 5);

        var result = await FinalizeFormationAsync(arrange);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
        arrange.Context.ChangeTracker.Clear();
        (await TheTeamAsync(arrange)).TeamMembers.Select(item => item.StudentId)
            .Should().BeEquivalentTo(arrange.Students.Take(5));
    }

    [Fact]
    public async Task Finalize_FourAcceptedTwoExpired_Succeeds()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 6);
        await AcceptAllAsync(arrange, 1, 2, 3);
        await ExpireAsync(scope, arrange, 4, 5);

        var result = await FinalizeFormationAsync(arrange);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
        arrange.Context.ChangeTracker.Clear();
        (await TheTeamAsync(arrange)).TeamMembers.Should().HaveCount(4);
    }

    [Fact]
    public async Task Finalize_FourAcceptedOnePendingOneExpired_RequiresConfirmation_AndExcludesPending()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 6);
        await AcceptAllAsync(arrange, 1, 2, 3);
        await ExpireAsync(scope, arrange, 5);

        var withoutConfirmation = await FinalizeFormationAsync(arrange);
        withoutConfirmation.IsFailure.Should().BeTrue();
        withoutConfirmation.Error.Code.Should().Be(ErrorCodes.TeamFormationPendingConfirmationRequired);
        arrange.Context.ChangeTracker.Clear();
        (await arrange.Context.Teams.CountAsync(item => item.ClassId == arrange.Seed.ClassId)).Should().Be(0);

        var confirmed = await FinalizeFormationAsync(arrange, confirmPending: true);

        confirmed.IsSuccess.Should().BeTrue(confirmed.IsFailure ? confirmed.Error.Message : "");
        arrange.Context.ChangeTracker.Clear();
        var team = await TheTeamAsync(arrange);
        team.TeamMembers.Should().HaveCount(4);
        team.TeamMembers.Select(item => item.StudentId).Should().NotContain(arrange.Students[4], "pending is not official");
        (await arrange.Context.TeamFormationInvitations.CountAsync(item =>
            item.FormationId == arrange.Formation.Id && item.ReservationReleasedAtUtc == null)).Should().Be(0);
    }

    [Fact]
    public async Task Finalize_ThreeAcceptedThreeExpired_IsBlocked()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 6);
        await AcceptAllAsync(arrange, 1, 2);
        await ExpireAsync(scope, arrange, 3, 4, 5);

        var result = await FinalizeFormationAsync(arrange);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.TeamFormationNotReady);
        arrange.Context.ChangeTracker.Clear();
        (await arrange.Context.Teams.CountAsync(item => item.ClassId == arrange.Seed.ClassId)).Should().Be(0);
        (await arrange.Handler.GetAsync(arrange.Formation.Id, arrange.Seed.ProposerUserId, SystemRoles.Student))
            .Value.CanFinalize.Should().BeFalse();
    }

    [Fact]
    public async Task Finalize_FourAcceptedWithoutAnyBitStudent_IsBlocked()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 4);
        foreach (var student in arrange.Students.Skip(1))
            await SetMajorAsync(arrange.Context, arrange.Seed.ClassId, student, MajorCodes.BEN);
        await AcceptAllAsync(arrange, 1, 2, 3);

        var result = await FinalizeFormationAsync(arrange);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.TeamFormationNotReady);
    }

    [Fact]
    public async Task Finalize_FourAcceptedWithoutAnyBbaStudent_IsBlocked()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 4);
        await SetMajorAsync(arrange.Context, arrange.Seed.ClassId, arrange.Students[0], MajorCodes.BIT_SE);
        await AcceptAllAsync(arrange, 1, 2, 3);

        var result = await FinalizeFormationAsync(arrange);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.TeamFormationNotReady);
    }

    [Fact]
    public async Task Decline_AndExpire_KeepFormationPending_AndNeverCreateTeamMembers()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 6);
        var declined = await arrange.Handler.DeclineAsync(arrange.Formation.Id,
            await UserIdOfAsync(arrange.Context, arrange.Students[2]), SystemRoles.Student);
        declined.IsSuccess.Should().BeTrue();
        await ExpireAsync(scope, arrange, 3);

        var formation = await arrange.Handler.GetAsync(arrange.Formation.Id, arrange.Seed.ProposerUserId, SystemRoles.Student);

        formation.Value.Status.Should().Be("Pending");
        formation.Value.Invitations.Single(item => item.StudentId == arrange.Students[2]).Status.Should().Be("Declined");
        formation.Value.Invitations.Single(item => item.StudentId == arrange.Students[3]).Status.Should().Be("Expired");
        (await arrange.Context.TeamMembers.CountAsync(item => item.ClassId == arrange.Seed.ClassId)).Should().Be(0);
    }

    [Fact]
    public async Task OverdueInvitation_IsReportedExpired_EvenBeforeTheJobRuns_AndCannotBeAccepted()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 4);
        await arrange.Context.TeamFormationInvitations
            .Where(item => item.FormationId == arrange.Formation.Id && item.StudentId == arrange.Students[2])
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        arrange.Context.ChangeTracker.Clear();

        var viewed = await arrange.Handler.GetAsync(arrange.Formation.Id, arrange.Seed.ProposerUserId, SystemRoles.Student);
        viewed.Value.Invitations.Single(item => item.StudentId == arrange.Students[2]).Status.Should().Be("Expired");

        var accepted = await arrange.Handler.AcceptAsync(arrange.Formation.Id,
            await UserIdOfAsync(arrange.Context, arrange.Students[2]), SystemRoles.Student);
        accepted.IsFailure.Should().BeTrue();
        accepted.Error.Code.Should().Be(ErrorCodes.TeamInvitationExpired);
        arrange.Context.ChangeTracker.Clear();
        (await arrange.Context.TeamFormationInvitations.AsNoTracking().SingleAsync(item =>
            item.FormationId == arrange.Formation.Id && item.StudentId == arrange.Students[2]))
            .Status.Should().Be(TeamInvitationStatus.Expired);
    }

    [Theory]
    [InlineData("Declined")]
    [InlineData("Expired")]
    public async Task ReInvite_AfterDeclinedOrExpired_CreatesNewRecord_AndKeepsHistory(string previous)
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 5);
        if (previous == "Declined")
            (await arrange.Handler.DeclineAsync(arrange.Formation.Id,
                await UserIdOfAsync(arrange.Context, arrange.Students[2]), SystemRoles.Student)).IsSuccess.Should().BeTrue();
        else
            await ExpireAsync(scope, arrange, 2);

        var reinvited = await arrange.Handler.InviteAsync(arrange.Formation.Id,
            new InviteTeamFormationMembersRequest { StudentIds = [arrange.Students[2]] },
            arrange.Seed.ProposerUserId, SystemRoles.Student);

        reinvited.IsSuccess.Should().BeTrue(reinvited.IsFailure ? reinvited.Error.Message : "");
        var records = reinvited.Value.Invitations.Where(item => item.StudentId == arrange.Students[2])
            .OrderBy(item => item.CreatedAtUtc).ToArray();
        records.Should().HaveCount(2);
        records[0].Status.Should().Be(previous);
        records[1].Status.Should().Be("Pending");
        records[1].IsCurrent.Should().BeTrue();
        records[0].IsCurrent.Should().BeFalse();
        records[1].ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromMinutes(2));

        var accepted = await arrange.Handler.AcceptAsync(arrange.Formation.Id,
            await UserIdOfAsync(arrange.Context, arrange.Students[2]), SystemRoles.Student);
        accepted.IsSuccess.Should().BeTrue(accepted.IsFailure ? accepted.Error.Message : "");
        (await arrange.Context.TeamFormationInvitations.CountAsync(item =>
            item.FormationId == arrange.Formation.Id && item.StudentId == arrange.Students[2])).Should().Be(2);
    }

    [Fact]
    public async Task ReInvite_WhilePending_OrAfterAccepted_IsBlocked()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 5);
        await AcceptAllAsync(arrange, 1);

        var pending = await arrange.Handler.InviteAsync(arrange.Formation.Id,
            new InviteTeamFormationMembersRequest { StudentIds = [arrange.Students[2]] },
            arrange.Seed.ProposerUserId, SystemRoles.Student);
        var accepted = await arrange.Handler.InviteAsync(arrange.Formation.Id,
            new InviteTeamFormationMembersRequest { StudentIds = [arrange.Students[1]] },
            arrange.Seed.ProposerUserId, SystemRoles.Student);

        pending.IsFailure.Should().BeTrue();
        pending.Error.Code.Should().Be(ErrorCodes.TeamInvitationConflict);
        accepted.IsFailure.Should().BeTrue();
        accepted.Error.Code.Should().Be(ErrorCodes.TeamInvitationConflict);
        arrange.Context.ChangeTracker.Clear();
        (await arrange.Context.TeamFormationInvitations.CountAsync(item =>
            item.FormationId == arrange.Formation.Id && item.StudentId == arrange.Students[2])).Should().Be(1);
    }

    [Fact]
    public async Task Invite_BeyondSixActiveMembers_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 6);
        var extra = (await AddStudentsAsync(arrange.Context, arrange.Seed, 1, MajorCodes.BIT_SE))[0];

        var result = await arrange.Handler.InviteAsync(arrange.Formation.Id,
            new InviteTeamFormationMembersRequest { StudentIds = [extra] },
            arrange.Seed.ProposerUserId, SystemRoles.Student);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.TeamFormationCapacityConflict);
    }

    [Fact]
    public async Task Finalize_ProposedLeaderAccepted_CannotBeReplaced_AndCreatorMayNotBeLeader()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 4, proposedLeaderIndex: 2);
        await AcceptAllAsync(arrange, 1, 2, 3);

        var replaced = await FinalizeFormationAsync(arrange, leader: arrange.Students[0]);
        replaced.IsFailure.Should().BeTrue();
        replaced.Error.Code.Should().Be(ErrorCodes.ClassValidationError);

        var result = await FinalizeFormationAsync(arrange);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
        arrange.Context.ChangeTracker.Clear();
        var team = await TheTeamAsync(arrange);
        team.TeamMembers.Single(item => item.RoleInTeam == TeamMemberRole.Leader).StudentId.Should().Be(arrange.Students[2]);
        arrange.Students[2].Should().NotBe(arrange.Formation.CreatorStudentId);
    }

    [Fact]
    public async Task Finalize_ProposedLeaderDeclined_CreatorMustChooseAcceptedLeader()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 5, proposedLeaderIndex: 1);
        (await arrange.Handler.DeclineAsync(arrange.Formation.Id,
            await UserIdOfAsync(arrange.Context, arrange.Students[1]), SystemRoles.Student)).IsSuccess.Should().BeTrue();
        await AcceptAllAsync(arrange, 2, 3, 4);
        (await arrange.Handler.GetAsync(arrange.Formation.Id, arrange.Seed.ProposerUserId, SystemRoles.Student))
            .Value.RequiresLeaderSelection.Should().BeTrue();

        var missing = await FinalizeFormationAsync(arrange);
        var notAccepted = await FinalizeFormationAsync(arrange, leader: arrange.Students[1]);
        var chosen = await FinalizeFormationAsync(arrange, leader: arrange.Students[3]);

        missing.IsFailure.Should().BeTrue();
        missing.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
        notAccepted.IsFailure.Should().BeTrue();
        chosen.IsSuccess.Should().BeTrue(chosen.IsFailure ? chosen.Error.Message : "");
        arrange.Context.ChangeTracker.Clear();
        var team = await TheTeamAsync(arrange);
        team.TeamMembers.Should().HaveCount(4);
        team.TeamMembers.Single(item => item.RoleInTeam == TeamMemberRole.Leader).StudentId.Should().Be(arrange.Students[3]);
    }

    [Fact]
    public async Task Finalize_CreatorCanBeChosenAsLeader_WhenProposedLeaderIsStillPending()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 5, proposedLeaderIndex: 1);
        await AcceptAllAsync(arrange, 2, 3, 4);

        var result = await FinalizeFormationAsync(arrange, leader: arrange.Students[0], confirmPending: true);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
        arrange.Context.ChangeTracker.Clear();
        var team = await TheTeamAsync(arrange);
        team.TeamMembers.Should().HaveCount(4);
        team.TeamMembers.Single(item => item.RoleInTeam == TeamMemberRole.Leader).StudentId.Should().Be(arrange.Students[0]);
    }

    [Fact]
    public async Task Finalize_Twice_CreatesOnlyOneTeam()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 4);
        await AcceptAllAsync(arrange, 1, 2, 3);

        var first = await FinalizeFormationAsync(arrange);
        var second = await FinalizeFormationAsync(arrange);

        first.IsSuccess.Should().BeTrue(first.IsFailure ? first.Error.Message : "");
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be(ErrorCodes.TeamFormationStateInvalid);
        arrange.Context.ChangeTracker.Clear();
        (await arrange.Context.Teams.CountAsync(item => item.ClassId == arrange.Seed.ClassId)).Should().Be(1);
        (await arrange.Context.TeamMembers.CountAsync(item => item.ClassId == arrange.Seed.ClassId)).Should().Be(4);
    }

    [Fact]
    public async Task Finalize_Concurrently_CreatesOnlyOneTeam()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 4);
        await AcceptAllAsync(arrange, 1, 2, 3);
        arrange.Context.ChangeTracker.Clear();

        async Task<EHub.Shared.Results.Result<TeamFormationDto>> RunAsync()
        {
            using var other = _factory.Services.CreateScope();
            return await other.ServiceProvider.GetRequiredService<ITeamFormationHandler>().FinalizeAsync(
                arrange.Formation.Id, new FinalizeTeamFormationRequest(), arrange.Seed.ProposerUserId, SystemRoles.Student);
        }

        var results = await Task.WhenAll(RunAsync(), RunAsync(), RunAsync());

        results.Count(item => item.IsSuccess).Should().Be(1);
        (await arrange.Context.Teams.CountAsync(item => item.ClassId == arrange.Seed.ClassId)).Should().Be(1);
        var members = await arrange.Context.TeamMembers.AsNoTracking()
            .Where(item => item.ClassId == arrange.Seed.ClassId).Select(item => item.StudentId).ToListAsync();
        members.Should().HaveCount(4).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Accepting_NeverCreatesTeamMembers_BeforeFinalize()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 4);

        await AcceptAllAsync(arrange, 1, 2, 3);

        arrange.Context.ChangeTracker.Clear();
        (await arrange.Context.TeamMembers.CountAsync(item => item.ClassId == arrange.Seed.ClassId)).Should().Be(0);
        (await arrange.Context.Teams.CountAsync(item => item.ClassId == arrange.Seed.ClassId)).Should().Be(0);
        var dto = (await arrange.Handler.GetAsync(arrange.Formation.Id, arrange.Seed.ProposerUserId, SystemRoles.Student)).Value;
        dto.Status.Should().Be("Pending");
        dto.AcceptedCount.Should().Be(4);
        dto.CanFinalize.Should().BeTrue();
    }

    [Fact]
    public async Task Leave_ByAcceptedMember_ReleasesSlot_ButCreatorCannotLeave()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 5, proposedLeaderIndex: 1);
        await AcceptAllAsync(arrange, 1, 2);

        var creatorLeave = await arrange.Handler.LeaveAsync(arrange.Formation.Id, arrange.Seed.ProposerUserId, SystemRoles.Student);
        creatorLeave.IsFailure.Should().BeTrue();
        creatorLeave.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);

        var pendingLeave = await arrange.Handler.LeaveAsync(arrange.Formation.Id,
            await UserIdOfAsync(arrange.Context, arrange.Students[3]), SystemRoles.Student);
        pendingLeave.IsFailure.Should().BeTrue("a pending invitee must decline instead");

        var leaderLeave = await arrange.Handler.LeaveAsync(arrange.Formation.Id,
            await UserIdOfAsync(arrange.Context, arrange.Students[1]), SystemRoles.Student);
        leaderLeave.IsSuccess.Should().BeTrue(leaderLeave.IsFailure ? leaderLeave.Error.Message : "");
        leaderLeave.Value.Status.Should().Be("Pending");
        leaderLeave.Value.Invitations.Single(item => item.StudentId == arrange.Students[1]).Status.Should().Be("Left");
        var dto = (await arrange.Handler.GetAsync(arrange.Formation.Id, arrange.Seed.ProposerUserId, SystemRoles.Student)).Value;
        dto.RequiresLeaderSelection.Should().BeTrue();
    }

    [Fact]
    public async Task OnlyCreator_CanInviteFinalizeOrCancel()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 4);
        await AcceptAllAsync(arrange, 1, 2, 3);
        var memberUserId = await UserIdOfAsync(arrange.Context, arrange.Students[1]);

        var invite = await arrange.Handler.InviteAsync(arrange.Formation.Id,
            new InviteTeamFormationMembersRequest { StudentIds = [arrange.Students[2]] }, memberUserId, SystemRoles.Student);
        var finalize = await arrange.Handler.FinalizeAsync(arrange.Formation.Id,
            new FinalizeTeamFormationRequest(), memberUserId, SystemRoles.Student);
        var cancel = await arrange.Handler.CancelAsync(arrange.Formation.Id, memberUserId, SystemRoles.Student);
        var lecturer = await arrange.Handler.FinalizeAsync(arrange.Formation.Id,
            new FinalizeTeamFormationRequest(), arrange.Seed.LecturerId, SystemRoles.Lecturer);

        invite.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
        finalize.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
        cancel.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
        lecturer.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
        arrange.Context.ChangeTracker.Clear();
        (await arrange.Context.Teams.CountAsync(item => item.ClassId == arrange.Seed.ClassId)).Should().Be(0);
    }

    [Fact]
    public async Task Create_RejectsCreatorInInviteesAndLeaderOutsideTheFormation()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: false);
        context.ChangeTracker.Clear();
        var handler = scope.ServiceProvider.GetRequiredService<ITeamFormationHandler>();

        var creatorInvited = await handler.CreateAsync(seed.ClassId, new CreateTeamFormationRequest
        {
            TeamName = "Creator Invited", InviteeStudentIds = seed.StudentIds, LeaderStudentId = seed.StudentIds[1]
        }, seed.ProposerUserId, SystemRoles.Student);
        var strangerLeader = await handler.CreateAsync(seed.ClassId, new CreateTeamFormationRequest
        {
            TeamName = "Stranger Leader", InviteeStudentIds = seed.StudentIds.Skip(1).Take(2).ToArray(),
            LeaderStudentId = seed.StudentIds[3]
        }, seed.ProposerUserId, SystemRoles.Student);
        var noInvitee = await handler.CreateAsync(seed.ClassId, new CreateTeamFormationRequest
        {
            TeamName = "Nobody Invited", InviteeStudentIds = [], LeaderStudentId = seed.StudentIds[0]
        }, seed.ProposerUserId, SystemRoles.Student);

        creatorInvited.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
        strangerLeader.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
        noInvitee.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }

    [Fact]
    public async Task Create_AllowsFewerThanFourMembers_AndEnqueuesLeaderRoleInInvitationEvent()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: false);
        context.ChangeTracker.Clear();
        var handler = scope.ServiceProvider.GetRequiredService<ITeamFormationHandler>();

        var created = await handler.CreateAsync(seed.ClassId, new CreateTeamFormationRequest
        {
            TeamName = "Small Start", InviteeStudentIds = [seed.StudentIds[1], seed.StudentIds[2]],
            LeaderStudentId = seed.StudentIds[2]
        }, seed.ProposerUserId, SystemRoles.Student);

        created.IsSuccess.Should().BeTrue(created.IsFailure ? created.Error.Message : "");
        created.Value.CanFinalize.Should().BeFalse();
        created.Value.FinalizeBlockers.Should().NotBeEmpty();
        created.Value.Invitations.Single(item => item.StudentId == seed.StudentIds[2]).IsProposedLeader.Should().BeTrue();
        var invited = await context.OutboxMessages.AsNoTracking().SingleAsync(item =>
            item.Type == "TeamFormation.Invited.v1" && item.AggregateId == seed.ClassId);
        invited.PayloadJson.Should().Contain(seed.StudentIds[2].ToString());
        invited.PayloadJson.Should().Contain("proposedLeaderStudentId");
    }

    [Fact]
    public async Task ExpiryJob_ReleasesReservation_AllowsStudentToJoinAnotherFormation_AndIsIdempotent()
    {
        using var scope = _factory.Services.CreateScope();
        var arrange = await ArrangeFormationAsync(scope, 4);
        await ExpireAsync(scope, arrange, 3);

        var again = await scope.ServiceProvider.GetRequiredService<ITeamFormationExpirationService>().ExpireDueAsync();

        again.Should().Be(0);
        (await arrange.Context.OutboxMessages.CountAsync(item =>
            item.Type == "TeamFormation.InvitationExpired.v1" && item.AggregateId == arrange.Seed.ClassId)).Should().Be(1);
        // Student[3]'s reservation was released by the job, so they can start their own formation.
        var extra = (await AddStudentsAsync(arrange.Context, arrange.Seed, 1, MajorCodes.BEN))[0];
        var freed = await arrange.Handler.CreateAsync(arrange.Seed.ClassId, new CreateTeamFormationRequest
        {
            TeamName = $"Freed {Guid.NewGuid():N}"[..16],
            InviteeStudentIds = [extra], LeaderStudentId = arrange.Students[3]
        }, await UserIdOfAsync(arrange.Context, arrange.Students[3]), SystemRoles.Student);
        freed.IsSuccess.Should().BeTrue(freed.IsFailure ? freed.Error.Message : "");
        // Student[2] still holds a pending invitation in the first formation.
        var blocked = await arrange.Handler.CreateAsync(arrange.Seed.ClassId, new CreateTeamFormationRequest
        {
            TeamName = $"Blocked {Guid.NewGuid():N}"[..16],
            InviteeStudentIds = [arrange.Students[2]], LeaderStudentId = arrange.Students[2]
        }, await UserIdOfAsync(arrange.Context, extra), SystemRoles.Student);
        blocked.IsFailure.Should().BeTrue();
        blocked.Error.Code.Should().Be(ErrorCodes.TeamFormationReservationConflict);
    }
}
