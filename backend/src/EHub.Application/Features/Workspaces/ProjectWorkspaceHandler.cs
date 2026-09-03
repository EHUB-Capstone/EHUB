using System.Text.RegularExpressions;
using System.Text.Json;
using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces;

public sealed class ProjectWorkspaceHandler : IProjectWorkspaceHandler
{
    private const int MaximumTagsPerType = 10;
    private static readonly Regex TagPattern = new(
        @"^[\p{L}\p{N}.+#][\p{L}\p{N} .+#&/_-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectWorkspaceHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyCollection<WorkspaceOptionDto>>> GetAccessibleAsync(
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var query = AccessibleTeamQuery(userId, role);
        if (query == null)
            return FailureList(ErrorCodes.WorkspaceAccessDenied, "You do not have access to project workspaces.");

        var teams = await query
            .OrderByDescending(team => team.Class.Semester.Status == SemesterStatus.Active)
            .ThenByDescending(team => team.Class.Semester.Year)
            .ThenBy(team => team.Class.ClassCode)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<WorkspaceOptionDto>>(teams.Select(MapOption).ToArray());
    }

    public async Task<Result<WorkspaceContextDto?>> GetCurrentContextAsync(
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var accessible = await GetAccessibleAsync(userId, role, cancellationToken);
        if (accessible.IsFailure) return Result.Failure<WorkspaceContextDto?>(accessible.Error);
        var selected = accessible.Value.FirstOrDefault();
        return selected == null
            ? Result.Success<WorkspaceContextDto?>(null)
            : Result.Success<WorkspaceContextDto?>(new WorkspaceContextDto
            {
                SelectedWorkspace = selected,
                AvailableWorkspaces = accessible.Value,
                AccessMode = selected.AccessMode
            });
    }

    public async Task<Result<WorkspaceContextDto>> GetContextAsync(
        Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var accessible = await GetAccessibleAsync(userId, role, cancellationToken);
        if (accessible.IsFailure) return Result.Failure<WorkspaceContextDto>(accessible.Error);
        var selected = accessible.Value.FirstOrDefault(option => option.TeamId == teamId);
        if (selected == null)
            return Failure<WorkspaceContextDto>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        return Result.Success(new WorkspaceContextDto
        {
            SelectedWorkspace = selected,
            AvailableWorkspaces = accessible.Value,
            AccessMode = selected.AccessMode
        });
    }

    public async Task<Result<ProjectWorkspaceDetailDto>> GetDetailAsync(
        Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var query = AccessibleTeamQuery(userId, role);
        if (query == null)
            return Failure<ProjectWorkspaceDetailDto>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to project workspaces.");
        var team = await query.FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team == null)
            return Failure<ProjectWorkspaceDetailDto>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        return Result.Success(MapDetail(team));
    }

    public async Task<Result<ProjectWorkspaceDto>> CreateAsync(
        Guid teamId,
        CreateProjectWorkspaceRequest request,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!IsRole(role, SystemRoles.Student))
            return Failure<ProjectWorkspaceDto>(ErrorCodes.WorkspaceAccessDenied, "Only the active team leader can create a project workspace.");

        var validation = Validate(request.ProjectName, request.Description, request.StartupField, request.TechnologyStack, request.Keywords);
        if (validation != null) return Result.Failure<ProjectWorkspaceDto>(validation);

        var technologies = NormalizeTags(request.TechnologyStack ?? Array.Empty<string>());
        var keywords = NormalizeTags(request.Keywords ?? Array.Empty<string>());
        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var team = await TeamQuery(tracking: true)
                    .FirstOrDefaultAsync(item => item.Id == teamId, transactionCancellationToken);
                if (team == null)
                    return Failure<ProjectWorkspaceDto>(ErrorCodes.TeamNotFound, "The requested team was not found.");
                if (team.Status != TeamStatus.Active)
                    return Failure<ProjectWorkspaceDto>(ErrorCodes.TeamInactive, "A workspace can only be created for an active team.");
                var classError = ClassStateRules.GetMutationError(team.Class.Status);
                if (classError != null) return Result.Failure<ProjectWorkspaceDto>(classError);

                var leader = team.TeamMembers.SingleOrDefault(member =>
                    member.CountsTowardActiveTeam &&
                    member.RoleInTeam == TeamMemberRole.Leader &&
                    member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active);
                if (leader?.ClassStudent.Student.UserId != userId)
                    return Failure<ProjectWorkspaceDto>(ErrorCodes.WorkspaceLeaderRequired, "Only the active team leader can create this project workspace.");
                if (team.Project != null || await _context.Projects.AnyAsync(project => project.TeamId == teamId, transactionCancellationToken))
                    return Failure<ProjectWorkspaceDto>(ErrorCodes.WorkspaceAlreadyExists, "This team already has an active project workspace.");

                var now = DateTime.UtcNow;
                var project = new Project
                {
                    TeamId = team.Id,
                    Team = team,
                    Name = (request.ProjectName ?? string.Empty).Trim(),
                    Description = (request.Description ?? string.Empty).Trim(),
                    StartupField = (request.StartupField ?? string.Empty).Trim(),
                    Technology = string.Join(", ", technologies.Select(tag => tag.Display)),
                    Status = ProjectStatus.Draft,
                    CreatedById = userId,
                    CreatedBy = userId,
                    CreatedAt = now
                };
                foreach (var tag in technologies)
                    project.ProjectTags.Add(CreateTag(project, tag, ProjectTagType.Technology, userId, now));
                foreach (var tag in keywords)
                    project.ProjectTags.Add(CreateTag(project, tag, ProjectTagType.Keyword, userId, now));
                project.ActivityLogs.Add(new ProjectActivityLog
                {
                    ProjectId = project.Id,
                    Project = project,
                    ActorUserId = userId,
                    Action = "WORKSPACE_CREATED",
                    Summary = "Created the project workspace.",
                    ChangedFieldsJson = JsonSerializer.Serialize(new[] { "projectName", "description", "startupField", "technologyStack", "keywords" }),
                    OccurredAtUtc = now
                });

                _context.Projects.Add(project);
                ClassOutbox.Enqueue(_context, "ProjectWorkspace.Created.v1", team.ClassId, new
                {
                    ProjectId = project.Id,
                    TeamId = team.Id,
                    ClassId = team.ClassId,
                    SubjectId = team.Class.CourseId,
                    SemesterId = team.Class.SemesterId,
                    LeaderUserId = userId
                }, now);
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(MapProject(project, team));
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Failure<ProjectWorkspaceDto>(ErrorCodes.WorkspaceAlreadyExists, "This team already has an active project workspace.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure<ProjectWorkspaceDto>(ErrorCodes.WorkspaceConcurrencyConflict, "Workspace creation conflicted with another request. Refresh and try again.");
        }
    }

    public async Task<Result<ProjectWorkspaceDto>> UpdateAsync(
        Guid teamId,
        UpdateProjectWorkspaceRequest request,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!IsRole(role, SystemRoles.Student))
            return Failure<ProjectWorkspaceDto>(ErrorCodes.WorkspaceAccessDenied, "Only the active team leader can update the project profile.");

        var validation = Validate(request.ProjectName, request.Description, request.StartupField, request.TechnologyStack, request.Keywords);
        if (validation != null) return Result.Failure<ProjectWorkspaceDto>(validation);
        var technologies = NormalizeTags(request.TechnologyStack ?? Array.Empty<string>());
        var keywords = NormalizeTags(request.Keywords ?? Array.Empty<string>());

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var team = await TeamQuery(tracking: true)
                    .FirstOrDefaultAsync(item => item.Id == teamId, transactionCancellationToken);
                if (team == null)
                    return Failure<ProjectWorkspaceDto>(ErrorCodes.TeamNotFound, "The requested team was not found.");
                if (team.Status != TeamStatus.Active)
                    return Failure<ProjectWorkspaceDto>(ErrorCodes.TeamInactive, "An inactive team workspace cannot be updated.");
                var classError = ClassStateRules.GetMutationError(team.Class.Status);
                if (classError != null) return Result.Failure<ProjectWorkspaceDto>(classError);

                var leader = team.TeamMembers.SingleOrDefault(member =>
                    member.CountsTowardActiveTeam &&
                    member.RoleInTeam == TeamMemberRole.Leader &&
                    member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active);
                if (leader?.ClassStudent.Student.UserId != userId)
                    return Failure<ProjectWorkspaceDto>(ErrorCodes.WorkspaceLeaderRequired, "Only the active team leader can update this project profile.");
                if (team.Project == null)
                    return Failure<ProjectWorkspaceDto>(ErrorCodes.WorkspaceNotFound, "This team does not have a project workspace.");

                var project = team.Project;
                var nextName = (request.ProjectName ?? string.Empty).Trim();
                var nextDescription = (request.Description ?? string.Empty).Trim();
                var nextStartupField = (request.StartupField ?? string.Empty).Trim();
                var changedFields = new List<string>();
                if (!string.Equals(project.Name, nextName, StringComparison.Ordinal)) changedFields.Add("projectName");
                if (!string.Equals(project.Description ?? string.Empty, nextDescription, StringComparison.Ordinal)) changedFields.Add("description");
                if (!string.Equals(project.StartupField ?? string.Empty, nextStartupField, StringComparison.Ordinal)) changedFields.Add("startupField");
                if (!SameTags(project.ProjectTags, ProjectTagType.Technology, technologies)) changedFields.Add("technologyStack");
                if (!SameTags(project.ProjectTags, ProjectTagType.Keyword, keywords)) changedFields.Add("keywords");
                if (changedFields.Count == 0) return Result.Success(MapProject(project, team));

                project.Name = nextName;
                project.Description = nextDescription;
                project.StartupField = nextStartupField;
                project.Technology = string.Join(", ", technologies.Select(tag => tag.Display));
                project.UpdatedBy = userId;
                var now = DateTime.UtcNow;
                SyncTags(project, ProjectTagType.Technology, technologies, userId, now);
                SyncTags(project, ProjectTagType.Keyword, keywords, userId, now);
                var activity = new ProjectActivityLog
                {
                    ProjectId = project.Id,
                    Project = project,
                    ActorUserId = userId,
                    Action = "PROJECT_PROFILE_UPDATED",
                    Summary = $"Updated {string.Join(", ", changedFields.Select(ToDisplayField))}.",
                    ChangedFieldsJson = JsonSerializer.Serialize(changedFields),
                    OccurredAtUtc = now
                };
                _context.ProjectActivityLogs.Add(activity);
                project.ActivityLogs.Add(activity);
                ClassOutbox.Enqueue(_context, "ProjectWorkspace.ProfileUpdated.v1", team.ClassId, new
                {
                    ProjectId = project.Id,
                    TeamId = team.Id,
                    ChangedFields = changedFields,
                    UpdatedByUserId = userId
                }, now);
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(MapProject(project, team));
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Failure<ProjectWorkspaceDto>(ErrorCodes.WorkspaceConcurrencyConflict, "The project profile changed in another request. Refresh and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure<ProjectWorkspaceDto>(ErrorCodes.WorkspaceConcurrencyConflict, "The project profile changed in another request. Refresh and try again.");
        }
    }

    private IQueryable<Team>? AccessibleTeamQuery(Guid userId, string role)
    {
        var query = TeamQuery();
        if (IsRole(role, SystemRoles.Admin)) return query;
        if (IsRole(role, SystemRoles.Lecturer)) return query.Where(team => team.Class.PrimaryLecturerId == userId);
        if (IsRole(role, SystemRoles.Mentor)) return query.Where(team => team.MentorAssignments.Any(assignment =>
            assignment.MentorProfile.UserId == userId && assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null));
        if (IsRole(role, SystemRoles.Student)) return query.Where(team => team.TeamMembers.Any(member =>
            member.CountsTowardActiveTeam && member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active && member.ClassStudent.Student.UserId == userId));
        return null;
    }

    private IQueryable<Team> TeamQuery(bool tracking = false)
    {
        var query = tracking ? _context.Teams.AsQueryable() : _context.Teams.AsNoTracking();
        return query
            .Include(team => team.Class).ThenInclude(targetClass => targetClass.Course)
            .Include(team => team.Class).ThenInclude(targetClass => targetClass.Semester)
            .Include(team => team.Class).ThenInclude(targetClass => targetClass.PrimaryLecturer)
            .Include(team => team.TeamMembers).ThenInclude(member => member.ClassStudent).ThenInclude(enrollment => enrollment.Student)
            .Include(team => team.Project).ThenInclude(project => project!.ProjectTags)
            .Include(team => team.Project).ThenInclude(project => project!.ActivityLogs).ThenInclude(activity => activity.ActorUser)
            .Include(team => team.MentorAssignments).ThenInclude(assignment => assignment.MentorProfile).ThenInclude(profile => profile.User);
    }

    private static Error? Validate(
        string? projectName,
        string? description,
        string? startupField,
        IReadOnlyCollection<string>? technologyStack,
        IReadOnlyCollection<string>? keywords)
    {
        if ((projectName ?? string.Empty).Trim().Length is < 3 or > 200)
            return new Error(ErrorCodes.WorkspaceValidationError, "Project name must be between 3 and 200 characters.");
        if ((description ?? string.Empty).Trim().Length is < 20 or > 2_000)
            return new Error(ErrorCodes.WorkspaceValidationError, "Project description must be between 20 and 2000 characters.");
        if ((startupField ?? string.Empty).Trim().Length is < 2 or > 100)
            return new Error(ErrorCodes.WorkspaceValidationError, "Startup field must be between 2 and 100 characters.");
        var technologies = technologyStack ?? Array.Empty<string>();
        var technologyError = ValidateTags(technologies, "technology");
        if (technologyError != null) return technologyError;
        if (technologies.Count == 0)
            return new Error(ErrorCodes.WorkspaceValidationError, "At least one technology is required.");
        return ValidateTags(keywords ?? Array.Empty<string>(), "keyword");
    }

    private static Error? ValidateTags(IReadOnlyCollection<string> values, string label)
    {
        if (values.Count > MaximumTagsPerType)
            return new Error(ErrorCodes.WorkspaceTagInvalid, $"A maximum of {MaximumTagsPerType} {label} tags is allowed.");
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length is < 1 or > 50 || !TagPattern.IsMatch(trimmed))
                return new Error(ErrorCodes.WorkspaceTagInvalid, $"Each {label} tag must be 1-50 characters and contain only letters, numbers, spaces, or . + # & / _ -.");
            var key = NormalizeTag(trimmed);
            if (!normalized.Add(key))
                return new Error(ErrorCodes.WorkspaceTagDuplicated, $"Duplicate {label} tag '{trimmed}' is not allowed.");
        }
        return null;
    }

    private static (string Display, string Normalized)[] NormalizeTags(IEnumerable<string> values) => values
        .Select(value => (value ?? string.Empty).Trim())
        .Select(value => (Display: value, Normalized: NormalizeTag(value)))
        .ToArray();

    private static string NormalizeTag(string value) => Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();

    private static bool SameTags(
        IEnumerable<ProjectTag> current,
        ProjectTagType type,
        IEnumerable<(string Display, string Normalized)> next) =>
        current.Where(tag => tag.TagType == type).Select(tag => tag.NormalizedTagName).OrderBy(value => value)
            .SequenceEqual(next.Select(tag => tag.Normalized).OrderBy(value => value), StringComparer.Ordinal);

    private static string ToDisplayField(string field) => field switch
    {
        "projectName" => "project name",
        "startupField" => "startup field",
        "technologyStack" => "technology stack",
        _ => field
    };

    private static ProjectTag CreateTag(Project project, (string Display, string Normalized) tag, ProjectTagType type, Guid userId, DateTime now) => new()
    {
        ProjectId = project.Id,
        Project = project,
        TagName = tag.Display,
        NormalizedTagName = tag.Normalized,
        TagType = type,
        CreatedById = userId,
        CreatedAt = now
    };

    private void SyncTags(
        Project project,
        ProjectTagType type,
        IReadOnlyCollection<(string Display, string Normalized)> next,
        Guid userId,
        DateTime now)
    {
        var nextByKey = next.ToDictionary(tag => tag.Normalized, StringComparer.Ordinal);
        var current = project.ProjectTags.Where(tag => tag.TagType == type).ToArray();
        foreach (var tag in current.Where(tag => !nextByKey.ContainsKey(tag.NormalizedTagName)))
        {
            _context.ProjectTags.Remove(tag);
            project.ProjectTags.Remove(tag);
        }
        var currentKeys = current.Select(tag => tag.NormalizedTagName).ToHashSet(StringComparer.Ordinal);
        foreach (var tag in next.Where(tag => !currentKeys.Contains(tag.Normalized)))
        {
            var created = CreateTag(project, tag, type, userId, now);
            _context.ProjectTags.Add(created);
            project.ProjectTags.Add(created);
        }
    }

    private static WorkspaceOptionDto MapOption(Team team)
    {
        var readOnly = team.Class.Status is ClassStatus.Completed or ClassStatus.Archived;
        return new WorkspaceOptionDto
        {
            TeamId = team.Id,
            TeamName = team.TeamName,
            ClassId = team.ClassId,
            ClassCode = team.Class.ClassCode,
            CourseCode = team.Class.Course.Code,
            Semester = team.Class.Semester.Code,
            AccessMode = readOnly ? "READ_ONLY" : "READ_WRITE",
            IsArchived = team.Class.Status == ClassStatus.Archived,
            IsCurrent = team.Class.Semester.Status == SemesterStatus.Active && !readOnly,
            HasWorkspace = team.Project != null
        };
    }

    private static ProjectWorkspaceDetailDto MapDetail(Team team)
    {
        var activeMentor = team.MentorAssignments.FirstOrDefault(assignment =>
            assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null);
        var leader = team.TeamMembers.FirstOrDefault(member =>
            member.CountsTowardActiveTeam && member.RoleInTeam == TeamMemberRole.Leader);
        return new ProjectWorkspaceDetailDto
        {
            Team = new WorkspaceTeamDto
            {
                Id = team.Id,
                TeamCode = team.TeamCode,
                TeamName = team.TeamName,
                Description = team.Description,
                LeaderId = leader?.StudentId,
                Status = team.Status.ToString()
            },
            Class = new WorkspaceClassDto
            {
                Id = team.ClassId,
                ClassCode = team.Class.ClassCode,
                SubjectId = team.Class.CourseId,
                SubjectCode = team.Class.Course.Code,
                SubjectName = team.Class.Course.Name,
                SemesterId = team.Class.SemesterId,
                SemesterCode = team.Class.Semester.Code
            },
            Members = team.TeamMembers.Where(member => member.CountsTowardActiveTeam).Select(member => new WorkspaceMemberDto
            {
                StudentId = member.StudentId,
                UserId = member.ClassStudent.Student.UserId,
                FullName = member.ClassStudent.Student.FullName,
                Email = member.ClassStudent.Student.Email,
                RollNumber = member.ClassStudent.Student.RollNumber ?? string.Empty,
                MajorCode = member.ClassStudent.MajorCodeAtEnrollment,
                RoleInTeam = member.RoleInTeam.ToString()
            }).ToArray(),
            Lecturer = team.Class.PrimaryLecturer == null ? null : new WorkspacePersonDto
            {
                Id = team.Class.PrimaryLecturer.Id,
                Name = team.Class.PrimaryLecturer.FullName,
                Email = team.Class.PrimaryLecturer.Email
            },
            Mentor = activeMentor == null ? null : new WorkspacePersonDto
            {
                Id = activeMentor.MentorProfile.UserId,
                Name = activeMentor.MentorProfile.User.FullName,
                Email = activeMentor.MentorProfile.User.Email
            },
            Project = team.Project == null ? null : MapProject(team.Project, team),
            Activities = team.Project?.ActivityLogs
                .OrderByDescending(activity => activity.OccurredAtUtc)
                .Select(MapActivity)
                .ToArray() ?? Array.Empty<ProjectActivityDto>()
        };
    }

    private static ProjectWorkspaceDto MapProject(Project project, Team team) => new()
    {
        Id = project.Id,
        TeamId = team.Id,
        ClassId = team.ClassId,
        SubjectId = team.Class.CourseId,
        SemesterId = team.Class.SemesterId,
        ProjectName = project.Name,
        Description = project.Description ?? string.Empty,
        StartupField = project.StartupField ?? string.Empty,
        TechnologyStack = project.ProjectTags.Where(tag => tag.TagType == ProjectTagType.Technology).Select(tag => tag.TagName).ToArray(),
        Keywords = project.ProjectTags.Where(tag => tag.TagType == ProjectTagType.Keyword).Select(tag => tag.TagName).ToArray(),
        Status = project.Status.ToString(),
        CreatedAtUtc = project.CreatedAt,
        UpdatedAtUtc = project.UpdatedAt
    };

    private static ProjectActivityDto MapActivity(ProjectActivityLog activity) => new()
    {
        Id = activity.Id,
        Action = activity.Action,
        Summary = activity.Summary,
        ActorUserId = activity.ActorUserId,
        ActorName = activity.ActorUser?.FullName ?? "System",
        ChangedFields = DeserializeChangedFields(activity.ChangedFieldsJson),
        OccurredAtUtc = activity.OccurredAtUtc
    };

    private static IReadOnlyCollection<string> DeserializeChangedFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
    private static Result<T> Failure<T>(string code, string message) => Result.Failure<T>(new Error(code, message));
    private static Result<IReadOnlyCollection<WorkspaceOptionDto>> FailureList(string code, string message) =>
        Result.Failure<IReadOnlyCollection<WorkspaceOptionDto>>(new Error(code, message));
}
