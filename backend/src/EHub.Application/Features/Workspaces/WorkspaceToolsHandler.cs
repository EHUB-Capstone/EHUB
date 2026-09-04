using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces;

public sealed class WorkspaceToolsHandler(
    IApplicationDbContext context,
    IValidator<SaveWeeklyTaskRequest> weeklyTaskValidator,
    IValidator<SaveShortcutRequest> shortcutValidator) : IWorkspaceToolsHandler
{
    public async Task<Result<WeeklyTaskBoardDto>> GetWeeklyTasksAsync(WeeklyTaskQuery query, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (query.WeekNumber is < 1 or > 10)
            return Fail<WeeklyTaskBoardDto>(ErrorCodes.WorkspaceValidationError, "Week number must be between 1 and 10.");
        Guid? effectiveClassId = query.ClassId;
        Course? course = null;
        if (query.TeamId.HasValue)
        {
            if (!await CanAccessTeamAsync(query.TeamId.Value, userId, role, cancellationToken))
                return Fail<WeeklyTaskBoardDto>(ErrorCodes.WorkspaceAccessDenied, "You cannot access this team workspace.");
            var teamContext = await context.Teams.AsNoTracking().Include(item => item.Class).ThenInclude(item => item.Course)
                .FirstOrDefaultAsync(item => item.Id == query.TeamId.Value, cancellationToken);
            if (teamContext is null) return Fail<WeeklyTaskBoardDto>(ErrorCodes.TeamNotFound, "Team was not found.");
            if (effectiveClassId.HasValue && effectiveClassId != teamContext.ClassId)
                return Fail<WeeklyTaskBoardDto>(ErrorCodes.WorkspaceAccessDenied, "The class does not belong to this team workspace.");
            effectiveClassId = teamContext.ClassId;
            course = teamContext.Class.Course;
        }
        if (!query.TeamId.HasValue && effectiveClassId.HasValue && !await CanAccessClassAsync(effectiveClassId.Value, userId, role, cancellationToken))
            return Fail<WeeklyTaskBoardDto>(ErrorCodes.WorkspaceAccessDenied, "You cannot access this class roadmap.");

        if (course is null && effectiveClassId.HasValue)
            course = await context.Classes.AsNoTracking().Where(item => item.Id == effectiveClassId).Select(item => item.Course).FirstOrDefaultAsync(cancellationToken);
        if (course is not null && !string.IsNullOrWhiteSpace(query.CourseCode) && !string.Equals(course.Code, query.CourseCode.Trim(), StringComparison.OrdinalIgnoreCase))
            return Fail<WeeklyTaskBoardDto>(ErrorCodes.WorkspaceAccessDenied, "The course does not belong to this workspace.");
        if (course is null && !string.IsNullOrWhiteSpace(query.CourseCode))
            course = await context.Courses.AsNoTracking().FirstOrDefaultAsync(item => item.Code.ToUpper() == query.CourseCode.Trim().ToUpper(), cancellationToken);
        if (!string.IsNullOrWhiteSpace(query.CourseCode) && course is null)
            return Fail<WeeklyTaskBoardDto>(ErrorCodes.WorkspaceNotFound, "Course was not found.");

        var tasks = context.WeeklyTasks.AsNoTracking()
            .Include(item => item.Course)
            .Include(item => item.AssigneeStudent)
            .Include(item => item.Creator)
            .Where(item => !query.WeekNumber.HasValue || item.WeekNumber == query.WeekNumber)
            .Where(item =>
                item.Scope == WeeklyTaskScope.Course || item.Scope == WeeklyTaskScope.GlobalTemplate ||
                effectiveClassId.HasValue && item.Scope == WeeklyTaskScope.Class && item.ClassId == effectiveClassId ||
                query.TeamId.HasValue && item.Scope == WeeklyTaskScope.Team && item.TeamId == query.TeamId);
        if (course is not null) tasks = tasks.Where(item => item.CourseId == course.Id);
        if (IsRole(role, SystemRoles.Student)) tasks = tasks.Where(item => item.VisibleToStudents);
        if (!string.IsNullOrWhiteSpace(query.Status) && TryStatus(query.Status, out var status)) tasks = tasks.Where(item => item.Status == status);
        if (query.AssigneeStudentId.HasValue) tasks = tasks.Where(item => item.AssigneeStudentId == query.AssigneeStudentId);
        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            if (!Enum.TryParse<TaskPriority>(query.Priority, true, out var priority) || !Enum.IsDefined(priority))
                return Fail<WeeklyTaskBoardDto>(ErrorCodes.WorkspaceValidationError, "Task priority is invalid.");
            tasks = tasks.Where(item => item.Priority == priority);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            tasks = tasks.Where(item => item.Title.ToLower().Contains(search) || item.Description != null && item.Description.ToLower().Contains(search));
        }

        var rows = await tasks.OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var result = new WeeklyTaskBoardDto
        {
            CourseTasks = rows.Where(item => item.Scope is WeeklyTaskScope.Course or WeeklyTaskScope.GlobalTemplate).Select(MapTask).ToArray(),
            ClassTasks = rows.Where(item => item.Scope == WeeklyTaskScope.Class && item.ClassId == effectiveClassId).Select(MapTask).ToArray(),
            TeamTasks = rows.Where(item => item.Scope == WeeklyTaskScope.Team && item.TeamId == query.TeamId).Select(MapTask).ToArray()
        };
        return Result.Success(result);
    }

    public async Task<Result<WeeklyTaskDto>> CreateWeeklyTaskAsync(SaveWeeklyTaskRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var validation = await weeklyTaskValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Fail<WeeklyTaskDto>(ErrorCodes.WorkspaceValidationError, validation.Errors[0].ErrorMessage);
        var scope = ParseScope(request.TaskType, request.Scope);
        var course = await context.Courses.FirstOrDefaultAsync(item => item.Code.ToUpper() == request.CourseCode.Trim().ToUpper(), cancellationToken);
        if (course is null) return Fail<WeeklyTaskDto>(ErrorCodes.WorkspaceNotFound, "Course was not found.");
        var access = await CanMutateTaskScopeAsync(scope, course.Id, request.ClassId, request.TeamId, userId, role, cancellationToken);
        if (!access) return Fail<WeeklyTaskDto>(ErrorCodes.WorkspaceAccessDenied, "You cannot create a task in this roadmap.");
        if (!await ContextMatchesAsync(scope, course.Id, request.ClassId, request.TeamId, request.AssigneeStudentId, cancellationToken))
            return Fail<WeeklyTaskDto>(ErrorCodes.WorkspaceValidationError, "Course, class, team, or assignee context is inconsistent.");
        if (await DuplicateTaskAsync(null, scope, course.Id, request.ClassId, request.TeamId, request.WeekNumber, request.Title, cancellationToken))
            return Fail<WeeklyTaskDto>(ErrorCodes.WeeklyTaskDuplicated, "A task with this title already exists for the selected week.");

        var now = DateTime.UtcNow;
        var task = new WeeklyTask { CreatedAt = now, CreatedById = userId, CreatedBy = userId, CourseId = course.Id, Course = course };
        ApplyTask(task, request, scope, userId, now);
        context.WeeklyTasks.Add(task);
        await context.SaveChangesAsync(cancellationToken);
        task.Creator = await context.Users.FirstAsync(item => item.Id == userId, cancellationToken);
        if (task.AssigneeStudentId.HasValue) task.AssigneeStudent = await context.Students.FirstOrDefaultAsync(item => item.Id == task.AssigneeStudentId, cancellationToken);
        return Result.Success(MapTask(task));
    }

    public async Task<Result<WeeklyTaskDto>> UpdateWeeklyTaskAsync(Guid taskId, SaveWeeklyTaskRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var validation = await weeklyTaskValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Fail<WeeklyTaskDto>(ErrorCodes.WorkspaceValidationError, validation.Errors[0].ErrorMessage);
        var task = await context.WeeklyTasks.Include(item => item.Course).Include(item => item.Creator).Include(item => item.AssigneeStudent).FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null) return Fail<WeeklyTaskDto>(ErrorCodes.WeeklyTaskNotFound, "Weekly task was not found.");
        if (!await CanMutateTaskScopeAsync(task.Scope, task.CourseId, task.ClassId, task.TeamId, userId, role, cancellationToken))
            return Fail<WeeklyTaskDto>(ErrorCodes.WorkspaceAccessDenied, "You cannot update this roadmap task.");
        if (!task.CourseId.HasValue || task.Course is null ||
            !string.Equals(task.Course.Code, request.CourseCode.Trim(), StringComparison.OrdinalIgnoreCase) ||
            ParseScope(request.TaskType, request.Scope) != task.Scope ||
            !await ContextMatchesAsync(task.Scope, task.CourseId.Value, request.ClassId, request.TeamId, request.AssigneeStudentId, cancellationToken))
            return Fail<WeeklyTaskDto>(ErrorCodes.WorkspaceValidationError, "Course, class, team, or assignee context is inconsistent.");
        if (!await CanMutateTaskScopeAsync(task.Scope, task.CourseId, request.ClassId, request.TeamId, userId, role, cancellationToken))
            return Fail<WeeklyTaskDto>(ErrorCodes.WorkspaceAccessDenied, "You cannot move a task into this roadmap.");
        if (await DuplicateTaskAsync(task.Id, task.Scope, task.CourseId, request.ClassId, request.TeamId, request.WeekNumber, request.Title, cancellationToken))
            return Fail<WeeklyTaskDto>(ErrorCodes.WeeklyTaskDuplicated, "A task with this title already exists for the selected week.");
        ApplyTask(task, request, task.Scope, userId, DateTime.UtcNow);
        await context.SaveChangesAsync(cancellationToken);
        task.AssigneeStudent = task.AssigneeStudentId.HasValue
            ? await context.Students.FirstOrDefaultAsync(item => item.Id == task.AssigneeStudentId, cancellationToken)
            : null;
        return Result.Success(MapTask(task));
    }

    public async Task<Result<WeeklyTaskDto>> UpdateWeeklyTaskStatusAsync(Guid taskId, UpdateWeeklyTaskStatusRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var task = await context.WeeklyTasks.Include(item => item.Course).Include(item => item.Creator).Include(item => item.AssigneeStudent).FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null) return Fail<WeeklyTaskDto>(ErrorCodes.WeeklyTaskNotFound, "Weekly task was not found.");
        if (!TryStatus(request.Status, out var status)) return Fail<WeeklyTaskDto>(ErrorCodes.WorkspaceValidationError, "Task status is invalid.");
        if (!await CanMutateTaskScopeAsync(task.Scope, task.CourseId, task.ClassId, task.TeamId, userId, role, cancellationToken))
            return Fail<WeeklyTaskDto>(ErrorCodes.WorkspaceAccessDenied, "You cannot change this task status.");
        task.Status = status;
        if (request.Checklist is not null) task.ChecklistJson = JsonSerializer.Serialize(request.Checklist);
        task.CompletionPercentage = status == WeeklyTaskStatus.Done ? 100 : CalculateCompletion(task.ChecklistJson);
        task.UpdatedById = userId; task.UpdatedBy = userId; task.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(MapTask(task));
    }

    public async Task<Result> DeleteWeeklyTaskAsync(Guid taskId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var task = await context.WeeklyTasks.FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null) return Result.Failure(ErrorCodes.WeeklyTaskNotFound, "Weekly task was not found.");
        if (!await CanMutateTaskScopeAsync(task.Scope, task.CourseId, task.ClassId, task.TeamId, userId, role, cancellationToken))
            return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You cannot delete this roadmap task.");
        if (IsRole(role, SystemRoles.Student) && task.CreatedById != userId)
            return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You can only delete tasks you created.");
        task.IsDeleted = true; task.DeletedAt = DateTime.UtcNow; task.DeletedBy = userId;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyCollection<ProjectShortcutDto>>> GetShortcutsAsync(Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessTeamAsync(teamId, userId, role, cancellationToken))
            return Fail<IReadOnlyCollection<ProjectShortcutDto>>(ErrorCodes.WorkspaceAccessDenied, "You cannot access this team workspace.");
        var rows = await context.Shortcuts.AsNoTracking().Include(item => item.Creator).Where(item => item.TeamId == teamId).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<ProjectShortcutDto>>(rows.Select(MapShortcut).ToArray());
    }

    public async Task<Result<ProjectShortcutDto>> CreateShortcutAsync(Guid teamId, SaveShortcutRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var validation = await shortcutValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Fail<ProjectShortcutDto>(ErrorCodes.WorkspaceValidationError, validation.Errors[0].ErrorMessage);
        if (!await CanMutateTeamAsync(teamId, userId, role, cancellationToken)) return Fail<ProjectShortcutDto>(ErrorCodes.WorkspaceAccessDenied, "This workspace is read-only for your role.");
        var project = await context.Projects.FirstOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        if (project is null) return Fail<ProjectShortcutDto>(ErrorCodes.WorkspaceNotFound, "Create the project workspace before adding shortcuts.");
        var url = NormalizeUrl(request.Url);
        if (await context.Shortcuts.AnyAsync(item => item.TeamId == teamId && item.Url.ToLower() == url.ToLower(), cancellationToken))
            return Fail<ProjectShortcutDto>(ErrorCodes.ShortcutDuplicated, "A shortcut with this URL already exists in the team.");
        var shortcut = new ProjectShortcut { TeamId = teamId, ProjectId = project.Id, Name = request.Name.Trim(), Url = url, Description = request.Description?.Trim(), ShortcutType = ParseShortcutType(request.ShortcutType), CreatedById = userId, CreatedBy = userId, CreatedAt = DateTime.UtcNow };
        context.Shortcuts.Add(shortcut);
        await context.SaveChangesAsync(cancellationToken);
        shortcut.Creator = await context.Users.AsNoTracking().FirstAsync(item => item.Id == userId, cancellationToken);
        return Result.Success(MapShortcut(shortcut));
    }

    public async Task<Result<ProjectShortcutDto>> UpdateShortcutAsync(Guid teamId, Guid shortcutId, SaveShortcutRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var validation = await shortcutValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Fail<ProjectShortcutDto>(ErrorCodes.WorkspaceValidationError, validation.Errors[0].ErrorMessage);
        if (!await CanMutateTeamAsync(teamId, userId, role, cancellationToken)) return Fail<ProjectShortcutDto>(ErrorCodes.WorkspaceAccessDenied, "This workspace is read-only for your role.");
        var shortcut = await context.Shortcuts.Include(item => item.Creator).FirstOrDefaultAsync(item => item.Id == shortcutId && item.TeamId == teamId, cancellationToken);
        if (shortcut is null) return Fail<ProjectShortcutDto>(ErrorCodes.ShortcutNotFound, "Shortcut was not found.");
        if (IsRole(role, SystemRoles.Student) && shortcut.CreatedById != userId) return Fail<ProjectShortcutDto>(ErrorCodes.WorkspaceAccessDenied, "You can only manage shortcuts you created.");
        var url = NormalizeUrl(request.Url);
        if (await context.Shortcuts.AnyAsync(item => item.TeamId == teamId && item.Id != shortcutId && item.Url.ToLower() == url.ToLower(), cancellationToken))
            return Fail<ProjectShortcutDto>(ErrorCodes.ShortcutDuplicated, "A shortcut with this URL already exists in the team.");
        shortcut.Name = request.Name.Trim(); shortcut.Url = url; shortcut.Description = request.Description?.Trim(); shortcut.ShortcutType = ParseShortcutType(request.ShortcutType); shortcut.UpdatedAt = DateTime.UtcNow; shortcut.UpdatedBy = userId;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(MapShortcut(shortcut));
    }

    public async Task<Result> DeleteShortcutAsync(Guid teamId, Guid shortcutId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!await CanMutateTeamAsync(teamId, userId, role, cancellationToken)) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "This workspace is read-only for your role.");
        var shortcut = await context.Shortcuts.FirstOrDefaultAsync(item => item.Id == shortcutId && item.TeamId == teamId, cancellationToken);
        if (shortcut is null) return Result.Failure(ErrorCodes.ShortcutNotFound, "Shortcut was not found.");
        if (IsRole(role, SystemRoles.Student) && shortcut.CreatedById != userId) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You can only manage shortcuts you created.");
        shortcut.IsDeleted = true; shortcut.DeletedAt = DateTime.UtcNow; shortcut.DeletedBy = userId;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<bool> CanAccessTeamAsync(Guid teamId, Guid userId, string role, CancellationToken ct)
    {
        if (IsRole(role, SystemRoles.Admin)) return await context.Teams.AnyAsync(team => team.Id == teamId, ct);
        if (IsRole(role, SystemRoles.Lecturer)) return await context.Teams.AnyAsync(team => team.Id == teamId && team.Class.PrimaryLecturerId == userId, ct);
        if (IsRole(role, SystemRoles.Mentor)) return await context.Teams.AnyAsync(team => team.Id == teamId && team.MentorAssignments.Any(item => item.MentorProfile.UserId == userId && item.EndedAt == null), ct);
        if (IsRole(role, SystemRoles.Student)) return await context.Teams.AnyAsync(team => team.Id == teamId && team.TeamMembers.Any(member => member.CountsTowardActiveTeam && member.ClassStudent.Student.UserId == userId), ct);
        return false;
    }

    private Task<bool> CanMutateTeamAsync(Guid teamId, Guid userId, string role, CancellationToken ct)
    {
        var mutable = context.Teams.Where(team => team.Id == teamId && team.Status == TeamStatus.Active && team.Class.Status != ClassStatus.Archived && team.Class.Status != ClassStatus.Completed);
        if (IsRole(role, SystemRoles.Admin)) return mutable.AnyAsync(ct);
        if (IsRole(role, SystemRoles.Lecturer)) return mutable.AnyAsync(team => team.Class.PrimaryLecturerId == userId, ct);
        if (IsRole(role, SystemRoles.Student)) return mutable.AnyAsync(team => team.TeamMembers.Any(member => member.CountsTowardActiveTeam && member.ClassStudent.Student.UserId == userId), ct);
        return Task.FromResult(false);
    }

    private async Task<bool> CanAccessClassAsync(Guid classId, Guid userId, string role, CancellationToken ct)
    {
        if (IsRole(role, SystemRoles.Admin)) return await context.Classes.AnyAsync(item => item.Id == classId, ct);
        if (IsRole(role, SystemRoles.Lecturer)) return await context.Classes.AnyAsync(item => item.Id == classId && item.PrimaryLecturerId == userId, ct);
        if (IsRole(role, SystemRoles.Mentor)) return await context.Teams.AnyAsync(team => team.ClassId == classId && team.MentorAssignments.Any(item => item.MentorProfile.UserId == userId && item.EndedAt == null), ct);
        if (IsRole(role, SystemRoles.Student)) return await context.ClassStudents.AnyAsync(item => item.ClassId == classId && item.Student.UserId == userId && item.EnrollmentStatus == EnrollmentStatus.Active, ct);
        return false;
    }

    private async Task<bool> CanMutateTaskScopeAsync(WeeklyTaskScope scope, Guid? courseId, Guid? classId, Guid? teamId, Guid userId, string role, CancellationToken ct)
    {
        if (scope is WeeklyTaskScope.Course or WeeklyTaskScope.GlobalTemplate)
            return IsRole(role, SystemRoles.Admin) || IsRole(role, SystemRoles.Lecturer) && courseId.HasValue && await context.Classes.AnyAsync(item => item.CourseId == courseId && item.PrimaryLecturerId == userId, ct);
        if (scope == WeeklyTaskScope.Class)
            return classId.HasValue && (IsRole(role, SystemRoles.Admin) || IsRole(role, SystemRoles.Lecturer)) &&
                await context.Classes.AnyAsync(item => item.Id == classId && item.Status != ClassStatus.Completed && item.Status != ClassStatus.Archived, ct) &&
                await CanAccessClassAsync(classId.Value, userId, role, ct);
        return teamId.HasValue && await CanMutateTeamAsync(teamId.Value, userId, role, ct);
    }

    private async Task<bool> ContextMatchesAsync(WeeklyTaskScope scope, Guid courseId, Guid? classId, Guid? teamId, Guid? assigneeId, CancellationToken ct)
    {
        if (scope == WeeklyTaskScope.Class && (!classId.HasValue || !await context.Classes.AnyAsync(item => item.Id == classId && item.CourseId == courseId, ct))) return false;
        if (scope == WeeklyTaskScope.Team && (!teamId.HasValue || !await context.Teams.AnyAsync(item => item.Id == teamId && item.Class.CourseId == courseId && (!classId.HasValue || item.ClassId == classId), ct))) return false;
        return !assigneeId.HasValue || (teamId.HasValue && await context.TeamMembers.AnyAsync(item => item.TeamId == teamId && item.StudentId == assigneeId && item.CountsTowardActiveTeam, ct));
    }

    private Task<bool> DuplicateTaskAsync(Guid? id, WeeklyTaskScope scope, Guid? courseId, Guid? classId, Guid? teamId, int week, string title, CancellationToken ct)
    {
        var normalized = title.Trim().ToLower();
        return context.WeeklyTasks.AnyAsync(item => item.Id != id && item.Scope == scope && item.CourseId == courseId && item.ClassId == classId && item.TeamId == teamId && item.WeekNumber == week && item.Title.ToLower() == normalized, ct);
    }

    private static void ApplyTask(WeeklyTask task, SaveWeeklyTaskRequest request, WeeklyTaskScope scope, Guid userId, DateTime now)
    {
        task.Title = request.Title.Trim(); task.Description = request.Description?.Trim(); task.Scope = scope; task.TaskType = WeeklyTaskType.Other; task.WeekNumber = request.WeekNumber;
        task.ClassId = scope == WeeklyTaskScope.Course ? null : request.ClassId; task.TeamId = scope == WeeklyTaskScope.Team ? request.TeamId : null; task.AssigneeStudentId = scope == WeeklyTaskScope.Team ? request.AssigneeStudentId : null;
        task.Status = TryStatus(request.Status, out var status) ? status : WeeklyTaskStatus.Todo; task.Priority = Enum.TryParse<TaskPriority>(request.Priority, true, out var priority) ? priority : TaskPriority.Medium;
        task.StartDate = NormalizeTaskDate(request.StartDate); task.DueDate = NormalizeTaskDate(request.DueDate); task.AttachmentsJson = JsonSerializer.Serialize(request.Attachments); task.ChecklistJson = JsonSerializer.Serialize(request.Checklist); task.Tags = request.Tags.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        task.IsTemplate = scope == WeeklyTaskScope.Course; task.IsMandatory = request.IsMandatory; task.VisibleToStudents = request.VisibleToStudents; task.EstimatedHours = request.EstimatedHours; task.CompletionPercentage = task.Status == WeeklyTaskStatus.Done ? 100 : CalculateCompletion(task.ChecklistJson);
        if (task.DueDate < now && task.Status is not WeeklyTaskStatus.Done and not WeeklyTaskStatus.Cancelled && scope == WeeklyTaskScope.Team) task.Status = WeeklyTaskStatus.Overdue;
        task.UpdatedById = userId; task.UpdatedBy = userId; task.UpdatedAt = now;
    }

    // Date-only form values represent UTC calendar dates, independent of server timezone.
    private static DateTime? NormalizeTaskDate(DateTime? value) => value?.Kind switch
    {
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => value
    };

    private static int CalculateCompletion(string json)
    {
        var rows = DeserializeChecklist(json);
        return rows.Count > 0 ? (int)Math.Round(rows.Count(row => row.IsCompleted) * 100d / rows.Count) : 0;
    }

    private static WeeklyTaskDto MapTask(WeeklyTask item) => new()
    {
        Id = item.Id, Title = item.Title, Description = item.Description ?? string.Empty, TaskType = item.Scope switch { WeeklyTaskScope.Course or WeeklyTaskScope.GlobalTemplate => "COURSE_TEMPLATE", WeeklyTaskScope.Class => "CLASS_TASK", _ => "TEAM_TASK" }, Scope = item.Scope.ToString().ToUpperInvariant(), WeekNumber = item.WeekNumber,
        CourseCode = item.Course?.Code ?? string.Empty, ClassId = item.ClassId, TeamId = item.TeamId, AssigneeStudentId = item.AssigneeStudent is null ? null : new WeeklyTaskAssigneeDto { Id = item.AssigneeStudent.Id, FullName = item.AssigneeStudent.FullName, RollNumber = item.AssigneeStudent.RollNumber ?? string.Empty },
        Status = item.Status switch { WeeklyTaskStatus.InProgress => "IN_PROGRESS", WeeklyTaskStatus.Done => "COMPLETED", _ => item.Status.ToString().ToUpperInvariant() }, Priority = item.Priority.ToString().ToUpperInvariant(), StartDate = item.StartDate, DueDate = item.DueDate,
        AttachmentsJson = item.AttachmentsJson, ChecklistJson = item.ChecklistJson, Attachments = DeserializeAttachments(item.AttachmentsJson), Checklist = DeserializeChecklist(item.ChecklistJson), Tags = item.Tags, IsTemplate = item.IsTemplate, IsMandatory = item.IsMandatory, VisibleToStudents = item.VisibleToStudents, CompletionPercentage = item.CompletionPercentage, EstimatedHours = item.EstimatedHours,
        CreatedBy = new WeeklyTaskCreatorDto { Id = item.Creator.Id, Name = item.Creator.FullName, Avatar = item.Creator.AvatarUrl }, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
    };

    private static ProjectShortcutDto MapShortcut(ProjectShortcut item) => new() { Id = item.Id, TeamId = item.TeamId, ProjectId = item.ProjectId, Name = item.Name, Url = item.Url, Description = item.Description, ShortcutType = item.ShortcutType.ToString().ToUpperInvariant(), CreatedBy = new ShortcutCreatorDto { Id = item.Creator.Id, Name = item.Creator.FullName, Avatar = item.Creator.AvatarUrl }, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt };
    private static IReadOnlyCollection<WeeklyTaskAttachmentDto> DeserializeAttachments(string json) { try { return JsonSerializer.Deserialize<WeeklyTaskAttachmentDto[]>(json, JsonOptions) ?? Array.Empty<WeeklyTaskAttachmentDto>(); } catch (JsonException) { return Array.Empty<WeeklyTaskAttachmentDto>(); } }
    private static IReadOnlyCollection<WeeklyTaskChecklistItemDto> DeserializeChecklist(string json) { try { return JsonSerializer.Deserialize<WeeklyTaskChecklistItemDto[]>(json, JsonOptions) ?? Array.Empty<WeeklyTaskChecklistItemDto>(); } catch (JsonException) { return Array.Empty<WeeklyTaskChecklistItemDto>(); } }
    private static WeeklyTaskScope ParseScope(string type, string scope) => type.ToUpperInvariant() switch { "COURSE_TEMPLATE" => WeeklyTaskScope.Course, "CLASS_TASK" => WeeklyTaskScope.Class, "TEAM_TASK" => WeeklyTaskScope.Team, _ => Enum.TryParse<WeeklyTaskScope>(scope, true, out var parsed) ? parsed : WeeklyTaskScope.Team };
    private static bool TryStatus(string? value, out WeeklyTaskStatus status) { var normalized = value?.Trim().ToUpperInvariant(); status = normalized switch { "TODO" => WeeklyTaskStatus.Todo, "IN_PROGRESS" => WeeklyTaskStatus.InProgress, "REVIEW" => WeeklyTaskStatus.Review, "COMPLETED" => WeeklyTaskStatus.Done, "DONE" => WeeklyTaskStatus.Done, "CANCELLED" => WeeklyTaskStatus.Cancelled, "OVERDUE" => WeeklyTaskStatus.Overdue, _ => (WeeklyTaskStatus)(-1) }; return (int)status >= 0; }
    private static ShortcutType ParseShortcutType(string? value) => Enum.TryParse<ShortcutType>(value, true, out var type) ? type : ShortcutType.Other;
    private static string NormalizeUrl(string value) { var trimmed = value.Trim(); return trimmed.EndsWith('/') ? trimmed.TrimEnd('/') : trimmed; }
    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
    private static Result<T> Fail<T>(string code, string message) => Result.Failure<T>(code, message);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
}
