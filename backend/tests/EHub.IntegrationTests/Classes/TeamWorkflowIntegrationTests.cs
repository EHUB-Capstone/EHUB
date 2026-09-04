using EHub.Application.Features.Classes.Common;
using EHub.Application.Features.Classes.GetClassRoster;
using EHub.Application.Features.Classes.AssignStudents;
using EHub.Application.Features.Classes.RemoveStudentFromClass;
using EHub.Application.Features.Classes.StudentSelfService;
using EHub.Application.Features.Teams.ManageTeams;
using EHub.Application.Features.Teams.MentorAssignments;
using EHub.Application.Features.Teams.ProjectDirections;
using EHub.Application.Features.Teams.TeamProposals;
using EHub.Application.Features.Workspaces;
using EHub.Application.Features.Admin.Users.ManageUsers;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Classes;
using EHub.Contracts.Teams;
using EHub.Contracts.Users;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.IntegrationTests.Common;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Classes;

[Collection("Sequential")]
public sealed class TeamWorkflowIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public TeamWorkflowIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentReviewers_CreateExactlyOneOfficialTeam()
    {
        using var scope = _factory.Services.CreateScope();
        var seedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(seedContext, createProposal: true, createTeam: false);
        seedContext.ChangeTracker.Clear();
        var rowVersion = (await seedContext.TeamProposals.AsNoTracking().SingleAsync(item => item.Id == seed.ProposalId)).Version.ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(seedContext.Database.GetConnectionString())
            .Options;

        await using var firstContext = new AppDbContext(options);
        await using var secondContext = new AppDbContext(options);
        var request = new ReviewTeamProposalRequest
        {
            Decision = "Approved",
            Comment = "Approved after composition review.",
            RowVersion = rowVersion
        };
        var firstHandler = new TeamProposalHandler(firstContext, new UnitOfWork(firstContext));
        var secondHandler = new TeamProposalHandler(secondContext, new UnitOfWork(secondContext));

        var results = await Task.WhenAll(
            firstHandler.ReviewAsync(seed.ProposalId!.Value, request, seed.AdminId, SystemRoles.Admin),
            secondHandler.ReviewAsync(seed.ProposalId.Value, request, seed.AdminId, SystemRoles.Admin));

        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Count(result => result.IsFailure).Should().Be(1);
        seedContext.ChangeTracker.Clear();
        (await seedContext.Teams.AsNoTracking().CountAsync(team => team.ClassId == seed.ClassId)).Should().Be(1);
        (await seedContext.TeamProposals.AsNoTracking().SingleAsync(item => item.Id == seed.ProposalId)).Status
            .Should().Be(TeamProposalStatus.Approved);
    }

    [Fact]
    public async Task NeedsRevision_CanBeResubmittedWithHistoryAndOutbox()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: true, createTeam: false);
        context.ChangeTracker.Clear();
        var proposal = await context.TeamProposals.AsNoTracking().SingleAsync(item => item.Id == seed.ProposalId);
        var handler = new TeamProposalHandler(context, scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());

        var review = await handler.ReviewAsync(seed.ProposalId!.Value, new ReviewTeamProposalRequest
        {
            Decision = "NeedsRevision",
            Comment = "Clarify the proposed team focus.",
            RowVersion = proposal.Version.ToString()
        }, seed.LecturerId, SystemRoles.Lecturer);

        review.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        proposal = await context.TeamProposals.AsNoTracking().SingleAsync(item => item.Id == seed.ProposalId);
        var submit = await handler.SubmitAsync(seed.ProposalId.Value,
            new SubmitTeamProposalRequest { RowVersion = proposal.Version.ToString() },
            seed.ProposerUserId,
            SystemRoles.Student);

        submit.IsSuccess.Should().BeTrue();
        submit.Value.Status.Should().Be(nameof(TeamProposalStatus.Pending));
        context.ChangeTracker.Clear();
        (await context.TeamProposalHistory.AsNoTracking().AnyAsync(item => item.ProposalId == seed.ProposalId && item.Action == "Resubmitted"))
            .Should().BeTrue();
        (await context.OutboxMessages.AsNoTracking().AnyAsync(item => item.Type == "TeamProposal.Submitted.v1" && item.AggregateId == seed.ClassId))
            .Should().BeTrue();
    }

    [Fact]
    public async Task NeedsRevision_CanChangeLeaderWithoutBreakingTheUniqueLeaderInvariant()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: true, createTeam: false);
        var proposal = await context.TeamProposals.SingleAsync(item => item.Id == seed.ProposalId);
        proposal.Status = TeamProposalStatus.NeedsRevision;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        proposal = await context.TeamProposals.AsNoTracking().SingleAsync(item => item.Id == seed.ProposalId);
        var handler = new TeamProposalHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());

        var result = await handler.UpdateAsync(
            proposal.Id,
            new UpdateTeamProposalRequest
            {
                TeamName = proposal.TeamName,
                Description = proposal.Description,
                ProjectName = proposal.ProjectName,
                MemberIds = seed.StudentIds,
                LeaderStudentId = seed.StudentIds[1],
                RowVersion = proposal.Version.ToString()
            },
            seed.ProposerUserId,
            SystemRoles.Student);

        result.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        var included = await context.TeamProposalMembers.AsNoTracking()
            .Where(member => member.ProposalId == proposal.Id && member.IsIncluded)
            .ToListAsync();
        included.Count(member => member.IsLeader).Should().Be(1);
        included.Single(member => member.IsLeader).StudentId.Should().Be(seed.StudentIds[1]);
    }

    [Fact]
    public async Task Mentor_CanOnlyViewAssignedTeam_AndCannotReadClassRoster()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        context.ChangeTracker.Clear();
        var teamHandler = new TeamManagementHandler(context, scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());

        var assignedTeam = await teamHandler.GetAsync(seed.TeamId!.Value, seed.MentorUserId, SystemRoles.Mentor);
        var unrelatedTeam = await teamHandler.GetAsync(seed.OtherTeamId!.Value, seed.MentorUserId, SystemRoles.Mentor);
        var roster = await new GetClassRosterQueryHandler(context).HandleAsync(
            seed.ClassId, new GetClassRosterRequest(), seed.MentorUserId, SystemRoles.Mentor);

        assignedTeam.IsSuccess.Should().BeTrue();
        unrelatedTeam.IsFailure.Should().BeTrue();
        unrelatedTeam.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
        roster.IsFailure.Should().BeTrue();
        roster.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task CreateTeam_WhenImportedMajorIsMissing_UsesRegisteredProfileMajor()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: false);
        var enrollments = await context.ClassStudents
            .Where(item => item.ClassId == seed.ClassId)
            .ToListAsync();
        enrollments.ForEach(item => item.MajorCodeAtEnrollment = MajorCodes.Undeclared);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var handler = new TeamManagementHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());
        var result = await handler.CreateAsync(
            seed.ClassId,
            new CreateTeamRequest
            {
                TeamName = "Profile Major Fallback",
                MemberIds = seed.StudentIds,
                LeaderStudentId = seed.StudentIds[0]
            },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        result.Value.Members.Should().Contain(member => member.MajorCode == MajorCodes.BEN);
        result.Value.Members.Should().Contain(member => member.MajorCode == MajorCodes.BIT_SE);
    }

    [Fact]
    public async Task ReassigningMentor_EndsThePreviousAssignmentAndKeepsOneActiveSource()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        var replacementUser = await CreateUserAsync(context, SystemRoles.Mentor, "replacement-mentor");
        var replacementProfile = new MentorProfile
        {
            UserId = replacementUser.Id,
            User = replacementUser,
            Organization = "Replacement Mentor Org",
            Status = MentorProfileStatus.Active,
            MaxTeams = 3,
            CreatedBy = seed.AdminId
        };
        context.MentorProfiles.Add(replacementProfile);
        var semesterId = await context.Classes
            .Where(item => item.Id == seed.ClassId)
            .Select(item => item.SemesterId)
            .SingleAsync();
        context.SemesterStaffAssignments.Add(new SemesterStaffAssignment
        {
            SemesterId = semesterId,
            UserId = replacementUser.Id,
            User = replacementUser,
            Role = SemesterStaffRole.Mentor,
            Status = SemesterStaffStatus.Active,
            CreatedBy = seed.AdminId
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var handler = new MentorAssignmentHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());

        var result = await handler.AssignAsync(
            seed.TeamId!.Value,
            new AssignMentorRequest { MentorProfileId = replacementProfile.Id, Note = "Reassigned for domain fit." },
            seed.AdminId,
            SystemRoles.Admin);

        result.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        var assignments = await context.MentorAssignments.AsNoTracking()
            .Where(assignment => assignment.TeamId == seed.TeamId)
            .ToListAsync();
        assignments.Count(assignment => assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null)
            .Should().Be(1);
        assignments.Single(assignment => assignment.Status == MentorAssignmentStatus.Active).MentorProfileId
            .Should().Be(replacementProfile.Id);
        assignments.Should().Contain(assignment => assignment.Status == MentorAssignmentStatus.Ended && assignment.EndedAt != null);
    }

    [Fact]
    public async Task StudentCannotViewClassWhereTheyAreNotEnrolled()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: false);
        var outsider = await CreateUserAsync(context, SystemRoles.Student, "outsider");
        context.Students.Add(new Student
        {
            UserId = outsider.Id,
            User = outsider,
            RollNumber = $"SE{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            NormalizedRollNumber = $"SE{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            FullName = outsider.FullName,
            Email = outsider.Email,
            MajorCode = MajorCodes.BIT_SE,
            Status = StudentStatus.Active
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new StudentClassSelfServiceHandler(context)
            .GetClassDetailAsync(seed.ClassId, outsider.Id, SystemRoles.Student);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task DroppingStudentInActiveTeam_IsBlocked()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        context.ChangeTracker.Clear();

        var result = await new RemoveStudentFromClassCommandHandler(context).HandleAsync(
            seed.ClassId, seed.StudentIds[1], seed.LecturerId, SystemRoles.Lecturer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassStudentInActiveTeam);
    }

    [Fact]
    public async Task ChangingLeader_IsAtomicAndLeavesExactlyOneActiveLeader()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        context.ChangeTracker.Clear();
        var team = await context.Teams.AsNoTracking().SingleAsync(item => item.Id == seed.TeamId);
        var handler = new TeamManagementHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());

        var result = await handler.AssignLeaderAsync(
            seed.TeamId!.Value,
            new AssignTeamLeaderRequest
            {
                StudentId = seed.StudentIds[1],
                RowVersion = team.Version.ToString()
            },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        var activeMembers = await context.TeamMembers.AsNoTracking()
            .Where(member => member.TeamId == seed.TeamId && member.CountsTowardActiveTeam)
            .ToListAsync();
        activeMembers.Count(member => member.RoleInTeam == TeamMemberRole.Leader).Should().Be(1);
        activeMembers.Single(member => member.RoleInTeam == TeamMemberRole.Leader).StudentId
            .Should().Be(seed.StudentIds[1]);
    }

    [Fact]
    public async Task AdminCreatedMentor_HasProfileRequiredByTeamAssignmentFlow()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminId = await context.Users
            .Where(user => user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Admin))
            .Select(user => user.Id)
            .FirstAsync();
        var handler = new UserManagementHandler(
            context,
            new TestCurrentUser(adminId, SystemRoles.Admin),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
        var unique = Guid.NewGuid().ToString("N");

        var result = await handler.CreateUserAsync(new SaveManagedUserRequest
        {
            Name = "Team Flow Mentor",
            Email = $"team-flow-mentor-{unique}@ehub.local",
            Password = "QaMentor!123",
            Role = "MENTOR",
            Status = "APPROVED"
        });

        result.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        var profile = await context.MentorProfiles.AsNoTracking()
            .SingleAsync(item => item.UserId == result.Value.Id);
        profile.Status.Should().Be(MentorProfileStatus.Active);
        profile.MaxTeams.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task LecturerCanReplaceMemberAndTransferLeaderInOneAtomicUpdate()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        var targetClass = await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId);
        var replacementUser = await CreateUserAsync(context, SystemRoles.Student, "replacement-member");
        var replacement = new Student
        {
            UserId = replacementUser.Id,
            User = replacementUser,
            RollNumber = $"SE{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            FullName = replacementUser.FullName,
            Email = replacementUser.Email,
            MajorCode = MajorCodes.BIT_SE,
            Status = StudentStatus.Active,
            CreatedBy = seed.AdminId
        };
        context.Students.Add(replacement);
        context.ClassStudents.Add(new ClassStudent
        {
            ClassId = seed.ClassId,
            StudentId = replacement.Id,
            SemesterId = targetClass.SemesterId,
            CourseId = targetClass.CourseId,
            EnrollmentStatus = EnrollmentStatus.Active,
            CountsTowardCourseSemesterLimit = true,
            MajorCodeAtEnrollment = MajorCodes.BIT_SE
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var team = await context.Teams.AsNoTracking().SingleAsync(item => item.Id == seed.TeamId);
        var handler = new TeamManagementHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());
        var desiredMembers = new[] { seed.StudentIds[0], seed.StudentIds[1], seed.StudentIds[2], replacement.Id };

        var result = await handler.UpdateMembersAsync(
            seed.TeamId!.Value,
            new UpdateTeamMembersRequest
            {
                TeamName = "Updated Lecturer Team",
                Description = "Members and leader adjusted by the assigned lecturer.",
                MemberIds = desiredMembers,
                LeaderStudentId = replacement.Id,
                RowVersion = team.Version.ToString()
            },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        result.Value.LeaderId.Should().Be(replacement.Id);
        result.Value.Members.Select(member => member.StudentId).Should().BeEquivalentTo(desiredMembers);
        context.ChangeTracker.Clear();
        var activeMembers = await context.TeamMembers.AsNoTracking()
            .Where(member => member.TeamId == seed.TeamId && member.CountsTowardActiveTeam)
            .ToListAsync();
        activeMembers.Should().HaveCount(4);
        activeMembers.Should().ContainSingle(member => member.RoleInTeam == TeamMemberRole.Leader)
            .Which.StudentId.Should().Be(replacement.Id);
        (await context.TeamMembers.AsNoTracking().SingleAsync(member =>
            member.TeamId == seed.TeamId && member.StudentId == seed.StudentIds[3]))
            .CountsTowardActiveTeam.Should().BeFalse();
    }

    [Fact]
    public async Task StudentCreatesTeamImmediatelyAndSubmitsProjectForLecturerApproval()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: false);
        context.ChangeTracker.Clear();
        var handler = new TeamProposalHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());

        var classDetail = await new StudentClassSelfServiceHandler(context)
            .GetClassDetailAsync(seed.ClassId, seed.ProposerUserId, SystemRoles.Student);
        classDetail.IsSuccess.Should().BeTrue();
        classDetail.Value.Students.Should().OnlyContain(student => student.EnrollmentStatus == nameof(EnrollmentStatus.Active));

        var result = await handler.SubmitStudentProposalAsync(
            seed.ClassId,
            new SubmitStudentTeamProposalRequest
            {
                StudentIds = seed.StudentIds,
                LeaderStudentId = seed.StudentIds[1],
                GroupName = "Student Venture Team",
                ProjectName = "Student Venture Project",
                IsProjectNameSameAsGroup = false,
                Description = "A balanced student-created proposal ready for lecturer review."
            },
            seed.ProposerUserId,
            SystemRoles.Student);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(TeamProposalStatus.Pending));
        result.Value.Members.Should().HaveCount(4);
        result.Value.Members.Should().ContainSingle(member => member.IsLeader)
            .Which.StudentId.Should().Be(seed.StudentIds[1]);
        context.ChangeTracker.Clear();
        var createdTeam = await context.Teams.AsNoTracking()
            .Include(team => team.TeamMembers)
            .SingleAsync(team => team.ClassId == seed.ClassId);
        createdTeam.TeamName.Should().Be("Student Venture Team");
        createdTeam.TeamMembers.Should().HaveCount(4);
        result.Value.ApprovedTeamId.Should().Be(createdTeam.Id);
        (await context.Projects.AsNoTracking().CountAsync(project => project.TeamId == createdTeam.Id)).Should().Be(0);
        (await context.OutboxMessages.AsNoTracking().AnyAsync(message =>
            message.AggregateId == seed.ClassId && message.Type == "TeamProposal.Submitted.v1"))
            .Should().BeTrue();

        var revision = await handler.ReviewAsync(result.Value.Id, new ReviewTeamProposalRequest
        {
            Decision = "NeedsRevision", Comment = "Please clarify the project scope.", RowVersion = result.Value.RowVersion
        }, seed.LecturerId, SystemRoles.Lecturer);
        revision.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        var updated = await handler.UpdateAsync(result.Value.Id, new UpdateTeamProposalRequest
        {
            TeamName = "Student Venture Team", ProjectName = "Revised Venture Project",
            Description = "A clarified project scope for the existing student team.",
            MemberIds = seed.StudentIds, LeaderStudentId = seed.StudentIds[1], RowVersion = revision.Value.RowVersion
        }, seed.ProposerUserId, SystemRoles.Student);
        updated.IsSuccess.Should().BeTrue($"{updated.Error.Code}: {updated.Error.Message}");
        context.ChangeTracker.Clear();
        var submitted = await handler.SubmitAsync(result.Value.Id,
            new SubmitTeamProposalRequest { RowVersion = updated.Value.RowVersion }, seed.ProposerUserId, SystemRoles.Student);
        submitted.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        var approved = await handler.ReviewAsync(result.Value.Id,
            new ReviewTeamProposalRequest { Decision = "Approved", RowVersion = submitted.Value.RowVersion }, seed.LecturerId, SystemRoles.Lecturer);
        approved.IsSuccess.Should().BeTrue($"{approved.Error.Code}: {approved.Error.Message}");
        context.ChangeTracker.Clear();
        (await context.Teams.CountAsync(team => team.ClassId == seed.ClassId)).Should().Be(1);
        (await context.Projects.SingleAsync(project => project.TeamId == createdTeam.Id)).Name.Should().Be("Revised Venture Project");
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Cancelled")]
    public async Task ProjectProposalRejectionOrCancellationKeepsCreatedTeam(string decision)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: false);
        context.ChangeTracker.Clear();
        var handler = new TeamProposalHandler(context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());
        var created = await handler.SubmitStudentProposalAsync(seed.ClassId, new SubmitStudentTeamProposalRequest
        {
            StudentIds = seed.StudentIds, LeaderStudentId = seed.StudentIds[0], GroupName = "Independent Team",
            ProjectName = "Reviewable Project", Description = "A project whose review must never remove its team."
        }, seed.ProposerUserId, SystemRoles.Student);
        created.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        var reviewed = decision == "Cancelled"
            ? await handler.CancelAsync(created.Value.Id,
                new CancelTeamProposalRequest { RowVersion = created.Value.RowVersion, Reason = "Withdraw project for reconsideration." }, seed.ProposerUserId, SystemRoles.Student)
            : await handler.ReviewAsync(created.Value.Id,
                new ReviewTeamProposalRequest { Decision = decision, RowVersion = created.Value.RowVersion, Comment = "Project scope is not suitable." }, seed.LecturerId, SystemRoles.Lecturer);
        reviewed.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        (await context.Teams.SingleAsync(item => item.Id == created.Value.ApprovedTeamId)).Status.Should().Be(TeamStatus.Active);
        (await context.TeamMembers.CountAsync(item => item.TeamId == created.Value.ApprovedTeamId && item.CountsTowardActiveTeam)).Should().Be(4);
        (await context.Projects.AnyAsync(item => item.TeamId == created.Value.ApprovedTeamId)).Should().BeFalse();
    }

    [Fact]
    public async Task TeamLeaderCreatesWorkspaceLinkedToAcademicContextAndTags()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        context.ChangeTracker.Clear();
        var handler = new ProjectWorkspaceHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());

        var result = await handler.CreateAsync(
            seed.TeamId!.Value,
            new CreateProjectWorkspaceRequest
            {
                ProjectName = "Campus Circular",
                Description = "A student marketplace that helps campuses reuse equipment safely.",
                StartupField = "EdTech",
                TechnologyStack = new[] { "React", ".NET", "PostgreSQL" },
                Keywords = new[] { "campus", "circular economy" }
            },
            seed.ProposerUserId,
            SystemRoles.Student);

        result.IsSuccess.Should().BeTrue($"workspace creation failed with {result.Error.Code}: {result.Error.Message}");
        result.Value.TeamId.Should().Be(seed.TeamId!.Value);
        result.Value.ClassId.Should().Be(seed.ClassId);
        result.Value.TechnologyStack.Should().BeEquivalentTo("React", ".NET", "PostgreSQL");
        result.Value.Keywords.Should().BeEquivalentTo("campus", "circular economy");
        context.ChangeTracker.Clear();
        var targetClass = await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId);
        result.Value.SubjectId.Should().Be(targetClass.CourseId);
        result.Value.SemesterId.Should().Be(targetClass.SemesterId);
        (await context.Projects.AsNoTracking().CountAsync(project => project.TeamId == seed.TeamId)).Should().Be(1);
        (await context.OutboxMessages.AsNoTracking().AnyAsync(message =>
            message.AggregateId == seed.ClassId && message.Type == "ProjectWorkspace.Created.v1"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task WorkspaceCreationRejectsDuplicateWorkspaceNonLeaderAndInvalidTags()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        context.ChangeTracker.Clear();
        var handler = new ProjectWorkspaceHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());
        var validRequest = new CreateProjectWorkspaceRequest
        {
            ProjectName = "Founder Workspace",
            Description = "A complete project workspace description for the team.",
            StartupField = "SaaS",
            TechnologyStack = new[] { "React" },
            Keywords = new[] { "startup" }
        };

        var nonLeader = await handler.CreateAsync(
            seed.TeamId!.Value,
            validRequest,
            (await context.Students.AsNoTracking().Where(student => student.Id == seed.StudentIds[1]).Select(student => student.UserId).SingleAsync())!.Value,
            SystemRoles.Student);
        nonLeader.IsFailure.Should().BeTrue();
        nonLeader.Error.Code.Should().Be(ErrorCodes.WorkspaceLeaderRequired);

        var invalidTags = await handler.CreateAsync(
            seed.TeamId.Value,
            new CreateProjectWorkspaceRequest
            {
                ProjectName = validRequest.ProjectName,
                Description = validRequest.Description,
                StartupField = validRequest.StartupField,
                TechnologyStack = new[] { "React", " react " }
            },
            seed.ProposerUserId,
            SystemRoles.Student);
        invalidTags.IsFailure.Should().BeTrue();
        invalidTags.Error.Code.Should().Be(ErrorCodes.WorkspaceTagDuplicated);

        (await handler.CreateAsync(seed.TeamId.Value, validRequest, seed.ProposerUserId, SystemRoles.Student)).IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        var duplicate = await handler.CreateAsync(seed.TeamId.Value, validRequest, seed.ProposerUserId, SystemRoles.Student);
        duplicate.IsFailure.Should().BeTrue();
        duplicate.Error.Code.Should().Be(ErrorCodes.WorkspaceAlreadyExists);
    }

    [Fact]
    public async Task TeamMemberViewsLatestWorkspaceProfileAndActivity_WhileOutsiderIsDenied()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        context.ChangeTracker.Clear();
        var handler = new ProjectWorkspaceHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());
        var initial = new CreateProjectWorkspaceRequest
        {
            ProjectName = "Campus Circular",
            Description = "A student marketplace that helps campuses reuse equipment safely.",
            StartupField = "EdTech",
            TechnologyStack = new[] { "React", ".NET" },
            Keywords = new[] { "campus" }
        };
        (await handler.CreateAsync(seed.TeamId!.Value, initial, seed.ProposerUserId, SystemRoles.Student))
            .IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();

        var updated = await handler.UpdateAsync(
            seed.TeamId.Value,
            new UpdateProjectWorkspaceRequest
            {
                ProjectName = "Campus Circular Hub",
                Description = "The latest student marketplace profile for safe campus equipment reuse.",
                StartupField = "Circular Economy",
                TechnologyStack = new[] { "React", ".NET", "PostgreSQL" },
                Keywords = new[] { "campus", "reuse" }
            },
            seed.ProposerUserId,
            SystemRoles.Student);
        updated.IsSuccess.Should().BeTrue($"workspace update failed with {updated.Error.Code}: {updated.Error.Message}");

        context.ChangeTracker.Clear();
        var memberUserId = (await context.Students.AsNoTracking()
            .Where(student => student.Id == seed.StudentIds[1])
            .Select(student => student.UserId)
            .SingleAsync())!.Value;
        var detail = await handler.GetDetailAsync(seed.TeamId.Value, memberUserId, SystemRoles.Student);
        detail.IsSuccess.Should().BeTrue();
        detail.Value.Project!.ProjectName.Should().Be("Campus Circular Hub");
        detail.Value.Project.StartupField.Should().Be("Circular Economy");
        detail.Value.Class.Id.Should().Be(seed.ClassId);
        detail.Value.Class.SubjectId.Should().NotBeEmpty();
        detail.Value.Class.SemesterId.Should().NotBeEmpty();
        detail.Value.Members.Should().HaveCount(4);
        detail.Value.Activities.Select(activity => activity.Action)
            .Should().ContainInOrder("PROJECT_PROFILE_UPDATED", "WORKSPACE_CREATED");
        detail.Value.Activities.First().ChangedFields.Should().Contain("projectName");
        detail.Value.Activities.First().ActorName.Should().NotBe("System");

        var outsider = await CreateUserAsync(context, SystemRoles.Student, "workspace-outsider");
        context.ChangeTracker.Clear();
        var denied = await handler.GetDetailAsync(seed.TeamId.Value, outsider.Id, SystemRoles.Student);
        denied.IsFailure.Should().BeTrue();
        denied.Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
    }

    [Fact]
    public async Task DifferentLecturerCannotReviewProjectDirection()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        var otherLecturer = await CreateUserAsync(context, SystemRoles.Lecturer, "other-reviewer");
        var direction = new ProjectDirection
        {
            TeamId = seed.TeamId!.Value,
            Title = "Validated startup direction",
            Summary = "A sufficiently detailed project direction ready for lecturer review.",
            Status = ProjectDirectionStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow,
            CreatedBy = seed.ProposerUserId
        };
        context.ProjectDirections.Add(direction);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        direction = await context.ProjectDirections.AsNoTracking().SingleAsync(item => item.Id == direction.Id);

        var result = await new ProjectDirectionHandler(context).ReviewAsync(seed.TeamId.Value,
            new ReviewProjectDirectionRequest
            {
                Decision = "Approved",
                Comment = "Approved after lecturer review.",
                RowVersion = direction.Version.ToString()
            },
            otherLecturer.Id,
            SystemRoles.Lecturer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task ProjectDirection_NeedsRevisionCanBeEditedAndResubmittedWithHistoryAndOutbox()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        context.ChangeTracker.Clear();
        var handler = new ProjectDirectionHandler(context);

        var draft = await handler.SaveAsync(
            seed.TeamId!.Value,
            new SaveProjectDirectionRequest
            {
                Title = "Initial startup direction",
                Summary = "A detailed initial direction that is long enough for lecturer review."
            },
            seed.ProposerUserId,
            SystemRoles.Student);
        draft.IsSuccess.Should().BeTrue();

        var submitted = await handler.SubmitAsync(
            seed.TeamId.Value,
            new ProjectDirectionStateRequest { RowVersion = draft.Value.RowVersion },
            seed.ProposerUserId,
            SystemRoles.Student);
        submitted.IsSuccess.Should().BeTrue();

        var reviewed = await handler.ReviewAsync(
            seed.TeamId.Value,
            new ReviewProjectDirectionRequest
            {
                Decision = "NeedsRevision",
                Comment = "Clarify the target customer and validation plan.",
                RowVersion = submitted.Value.RowVersion
            },
            seed.LecturerId,
            SystemRoles.Lecturer);
        reviewed.IsSuccess.Should().BeTrue();

        var revised = await handler.SaveAsync(
            seed.TeamId.Value,
            new SaveProjectDirectionRequest
            {
                Title = "Validated startup direction",
                Summary = "The revised direction clarifies target customers, interviews, and the validation plan.",
                RowVersion = reviewed.Value.RowVersion
            },
            seed.ProposerUserId,
            SystemRoles.Student);
        revised.IsSuccess.Should().BeTrue();

        var resubmitted = await handler.SubmitAsync(
            seed.TeamId.Value,
            new ProjectDirectionStateRequest { RowVersion = revised.Value.RowVersion },
            seed.ProposerUserId,
            SystemRoles.Student);

        resubmitted.IsSuccess.Should().BeTrue();
        resubmitted.Value.Status.Should().Be(nameof(ProjectDirectionStatus.Submitted));
        resubmitted.Value.Reviews.Should().ContainSingle(review => review.ToStatus == nameof(ProjectDirectionStatus.NeedsRevision));
        context.ChangeTracker.Clear();
        (await context.OutboxMessages.AsNoTracking().CountAsync(message =>
            message.AggregateId == seed.ClassId && message.Type == "ProjectDirection.Submitted.v1"))
            .Should().Be(2);
        (await context.OutboxMessages.AsNoTracking().AnyAsync(message =>
            message.AggregateId == seed.ClassId && message.Type == "ProjectDirection.Reviewed.v1"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task LecturerGeneratedThreeMemberTeam_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: false);
        context.ChangeTracker.Clear();
        var handler = new TeamManagementHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());

        var result = await handler.GenerateAsync(
            seed.ClassId,
            new GenerateClassTeamRequest
            {
                StudentIds = seed.StudentIds.Take(3).ToArray(),
                LeaderStudentId = seed.StudentIds[0],
                TeamName = "Three Member Team"
            },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
        context.ChangeTracker.Clear();
        (await context.Teams.AsNoTracking().CountAsync(team => team.ClassId == seed.ClassId)).Should().Be(0);
        (await context.TeamProposals.AsNoTracking().CountAsync(item => item.ClassId == seed.ClassId)).Should().Be(0);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "Independent Project")]
    [InlineData(false, null)]
    public async Task LecturerGeneratedTeam_PersistsOptionalDraftProject(bool useTeamName, string? projectName)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: false);
        context.ChangeTracker.Clear();
        var handler = new TeamManagementHandler(context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());
        var result = await handler.GenerateAsync(seed.ClassId, new GenerateClassTeamRequest
        {
            StudentIds = seed.StudentIds, LeaderStudentId = seed.StudentIds[0],
            UseTeamNameForProject = useTeamName, ProjectName = projectName
        }, seed.LecturerId, SystemRoles.Lecturer);
        result.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        var savedTeam = await context.Teams.Include(item => item.Project).SingleAsync(item => item.Id == result.Value.Team!.Id);
        if (!useTeamName && projectName == null) savedTeam.Project.Should().BeNull();
        else
        {
            savedTeam.Project.Should().NotBeNull();
            savedTeam.Project!.Name.Should().Be(useTeamName ? savedTeam.TeamName : projectName);
            savedTeam.Project.Status.Should().Be(ProjectStatus.Draft);
        }
    }

    [Fact]
    public async Task ReviewingThreeMemberProposal_IsRejectedByValidation()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: true, createTeam: false);
        var proposal = await context.TeamProposals
            .Include(item => item.Members)
            .SingleAsync(item => item.Id == seed.ProposalId);
        proposal.Members.Last().IsIncluded = false;
        proposal.Members.Last().CountsTowardOpenProposal = false;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        proposal = await context.TeamProposals.AsNoTracking().SingleAsync(item => item.Id == seed.ProposalId);
        var handler = new TeamProposalHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());

        var result = await handler.ReviewAsync(
            proposal.Id,
            new ReviewTeamProposalRequest
            {
                Decision = "Approved",
                RowVersion = proposal.Version.ToString()
            },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.TeamProposalInvalid);
    }

    [Fact]
    public async Task DissolvingTeam_DeletesProjectAndReleasesNameAndMembers()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        context.Projects.Add(new Project { TeamId = seed.TeamId!.Value, Name = "Reusable Project", Status = ProjectStatus.Draft, CreatedById = seed.LecturerId });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var handler = new TeamManagementHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());

        var result = await handler.DeleteAsync(
            seed.TeamId!.Value,
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        (await context.Teams.IgnoreQueryFilters().AnyAsync(item => item.Id == seed.TeamId)).Should().BeFalse();
        (await context.Projects.IgnoreQueryFilters().AnyAsync(item => item.TeamId == seed.TeamId)).Should().BeFalse();
        (await context.ClassStudents.CountAsync(item => item.ClassId == seed.ClassId)).Should().Be(4);
        (await context.Teams.AnyAsync(item => item.Id == seed.OtherTeamId)).Should().BeTrue();
        (await context.TeamMembers.AsNoTracking()
            .AnyAsync(member => member.TeamId == seed.TeamId && member.CountsTowardActiveTeam))
            .Should().BeFalse();
        var recreated = await handler.GenerateAsync(seed.ClassId, new GenerateClassTeamRequest
        {
            TeamName = "Official Team", StudentIds = seed.StudentIds, LeaderStudentId = seed.StudentIds[0],
            ProjectName = "Reusable Project"
        }, seed.LecturerId, SystemRoles.Lecturer);
        recreated.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(SystemRoles.Admin)]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Mentor)]
    [InlineData(SystemRoles.Lecturer)]
    public async Task DissolvingTeam_DeniesEveryoneExceptAssignedLecturer(string role)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        context.ChangeTracker.Clear();
        var handler = new TeamManagementHandler(context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());
        var result = await handler.DeleteAsync(seed.TeamId!.Value, seed.AdminId, role);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
        (await context.Teams.AnyAsync(item => item.Id == seed.TeamId)).Should().BeTrue();
    }

    [Fact]
    public async Task AssigningDirectoryStudentToClass_CreatesEnrollmentAndChatSyncOutboxEvent()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: false);
        var studentUser = await CreateUserAsync(context, SystemRoles.Student, "class-assignment");
        var student = new Student
        {
            UserId = studentUser.Id,
            User = studentUser,
            RollNumber = $"SE{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            FullName = studentUser.FullName,
            Email = studentUser.Email,
            MajorCode = MajorCodes.BIT_SE,
            Status = StudentStatus.Active,
            CreatedBy = seed.AdminId
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var handler = new AssignStudentsCommandHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());
        var result = await handler.AssignToClassAsync(
            seed.ClassId,
            new AssignStudentsToClassRequest { StudentIds = new[] { studentUser.Id } },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        result.Value.AssignedStudentIds.Should().ContainSingle().Which.Should().Be(student.Id);
        context.ChangeTracker.Clear();
        (await context.ClassStudents.AsNoTracking().SingleAsync(item =>
            item.ClassId == seed.ClassId && item.StudentId == student.Id)).EnrollmentStatus.Should().Be(EnrollmentStatus.Active);
        (await context.OutboxMessages.AsNoTracking().AnyAsync(message =>
            message.AggregateId == seed.ClassId && message.Type == "Class.StudentEnrollmentAdded.v1")).Should().BeTrue();
    }

    [Fact]
    public async Task AssigningClassStudentToTeam_AddsMemberAndPreservesSingleLeader()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        var targetClass = await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId);
        var studentUser = await CreateUserAsync(context, SystemRoles.Student, "team-assignment");
        var student = new Student
        {
            UserId = studentUser.Id,
            User = studentUser,
            RollNumber = $"SE{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            FullName = studentUser.FullName,
            Email = studentUser.Email,
            MajorCode = MajorCodes.BIT_SE,
            Status = StudentStatus.Active,
            CreatedBy = seed.AdminId
        };
        context.Students.Add(student);
        context.ClassStudents.Add(new ClassStudent
        {
            ClassId = seed.ClassId,
            StudentId = student.Id,
            SemesterId = targetClass.SemesterId,
            CourseId = targetClass.CourseId,
            EnrollmentStatus = EnrollmentStatus.Active,
            CountsTowardCourseSemesterLimit = true,
            MajorCodeAtEnrollment = MajorCodes.BIT_SE
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var handler = new AssignStudentsCommandHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());
        var result = await handler.AssignToTeamAsync(
            seed.ClassId,
            seed.TeamId!.Value,
            new AssignStudentsToTeamRequest { StudentIds = new[] { student.Id } },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        (await context.TeamMembers.AsNoTracking().CountAsync(item =>
            item.TeamId == seed.TeamId && item.CountsTowardActiveTeam)).Should().Be(5);
        (await context.TeamMembers.AsNoTracking().CountAsync(item =>
            item.TeamId == seed.TeamId && item.CountsTowardActiveTeam && item.RoleInTeam == TeamMemberRole.Leader)).Should().Be(1);
        (await context.OutboxMessages.AsNoTracking().AnyAsync(message =>
            message.AggregateId == seed.ClassId && message.Type == "Team.MembersUpdated.v1")).Should().BeTrue();
    }

    [Fact]
    public async Task AssigningStudentAlreadyInAnotherClassTeam_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        context.ChangeTracker.Clear();
        var handler = new AssignStudentsCommandHandler(
            context,
            scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Persistence.IUnitOfWork>());

        var result = await handler.AssignToTeamAsync(
            seed.ClassId,
            seed.OtherTeamId!.Value,
            new AssignStudentsToTeamRequest { StudentIds = new[] { seed.StudentIds[0] } },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.TeamMembershipConflict);
    }

    [Theory]
    [InlineData(ClassStatus.Completed)]
    [InlineData(ClassStatus.Archived)]
    public async Task ClosedClass_BlocksAllRoadmapMutations(ClassStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        var targetClass = await context.Classes.Include(x => x.Course).SingleAsync(x => x.Id == seed.ClassId);
        var handler = new WorkspaceToolsHandler(context, new SaveWeeklyTaskRequestValidator(), new SaveShortcutRequestValidator());
        var request = new SaveWeeklyTaskRequest { Title = "Roadmap regression", TaskType = "CLASS_TASK", Scope = "CLASS", WeekNumber = 1, CourseCode = targetClass.Course.Code, ClassId = seed.ClassId };
        var created = await handler.CreateWeeklyTaskAsync(request, seed.AdminId, SystemRoles.Admin);
        created.IsSuccess.Should().BeTrue();
        targetClass.Status = status;
        if (status == ClassStatus.Completed)
        {
            targetClass.CompletedAtUtc = DateTime.UtcNow;
            targetClass.CompletedByUserId = seed.AdminId;
            targetClass.CompletionReason = "Regression test: completed class is read-only.";
        }
        await context.SaveChangesAsync();
        (await handler.UpdateWeeklyTaskAsync(created.Value.Id, request, seed.AdminId, SystemRoles.Admin)).IsFailure.Should().BeTrue();
        (await handler.UpdateWeeklyTaskStatusAsync(created.Value.Id, new UpdateWeeklyTaskStatusRequest { Status = "COMPLETED" }, seed.AdminId, SystemRoles.Admin)).IsFailure.Should().BeTrue();
        (await handler.DeleteWeeklyTaskAsync(created.Value.Id, seed.AdminId, SystemRoles.Admin)).IsFailure.Should().BeTrue();
        (await handler.CreateWeeklyTaskAsync(request, seed.AdminId, SystemRoles.Admin)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateRoadmap_RejectsAssigneeOutsideTeam_WithoutChangingTask()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        var targetClass = await context.Classes.Include(x => x.Course).SingleAsync(x => x.Id == seed.ClassId);
        var handler = new WorkspaceToolsHandler(context, new SaveWeeklyTaskRequestValidator(), new SaveShortcutRequestValidator());
        var created = await handler.CreateWeeklyTaskAsync(new SaveWeeklyTaskRequest { Title = "Original", WeekNumber = 1, CourseCode = targetClass.Course.Code, ClassId = seed.ClassId, TeamId = seed.TeamId }, seed.AdminId, SystemRoles.Admin);
        created.IsSuccess.Should().BeTrue();
        var result = await handler.UpdateWeeklyTaskAsync(created.Value.Id, new SaveWeeklyTaskRequest { Title = "Invalid edit", WeekNumber = 1, CourseCode = targetClass.Course.Code, ClassId = seed.ClassId, TeamId = seed.TeamId, AssigneeStudentId = Guid.NewGuid() }, seed.AdminId, SystemRoles.Admin);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.WorkspaceValidationError);
        context.ChangeTracker.Clear();
        (await context.WeeklyTasks.SingleAsync(x => x.Id == created.Value.Id)).Title.Should().Be("Original");
    }

    [Fact]
    public async Task RoadmapDates_RoundTripDateOnlyValues_AndCanBeCleared()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        var course = await context.Classes.Where(x => x.Id == seed.ClassId).Select(x => x.Course).SingleAsync();
        var handler = new WorkspaceToolsHandler(context, new SaveWeeklyTaskRequestValidator(), new SaveShortcutRequestValidator());
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var result = await handler.CreateWeeklyTaskAsync(new SaveWeeklyTaskRequest { Title = "Dates QA", CourseCode = course.Code, ClassId = seed.ClassId, TeamId = seed.TeamId, WeekNumber = 1, StartDate = start, DueDate = start.AddDays(1) }, seed.AdminId, SystemRoles.Admin);
        result.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        var saved = await context.WeeklyTasks.SingleAsync(x => x.Id == result.Value.Id);
        saved.StartDate.Should().Be(DateTime.SpecifyKind(start, DateTimeKind.Utc));
        saved.StartDate!.Value.Kind.Should().Be(DateTimeKind.Utc);
        var updated = await handler.UpdateWeeklyTaskAsync(saved.Id, new SaveWeeklyTaskRequest { Title = "Dates cleared", CourseCode = course.Code, ClassId = seed.ClassId, TeamId = seed.TeamId, WeekNumber = 1 }, seed.AdminId, SystemRoles.Admin);
        updated.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        (await context.WeeklyTasks.SingleAsync(x => x.Id == saved.Id)).StartDate.Should().BeNull();
    }

    [Fact]
    public async Task RoadmapRead_RejectsClassAndCourseOutsideAuthorizedTeam()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        var handler = new WorkspaceToolsHandler(context, new SaveWeeklyTaskRequestValidator(), new SaveShortcutRequestValidator());
        var wrongClass = await handler.GetWeeklyTasksAsync(new WeeklyTaskQuery { TeamId = seed.TeamId, ClassId = Guid.NewGuid() }, seed.ProposerUserId, SystemRoles.Student);
        wrongClass.IsFailure.Should().BeTrue();
        wrongClass.Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
        var wrongCourse = await handler.GetWeeklyTasksAsync(new WeeklyTaskQuery { TeamId = seed.TeamId, CourseCode = "OTHER" }, seed.ProposerUserId, SystemRoles.Student);
        wrongCourse.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecutionBoard_IncludesAllRoadmapSourcesAndWeeks_WithFiltersAndSharedStatus()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        var course = await context.Classes.Where(x => x.Id == seed.ClassId).Select(x => x.Course).SingleAsync();
        var handler = new WorkspaceToolsHandler(context, new SaveWeeklyTaskRequestValidator(), new SaveShortcutRequestValidator());
        foreach (var kind in new[] { "COURSE_TEMPLATE", "CLASS_TASK", "TEAM_TASK" })
        {
            var created = await handler.CreateWeeklyTaskAsync(new SaveWeeklyTaskRequest
            {
                Title = $"Board {kind}", TaskType = kind,
                Scope = kind == "COURSE_TEMPLATE" ? "COURSE" : kind == "CLASS_TASK" ? "CLASS" : "TEAM",
                CourseCode = course.Code, WeekNumber = kind == "TEAM_TASK" ? 2 : 1,
                ClassId = kind == "COURSE_TEMPLATE" ? null : seed.ClassId,
                TeamId = kind == "TEAM_TASK" ? seed.TeamId : null, Priority = "HIGH"
            }, seed.AdminId, SystemRoles.Admin);
            created.IsSuccess.Should().BeTrue();
        }
        var board = await handler.GetWeeklyTasksAsync(new WeeklyTaskQuery { TeamId = seed.TeamId }, seed.ProposerUserId, SystemRoles.Student);
        board.IsSuccess.Should().BeTrue();
        board.Value.CourseTasks.Should().ContainSingle();
        board.Value.ClassTasks.Should().ContainSingle();
        board.Value.TeamTasks.Should().ContainSingle();
        var filtered = await handler.GetWeeklyTasksAsync(new WeeklyTaskQuery { TeamId = seed.TeamId, WeekNumber = 2, Search = "TEAM_TASK", Priority = "HIGH" }, seed.ProposerUserId, SystemRoles.Student);
        filtered.Value.TeamTasks.Should().ContainSingle();
        filtered.Value.CourseTasks.Should().BeEmpty();
        var taskId = board.Value.TeamTasks.Single().Id;
        (await handler.UpdateWeeklyTaskStatusAsync(taskId, new UpdateWeeklyTaskStatusRequest { Status = "COMPLETED" }, seed.ProposerUserId, SystemRoles.Student)).IsSuccess.Should().BeTrue();
        var roadmap = await handler.GetWeeklyTasksAsync(new WeeklyTaskQuery { TeamId = seed.TeamId, WeekNumber = 2 }, seed.ProposerUserId, SystemRoles.Student);
        roadmap.Value.TeamTasks.Single().Status.Should().Be("COMPLETED");
        (await handler.UpdateWeeklyTaskStatusAsync(board.Value.CourseTasks.Single().Id, new UpdateWeeklyTaskStatusRequest { Status = "COMPLETED" }, seed.ProposerUserId, SystemRoles.Student)).IsFailure.Should().BeTrue();
        (await handler.GetWeeklyTasksAsync(new WeeklyTaskQuery { TeamId = seed.TeamId }, Guid.NewGuid(), SystemRoles.Student)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task StudentCannotReadHiddenTaskOrDeleteAnotherCreatorsTask()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        var course = await context.Classes.Where(x => x.Id == seed.ClassId).Select(x => x.Course).SingleAsync();
        var handler = new WorkspaceToolsHandler(context, new SaveWeeklyTaskRequestValidator(), new SaveShortcutRequestValidator());
        var created = await handler.CreateWeeklyTaskAsync(new SaveWeeklyTaskRequest { Title = "Staff-only QA", CourseCode = course.Code, ClassId = seed.ClassId, TeamId = seed.TeamId, WeekNumber = 1, VisibleToStudents = false }, seed.AdminId, SystemRoles.Admin);
        created.IsSuccess.Should().BeTrue();
        var studentBoard = await handler.GetWeeklyTasksAsync(new WeeklyTaskQuery { TeamId = seed.TeamId }, seed.ProposerUserId, SystemRoles.Student);
        studentBoard.IsSuccess.Should().BeTrue();
        studentBoard.Value.TeamTasks.Should().NotContain(x => x.Id == created.Value.Id);
        var adminBoard = await handler.GetWeeklyTasksAsync(new WeeklyTaskQuery { TeamId = seed.TeamId }, seed.AdminId, SystemRoles.Admin);
        adminBoard.Value.TeamTasks.Should().Contain(x => x.Id == created.Value.Id);
        var deleted = await handler.DeleteWeeklyTaskAsync(created.Value.Id, seed.ProposerUserId, SystemRoles.Student);
        deleted.IsFailure.Should().BeTrue();
        deleted.Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
    }

    private static async Task<WorkflowSeed> CreateSeedAsync(AppDbContext context, bool createProposal, bool createTeam)
    {
        var admin = await context.Users.Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .FirstAsync(user => user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Admin));
        var lecturer = await CreateUserAsync(context, SystemRoles.Lecturer, "owner");
        var mentorUser = await CreateUserAsync(context, SystemRoles.Mentor, "mentor");
        var mentorProfile = new MentorProfile
        {
            UserId = mentorUser.Id,
            User = mentorUser,
            Organization = "Integration Mentor Org",
            Status = MentorProfileStatus.Active,
            MaxTeams = 3,
            CreatedBy = admin.Id
        };
        context.MentorProfiles.Add(mentorProfile);

        var unique = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var course = new Course { Code = $"TW{unique}", Name = $"Team Workflow {unique}", Status = CourseStatus.Active, CreatedBy = admin.Id };
        var semester = await context.Semesters.FirstAsync(item => item.Status == SemesterStatus.Active);
        var targetClass = new Class
        {
            ClassCode = $"TW{unique}_1",
            Slug = ClassSlugRules.BuildBaseSlug(semester.Code, course.Code, 1),
            ClassIndex = 1,
            CourseId = course.Id,
            Course = course,
            SemesterId = semester.Id,
            Semester = semester,
            PrimaryLecturerId = lecturer.Id,
            PrimaryLecturer = lecturer,
            Status = ClassStatus.Active,
            ScheduleJson = "[{\"dayOfWeek\":2,\"slotNumber\":4,\"room\":\"TW-401\"}]",
            CreatedById = admin.Id,
            CreatedBy = admin.Id
        };
        context.Courses.Add(course);
        context.Classes.Add(targetClass);
        context.ClassLecturers.Add(new ClassLecturer { ClassId = targetClass.Id, LecturerId = lecturer.Id, IsPrimary = true, AssignedById = admin.Id });
        context.SemesterStaffAssignments.AddRange(
            new SemesterStaffAssignment
            {
                SemesterId = semester.Id,
                Semester = semester,
                UserId = lecturer.Id,
                User = lecturer,
                Role = SemesterStaffRole.Lecturer,
                Status = SemesterStaffStatus.Active,
                CreatedBy = admin.Id
            },
            new SemesterStaffAssignment
            {
                SemesterId = semester.Id,
                Semester = semester,
                UserId = mentorUser.Id,
                User = mentorUser,
                Role = SemesterStaffRole.Mentor,
                Status = SemesterStaffStatus.Active,
                CreatedBy = admin.Id
            });

        var studentIds = new List<Guid>();
        var studentUsers = new List<User>();
        for (var index = 0; index < 4; index++)
        {
            var studentUser = await CreateUserAsync(context, SystemRoles.Student, $"student-{index}");
            var rollNumber = $"SE{Guid.NewGuid():N}"[..10].ToUpperInvariant();
            var major = index == 0 ? MajorCodes.BEN : MajorCodes.BIT_SE;
            var student = new Student
            {
                UserId = studentUser.Id,
                User = studentUser,
                RollNumber = rollNumber,
                NormalizedRollNumber = rollNumber,
                FullName = studentUser.FullName,
                Email = studentUser.Email,
                MajorCode = major,
                Status = StudentStatus.Active,
                CreatedBy = admin.Id
            };
            context.Students.Add(student);
            context.ClassStudents.Add(new ClassStudent
            {
                ClassId = targetClass.Id,
                StudentId = student.Id,
                Class = targetClass,
                Student = student,
                SemesterId = semester.Id,
                CourseId = course.Id,
                EnrollmentStatus = EnrollmentStatus.Active,
                CountsTowardCourseSemesterLimit = true,
                MajorCodeAtEnrollment = major
            });
            studentIds.Add(student.Id);
            studentUsers.Add(studentUser);
        }

        Team? team = null;
        Team? otherTeam = null;
        if (createTeam)
        {
            team = new Team { ClassId = targetClass.Id, Class = targetClass, TeamCode = $"{targetClass.ClassCode}_T1", TeamName = "Official Team", Status = TeamStatus.Active, CreatedById = admin.Id, CreatedBy = admin.Id };
            for (var index = 0; index < studentIds.Count; index++)
            {
                team.TeamMembers.Add(new TeamMember
                {
                    TeamId = team.Id,
                    Team = team,
                    ClassId = targetClass.Id,
                    StudentId = studentIds[index],
                    RoleInTeam = index == 0 ? TeamMemberRole.Leader : TeamMemberRole.Member,
                    CountsTowardActiveTeam = true,
                    JoinedAt = DateTime.UtcNow,
                    CreatedById = admin.Id
                });
            }
            team.MentorAssignments.Add(new MentorAssignment
            {
                MentorProfileId = mentorProfile.Id,
                MentorProfile = mentorProfile,
                TeamId = team.Id,
                Team = team,
                AssignedById = admin.Id,
                AssignedAt = DateTime.UtcNow,
                Status = MentorAssignmentStatus.Active,
                CreatedBy = admin.Id
            });
            otherTeam = new Team { ClassId = targetClass.Id, Class = targetClass, TeamCode = $"{targetClass.ClassCode}_T2", TeamName = "Unassigned Mentor Team", Status = TeamStatus.Active, CreatedById = admin.Id, CreatedBy = admin.Id };
            context.Teams.AddRange(team, otherTeam);
        }

        TeamProposal? proposal = null;
        if (createProposal)
        {
            proposal = new TeamProposal
            {
                ClassId = targetClass.Id,
                Class = targetClass,
                ProposedByStudentId = studentIds[0],
                TeamName = "Pending Proposal",
                Description = "Balanced student proposal",
                ProjectName = "Workflow Project",
                Status = TeamProposalStatus.Pending,
                SubmittedAtUtc = DateTime.UtcNow,
                CreatedBy = studentUsers[0].Id
            };
            for (var index = 0; index < studentIds.Count; index++)
            {
                proposal.Members.Add(new TeamProposalMember
                {
                    ProposalId = proposal.Id,
                    Proposal = proposal,
                    ClassId = targetClass.Id,
                    StudentId = studentIds[index],
                    IsLeader = index == 0,
                    IsIncluded = true,
                    CountsTowardOpenProposal = true
                });
            }
            context.TeamProposals.Add(proposal);
        }

        await context.SaveChangesAsync();
        return new WorkflowSeed(targetClass.Id, admin.Id, lecturer.Id, mentorUser.Id, studentUsers[0].Id,
            studentIds.ToArray(), proposal?.Id, team?.Id, otherTeam?.Id);
    }

    private sealed record TestCurrentUser(Guid Id, string Role) : ICurrentUserService
    {
        public Guid? UserId => Id;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => new[] { Role };
        public bool IsAuthenticated => true;
    }

    private static async Task<User> CreateUserAsync(AppDbContext context, string roleName, string suffix)
    {
        var role = await context.Roles.SingleAsync(item => item.Name == roleName);
        var email = $"team-{suffix}-{Guid.NewGuid():N}@example.com";
        var user = new User
        {
            FullName = $"Team {suffix}",
            Email = email,
            NormalizedEmail = email.ToLowerInvariant(),
            PasswordHash = "integration-test-only",
            Status = UserStatus.Active,
            IsEmailVerified = true
        };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, User = user, Role = role });
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private sealed record WorkflowSeed(
        Guid ClassId,
        Guid AdminId,
        Guid LecturerId,
        Guid MentorUserId,
        Guid ProposerUserId,
        Guid[] StudentIds,
        Guid? ProposalId,
        Guid? TeamId,
        Guid? OtherTeamId);
}
