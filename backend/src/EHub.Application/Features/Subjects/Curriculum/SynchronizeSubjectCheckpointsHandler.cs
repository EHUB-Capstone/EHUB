using System.Text.Json;
using System.Text.RegularExpressions;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Subjects.Curriculum;

public sealed class SynchronizeSubjectCheckpointsHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IGetSubjectCurriculumQueryHandler curriculumQuery) : ISynchronizeSubjectCheckpointsHandler
{
    public async Task<Result<SubjectCurriculumResponse>> SynchronizeAsync(
        string subjectCode,
        SaveSubjectCheckpointsRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request.Checkpoints);
        if (validationError is not null)
        {
            return Failure("VALIDATION_ERROR", validationError);
        }

        var code = subjectCode.Trim().ToUpperInvariant();
        var course = await context.Courses.FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
        if (course is null)
        {
            return Failure("NOT_FOUND", "Subject was not found.");
        }

        var existing = await context.Checkpoints
            .Include(item => item.Rubrics)
            .ThenInclude(item => item.Criteria)
            .Where(item => item.CourseId == course.Id && item.ClassId == null)
            .ToListAsync(cancellationToken);
        var retainedNumbers = request.Checkpoints.Select(item => item.Number).ToArray();
        var removed = existing
            .Where(item => !retainedNumbers.Contains(item.CheckpointNumber))
            .ToArray();
        var removedRubricIds = removed
            .SelectMany(item => item.Rubrics)
            .Select(item => item.Id)
            .ToArray();

        if (removedRubricIds.Length > 0)
        {
            await context.RubricCriteria
                .Where(item => removedRubricIds.Contains(item.RubricId))
                .ExecuteDeleteAsync(cancellationToken);
            await context.Rubrics
                .Where(item => removedRubricIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (removed.Length > 0)
        {
            var removedCheckpointIds = removed.Select(item => item.Id).ToArray();
            await context.Checkpoints
                .Where(item => removedCheckpointIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        foreach (var input in request.Checkpoints)
        {
            var checkpoint = existing.FirstOrDefault(item => item.CheckpointNumber == input.Number);
            if (checkpoint is null)
            {
                checkpoint = new Checkpoint
                {
                    CourseId = course.Id,
                    CheckpointNumber = input.Number,
                    CreatedById = currentUser.UserId,
                    Status = CheckpointStatus.Draft,
                };
                await context.Checkpoints.AddAsync(checkpoint, cancellationToken);
            }

            checkpoint.Name = input.Title.Trim();
            checkpoint.Description = input.ShortDescription?.Trim();
            checkpoint.RequirementsJson = JsonSerializer.Serialize(input.Requirements
                .Select(item => item.Trim())
                .Where(item => item.Length > 0));
            checkpoint.UpdatedBy = currentUser.UserId;

            var rubric = checkpoint.Rubrics.FirstOrDefault(item => item.ClassId == null);
            if (rubric is null)
            {
                rubric = new Rubric
                {
                    CourseId = course.Id,
                    Checkpoint = checkpoint,
                    Name = $"{course.Code} Checkpoint {input.Number}",
                    Status = RubricStatus.Active,
                    TotalWeight = 100,
                    CreatedById = currentUser.UserId,
                };
                await context.Rubrics.AddAsync(rubric, cancellationToken);
            }

            await context.RubricCriteria
                .Where(item => item.RubricId == rubric.Id)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var (criterion, index) in input.Rubrics.Select((value, index) => (value, index)))
            {
                await context.RubricCriteria.AddAsync(new RubricCriterion
                {
                    Rubric = rubric,
                    Name = criterion.Label.Trim(),
                    Key = criterion.Key.Trim(),
                    Description = criterion.Description?.Trim(),
                    Weight = criterion.Weight,
                    MaxScore = 10,
                    DisplayOrder = index + 1,
                    LevelsJson = JsonSerializer.Serialize(criterion.Levels),
                    CreatedBy = currentUser.UserId,
                }, cancellationToken);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return await curriculumQuery.GetAsync(code, cancellationToken);
    }

    private static string? Validate(IEnumerable<SubjectCheckpointRequest> checkpoints)
    {
        var values = checkpoints.ToArray();
        if (values.Length == 0) return "At least one checkpoint is required.";
        if (values.Select(item => item.Number).Distinct().Count() != values.Length ||
            values.Any(item => item.Number is < 1 or > 10))
        {
            return "Checkpoint numbers must be unique and range from 1 to 10.";
        }

        foreach (var checkpoint in values)
        {
            if (string.IsNullOrWhiteSpace(checkpoint.Title)) return $"Checkpoint {checkpoint.Number} title is required.";
            if (checkpoint.Rubrics.Count == 0) return $"Checkpoint {checkpoint.Number} needs at least one rubric criterion.";
            if (checkpoint.Rubrics.Any(item => string.IsNullOrWhiteSpace(item.Key) || !Regex.IsMatch(item.Key, "^[A-Za-z][A-Za-z0-9_-]*$"))) return $"Checkpoint {checkpoint.Number} contains an invalid rubric key.";
            if (checkpoint.Rubrics.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) return $"Checkpoint {checkpoint.Number} contains duplicate rubric keys.";
            if (checkpoint.Rubrics.Any(item => string.IsNullOrWhiteSpace(item.Label) || item.Weight <= 0 || item.Weight > 100)) return $"Checkpoint {checkpoint.Number} contains an invalid rubric criterion.";
            if (checkpoint.Rubrics.Sum(item => item.Weight) != 100) return $"Checkpoint {checkpoint.Number} rubric weights must total 100%.";
        }

        return null;
    }

    private static Result<SubjectCurriculumResponse> Failure(string code, string message) =>
        Result.Failure<SubjectCurriculumResponse>(new Error(code, message));
}
