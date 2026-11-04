using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Subjects.Curriculum;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Subjects;

public sealed class SynchronizeSubjectCheckpointsLegacyKeysTests
{
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SynchronizeSubjectCheckpointsHandler Handler(AppDbContext db)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(Guid.NewGuid());
        var query = Substitute.For<IGetSubjectCurriculumQueryHandler>();
        query.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success(new SubjectCurriculumResponse()));
        return new SynchronizeSubjectCheckpointsHandler(db, user, query);
    }

    // Rubrics seeded before criteria had keys: every key is empty, which broke saving ("An item with the same key has
    // already been added").
    private static async Task<(Checkpoint Checkpoint, RubricCriterion[] Criteria)> SeedLegacyAsync(AppDbContext db)
    {
        var course = new Course { Code = "EXE201", Name = "Startup 2" };
        var checkpoint = new Checkpoint { Course = course, Name = "Idea", CheckpointNumber = 1, CourseWeight = 100 };
        var rubric = new Rubric { Course = course, Checkpoint = checkpoint, Name = "EXE201 Checkpoint 1", TotalWeight = 100 };
        RubricCriterion[] criteria =
        [
            new() { Rubric = rubric, Name = "Problem Clarity", Weight = 50, MaxScore = 10, DisplayOrder = 1 },
            new() { Rubric = rubric, Name = "Solution Fit", Weight = 50, MaxScore = 10, DisplayOrder = 2 }
        ];
        db.Courses.Add(course);
        db.Checkpoints.Add(checkpoint);
        db.Rubrics.Add(rubric);
        db.RubricCriteria.AddRange(criteria);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (checkpoint, criteria);
    }

    private static SaveSubjectCheckpointsRequest Request(params (string Key, string Label, decimal Weight)[] criteria) => new()
    {
        Checkpoints =
        [
            new SubjectCheckpointRequest
            {
                Number = 1, Title = "Idea", CourseWeight = 100, Requirements = [],
                Rubrics = criteria.Select(item => new SubjectCriterionRequest { Key = item.Key, Label = item.Label, Weight = item.Weight }).ToArray()
            }
        ]
    };

    [Fact]
    public async Task Save_AdoptsTheKeysOfCriteriaSeededWithoutOneAndKeepsTheirRows()
    {
        await using var db = Context();
        var (_, seeded) = await SeedLegacyAsync(db);

        var result = await Handler(db).SynchronizeAsync("EXE201",
            Request(("problemClarity", "Problem Clarity", 50), ("solutionFit", "Solution Fit", 50)));

        result.IsSuccess.Should().BeTrue(because: result.IsFailure ? result.Error.Message : string.Empty);
        var saved = await db.RubricCriteria.AsNoTracking().OrderBy(item => item.DisplayOrder).ToArrayAsync();
        // The same rows, now with keys: evaluations saved against them stay attached.
        saved.Select(item => item.Id).Should().Equal(seeded.Select(item => item.Id));
        saved.Select(item => item.Key).Should().Equal("problemClarity", "solutionFit");
    }

    [Fact]
    public async Task Save_AddsCriteriaThatMatchNoLegacyNameAndSavesRepeatedly()
    {
        await using var db = Context();
        await SeedLegacyAsync(db);
        var request = Request(("problemClarity", "Problem Clarity", 40), ("solutionFit", "Solution Fit", 40), ("demo", "Demo Skills", 20));

        (await Handler(db).SynchronizeAsync("EXE201", request)).IsSuccess.Should().BeTrue();
        db.ChangeTracker.Clear();
        // A second save must not fail on the keys written by the first.
        (await Handler(db).SynchronizeAsync("EXE201", request)).IsSuccess.Should().BeTrue();

        var saved = await db.RubricCriteria.AsNoTracking().OrderBy(item => item.DisplayOrder).ToArrayAsync();
        saved.Should().HaveCount(3);
        saved.Select(item => item.Key).Should().Equal("problemClarity", "solutionFit", "demo");
    }
}
