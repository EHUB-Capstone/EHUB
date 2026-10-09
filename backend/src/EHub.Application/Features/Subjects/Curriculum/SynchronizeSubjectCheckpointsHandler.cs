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
        var validationError = Validate(request.Checkpoints, request.OtherAssessments);
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
        var existingOtherAssessments = await context.Rubrics
            .Include(item => item.Criteria)
            .Where(item => item.CourseId == course.Id && item.ClassId == null && item.CheckpointId == null)
            .ToListAsync(cancellationToken);
        var retainedNumbers = request.Checkpoints.Select(item => item.Number).ToArray();
        var removed = existing
            .Where(item => !retainedNumbers.Contains(item.CheckpointNumber))
            .ToArray();
        var removedCheckpointIds = removed.Select(item => item.Id).ToArray();
        if (removedCheckpointIds.Length > 0 &&
            (await context.ClassCheckpointSchedules.AnyAsync(
                item => removedCheckpointIds.Contains(item.CheckpointId), cancellationToken) ||
             await context.Submissions.AnyAsync(
                item => removedCheckpointIds.Contains(item.CheckpointId), cancellationToken)))
        {
            return Failure("VALIDATION_ERROR", "A checkpoint with class schedules or submissions cannot be removed.");
        }
        var removedRubricIds = removed
            .SelectMany(item => item.Rubrics)
            .Select(item => item.Id)
            .ToArray();

        if (removedRubricIds.Length > 0 &&
            await context.Evaluations.AnyAsync(
                item => removedRubricIds.Contains(item.RubricId), cancellationToken))
        {
            return Failure("VALIDATION_ERROR", "A checkpoint with saved evaluations cannot be removed.");
        }

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
            checkpoint.CourseWeight = input.CourseWeight;
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

            rubric.TotalWeight = 100;
            rubric.UpdatedBy = currentUser.UserId;

            var existingCriteriaByKey = rubric.Criteria
                .ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
            var retainedKeys = input.Rubrics
                .Select(item => item.Key.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removedCriteria = rubric.Criteria
                .Where(item => !retainedKeys.Contains(item.Key))
                .ToArray();
            var removedCriterionIds = removedCriteria.Select(item => item.Id).ToArray();

            if (removedCriterionIds.Length > 0 &&
                await context.EvaluationDetails.AnyAsync(
                    item => removedCriterionIds.Contains(item.RubricCriterionId), cancellationToken))
            {
                return Failure(
                    "VALIDATION_ERROR",
                    $"Checkpoint {input.Number} has a rubric criterion with saved evaluations. Its key cannot be changed and it cannot be removed.");
            }

            if (removedCriterionIds.Length > 0)
            {
                await context.RubricCriteria
                    .Where(item => removedCriterionIds.Contains(item.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            foreach (var (criterion, index) in input.Rubrics.Select((value, index) => (value, index)))
            {
                var key = criterion.Key.Trim();
                if (!existingCriteriaByKey.TryGetValue(key, out var rubricCriterion))
                {
                    rubricCriterion = new RubricCriterion
                    {
                        Rubric = rubric,
                        Key = key,
                        CreatedBy = currentUser.UserId,
                    };
                    await context.RubricCriteria.AddAsync(rubricCriterion, cancellationToken);
                }

                rubricCriterion.Name = criterion.Label.Trim();
                rubricCriterion.Description = criterion.Description?.Trim();
                rubricCriterion.Weight = criterion.Weight;
                rubricCriterion.MaxScore = 10;
                rubricCriterion.DisplayOrder = index + 1;
                rubricCriterion.LevelsJson = JsonSerializer.Serialize(criterion.Levels);
                rubricCriterion.UpdatedBy = currentUser.UserId;
            }
        }

        var requestedOtherAssessmentIds = request.OtherAssessments
            .Where(item => item.Id.HasValue)
            .Select(item => item.Id!.Value)
            .ToHashSet();
        var removedOtherAssessments = existingOtherAssessments
            .Where(item => !requestedOtherAssessmentIds.Contains(item.Id))
            .ToArray();
        var removedOtherAssessmentIds = removedOtherAssessments.Select(item => item.Id).ToArray();
        if (removedOtherAssessmentIds.Length > 0 &&
            await context.Evaluations.AnyAsync(item => removedOtherAssessmentIds.Contains(item.RubricId), cancellationToken))
        {
            return Failure("VALIDATION_ERROR", "An other assessment with saved grades cannot be removed.");
        }

        if (removedOtherAssessmentIds.Length > 0)
        {
            await context.RubricCriteria
                .Where(item => removedOtherAssessmentIds.Contains(item.RubricId))
                .ExecuteDeleteAsync(cancellationToken);
            await context.Rubrics
                .Where(item => removedOtherAssessmentIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        foreach (var input in request.OtherAssessments)
        {
            var assessment = input.Id.HasValue
                ? existingOtherAssessments.FirstOrDefault(item => item.Id == input.Id.Value)
                : null;
            if (input.Id.HasValue && assessment is null)
            {
                return Failure("VALIDATION_ERROR", "One or more other assessments no longer exist.");
            }

            if (assessment is null)
            {
                assessment = new Rubric
                {
                    CourseId = course.Id,
                    Name = input.Name.Trim(),
                    Status = RubricStatus.Active,
                    TotalWeight = 100,
                    CourseWeight = input.Weight,
                    CreatedById = currentUser.UserId,
                };
                await context.Rubrics.AddAsync(assessment, cancellationToken);
                await context.RubricCriteria.AddAsync(new RubricCriterion
                {
                    Rubric = assessment,
                    Name = input.Name.Trim(),
                    Key = "score",
                    Weight = 100,
                    MaxScore = 10,
                    DisplayOrder = 1,
                    LevelsJson = "[]",
                    CreatedBy = currentUser.UserId,
                }, cancellationToken);
            }
            else
            {
                assessment.Name = input.Name.Trim();
                assessment.TotalWeight = 100;
                assessment.CourseWeight = input.Weight;
                assessment.Status = RubricStatus.Active;
                assessment.UpdatedBy = currentUser.UserId;

                var criterion = assessment.Criteria.SingleOrDefault();
                if (criterion is not null)
                {
                    criterion.Name = input.Name.Trim();
                    criterion.UpdatedBy = currentUser.UserId;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return await curriculumQuery.GetAsync(code, cancellationToken);
    }

    private static string? Validate(
        IEnumerable<SubjectCheckpointRequest> checkpoints,
        IEnumerable<SubjectOtherAssessmentRequest> otherAssessments)
    {
        var values = checkpoints.ToArray();
        var otherValues = otherAssessments.ToArray();
        if (values.Length == 0) return "At least one checkpoint is required.";
        if (values.Select(item => item.Number).Distinct().Count() != values.Length ||
            values.Any(item => item.Number is < 1 or > 10))
        {
            return "Checkpoint numbers must be unique and range from 1 to 10.";
        }

        foreach (var checkpoint in values)
        {
            if (string.IsNullOrWhiteSpace(checkpoint.Title)) return $"Checkpoint {checkpoint.Number} title is required.";
            if (checkpoint.CourseWeight is <= 0 or > 100) return $"Checkpoint {checkpoint.Number} course weight must be greater than 0 and no more than 100%.";
            if (checkpoint.Rubrics.Count == 0) return $"Checkpoint {checkpoint.Number} needs at least one rubric criterion.";
            if (checkpoint.Rubrics.Any(item => string.IsNullOrWhiteSpace(item.Key) || !Regex.IsMatch(item.Key, "^[A-Za-z][A-Za-z0-9_-]*$"))) return $"Checkpoint {checkpoint.Number} contains an invalid rubric key.";
            if (checkpoint.Rubrics.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) return $"Checkpoint {checkpoint.Number} contains duplicate rubric keys.";
            if (checkpoint.Rubrics.Any(item => string.IsNullOrWhiteSpace(item.Label) || item.Weight <= 0 || item.Weight > 100)) return $"Checkpoint {checkpoint.Number} contains an invalid rubric criterion.";
            if (checkpoint.Rubrics.GroupBy(item => item.Label.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) return $"Checkpoint {checkpoint.Number} contains duplicate rubric criterion names.";
            if (checkpoint.Rubrics.Sum(item => item.Weight) != 100) return $"Checkpoint {checkpoint.Number} rubric weights must total 100%.";
        }

        if (otherValues.Any(item => string.IsNullOrWhiteSpace(item.Name) || item.Weight is <= 0 or > 100))
            return "Each other assessment needs a name and a weight greater than 0 and no more than 100%.";
        if (otherValues.GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            return "Other assessment names must be unique.";
        if (otherValues.Where(item => item.Id.HasValue).GroupBy(item => item.Id).Any(group => group.Count() > 1))
            return "An other assessment can only appear once.";

        var courseWeight = values.Sum(item => item.CourseWeight) + otherValues.Sum(item => item.Weight);
        if (courseWeight != 100)
            return $"Checkpoint and other assessment weights must total exactly 100.0% (currently {courseWeight:0.0}%).";

        return null;
    }

    private static Result<SubjectCurriculumResponse> Failure(string code, string message) =>
        Result.Failure<SubjectCurriculumResponse>(new Error(code, message));
}
