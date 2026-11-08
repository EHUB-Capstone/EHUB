using EHub.Application.Features.Teams.FeedbackOverview;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EHub.ApplicationTests.Features.Teams.FeedbackOverview;

public sealed class TeamFeedbackOverviewHandlerTests
{
    private sealed class World
    {
        public required AppDbContext Context { get; init; }
        public required Guid TeamId { get; init; }
        public required Guid AdminId { get; init; }
        public required Guid LecturerId { get; init; }
        public required Guid CoLecturerId { get; init; }
        public required Guid OtherLecturerId { get; init; }
        public required Guid MentorId { get; init; }
        public required Guid OtherMentorId { get; init; }
        public required Guid UnassignedMentorId { get; init; }
        public required Guid StudentId { get; init; }
        public required Guid OutsiderStudentId { get; init; }
        public required Guid MentorThreadId { get; init; }
    }

    private static AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User NewUser(string roleName, string name, Dictionary<string, Role> roles)
    {
        var user = new User
        {
            FullName = name,
            Email = $"{name.ToLowerInvariant().Replace(' ', '.')}@example.test",
            NormalizedEmail = $"{name.ToLowerInvariant().Replace(' ', '.')}@example.test",
            PasswordHash = "unit-test"
        };
        var role = roles[roleName];
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, User = user, Role = role });
        return user;
    }

    private static async Task<World> SeedAsync()
    {
        var context = NewContext();
        var roles = new[] { SystemRoles.Admin, SystemRoles.Lecturer, SystemRoles.Mentor, SystemRoles.Student }
            .ToDictionary(name => name, name => new Role { Name = name });
        context.Roles.AddRange(roles.Values);

        var admin = NewUser(SystemRoles.Admin, "Admin One", roles);
        var lecturer = NewUser(SystemRoles.Lecturer, "Lecturer Primary", roles);
        var coLecturer = NewUser(SystemRoles.Lecturer, "Lecturer Co", roles);
        var otherLecturer = NewUser(SystemRoles.Lecturer, "Lecturer Other", roles);
        var mentor = NewUser(SystemRoles.Mentor, "Mentor Mine", roles);
        var otherMentor = NewUser(SystemRoles.Mentor, "Mentor Another", roles);
        var unassignedMentor = NewUser(SystemRoles.Mentor, "Mentor Unassigned", roles);
        var studentUser = NewUser(SystemRoles.Student, "Student Member", roles);
        var outsiderUser = NewUser(SystemRoles.Student, "Student Outsider", roles);
        context.Users.AddRange(admin, lecturer, coLecturer, otherLecturer, mentor, otherMentor, unassignedMentor, studentUser, outsiderUser);

        var course = new Course { Code = "EXE101", Name = "Entrepreneurship" };
        var semester = new Semester { Code = "FA26", Name = "Fall 2026" };
        var @class = new Class
        {
            ClassCode = "EXE101_1",
            Slug = "fa26-exe101-1",
            CourseId = course.Id,
            SemesterId = semester.Id,
            PrimaryLecturerId = lecturer.Id
        };
        context.Courses.Add(course);
        context.Semesters.Add(semester);
        context.Classes.Add(@class);
        context.ClassLecturers.Add(new ClassLecturer { ClassId = @class.Id, LecturerId = coLecturer.Id });

        var student = new Student { RollNumber = "SE1", NormalizedRollNumber = "SE1", FullName = "Student Member", UserId = studentUser.Id };
        var outsider = new Student { RollNumber = "SE2", NormalizedRollNumber = "SE2", FullName = "Student Outsider", UserId = outsiderUser.Id };
        context.Students.AddRange(student, outsider);
        var enrollment = new ClassStudent
        {
            ClassId = @class.Id, StudentId = student.Id, SemesterId = semester.Id, CourseId = course.Id,
            EnrollmentStatus = EnrollmentStatus.Active
        };
        context.ClassStudents.Add(enrollment);

        var team = new Team { ClassId = @class.Id, TeamCode = "T1", TeamName = "Alpha" };
        team.TeamMembers.Add(new TeamMember
        {
            TeamId = team.Id, ClassId = @class.Id, StudentId = student.Id, CountsTowardActiveTeam = true
        });
        var project = new Project { TeamId = team.Id, Name = "Wallet" };
        team.Project = project;
        context.Teams.Add(team);

        var mentorProfile = new MentorProfile { UserId = mentor.Id };
        var otherMentorProfile = new MentorProfile { UserId = otherMentor.Id };
        context.MentorProfiles.AddRange(mentorProfile, otherMentorProfile);
        context.MentorAssignments.AddRange(
            new MentorAssignment { MentorProfileId = mentorProfile.Id, TeamId = team.Id, AssignedById = admin.Id, Status = MentorAssignmentStatus.Active },
            new MentorAssignment { MentorProfileId = otherMentorProfile.Id, TeamId = team.Id, AssignedById = admin.Id, Status = MentorAssignmentStatus.Active, Slot = MentorType.Academic });

        var checkpoint = new Checkpoint { CourseId = course.Id, Name = "Checkpoint 1", CheckpointNumber = 1, Status = CheckpointStatus.Open };
        var rubric = new Rubric { Name = "Rubric 1", CheckpointId = checkpoint.Id };
        var criterion = new RubricCriterion { RubricId = rubric.Id, Name = "Problem fit", DisplayOrder = 1 };
        rubric.Criteria.Add(criterion);
        context.Checkpoints.Add(checkpoint);
        context.Rubrics.Add(rubric);

        var submission = new Submission
        {
            ProjectId = project.Id, TeamId = team.Id, CheckpointId = checkpoint.Id, VersionNumber = 1, Title = "v1"
        };
        context.Submissions.Add(submission);

        var baseTime = DateTime.UtcNow.AddDays(-1);
        var lecturerThread = new SubmissionFeedback
        {
            SubmissionId = submission.Id, Content = "Lecturer comment", CreatedById = lecturer.Id, CreatedAt = baseTime
        };
        var mentorThread = new SubmissionFeedback
        {
            SubmissionId = submission.Id, Content = "TECHNICAL REVIEW\nMentor review", CreatedById = mentor.Id, CreatedAt = baseTime.AddMinutes(1)
        };
        var otherMentorThread = new SubmissionFeedback
        {
            SubmissionId = submission.Id, Content = "Other mentor comment", CreatedById = otherMentor.Id, CreatedAt = baseTime.AddMinutes(2)
        };
        var studentReply = new SubmissionFeedback
        {
            SubmissionId = submission.Id, Content = "Thanks, fixed", CreatedById = studentUser.Id,
            ParentFeedbackId = mentorThread.Id, CreatedAt = baseTime.AddMinutes(3)
        };
        var adminThread = new SubmissionFeedback
        {
            SubmissionId = submission.Id, Content = "Admin comment", CreatedById = admin.Id, CreatedAt = baseTime.AddMinutes(4)
        };
        context.SubmissionFeedbacks.AddRange(lecturerThread, mentorThread, otherMentorThread, studentReply, adminThread);

        context.Evaluations.AddRange(
            new Evaluation
            {
                ProjectId = project.Id, SubmissionId = submission.Id, RubricId = rubric.Id, EvaluatorId = lecturer.Id,
                Status = EvaluationStatus.Published, TotalScore = 8, MaxTotalScore = 10,
                OverallFeedback = "Solid work", Strengths = "Clear problem", PublishedAt = baseTime,
                Details = { new EvaluationDetail { RubricCriterionId = criterion.Id, Score = 4, Comment = "Good fit" } }
            },
            new Evaluation
            {
                ProjectId = project.Id, SubmissionId = submission.Id, RubricId = rubric.Id, EvaluatorId = coLecturer.Id,
                Status = EvaluationStatus.Draft, OverallFeedback = "Draft notes of co-lecturer"
            });

        await context.SaveChangesAsync();

        return new World
        {
            Context = context,
            TeamId = team.Id,
            AdminId = admin.Id,
            LecturerId = lecturer.Id,
            CoLecturerId = coLecturer.Id,
            OtherLecturerId = otherLecturer.Id,
            MentorId = mentor.Id,
            OtherMentorId = otherMentor.Id,
            UnassignedMentorId = unassignedMentor.Id,
            StudentId = studentUser.Id,
            OutsiderStudentId = outsiderUser.Id,
            MentorThreadId = mentorThread.Id
        };
    }

    [Fact]
    public async Task Admin_SeesAllThreadsAndEvaluations()
    {
        var world = await SeedAsync();
        var handler = new TeamFeedbackOverviewHandler(world.Context);

        var result = await handler.GetAsync(world.TeamId, world.AdminId, SystemRoles.Admin);

        result.IsSuccess.Should().BeTrue();
        var checkpoint = result.Value.Checkpoints.Single();
        checkpoint.Threads.Select(thread => thread.Content).Should().HaveCount(4);
        // Draft evaluations of someone else are never shown.
        checkpoint.Evaluations.Should().ContainSingle().Which.Status.Should().Be("Published");
    }

    [Fact]
    public async Task Admin_UnknownTeam_ReturnsNotFound()
    {
        var world = await SeedAsync();
        var handler = new TeamFeedbackOverviewHandler(world.Context);

        var result = await handler.GetAsync(Guid.NewGuid(), world.AdminId, SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.TeamNotFound);
    }

    [Fact]
    public async Task PrimaryAndCoLecturer_CanView_ButOtherLecturerIsDenied()
    {
        var world = await SeedAsync();
        var handler = new TeamFeedbackOverviewHandler(world.Context);

        (await handler.GetAsync(world.TeamId, world.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        (await handler.GetAsync(world.TeamId, world.CoLecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();

        var denied = await handler.GetAsync(world.TeamId, world.OtherLecturerId, SystemRoles.Lecturer);
        denied.IsFailure.Should().BeTrue();
        denied.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task Lecturer_SeesOwnDraftEvaluation_ButNotAnotherLecturersDraft()
    {
        var world = await SeedAsync();
        var handler = new TeamFeedbackOverviewHandler(world.Context);

        var coLecturer = await handler.GetAsync(world.TeamId, world.CoLecturerId, SystemRoles.Lecturer);
        coLecturer.Value.Checkpoints.Single().Evaluations.Should().HaveCount(2);

        var primary = await handler.GetAsync(world.TeamId, world.LecturerId, SystemRoles.Lecturer);
        primary.Value.Checkpoints.Single().Evaluations.Should().ContainSingle()
            .Which.Evaluator.Id.Should().Be(world.LecturerId);
    }

    [Fact]
    public async Task Student_Member_SeesThreadsAndPublishedEvaluationText_WithoutDrafts()
    {
        var world = await SeedAsync();
        var handler = new TeamFeedbackOverviewHandler(world.Context);

        var result = await handler.GetAsync(world.TeamId, world.StudentId, SystemRoles.Student);

        result.IsSuccess.Should().BeTrue();
        var checkpoint = result.Value.Checkpoints.Single();
        checkpoint.Threads.Should().HaveCount(4);
        var evaluation = checkpoint.Evaluations.Should().ContainSingle().Subject;
        evaluation.OverallFeedback.Should().Be("Solid work");
        evaluation.CriterionComments.Should().ContainSingle().Which.Comment.Should().Be("Good fit");
    }

    [Fact]
    public async Task Student_NotInTeam_IsDenied()
    {
        var world = await SeedAsync();
        var handler = new TeamFeedbackOverviewHandler(world.Context);

        var result = await handler.GetAsync(world.TeamId, world.OutsiderStudentId, SystemRoles.Student);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task Student_WhenProjectDirectionIsNotApproved_IsDenied()
    {
        var world = await SeedAsync();
        world.Context.ProjectDirections.Add(new ProjectDirection { TeamId = world.TeamId, Status = ProjectDirectionStatus.Draft });
        await world.Context.SaveChangesAsync();
        var handler = new TeamFeedbackOverviewHandler(world.Context);

        var result = await handler.GetAsync(world.TeamId, world.StudentId, SystemRoles.Student);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task Mentor_OnlySeesOwnThreadsWithReplies_AndNoEvaluations()
    {
        var world = await SeedAsync();
        var handler = new TeamFeedbackOverviewHandler(world.Context);

        var result = await handler.GetAsync(world.TeamId, world.MentorId, SystemRoles.Mentor);

        result.IsSuccess.Should().BeTrue();
        var checkpoint = result.Value.Checkpoints.Single();
        var thread = checkpoint.Threads.Should().ContainSingle().Subject;
        thread.Id.Should().Be(world.MentorThreadId);
        thread.IsTechnicalReview.Should().BeTrue();
        thread.Author.Id.Should().Be(world.MentorId);
        thread.Replies.Should().ContainSingle().Which.Content.Should().Be("Thanks, fixed");
        checkpoint.Threads.Select(item => item.Content).Should()
            .NotContain(["Lecturer comment", "Admin comment", "Other mentor comment"]);
        checkpoint.Evaluations.Should().BeEmpty();
    }

    [Fact]
    public async Task Mentor_SecondMentor_DoesNotSeeFirstMentorsThread()
    {
        var world = await SeedAsync();
        var handler = new TeamFeedbackOverviewHandler(world.Context);

        var result = await handler.GetAsync(world.TeamId, world.OtherMentorId, SystemRoles.Mentor);

        result.IsSuccess.Should().BeTrue();
        var threads = result.Value.Checkpoints.Single().Threads;
        threads.Should().ContainSingle().Which.Content.Should().Be("Other mentor comment");
    }

    [Fact]
    public async Task Mentor_NotAssignedToTeam_IsDenied()
    {
        var world = await SeedAsync();
        var handler = new TeamFeedbackOverviewHandler(world.Context);

        var result = await handler.GetAsync(world.TeamId, world.UnassignedMentorId, SystemRoles.Mentor);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task UnknownRole_IsDenied()
    {
        var world = await SeedAsync();
        var handler = new TeamFeedbackOverviewHandler(world.Context);

        var result = await handler.GetAsync(world.TeamId, world.AdminId, "UNKNOWN");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public void Contract_DoesNotExposeAnyScoreField()
    {
        var names = typeof(EHub.Contracts.Teams.TeamFeedbackEvaluationDto).GetProperties().Select(property => property.Name);

        names.Should().NotContain(name => name.Contains("Score", StringComparison.OrdinalIgnoreCase));
    }
}
