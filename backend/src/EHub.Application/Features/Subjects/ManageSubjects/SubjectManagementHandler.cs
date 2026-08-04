using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Subjects.ManageSubjects;

public sealed class SubjectManagementHandler(IApplicationDbContext context, ICurrentUserService currentUser) : ISubjectManagementHandler
{
    public async Task<Result<SubjectListResponse>> GetAsync(string? search, string? status, bool activeOnly, CancellationToken token = default)
    {
        if (!TryStatus(status, out var parsed)) return Fail<SubjectListResponse>("VALIDATION_ERROR", "Status must be active or disabled.");
        var query = context.Courses.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(course => course.Code.Contains(term) || course.Name.Contains(term)); }
        if (activeOnly) query = query.Where(course => course.Status == CourseStatus.Active); else if (parsed is not null) query = query.Where(course => course.Status == parsed.Value);
        var subjects = await query.OrderBy(course => course.Code).Select(course => ToResponse(course)).ToArrayAsync(token);
        return Result.Success(new SubjectListResponse { Subjects = subjects });
    }
    public async Task<Result<SubjectResponse>> CreateAsync(CreateSubjectRequest request, CancellationToken token = default)
    {
        var code = request.SubjectCode.Trim().ToUpperInvariant(); if (await context.Courses.AnyAsync(course => course.Code == code, token)) return Fail<SubjectResponse>("CONFLICT", "Subject code already exists.");
        var subject = new Course { Code = code, Name = request.SubjectName.Trim(), Status = ToStatus(request.Status), CreatedBy = currentUser.UserId }; await context.Courses.AddAsync(subject, token); await context.SaveChangesAsync(token); return Result.Success(ToResponse(subject));
    }
    public async Task<Result<SubjectResponse>> UpdateAsync(Guid id, UpdateSubjectRequest request, CancellationToken token = default) { var subject = await context.Courses.FirstOrDefaultAsync(course => course.Id == id, token); if (subject is null) return Fail<SubjectResponse>("NOT_FOUND", "Subject was not found."); subject.Name = request.SubjectName.Trim(); subject.Status = ToStatus(request.Status); subject.UpdatedBy = currentUser.UserId; await context.SaveChangesAsync(token); return Result.Success(ToResponse(subject)); }
    public async Task<Result<SubjectResponse>> DisableAsync(Guid id, CancellationToken token = default) { var subject = await context.Courses.FirstOrDefaultAsync(course => course.Id == id, token); if (subject is null) return Fail<SubjectResponse>("NOT_FOUND", "Subject was not found."); subject.Status = CourseStatus.Inactive; subject.UpdatedBy = currentUser.UserId; await context.SaveChangesAsync(token); return Result.Success(ToResponse(subject)); }
    private static bool TryStatus(string? status, out CourseStatus? value) { value = null; if (string.IsNullOrWhiteSpace(status)) return true; if (status.Equals("active", StringComparison.OrdinalIgnoreCase)) { value = CourseStatus.Active; return true; } if (status.Equals("disabled", StringComparison.OrdinalIgnoreCase)) { value = CourseStatus.Inactive; return true; } return false; }
    private static CourseStatus ToStatus(string status) => status.Equals("disabled", StringComparison.OrdinalIgnoreCase) ? CourseStatus.Inactive : CourseStatus.Active;
    private static SubjectResponse ToResponse(Course course) => new() { Id = course.Id, SubjectCode = course.Code, SubjectName = course.Name, Status = course.Status == CourseStatus.Active ? "active" : "disabled" };
    private static Result<T> Fail<T>(string code, string message) => Result.Failure<T>(new Error(code, message));
}
