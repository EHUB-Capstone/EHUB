using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Subjects.Rubrics;

public sealed class SubjectRubricHandler(IApplicationDbContext context, ICurrentUserService currentUser) : ISubjectRubricHandler
{
    public async Task<Result<CourseRubricResponse>> CreateAsync(string subjectCode, SaveRubricRequest request, CancellationToken cancellationToken = default)
    {
        var course = await FindCourseAsync(subjectCode, cancellationToken);
        if (course is null) return Failure<CourseRubricResponse>("NOT_FOUND", "Subject was not found.");
        var checkpoint = await FindCheckpointAsync(course.Id, request.CheckpointNumber, cancellationToken);
        if (request.CheckpointNumber.HasValue && checkpoint is null) return Failure<CourseRubricResponse>("VALIDATION_ERROR", "Checkpoint was not found for this subject.");
        if (ToStatus(request.Status) == RubricStatus.Active) return Failure<CourseRubricResponse>("BUSINESS_RULE", "Add criteria before activating a rubric.");

        var rubric = new Rubric { Name = request.Name.Trim(), Description = request.Description?.Trim(), CourseId = course.Id, CheckpointId = checkpoint?.Id, TotalWeight = request.TotalWeight, Status = ToStatus(request.Status), CreatedById = currentUser.UserId };
        await context.Rubrics.AddAsync(rubric, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(rubric, checkpoint));
    }

    public async Task<Result<CourseRubricResponse>> UpdateAsync(string subjectCode, Guid rubricId, SaveRubricRequest request, CancellationToken cancellationToken = default)
    {
        var course = await FindCourseAsync(subjectCode, cancellationToken);
        if (course is null) return Failure<CourseRubricResponse>("NOT_FOUND", "Subject was not found.");
        var rubric = await context.Rubrics.Include(item => item.Criteria).FirstOrDefaultAsync(item => item.Id == rubricId && item.CourseId == course.Id && item.ClassId == null, cancellationToken);
        if (rubric is null) return Failure<CourseRubricResponse>("NOT_FOUND", "Rubric was not found.");
        var checkpoint = await FindCheckpointAsync(course.Id, request.CheckpointNumber, cancellationToken);
        if (request.CheckpointNumber.HasValue && checkpoint is null) return Failure<CourseRubricResponse>("VALIDATION_ERROR", "Checkpoint was not found for this subject.");
        if (ToStatus(request.Status) == RubricStatus.Active && rubric.Criteria.Sum(item => item.Weight) != 100) return Failure<CourseRubricResponse>("BUSINESS_RULE", "Active rubrics must have criteria totaling 100%.");

        rubric.Name = request.Name.Trim(); rubric.Description = request.Description?.Trim(); rubric.CheckpointId = checkpoint?.Id; rubric.TotalWeight = request.TotalWeight; rubric.Status = ToStatus(request.Status); rubric.UpdatedBy = currentUser.UserId;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(rubric, checkpoint));
    }

    public async Task<Result> DeleteAsync(string subjectCode, Guid rubricId, CancellationToken cancellationToken = default)
    {
        var rubric = await FindRubricAsync(subjectCode, rubricId, cancellationToken);
        if (rubric is null) return Result.Failure(new Error("NOT_FOUND", "Rubric was not found."));
        context.Rubrics.Remove(rubric); await context.SaveChangesAsync(cancellationToken); return Result.Success();
    }

    public async Task<Result<RubricCriterionResponse>> CreateCriterionAsync(string subjectCode, Guid rubricId, SaveRubricCriterionRequest request, CancellationToken cancellationToken = default)
    {
        var rubric = await FindRubricAsync(subjectCode, rubricId, cancellationToken);
        if (rubric is null) return Failure<RubricCriterionResponse>("NOT_FOUND", "Rubric was not found.");
        var criterion = new RubricCriterion { RubricId = rubric.Id, Name = request.Name.Trim(), Description = request.Description?.Trim(), MaxScore = request.MaxScore, Weight = request.Weight, DisplayOrder = request.DisplayOrder };
        await context.RubricCriteria.AddAsync(criterion, cancellationToken); await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(criterion));
    }

    public async Task<Result<RubricCriterionResponse>> UpdateCriterionAsync(string subjectCode, Guid rubricId, Guid criterionId, SaveRubricCriterionRequest request, CancellationToken cancellationToken = default)
    {
        var rubric = await FindRubricAsync(subjectCode, rubricId, cancellationToken);
        if (rubric is null) return Failure<RubricCriterionResponse>("NOT_FOUND", "Rubric was not found.");
        var criterion = await context.RubricCriteria.FirstOrDefaultAsync(item => item.Id == criterionId && item.RubricId == rubric.Id, cancellationToken);
        if (criterion is null) return Failure<RubricCriterionResponse>("NOT_FOUND", "Criterion was not found.");
        criterion.Name = request.Name.Trim(); criterion.Description = request.Description?.Trim(); criterion.MaxScore = request.MaxScore; criterion.Weight = request.Weight; criterion.DisplayOrder = request.DisplayOrder; criterion.UpdatedBy = currentUser.UserId;
        await context.SaveChangesAsync(cancellationToken); return Result.Success(ToResponse(criterion));
    }

    public async Task<Result> DeleteCriterionAsync(string subjectCode, Guid rubricId, Guid criterionId, CancellationToken cancellationToken = default)
    {
        var rubric = await FindRubricAsync(subjectCode, rubricId, cancellationToken);
        if (rubric is null) return Result.Failure(new Error("NOT_FOUND", "Rubric was not found."));
        var criterion = await context.RubricCriteria.FirstOrDefaultAsync(item => item.Id == criterionId && item.RubricId == rubric.Id, cancellationToken);
        if (criterion is null) return Result.Failure(new Error("NOT_FOUND", "Criterion was not found."));
        context.RubricCriteria.Remove(criterion); await context.SaveChangesAsync(cancellationToken); return Result.Success();
    }

    private Task<Course?> FindCourseAsync(string subjectCode, CancellationToken cancellationToken) => context.Courses.FirstOrDefaultAsync(course => course.Code == subjectCode.Trim().ToUpperInvariant(), cancellationToken);
    private Task<Checkpoint?> FindCheckpointAsync(Guid courseId, int? number, CancellationToken cancellationToken) => number.HasValue ? context.Checkpoints.FirstOrDefaultAsync(item => item.CourseId == courseId && item.CheckpointNumber == number.Value && item.ClassId == null, cancellationToken) : Task.FromResult<Checkpoint?>(null);
    private async Task<Rubric?> FindRubricAsync(string subjectCode, Guid rubricId, CancellationToken cancellationToken) { var course = await FindCourseAsync(subjectCode, cancellationToken); return course is null ? null : await context.Rubrics.FirstOrDefaultAsync(item => item.Id == rubricId && item.CourseId == course.Id && item.ClassId == null, cancellationToken); }
    private static RubricStatus ToStatus(string status) => status.ToUpperInvariant() switch { "ACTIVE" => RubricStatus.Active, "ARCHIVED" => RubricStatus.Archived, _ => RubricStatus.Draft };
    private static CourseRubricResponse ToResponse(Rubric value, Checkpoint? checkpoint) => new() { Id = value.Id, Name = value.Name, Description = value.Description, Status = value.Status.ToString(), TotalWeight = value.TotalWeight, CheckpointNumber = checkpoint?.CheckpointNumber, Criteria = value.Criteria.OrderBy(item => item.DisplayOrder).Select(ToResponse).ToArray() };
    private static RubricCriterionResponse ToResponse(RubricCriterion value) => new() { Id = value.Id, Name = value.Name, Description = value.Description, MaxScore = value.MaxScore, Weight = value.Weight, DisplayOrder = value.DisplayOrder };
    private static Result<T> Failure<T>(string code, string message) => Result.Failure<T>(new Error(code, message));
}
