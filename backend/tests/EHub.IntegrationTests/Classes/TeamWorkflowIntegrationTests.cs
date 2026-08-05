using EHub.Application.Features.Classes.GetClassRoster;
using EHub.Application.Features.Classes.RemoveStudentFromClass;
using EHub.Application.Features.Classes.StudentSelfService;
using EHub.Application.Features.Teams.ManageTeams;
using EHub.Application.Features.Teams.MentorAssignments;
using EHub.Application.Features.Teams.ProjectDirections;
using EHub.Application.Features.Teams.TeamProposals;
using EHub.Contracts.Classes;
using EHub.Contracts.Teams;
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
        var semester = new Semester { Code = $"TW{unique}", Name = $"Team Semester {unique}", Term = SemesterTerm.Fall, Year = 2032, Status = SemesterStatus.Active, CreatedBy = admin.Id };
        var targetClass = new Class
        {
            ClassCode = $"TW{unique}_1",
            ClassIndex = 1,
            CourseId = course.Id,
            Course = course,
            SemesterId = semester.Id,
            Semester = semester,
            PrimaryLecturerId = lecturer.Id,
            PrimaryLecturer = lecturer,
            Status = ClassStatus.Active,
            ScheduleJson = "[]",
            CreatedById = admin.Id,
            CreatedBy = admin.Id
        };
        context.Courses.Add(course);
        context.Semesters.Add(semester);
        context.Classes.Add(targetClass);
        context.ClassLecturers.Add(new ClassLecturer { ClassId = targetClass.Id, LecturerId = lecturer.Id, IsPrimary = true, AssignedById = admin.Id });

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
