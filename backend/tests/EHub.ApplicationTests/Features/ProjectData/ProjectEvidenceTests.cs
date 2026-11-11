using EHub.Application.Features.ProjectData.GetProjectEvidence;
using EHub.Contracts.ProjectData;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EHub.ApplicationTests.Features.ProjectData;

public sealed class ProjectEvidenceTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    // ---- Access ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Admin_CanViewAnyProjectInScope()
    {
        var world = await Scenario.CreateAsync();

        var result = await world.Handler.HandleAsync(world.Project.Id, world.Admin.Id, SystemRoles.Admin);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectId.Should().Be(world.Project.Id);
    }

    [Fact]
    public async Task Lecturer_CanViewOnlyProjectsOfClassesTheyTeach()
    {
        var world = await Scenario.CreateAsync();

        (await world.Handler.HandleAsync(world.Project.Id, world.Lecturer.Id, SystemRoles.Lecturer))
            .IsSuccess.Should().BeTrue();

        var other = await world.Handler.HandleAsync(world.Project.Id, world.OtherLecturer.Id, SystemRoles.Lecturer);
        other.IsFailure.Should().BeTrue();
        other.Error.Code.Should().Be(ErrorCodes.ProjectDataAccessDenied);
    }

    [Theory]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Mentor)]
    [InlineData("")]
    public async Task OtherRoles_AreRejected(string role)
    {
        var world = await Scenario.CreateAsync();

        var result = await world.Handler.HandleAsync(world.Project.Id, world.Lecturer.Id, role);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ProjectDataAccessDenied);
    }

    [Fact]
    public async Task UnknownOrArchivedProject_IsNotFound()
    {
        var world = await Scenario.CreateAsync();

        (await world.Handler.HandleAsync(Guid.NewGuid(), world.Admin.Id, SystemRoles.Admin))
            .Error.Code.Should().Be(ErrorCodes.ProjectDataNotFound);

        world.Project.Status = ProjectStatus.Archived;
        await world.Db.SaveChangesAsync();

        (await world.Handler.HandleAsync(world.Project.Id, world.Admin.Id, SystemRoles.Admin))
            .Error.Code.Should().Be(ErrorCodes.ProjectDataNotFound);
    }

    // ---- Evaluation -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Evaluation_ShowsOfficialScoreAndPublishedFeedbackOfEveryPart()
    {
        var world = await Scenario.CreateAsync();
        world.Grade(world.Rubric1, EvaluationStatus.Published, 8m, feedback: "Clear problem and users.");
        world.Grade(world.Rubric2, EvaluationStatus.Published, 6m, feedback: "Pitch needs a business model.");
        world.Grade(world.Assessment, EvaluationStatus.Published, 40m, max: 40m, feedback: "Good teamwork.");
        await world.Db.SaveChangesAsync();

        var evaluation = (await world.AdminEvidenceAsync()).Evaluation;

        // 8 * 40% + 6 * 40% + 10 * 20% = 7.6, the same rule as the trends and the export.
        evaluation.CourseScore.Should().Be(7.6m);
        evaluation.Components.Select(item => (item.Name, item.Kind, item.CheckpointNumber, item.WeightPercent, item.Status, item.Score))
            .Should().Equal(
                ("Checkpoint 1", ProjectEvidenceKinds.Checkpoint, 1, 40m, ProjectEvidenceStatuses.Published, 8m),
                ("Checkpoint 2", ProjectEvidenceKinds.Checkpoint, 2, 40m, ProjectEvidenceStatuses.Published, 6m),
                ("Final assessment", ProjectEvidenceKinds.Assessment, null, 20m, ProjectEvidenceStatuses.Published, 10m));
        evaluation.Components.Select(item => item.Feedback).Should().Equal(
            "Clear problem and users.", "Pitch needs a business model.", "Good teamwork.");
        evaluation.Components.Should().OnlyContain(item => item.PublishedAtUtc != null);
    }

    [Fact]
    public async Task Evaluation_MatchesTheOfficialScoreWhenSomePartsAreStillOpen()
    {
        var world = await Scenario.CreateAsync();
        world.Grade(world.Rubric1, EvaluationStatus.Published, 8m);
        world.Grade(world.Rubric2, EvaluationStatus.Submitted, 6m, feedback: "Not published yet.");
        await world.Db.SaveChangesAsync();

        var evaluation = (await world.AdminEvidenceAsync()).Evaluation;

        evaluation.CourseScore.Should().BeNull("a course score needs every part published");
        evaluation.Components.Select(item => item.Status).Should().Equal(
            ProjectEvidenceStatuses.Published, ProjectEvidenceStatuses.PendingPublication, ProjectEvidenceStatuses.NotGraded);
    }

    [Fact]
    public async Task Evaluation_NeverShowsScoreOrFeedbackOfAnUnpublishedPart()
    {
        var world = await Scenario.CreateAsync();
        world.Grade(world.Rubric1, EvaluationStatus.Submitted, 9m, feedback: "Secret until published.");
        await world.Db.SaveChangesAsync();

        var pending = (await world.AdminEvidenceAsync()).Evaluation.Components.First();

        pending.Status.Should().Be(ProjectEvidenceStatuses.PendingPublication);
        pending.Score.Should().BeNull();
        pending.Feedback.Should().BeNull();
        pending.PublishedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Evaluation_ANewerUnpublishedRegradeHidesTheOlderPublishedVersion()
    {
        var world = await Scenario.CreateAsync();
        world.Grade(world.Rubric1, EvaluationStatus.Published, 8m, feedback: "Old published feedback.", minutes: 0);
        world.Grade(world.Rubric1, EvaluationStatus.Submitted, 2m, feedback: "New draft opinion.", minutes: 10);
        await world.Db.SaveChangesAsync();

        var first = (await world.AdminEvidenceAsync()).Evaluation.Components.First();

        first.Status.Should().Be(ProjectEvidenceStatuses.PendingPublication);
        first.Score.Should().BeNull();
        first.Feedback.Should().BeNull();
    }

    [Fact]
    public async Task Evaluation_ANewerPublishedVersionReplacesTheOlderOne()
    {
        var world = await Scenario.CreateAsync();
        world.Grade(world.Rubric1, EvaluationStatus.Published, 5m, feedback: "First attempt.", minutes: 0);
        world.Grade(world.Rubric1, EvaluationStatus.Published, 9m, feedback: "Regraded.", minutes: 10);
        await world.Db.SaveChangesAsync();

        var first = (await world.AdminEvidenceAsync()).Evaluation.Components.First();

        first.Score.Should().Be(9m);
        first.Feedback.Should().Be("Regraded.");
    }

    [Fact]
    public async Task Evaluation_IgnoresDraftsAndTreatsBlankFeedbackAsMissing()
    {
        var world = await Scenario.CreateAsync();
        world.Grade(world.Rubric1, EvaluationStatus.Draft, 1m, feedback: "Draft only.");
        world.Grade(world.Rubric2, EvaluationStatus.Published, 7m, feedback: "   ");
        await world.Db.SaveChangesAsync();

        var components = (await world.AdminEvidenceAsync()).Evaluation.Components.ToArray();

        components[0].Status.Should().Be(ProjectEvidenceStatuses.NotGraded);
        components[0].Feedback.Should().BeNull();
        components[1].Status.Should().Be(ProjectEvidenceStatuses.Published);
        components[1].Feedback.Should().BeNull("blank feedback is no feedback");
    }

    [Fact]
    public async Task Evaluation_WithoutAnyEvaluationListsEveryPartAsNotGraded()
    {
        var world = await Scenario.CreateAsync();

        var evaluation = (await world.AdminEvidenceAsync()).Evaluation;

        evaluation.CourseScore.Should().BeNull();
        evaluation.Components.Should().HaveCount(3).And.OnlyContain(item =>
            item.Status == ProjectEvidenceStatuses.NotGraded && item.Score == null && item.Feedback == null);
    }

    [Fact]
    public async Task Evaluation_IsEmptyWhenTheCourseHasNoWeightedPart()
    {
        var world = await Scenario.CreateAsync(withRubrics: false);

        var evaluation = (await world.AdminEvidenceAsync()).Evaluation;

        evaluation.CourseScore.Should().BeNull();
        evaluation.Components.Should().BeEmpty();
    }

    [Fact]
    public async Task Evaluation_OnlyShowsTheEvaluationsOfThisProject()
    {
        var world = await Scenario.CreateAsync();
        var other = world.AddProject("Other project");
        world.Grade(world.Rubric1, EvaluationStatus.Published, 10m, feedback: "Belongs to the other project.", project: other);
        await world.Db.SaveChangesAsync();

        var components = (await world.AdminEvidenceAsync()).Evaluation.Components;

        components.Should().OnlyContain(item => item.Status == ProjectEvidenceStatuses.NotGraded && item.Feedback == null);
    }

    // ---- Mentoring ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Mentoring_ListsCurrentMentorsFirstThenEarlierOnesWithSessionsAndExpertise()
    {
        var world = await Scenario.CreateAsync();
        var current = world.Assign("Current Enterprise", MentorType.Enterprise, MentorAssignmentStatus.Active, 20,
            organization: "  Acme Studio ", expertise: ["Marketing", "marketing"], domains: ["EdTech"], skills: ["Python"]);
        var academic = world.Assign("Current Academic", MentorType.Academic, MentorAssignmentStatus.Active, 18);
        var earlier = world.Assign("Replaced Enterprise", MentorType.Enterprise, MentorAssignmentStatus.Ended, 5, endedDaysAgo: 4);
        world.Session(current, MentoringSessionStatus.Completed, 3);
        world.Session(current, MentoringSessionStatus.Completed, 1);
        world.Session(current, MentoringSessionStatus.Scheduled, -2);
        world.Session(current, MentoringSessionStatus.Cancelled, 4);
        world.Session(earlier, MentoringSessionStatus.Completed, 9);
        await world.Db.SaveChangesAsync();

        var mentoring = (await world.AdminEvidenceAsync()).Mentoring;

        mentoring.Mentors.Select(item => (item.FullName, item.Slot, item.IsCurrent, item.CompletedSessions)).Should().Equal(
            ("Current Enterprise", "Enterprise", true, 2),
            ("Current Academic", "Academic", true, 0),
            ("Replaced Enterprise", "Enterprise", false, 1));
        mentoring.TotalCompletedSessions.Should().Be(3);
        mentoring.LastSessionAtUtc.Should().Be(Start.AddDays(9).AddHours(9));

        var first = mentoring.Mentors.First();
        first.AssignmentId.Should().Be(current.Id);
        first.Organization.Should().Be("Acme Studio");
        first.Expertise.Should().Equal("EdTech", "Marketing", "Python");
        mentoring.Mentors.Last().EndedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Mentoring_LeavesOutAssignmentsThatNeverStartedOrWereCancelled()
    {
        var world = await Scenario.CreateAsync();
        world.Assign("Active mentor", MentorType.Enterprise, MentorAssignmentStatus.Active, 3);
        world.Assign("Pending mentor", MentorType.Academic, MentorAssignmentStatus.Pending, 3);
        world.Assign("Cancelled mentor", MentorType.Academic, MentorAssignmentStatus.Cancelled, 3);
        await world.Db.SaveChangesAsync();

        (await world.AdminEvidenceAsync()).Mentoring.Mentors.Select(item => item.FullName).Should().Equal("Active mentor");
    }

    [Fact]
    public async Task Mentoring_KeepsOnlyTheTeamsOwnMentors()
    {
        var world = await Scenario.CreateAsync();
        var otherProject = world.AddProject("Another team");
        world.Assign("Mentor of another team", MentorType.Enterprise, MentorAssignmentStatus.Active, 3, team: otherProject.Team);
        await world.Db.SaveChangesAsync();

        (await world.AdminEvidenceAsync()).Mentoring.Mentors.Should().BeEmpty();
    }

    [Fact]
    public async Task Mentoring_BoundsTheExpertiseListOfOneMentor()
    {
        var world = await Scenario.CreateAsync();
        var tags = Enumerable.Range(1, 30).Select(index => $"Skill {index:00}").ToArray();
        world.Assign("Generalist", MentorType.Enterprise, MentorAssignmentStatus.Active, 3, expertise: tags);
        await world.Db.SaveChangesAsync();

        (await world.AdminEvidenceAsync()).Mentoring.Mentors.Single().Expertise
            .Should().HaveCount(GetProjectEvidenceQueryHandler.MaxExpertisePerMentor);
    }

    [Fact]
    public async Task Mentoring_IsEmptyForATeamThatNeverHadAMentor()
    {
        var world = await Scenario.CreateAsync();

        var mentoring = (await world.AdminEvidenceAsync()).Mentoring;

        mentoring.Mentors.Should().BeEmpty();
        mentoring.TotalCompletedSessions.Should().Be(0);
        mentoring.LastSessionAtUtc.Should().BeNull();
    }

    [Fact]
    public void Response_NeverCarriesMentorContactOrPersonalDetails()
    {
        var forbidden = new[] { "Email", "Phone", "Cv", "Address", "Birth", "LinkedIn", "Portfolio", "Contract", "Note" };

        typeof(ProjectEvidenceMentorResponse).GetProperties().Select(property => property.Name)
            .Should().NotContain(name => forbidden.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    // ---- Test data ------------------------------------------------------------------------------------------------

    private sealed class Scenario
    {
        private int _minutes;

        public AppDbContext Db { get; } = new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        public GetProjectEvidenceQueryHandler Handler => new(Db);
        public User Admin { get; } = NewUser("Admin");
        public User Lecturer { get; } = NewUser("Lecturer");
        public User OtherLecturer { get; } = NewUser("Other lecturer");
        public Class Class { get; private set; } = null!;
        public Project Project { get; private set; } = null!;
        public Rubric Rubric1 { get; private set; } = null!;
        public Rubric Rubric2 { get; private set; } = null!;
        public Rubric Assessment { get; private set; } = null!;

        private Course _course = null!;

        public static async Task<Scenario> CreateAsync(bool withRubrics = true)
        {
            var world = new Scenario();
            var semester = new Semester { Code = "T26", Name = "Test semester", Year = 2026, Status = SemesterStatus.Active };
            world._course = new Course { Code = "EXE101", Name = "Startup", Status = CourseStatus.Active };
            world.Class = new Class
            {
                ClassCode = "EXE101_1", Slug = "exe101-1", ClassIndex = 1, Semester = semester, Course = world._course,
                PrimaryLecturerId = world.Lecturer.Id, Status = ClassStatus.Active,
            };
            world.Db.Users.AddRange(world.Admin, world.Lecturer, world.OtherLecturer);
            world.Project = world.AddProject("Green Box");

            if (withRubrics)
            {
                // Course score = 40% checkpoint 1 + 40% checkpoint 2 + 20% final assessment.
                var checkpoint1 = new Checkpoint { Course = world._course, Name = "Checkpoint 1", CheckpointNumber = 1, CourseWeight = 40, Status = CheckpointStatus.Open };
                var checkpoint2 = new Checkpoint { Course = world._course, Name = "Checkpoint 2", CheckpointNumber = 2, CourseWeight = 40, Status = CheckpointStatus.Open };
                world.Rubric1 = world.NewRubric("Rubric 1", checkpoint1, 0);
                world.Rubric2 = world.NewRubric("Rubric 2", checkpoint2, 0);
                world.Assessment = world.NewRubric("Final assessment", null, 20);
                world.Db.Rubrics.AddRange(world.Rubric1, world.Rubric2, world.Assessment);
            }

            await world.Db.SaveChangesAsync();
            return world;
        }

        public Project AddProject(string name)
        {
            var team = new Team { Class = Class, TeamCode = $"T{Guid.NewGuid():N}"[..8], TeamName = $"Team {name}", Status = TeamStatus.Active };
            var project = new Project { Team = team, Name = name, Status = ProjectStatus.Approved };
            Db.Projects.Add(project);
            return project;
        }

        public async Task<ProjectEvidenceResponse> AdminEvidenceAsync()
        {
            var result = await Handler.HandleAsync(Project.Id, Admin.Id, SystemRoles.Admin);
            result.IsSuccess.Should().BeTrue();
            return result.Value;
        }

        public void Grade(Rubric rubric, EvaluationStatus status, decimal score, decimal max = 10m,
            string? feedback = null, int minutes = 0, Project? project = null) =>
            Db.Evaluations.Add(new Evaluation
            {
                Project = project ?? Project,
                Rubric = rubric,
                Evaluator = Lecturer,
                EvaluatorId = Lecturer.Id,
                Status = status,
                TotalScore = score,
                MaxTotalScore = max,
                OverallFeedback = feedback,
                PublishedAt = status == EvaluationStatus.Published ? Start.AddMinutes(minutes) : null,
                UpdatedAt = Start.AddMinutes(minutes + _minutes++),
            });

        public MentorAssignment Assign(string name, MentorType slot, MentorAssignmentStatus status, int assignedDaysAgo,
            int? endedDaysAgo = null, string? organization = null, string[]? expertise = null, string[]? domains = null,
            string[]? skills = null, Team? team = null)
        {
            var user = NewUser(name);
            var profile = new MentorProfile
            {
                User = user, UserId = user.Id, Type = slot, Organization = organization,
                Expertise = expertise ?? [], StartupDomains = domains ?? [], TechnologySkills = skills ?? [],
            };
            var assignment = new MentorAssignment
            {
                MentorProfile = profile,
                Team = team ?? Project.Team,
                AssignedBy = Admin,
                AssignedById = Admin.Id,
                Slot = slot,
                Status = status,
                AssignedAt = Start.AddDays(-assignedDaysAgo),
                EndedAt = endedDaysAgo is { } ended ? Start.AddDays(-ended) : null,
            };
            Db.MentorAssignments.Add(assignment);
            return assignment;
        }

        public void Session(MentorAssignment assignment, MentoringSessionStatus status, int daysFromStart) =>
            Db.MentoringSessions.Add(new MentoringSession
            {
                MentorAssignment = assignment,
                Title = "Weekly check-in",
                Status = status,
                StartAt = new DateTimeOffset(Start.AddDays(daysFromStart).AddHours(9)),
                EndAt = new DateTimeOffset(Start.AddDays(daysFromStart).AddHours(10)),
            });

        private Rubric NewRubric(string name, Checkpoint? checkpoint, decimal courseWeight) => new()
        {
            Course = _course, Checkpoint = checkpoint, Name = name, TotalWeight = 100, CourseWeight = courseWeight,
            Status = RubricStatus.Active,
        };

        private static User NewUser(string label) =>
            new() { FullName = label, Email = $"{Guid.NewGuid():N}@example.test" };
    }
}
