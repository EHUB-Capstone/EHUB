using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Subjects.Roadmap;

public sealed class SubjectRoadmapHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : ISubjectRoadmapHandler
{
    public async Task<Result<RoadmapItemResponse>> CreateAsync(string subjectCode, SaveRoadmapItemRequest request, CancellationToken cancellationToken = default)
    {
        var course = await FindCourseAsync(subjectCode, cancellationToken);
        if (course is null) return Failure<RoadmapItemResponse>("NOT_FOUND", "Subject was not found.");
        if (!request.CourseCode.Equals(course.Code, StringComparison.OrdinalIgnoreCase)) return Failure<RoadmapItemResponse>("VALIDATION_ERROR", "Course code does not match the requested subject.");

        var item = new WeeklyTask
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            CourseId = course.Id,
            Scope = WeeklyTaskScope.Course,
            IsTemplate = true,
            WeekNumber = request.WeekNumber,
            Priority = ToPriority(request.Priority),
            EstimatedHours = request.EstimatedHours,
            Tags = NormalizeTags(request.Tags),
            CreatedById = currentUser.UserId ?? Guid.Empty,
        };
        await context.WeeklyTasks.AddAsync(item, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(item, course.Code));
    }

    public async Task<Result<RoadmapItemResponse>> UpdateAsync(string subjectCode, Guid itemId, SaveRoadmapItemRequest request, CancellationToken cancellationToken = default)
    {
        var course = await FindCourseAsync(subjectCode, cancellationToken);
        if (course is null) return Failure<RoadmapItemResponse>("NOT_FOUND", "Subject was not found.");
        var item = await context.WeeklyTasks.FirstOrDefaultAsync(task =>
            task.Id == itemId && task.CourseId == course.Id && task.Scope == WeeklyTaskScope.Course && task.IsTemplate,
            cancellationToken);
        if (item is null) return Failure<RoadmapItemResponse>("NOT_FOUND", "Roadmap item was not found.");

        item.Title = request.Title.Trim();
        item.Description = request.Description?.Trim();
        item.WeekNumber = request.WeekNumber;
        item.Priority = ToPriority(request.Priority);
        item.EstimatedHours = request.EstimatedHours;
        item.Tags = NormalizeTags(request.Tags);
        item.UpdatedById = currentUser.UserId;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(item, course.Code));
    }

    public async Task<Result> DeleteAsync(string subjectCode, Guid itemId, CancellationToken cancellationToken = default)
    {
        var course = await FindCourseAsync(subjectCode, cancellationToken);
        if (course is null) return Result.Failure(new Error("NOT_FOUND", "Subject was not found."));
        var item = await context.WeeklyTasks.FirstOrDefaultAsync(task =>
            task.Id == itemId && task.CourseId == course.Id && task.Scope == WeeklyTaskScope.Course && task.IsTemplate,
            cancellationToken);
        if (item is null) return Result.Failure(new Error("NOT_FOUND", "Roadmap item was not found."));

        context.WeeklyTasks.Remove(item);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private Task<Course?> FindCourseAsync(string subjectCode, CancellationToken cancellationToken) =>
        context.Courses.FirstOrDefaultAsync(course => course.Code == subjectCode.Trim().ToUpperInvariant(), cancellationToken);

    private static TaskPriority ToPriority(string priority) => priority.ToUpperInvariant() switch
    {
        "LOW" => TaskPriority.Low,
        "HIGH" => TaskPriority.High,
        "CRITICAL" => TaskPriority.Critical,
        _ => TaskPriority.Medium,
    };

    private static string[] NormalizeTags(IEnumerable<string> tags) => tags
        .Select(tag => tag?.Trim())
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray()!;

    private static RoadmapItemResponse ToResponse(WeeklyTask item, string courseCode) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Description = item.Description,
        CourseCode = courseCode,
        WeekNumber = item.WeekNumber,
        Priority = item.Priority.ToString().ToUpper(),
        EstimatedHours = item.EstimatedHours,
        Tags = item.Tags,
    };

    private static Result<T> Failure<T>(string code, string message) =>
        Result.Failure<T>(new Error(code, message));
}
