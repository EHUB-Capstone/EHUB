using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Subjects.Curriculum;

public sealed class GetSubjectCurriculumQueryHandler(IApplicationDbContext context) : IGetSubjectCurriculumQueryHandler
{
    public async Task<Result<SubjectCurriculumResponse>> GetAsync(
        string subjectCode,
        CancellationToken cancellationToken = default)
    {
        var code = subjectCode.Trim().ToUpperInvariant();
        var course = await context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
        if (course is null)
        {
            return Result.Failure<SubjectCurriculumResponse>(
                new Error("NOT_FOUND", "Subject was not found."));
        }

        var roadmapItems = await context.WeeklyTasks
            .AsNoTracking()
            .Where(item =>
                item.CourseId == course.Id &&
                item.Scope == WeeklyTaskScope.Course &&
                item.IsTemplate)
            .OrderBy(item => item.WeekNumber)
            .ThenBy(item => item.Title)
            .Select(item => new RoadmapItemResponse
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description,
                CourseCode = code,
                WeekNumber = item.WeekNumber,
                Priority = item.Priority.ToString().ToUpper(),
                EstimatedHours = item.EstimatedHours,
                Tags = item.Tags,
            })
            .ToArrayAsync(cancellationToken);

        var rubrics = await context.Rubrics
            .AsNoTracking()
            .Include(item => item.Criteria)
            .Include(item => item.Checkpoint)
            .Where(item => item.CourseId == course.Id && item.ClassId == null)
            .OrderBy(item => item.Checkpoint!.CheckpointNumber)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var checkpoints = await context.Checkpoints
            .AsNoTracking()
            .Include(item => item.Rubrics)
            .ThenInclude(item => item.Criteria)
            .Where(item => item.CourseId == course.Id && item.ClassId == null)
            .OrderBy(item => item.CheckpointNumber)
            .ToListAsync(cancellationToken);

        return Result.Success(new SubjectCurriculumResponse
        {
            Subject = ToSubjectResponse(course),
            RoadmapItems = roadmapItems,
            Rubrics = rubrics.Select(ToRubricResponse).ToArray(),
            Checkpoints = checkpoints.Select(ToCheckpointResponse).ToArray(),
        });
    }

    private static SubjectResponse ToSubjectResponse(Course course) => new()
    {
        Id = course.Id,
        SubjectCode = course.Code,
        SubjectName = course.Name,
        Status = course.Status == CourseStatus.Active ? "active" : "disabled",
    };

    private static CourseRubricResponse ToRubricResponse(Rubric rubric) => new()
    {
        Id = rubric.Id,
        Name = rubric.Name,
        Description = rubric.Description,
        Status = rubric.Status.ToString(),
        TotalWeight = rubric.TotalWeight,
        CheckpointNumber = rubric.Checkpoint?.CheckpointNumber,
        Criteria = rubric.Criteria
            .OrderBy(criterion => criterion.DisplayOrder)
            .Select(criterion => new RubricCriterionResponse
            {
                Id = criterion.Id,
                Name = criterion.Name,
                Description = criterion.Description,
                MaxScore = criterion.MaxScore,
                Weight = criterion.Weight,
                DisplayOrder = criterion.DisplayOrder,
            })
            .ToArray(),
    };

    private static SubjectCheckpointResponse ToCheckpointResponse(Checkpoint checkpoint)
    {
        var rubric = checkpoint.Rubrics.FirstOrDefault(item => item.ClassId == null);
        return new SubjectCheckpointResponse
        {
            Number = checkpoint.CheckpointNumber,
            Title = checkpoint.Name,
            ShortDescription = checkpoint.Description,
            Requirements = JsonSerializer.Deserialize<string[]>(checkpoint.RequirementsJson) ?? Array.Empty<string>(),
            Rubrics = rubric?.Criteria
                .OrderBy(item => item.DisplayOrder)
                .Select(item => new SubjectCriterionResponse
                {
                    Key = string.IsNullOrWhiteSpace(item.Key) ? item.Name : item.Key,
                    Label = item.Name,
                    Description = item.Description,
                    Weight = item.Weight,
                    Levels = JsonSerializer.Deserialize<object[]>(item.LevelsJson) ?? Array.Empty<object>(),
                })
                .ToArray() ?? Array.Empty<SubjectCriterionResponse>(),
        };
    }
}
