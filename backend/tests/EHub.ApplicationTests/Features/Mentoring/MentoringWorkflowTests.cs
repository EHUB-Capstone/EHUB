using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Application.Features.Mentoring;
using EHub.Contracts.Mentoring;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentoringWorkflowTests
{
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task RecommendationRequiresAssignedLecturerAndRanksRelevantMentor()
    {
        await using var context = Context();
        var lecturer = new User { FullName = "Lecturer", Email = "lecturer@example.test" };
        var mentor = new User { FullName = "AI Mentor", Email = "mentor@example.test" };
        var other = new User { FullName = "Marketing Mentor", Email = "marketing@example.test" };
        var profile = new MentorProfile { User = mentor, UserId = mentor.Id, Expertise = ["AI", "Data"], MaxTeams = 2 };
        var otherProfile = new MentorProfile { User = other, UserId = other.Id, Expertise = ["Marketing"], MaxTeams = 2 };
        var semester = new Semester { Code = "FA26", Name = "Fall 2026", Year = 2026 };
        var course = new Course { Code = "EXE101", Name = "Startup" };
        var group = new Team { TeamName = "AI Data Platform", TeamCode = "T1", Description = "AI data product",
            Class = new Class { ClassCode = "EXE101-1", Semester = semester, Course = course, PrimaryLecturerId = lecturer.Id,
                Status = ClassStatus.Active } };
        context.Users.AddRange(lecturer, mentor, other);
        context.MentorProfiles.AddRange(profile, otherProfile);
        context.Teams.Add(group);
        context.SemesterStaffAssignments.Add(new SemesterStaffAssignment { Semester = semester, User = mentor,
            Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active });
        context.SemesterStaffAssignments.Add(new SemesterStaffAssignment { Semester = semester, User = other,
            Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active });
        await context.SaveChangesAsync();
        var embeddings = Substitute.For<IMentorEmbeddingSearch>();
        embeddings.SimilaritiesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(),
            Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, double>
                { [profile.Id] = 0.8, [otherProfile.Id] = 0.2 });
        var handler = new MentorProfileHandler(context, Substitute.For<IMentorDocumentStorageService>(), embeddings);

        var denied = await handler.RecommendAsync(group.Id, Guid.NewGuid(), SystemRoles.Lecturer, default);
        var allowed = await handler.RecommendAsync(group.Id, lecturer.Id, SystemRoles.Lecturer, default);

        denied.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
        allowed.IsSuccess.Should().BeTrue();
        allowed.Value.Should().HaveCount(2);
        allowed.Value.First().Mentor.Id.Should().Be(profile.Id);
        allowed.Value.First().FitScore.Should().BeGreaterThan(allowed.Value.Last().FitScore);
        var directory = await handler.GetDirectoryAsync(lecturer.Id, SystemRoles.Lecturer, default);
        directory.Value.Should().HaveCount(2);

        embeddings.SimilaritiesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<MentorProfile>>(),
            Arg.Any<CancellationToken>()).Returns<IReadOnlyDictionary<Guid, double>>(_ =>
                throw new MentorEmbeddingUnavailableException());
        var unavailable = await handler.RecommendAsync(group.Id, lecturer.Id, SystemRoles.Lecturer, default);
        unavailable.Error.Code.Should().Be(ErrorCodes.MentorMatchingUnavailable);

        group.Description = string.Empty;
        await context.SaveChangesAsync();
        var missingProjectDescription = await handler.RecommendAsync(group.Id, lecturer.Id, SystemRoles.Lecturer, default);
        missingProjectDescription.Error.Code.Should().Be(ErrorCodes.CommonValidationError);
    }

    [Fact]
    public async Task SessionRequiresAssignedMentorAndRejectsScheduleOverlap()
    {
        await using var context = Context();
        var mentor = new User { FullName = "Mentor", Email = "mentor@example.test" };
        var profile = new MentorProfile { User = mentor, UserId = mentor.Id, MaxTeams = 2 };
        var group = new Team { TeamName = "Team", TeamCode = "T1", Status = TeamStatus.Active,
            Class = new Class { ClassCode = "EXE101-1", Semester = new Semester { Code = "FA26", Name = "Fall 2026", Year = 2026 },
                Course = new Course { Code = "EXE101", Name = "Startup" }, Status = ClassStatus.Active } };
        var assignment = new MentorAssignment { Team = group, MentorProfile = profile, MentorProfileId = profile.Id,
            AssignedById = mentor.Id, Status = MentorAssignmentStatus.Active };
        context.Users.Add(mentor);
        context.MentorAssignments.Add(assignment);
        await context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);
        var handler = new MentoringSessionHandler(context, clock);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var first = new SaveMentoringSessionRequest { TeamId = group.Id, Title = "Discovery", StartAt = start, EndAt = start.AddHours(1) };
        var overlap = new SaveMentoringSessionRequest { TeamId = group.Id, Title = "Conflict", StartAt = start.AddMinutes(30), EndAt = start.AddHours(2) };

        var denied = await handler.CreateAsync(first, Guid.NewGuid(), SystemRoles.Mentor, default);
        var created = await handler.CreateAsync(first, mentor.Id, SystemRoles.Mentor, default);
        var conflict = await handler.CreateAsync(overlap, mentor.Id, SystemRoles.Mentor, default);

        denied.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
        created.IsSuccess.Should().BeTrue();
        conflict.IsFailure.Should().BeTrue();

        var studentUser = new User { FullName = "Student", Email = "student@example.test" };
        var student = new Student { User = studentUser, UserId = studentUser.Id, FullName = "Student" };
        context.Users.Add(studentUser);
        context.Students.Add(student);
        context.ClassStudents.Add(new ClassStudent { Class = group.Class, Student = student,
            SemesterId = group.Class.SemesterId, CourseId = group.Class.CourseId });
        context.TeamMembers.Add(new TeamMember { Team = group, ClassId = group.ClassId, StudentId = student.Id });
        await context.SaveChangesAsync();

        var premature = await handler.CompleteAsync(created.Value.Id, new SaveMentoringNotesRequest { Notes = "Useful session" },
            mentor.Id, SystemRoles.Mentor, default);
        premature.IsFailure.Should().BeTrue();

        clock.UtcNow.Returns(DateTime.UtcNow.AddDays(2));
        var completed = await handler.CompleteAsync(created.Value.Id, new SaveMentoringNotesRequest { Notes = "Useful session" },
            mentor.Id, SystemRoles.Mentor, default);
        var outsider = await handler.SaveFeedbackAsync(created.Value.Id, new SaveMentoringFeedbackRequest { Rating = 5, Comment = "Helpful" },
            Guid.NewGuid(), SystemRoles.Student, default);
        var feedback = await handler.SaveFeedbackAsync(created.Value.Id, new SaveMentoringFeedbackRequest { Rating = 5, Comment = "Helpful" },
            studentUser.Id, SystemRoles.Student, default);

        completed.IsSuccess.Should().BeTrue();
        outsider.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
        feedback.IsSuccess.Should().BeTrue();
    }
}
