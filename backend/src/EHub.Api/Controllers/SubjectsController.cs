using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Common;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/subjects")]
[Authorize(Policy = SystemPolicies.StaffOnly)]
public sealed class SubjectsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SubjectsController(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetSubjects(
        [FromQuery] string? search,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (!TryParseSubjectStatus(status, out var subjectStatus))
        {
            return BadRequest(ApiResponse<object>.FailureResponse(
                "Status must be active or disabled.", ErrorCodes.CommonValidationError));
        }

        var query = _context.Courses.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = search.Trim();
            query = query.Where(course => course.Code.Contains(pattern) || course.Name.Contains(pattern));
        }
        if (subjectStatus is not null)
        {
            query = query.Where(course => course.Status == subjectStatus.Value);
        }

        var subjects = await query
            .OrderBy(course => course.Code)
            .Select(course => new SubjectResponse
            {
                Id = course.Id,
                SubjectCode = course.Code,
                SubjectName = course.Name,
                Status = course.Status == CourseStatus.Active ? "active" : "disabled",
            })
            .ToArrayAsync(cancellationToken);

        return Ok(ApiResponse<SubjectListResponse>.SuccessResponse(
            new SubjectListResponse { Subjects = subjects },
            "Subjects retrieved successfully."));
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveSubjects(CancellationToken cancellationToken)
    {
        var subjects = await _context.Courses.AsNoTracking()
            .Where(course => course.Status == CourseStatus.Active)
            .OrderBy(course => course.Code)
            .Select(course => new SubjectResponse
            {
                Id = course.Id,
                SubjectCode = course.Code,
                SubjectName = course.Name,
                Status = "active",
            })
            .ToArrayAsync(cancellationToken);

        return Ok(ApiResponse<SubjectListResponse>.SuccessResponse(
            new SubjectListResponse { Subjects = subjects },
            "Active subjects retrieved successfully."));
    }

    [HttpPost]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> CreateSubject(
        [FromBody] CreateSubjectRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.SubjectCode.Trim().ToUpperInvariant();
        if (await _context.Courses.AnyAsync(course => course.Code == code, cancellationToken))
        {
            return Conflict(ApiResponse<object>.FailureResponse(
                "Subject code already exists.", ErrorCodes.CommonConflictError));
        }

        var subject = new Course
        {
            Code = code,
            Name = request.SubjectName.Trim(),
            Status = ToCourseStatus(request.Status),
            CreatedBy = _currentUser.UserId,
        };
        await _context.Courses.AddAsync(subject, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetSubjects), ApiResponse<SubjectResponse>.SuccessResponse(
            ToSubjectResponse(subject), "Subject created successfully."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> UpdateSubject(
        Guid id,
        [FromBody] UpdateSubjectRequest request,
        CancellationToken cancellationToken)
    {
        var subject = await _context.Courses.FirstOrDefaultAsync(course => course.Id == id, cancellationToken);
        if (subject is null)
        {
            return NotFound(ApiResponse<object>.FailureResponse(
                "Subject was not found.", ErrorCodes.CommonNotFoundError));
        }

        subject.Name = request.SubjectName.Trim();
        subject.Status = ToCourseStatus(request.Status);
        subject.UpdatedBy = _currentUser.UserId;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<SubjectResponse>.SuccessResponse(
            ToSubjectResponse(subject), "Subject updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> DisableSubject(Guid id, CancellationToken cancellationToken)
    {
        var subject = await _context.Courses.FirstOrDefaultAsync(course => course.Id == id, cancellationToken);
        if (subject is null)
        {
            return NotFound(ApiResponse<object>.FailureResponse(
                "Subject was not found.", ErrorCodes.CommonNotFoundError));
        }

        subject.Status = CourseStatus.Inactive;
        subject.UpdatedBy = _currentUser.UserId;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<SubjectResponse>.SuccessResponse(
            ToSubjectResponse(subject), "Subject disabled successfully."));
    }

    [HttpGet("current-semester")]
    public async Task<IActionResult> GetCurrentSemester(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var current = await _context.Semesters.AsNoTracking()
            .Where(semester => semester.Status == SemesterStatus.Active)
            .OrderByDescending(semester => semester.Year)
            .ThenBy(semester => semester.Term)
            .FirstOrDefaultAsync(cancellationToken);

        var years = await _context.Semesters.AsNoTracking()
            .Select(semester => semester.Year)
            .Distinct()
            .OrderByDescending(year => year)
            .ToListAsync(cancellationToken);
        if (!years.Contains(now.Year)) years.Add(now.Year);
        if (now.Month == 12 && !years.Contains(now.Year + 1)) years.Add(now.Year + 1);

        var fallback = new SemesterResponse { Semester = "SP", Year = now.Year };
        return Ok(ApiResponse<CurrentSemesterResponse>.SuccessResponse(
            new CurrentSemesterResponse
            {
                CurrentSemester = current is null ? fallback : ToSemesterResponse(current),
                AvailableYears = years.OrderByDescending(year => year).ToArray(),
                IsDecember = now.Month == 12,
            },
            "Current semester retrieved successfully."));
    }

    [HttpPost("current-semester")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> SetCurrentSemester(
        [FromBody] SetCurrentSemesterRequest request,
        CancellationToken cancellationToken)
    {
        var term = ToSemesterTerm(request.Semester);
        var now = DateTime.UtcNow;
        if (request.Year > now.Year && !(now.Month == 12 && request.Year == now.Year + 1))
        {
            return BadRequest(ApiResponse<object>.FailureResponse(
                "Next-year planning is only available in December.", ErrorCodes.CommonBusinessRuleViolation));
        }

        var semester = await _context.Semesters.FirstOrDefaultAsync(
            item => item.Term == term && item.Year == request.Year,
            cancellationToken);
        if (semester is null)
        {
            semester = new Semester
            {
                Code = $"{request.Semester.Trim().ToUpperInvariant()}{request.Year}",
                Name = $"{TermName(term)} {request.Year}",
                Term = term,
                Year = request.Year,
                Status = SemesterStatus.Planned,
                CreatedBy = _currentUser.UserId,
            };
            await _context.Semesters.AddAsync(semester, cancellationToken);
        }

        var activeSemesters = await _context.Semesters
            .Where(item => item.Status == SemesterStatus.Active && item.Id != semester.Id)
            .ToListAsync(cancellationToken);
        foreach (var activeSemester in activeSemesters)
        {
            activeSemester.Status = SemesterStatus.Planned;
            activeSemester.UpdatedBy = _currentUser.UserId;
        }

        semester.Status = SemesterStatus.Active;
        semester.UpdatedBy = _currentUser.UserId;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<CurrentSemesterResponse>.SuccessResponse(
            new CurrentSemesterResponse
            {
                CurrentSemester = ToSemesterResponse(semester),
                AvailableYears = await GetAvailableYearsAsync(now, cancellationToken),
                IsDecember = now.Month == 12,
            },
            "Active semester updated successfully."));
    }

    [HttpGet("teaching-staff")]
    public async Task<IActionResult> GetTeachingStaff(
        [FromQuery] string semester,
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        if (!TryParseSemesterTerm(semester, out var term) || year is < 2000 or > 2100)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(
                "Semester and year are invalid.", ErrorCodes.CommonValidationError));
        }

        var staff = await _context.Users.AsNoTracking()
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .Where(user => user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Lecturer || userRole.Role.Name == SystemRoles.Mentor))
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);

        var lecturerAssignments = await _context.ClassLecturers.AsNoTracking()
            .Where(assignment => assignment.Class.Semester.Term == term && assignment.Class.Semester.Year == year)
            .Select(assignment => new StaffAssignment(
                assignment.LecturerId,
                assignment.ClassId,
                assignment.Class.ClassCode,
                assignment.Class.Course.Code))
            .ToListAsync(cancellationToken);

        var mentorAssignments = await _context.MentorAssignments.AsNoTracking()
            .Where(assignment => assignment.Status == MentorAssignmentStatus.Active &&
                assignment.Team.Class.Semester.Term == term && assignment.Team.Class.Semester.Year == year)
            .Select(assignment => new StaffAssignment(
                assignment.MentorProfile.UserId,
                assignment.Team.ClassId,
                assignment.Team.Class.ClassCode,
                assignment.Team.Class.Course.Code))
            .ToListAsync(cancellationToken);

        var assignments = lecturerAssignments.Concat(mentorAssignments)
            .GroupBy(item => new { item.UserId, item.ClassId })
            .Select(group => group.First())
            .ToLookup(item => item.UserId);
        var response = staff.Select(user =>
        {
            var role = user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Lecturer)
                ? "LECTURER"
                : "MENTOR";
            var memberAssignments = assignments[user.Id]
                .Select(item => new TeachingAssignmentResponse
                {
                    Id = $"{item.UserId:N}-{item.ClassId:N}",
                    ClassCode = item.ClassCode,
                    SubjectCode = item.SubjectCode,
                })
                .ToArray();
            return new TeachingStaffResponse
            {
                Id = user.Id,
                Name = user.FullName,
                Email = user.Email,
                Avatar = user.AvatarUrl,
                Role = role,
                Status = user.Status.ToString(),
                ClassCount = memberAssignments.Length,
                Assignments = memberAssignments,
            };
        }).ToArray();

        var distinctClasses = assignments.SelectMany(group => group).Select(item => item.ClassId).Distinct().Count();
        return Ok(ApiResponse<TeachingStaffListResponse>.SuccessResponse(
            new TeachingStaffListResponse
            {
                Staff = response,
                Summary = new TeachingStaffSummaryResponse
                {
                    Lecturers = response.Count(item => item.Role == "LECTURER"),
                    Mentors = response.Count(item => item.Role == "MENTOR"),
                    Assigned = response.Count(item => item.ClassCount > 0),
                    Unassigned = response.Count(item => item.ClassCount == 0),
                    Classes = distinctClasses,
                },
            },
            "Teaching staff retrieved successfully."));
    }

    [HttpGet("{subjectCode}")]
    public async Task<IActionResult> GetCurriculum(string subjectCode, CancellationToken cancellationToken)
    {
        var code = subjectCode.Trim().ToUpperInvariant();
        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
        if (course is null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Subject was not found.", ErrorCodes.CommonNotFoundError));
        }

        var roadmapItems = await _context.WeeklyTasks.AsNoTracking()
            .Where(item => item.CourseId == course.Id && item.Scope == WeeklyTaskScope.Course && item.IsTemplate)
            .OrderBy(item => item.WeekNumber).ThenBy(item => item.Title)
            .Select(item => new RoadmapItemResponse
            {
                Id = item.Id, Title = item.Title, Description = item.Description, CourseCode = code,
                WeekNumber = item.WeekNumber, Priority = item.Priority.ToString().ToUpper(),
                EstimatedHours = item.EstimatedHours, Tags = item.Tags,
            }).ToArrayAsync(cancellationToken);
        var rubrics = await _context.Rubrics.AsNoTracking()
            .Include(item => item.Criteria)
            .Include(item => item.Checkpoint)
            .Where(item => item.CourseId == course.Id && item.ClassId == null)
            .OrderBy(item => item.Checkpoint!.CheckpointNumber).ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var checkpoints = await _context.Checkpoints.AsNoTracking()
            .Include(item => item.Rubrics).ThenInclude(item => item.Criteria)
            .Where(item => item.CourseId == course.Id && item.ClassId == null)
            .OrderBy(item => item.CheckpointNumber).ToListAsync(cancellationToken);

        return Ok(ApiResponse<SubjectCurriculumResponse>.SuccessResponse(new SubjectCurriculumResponse
        {
            Subject = ToSubjectResponse(course),
            RoadmapItems = roadmapItems,
            Rubrics = rubrics.Select(rubric => new CourseRubricResponse
            {
                Id = rubric.Id, Name = rubric.Name, Description = rubric.Description,
                Status = rubric.Status.ToString(), TotalWeight = rubric.TotalWeight,
                CheckpointNumber = rubric.Checkpoint?.CheckpointNumber,
                Criteria = rubric.Criteria.OrderBy(criterion => criterion.DisplayOrder).Select(criterion => new RubricCriterionResponse
                {
                    Id = criterion.Id, Name = criterion.Name, Description = criterion.Description,
                    MaxScore = criterion.MaxScore, Weight = criterion.Weight, DisplayOrder = criterion.DisplayOrder,
                }).ToArray(),
            }).ToArray(),
            Checkpoints = checkpoints.Select(ToCheckpointResponse).ToArray(),
        }, "Subject curriculum retrieved successfully."));
    }

    [HttpGet("{subjectCode}/curriculum")]
    public Task<IActionResult> GetCurriculumByRoute(string subjectCode, CancellationToken cancellationToken) =>
        GetCurriculum(subjectCode, cancellationToken);

    [HttpPut("{subjectCode}/checkpoints")]
    public async Task<IActionResult> SynchronizeCheckpoints(string subjectCode, [FromBody] SaveSubjectCheckpointsRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateCheckpoints(request.Checkpoints);
        if (validationError is not null) return BadRequest(ApiResponse<object>.FailureResponse(validationError, ErrorCodes.CommonValidationError));
        var course = await FindCourseAsync(subjectCode, cancellationToken);
        if (course is null) return NotFound(ApiResponse<object>.FailureResponse("Subject was not found.", ErrorCodes.CommonNotFoundError));
        var existing = await _context.Checkpoints.Include(item => item.Rubrics).ThenInclude(item => item.Criteria)
            .Where(item => item.CourseId == course.Id && item.ClassId == null).ToListAsync(cancellationToken);
        var retainedNumbers = request.Checkpoints.Select(item => item.Number).ToArray();
        var removedCheckpointIds = existing.Where(item => !retainedNumbers.Contains(item.CheckpointNumber)).Select(item => item.Id).ToArray();
        if (removedCheckpointIds.Length > 0)
        {
            var removedRubricIds = existing.Where(item => removedCheckpointIds.Contains(item.Id)).SelectMany(item => item.Rubrics).Select(item => item.Id).ToArray();
            if (removedRubricIds.Length > 0)
            {
                await _context.RubricCriteria.Where(item => removedRubricIds.Contains(item.RubricId)).ExecuteDeleteAsync(cancellationToken);
                await _context.Rubrics.Where(item => removedRubricIds.Contains(item.Id)).ExecuteDeleteAsync(cancellationToken);
            }
            await _context.Checkpoints.Where(item => removedCheckpointIds.Contains(item.Id)).ExecuteDeleteAsync(cancellationToken);
        }
        foreach (var input in request.Checkpoints)
        {
            var checkpoint = existing.FirstOrDefault(item => item.CheckpointNumber == input.Number);
            if (checkpoint is null)
            {
                checkpoint = new Checkpoint { CourseId = course.Id, CheckpointNumber = input.Number, CreatedById = _currentUser.UserId, Status = CheckpointStatus.Draft };
                await _context.Checkpoints.AddAsync(checkpoint, cancellationToken);
            }
            checkpoint.Name = input.Title.Trim(); checkpoint.Description = input.ShortDescription?.Trim(); checkpoint.RequirementsJson = JsonSerializer.Serialize(input.Requirements.Select(item => item.Trim()).Where(item => item.Length > 0)); checkpoint.UpdatedBy = _currentUser.UserId;
            var rubric = checkpoint.Rubrics.FirstOrDefault(item => item.ClassId == null);
            if (rubric is null)
            {
                rubric = new Rubric { CourseId = course.Id, Checkpoint = checkpoint, Name = $"{course.Code} Checkpoint {input.Number}", Status = RubricStatus.Active, TotalWeight = 100, CreatedById = _currentUser.UserId };
                await _context.Rubrics.AddAsync(rubric, cancellationToken);
            }
            await _context.RubricCriteria.Where(item => item.RubricId == rubric.Id).ExecuteDeleteAsync(cancellationToken);
            foreach (var criterion in input.Rubrics.Select((value, index) => new { value, index }))
                await _context.RubricCriteria.AddAsync(new RubricCriterion { Rubric = rubric, Name = criterion.value.Label.Trim(), Key = criterion.value.Key.Trim(), Description = criterion.value.Description?.Trim(), Weight = criterion.value.Weight, MaxScore = 10, DisplayOrder = criterion.index + 1, LevelsJson = JsonSerializer.Serialize(criterion.value.Levels), CreatedBy = _currentUser.UserId }, cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);
        return await GetCurriculum(subjectCode, cancellationToken);
    }

    [HttpPost("{subjectCode}/roadmap")]
    public async Task<IActionResult> CreateRoadmapItem(string subjectCode, [FromBody] SaveRoadmapItemRequest request, CancellationToken cancellationToken)
    {
        var course = await FindCourseAsync(subjectCode, cancellationToken);
        if (course is null) return NotFound(ApiResponse<object>.FailureResponse("Subject was not found.", ErrorCodes.CommonNotFoundError));
        if (!request.CourseCode.Equals(course.Code, StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.FailureResponse("Course code does not match the requested subject.", ErrorCodes.CommonValidationError));

        var item = new WeeklyTask
        {
            Title = request.Title.Trim(), Description = request.Description?.Trim(), CourseId = course.Id,
            Scope = WeeklyTaskScope.Course, IsTemplate = true, WeekNumber = request.WeekNumber,
            Priority = ToPriority(request.Priority), EstimatedHours = request.EstimatedHours,
            Tags = NormalizeTags(request.Tags), CreatedById = _currentUser.UserId ?? Guid.Empty,
        };
        await _context.WeeklyTasks.AddAsync(item, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<RoadmapItemResponse>.SuccessResponse(ToRoadmapResponse(item, course.Code), "Roadmap item created successfully."));
    }

    [HttpPut("{subjectCode}/roadmap/{itemId:guid}")]
    public async Task<IActionResult> UpdateRoadmapItem(string subjectCode, Guid itemId, [FromBody] SaveRoadmapItemRequest request, CancellationToken cancellationToken)
    {
        var course = await FindCourseAsync(subjectCode, cancellationToken);
        if (course is null) return NotFound(ApiResponse<object>.FailureResponse("Subject was not found.", ErrorCodes.CommonNotFoundError));
        var item = await _context.WeeklyTasks.FirstOrDefaultAsync(task => task.Id == itemId && task.CourseId == course.Id && task.Scope == WeeklyTaskScope.Course && task.IsTemplate, cancellationToken);
        if (item is null) return NotFound(ApiResponse<object>.FailureResponse("Roadmap item was not found.", ErrorCodes.CommonNotFoundError));
        item.Title = request.Title.Trim(); item.Description = request.Description?.Trim(); item.WeekNumber = request.WeekNumber;
        item.Priority = ToPriority(request.Priority); item.EstimatedHours = request.EstimatedHours; item.Tags = NormalizeTags(request.Tags); item.UpdatedById = _currentUser.UserId;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<RoadmapItemResponse>.SuccessResponse(ToRoadmapResponse(item, course.Code), "Roadmap item updated successfully."));
    }

    [HttpDelete("{subjectCode}/roadmap/{itemId:guid}")]
    public async Task<IActionResult> DeleteRoadmapItem(string subjectCode, Guid itemId, CancellationToken cancellationToken)
    {
        var course = await FindCourseAsync(subjectCode, cancellationToken);
        if (course is null) return NotFound(ApiResponse<object>.FailureResponse("Subject was not found.", ErrorCodes.CommonNotFoundError));
        var item = await _context.WeeklyTasks.FirstOrDefaultAsync(task => task.Id == itemId && task.CourseId == course.Id && task.Scope == WeeklyTaskScope.Course && task.IsTemplate, cancellationToken);
        if (item is null) return NotFound(ApiResponse<object>.FailureResponse("Roadmap item was not found.", ErrorCodes.CommonNotFoundError));
        _context.WeeklyTasks.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResponse(null, "Roadmap item deleted successfully."));
    }

    [HttpPost("{subjectCode}/rubrics")]
    public async Task<IActionResult> CreateRubric(string subjectCode, [FromBody] SaveRubricRequest request, CancellationToken cancellationToken)
    {
        var course = await FindCourseAsync(subjectCode, cancellationToken);
        if (course is null) return NotFound(ApiResponse<object>.FailureResponse("Subject was not found.", ErrorCodes.CommonNotFoundError));
        var checkpoint = await FindCheckpointAsync(course.Id, request.CheckpointNumber, cancellationToken);
        if (request.CheckpointNumber.HasValue && checkpoint is null) return BadRequest(ApiResponse<object>.FailureResponse("Checkpoint was not found for this subject.", ErrorCodes.CommonValidationError));
        if (ToRubricStatus(request.Status) == RubricStatus.Active) return BadRequest(ApiResponse<object>.FailureResponse("Add criteria before activating a rubric.", ErrorCodes.CommonBusinessRuleViolation));
        var rubric = new Rubric { Name = request.Name.Trim(), Description = request.Description?.Trim(), CourseId = course.Id, CheckpointId = checkpoint?.Id, TotalWeight = request.TotalWeight, Status = ToRubricStatus(request.Status), CreatedById = _currentUser.UserId };
        await _context.Rubrics.AddAsync(rubric, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<CourseRubricResponse>.SuccessResponse(ToRubricResponse(rubric, checkpoint), "Rubric created successfully."));
    }

    [HttpPut("{subjectCode}/rubrics/{rubricId:guid}")]
    public async Task<IActionResult> UpdateRubric(string subjectCode, Guid rubricId, [FromBody] SaveRubricRequest request, CancellationToken cancellationToken)
    {
        var course = await FindCourseAsync(subjectCode, cancellationToken);
        if (course is null) return NotFound(ApiResponse<object>.FailureResponse("Subject was not found.", ErrorCodes.CommonNotFoundError));
        var rubric = await _context.Rubrics.Include(item => item.Criteria).FirstOrDefaultAsync(item => item.Id == rubricId && item.CourseId == course.Id && item.ClassId == null, cancellationToken);
        if (rubric is null) return NotFound(ApiResponse<object>.FailureResponse("Rubric was not found.", ErrorCodes.CommonNotFoundError));
        var checkpoint = await FindCheckpointAsync(course.Id, request.CheckpointNumber, cancellationToken);
        if (request.CheckpointNumber.HasValue && checkpoint is null) return BadRequest(ApiResponse<object>.FailureResponse("Checkpoint was not found for this subject.", ErrorCodes.CommonValidationError));
        if (ToRubricStatus(request.Status) == RubricStatus.Active && rubric.Criteria.Sum(item => item.Weight) != 100) return BadRequest(ApiResponse<object>.FailureResponse("Active rubrics must have criteria totaling 100%.", ErrorCodes.CommonBusinessRuleViolation));
        rubric.Name = request.Name.Trim(); rubric.Description = request.Description?.Trim(); rubric.CheckpointId = checkpoint?.Id; rubric.TotalWeight = request.TotalWeight; rubric.Status = ToRubricStatus(request.Status); rubric.UpdatedBy = _currentUser.UserId;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<CourseRubricResponse>.SuccessResponse(ToRubricResponse(rubric, checkpoint), "Rubric updated successfully."));
    }

    [HttpDelete("{subjectCode}/rubrics/{rubricId:guid}")]
    public async Task<IActionResult> DeleteRubric(string subjectCode, Guid rubricId, CancellationToken cancellationToken)
    {
        var course = await FindCourseAsync(subjectCode, cancellationToken);
        if (course is null) return NotFound(ApiResponse<object>.FailureResponse("Subject was not found.", ErrorCodes.CommonNotFoundError));
        var rubric = await _context.Rubrics.FirstOrDefaultAsync(item => item.Id == rubricId && item.CourseId == course.Id && item.ClassId == null, cancellationToken);
        if (rubric is null) return NotFound(ApiResponse<object>.FailureResponse("Rubric was not found.", ErrorCodes.CommonNotFoundError));
        _context.Rubrics.Remove(rubric); await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResponse(null, "Rubric deleted successfully."));
    }

    [HttpPost("{subjectCode}/rubrics/{rubricId:guid}/criteria")]
    public async Task<IActionResult> CreateCriterion(string subjectCode, Guid rubricId, [FromBody] SaveRubricCriterionRequest request, CancellationToken cancellationToken)
    {
        var rubric = await FindRubricAsync(subjectCode, rubricId, cancellationToken);
        if (rubric is null) return NotFound(ApiResponse<object>.FailureResponse("Rubric was not found.", ErrorCodes.CommonNotFoundError));
        var criterion = new RubricCriterion { RubricId = rubric.Id, Name = request.Name.Trim(), Description = request.Description?.Trim(), MaxScore = request.MaxScore, Weight = request.Weight, DisplayOrder = request.DisplayOrder };
        await _context.RubricCriteria.AddAsync(criterion, cancellationToken); await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<RubricCriterionResponse>.SuccessResponse(ToCriterionResponse(criterion), "Criterion created successfully."));
    }

    [HttpPut("{subjectCode}/rubrics/{rubricId:guid}/criteria/{criterionId:guid}")]
    public async Task<IActionResult> UpdateCriterion(string subjectCode, Guid rubricId, Guid criterionId, [FromBody] SaveRubricCriterionRequest request, CancellationToken cancellationToken)
    {
        var rubric = await FindRubricAsync(subjectCode, rubricId, cancellationToken);
        if (rubric is null) return NotFound(ApiResponse<object>.FailureResponse("Rubric was not found.", ErrorCodes.CommonNotFoundError));
        var criterion = await _context.RubricCriteria.FirstOrDefaultAsync(item => item.Id == criterionId && item.RubricId == rubric.Id, cancellationToken);
        if (criterion is null) return NotFound(ApiResponse<object>.FailureResponse("Criterion was not found.", ErrorCodes.CommonNotFoundError));
        criterion.Name = request.Name.Trim(); criterion.Description = request.Description?.Trim(); criterion.MaxScore = request.MaxScore; criterion.Weight = request.Weight; criterion.DisplayOrder = request.DisplayOrder; criterion.UpdatedBy = _currentUser.UserId;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<RubricCriterionResponse>.SuccessResponse(ToCriterionResponse(criterion), "Criterion updated successfully."));
    }

    [HttpDelete("{subjectCode}/rubrics/{rubricId:guid}/criteria/{criterionId:guid}")]
    public async Task<IActionResult> DeleteCriterion(string subjectCode, Guid rubricId, Guid criterionId, CancellationToken cancellationToken)
    {
        var rubric = await FindRubricAsync(subjectCode, rubricId, cancellationToken);
        if (rubric is null) return NotFound(ApiResponse<object>.FailureResponse("Rubric was not found.", ErrorCodes.CommonNotFoundError));
        var criterion = await _context.RubricCriteria.FirstOrDefaultAsync(item => item.Id == criterionId && item.RubricId == rubric.Id, cancellationToken);
        if (criterion is null) return NotFound(ApiResponse<object>.FailureResponse("Criterion was not found.", ErrorCodes.CommonNotFoundError));
        _context.RubricCriteria.Remove(criterion); await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResponse(null, "Criterion deleted successfully."));
    }

    private async Task<int[]> GetAvailableYearsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var years = await _context.Semesters.AsNoTracking().Select(item => item.Year).Distinct().ToListAsync(cancellationToken);
        if (!years.Contains(now.Year)) years.Add(now.Year);
        if (now.Month == 12 && !years.Contains(now.Year + 1)) years.Add(now.Year + 1);
        return years.OrderByDescending(year => year).ToArray();
    }

    private Task<Course?> FindCourseAsync(string subjectCode, CancellationToken cancellationToken) =>
        _context.Courses.FirstOrDefaultAsync(course => course.Code == subjectCode.Trim().ToUpperInvariant(), cancellationToken);

    private Task<Checkpoint?> FindCheckpointAsync(Guid courseId, int? checkpointNumber, CancellationToken cancellationToken) =>
        checkpointNumber.HasValue ? _context.Checkpoints.FirstOrDefaultAsync(item => item.CourseId == courseId && item.CheckpointNumber == checkpointNumber.Value && item.ClassId == null, cancellationToken) : Task.FromResult<Checkpoint?>(null);

    private async Task<Rubric?> FindRubricAsync(string subjectCode, Guid rubricId, CancellationToken cancellationToken)
    {
        var course = await FindCourseAsync(subjectCode, cancellationToken);
        return course is null ? null : await _context.Rubrics.FirstOrDefaultAsync(item => item.Id == rubricId && item.CourseId == course.Id && item.ClassId == null, cancellationToken);
    }

    private static CourseRubricResponse ToRubricResponse(Rubric rubric, Checkpoint? checkpoint) => new()
    {
        Id = rubric.Id, Name = rubric.Name, Description = rubric.Description, Status = rubric.Status.ToString(), TotalWeight = rubric.TotalWeight, CheckpointNumber = checkpoint?.CheckpointNumber,
        Criteria = rubric.Criteria.OrderBy(item => item.DisplayOrder).Select(ToCriterionResponse).ToArray(),
    };

    private static RubricCriterionResponse ToCriterionResponse(RubricCriterion criterion) => new() { Id = criterion.Id, Name = criterion.Name, Description = criterion.Description, MaxScore = criterion.MaxScore, Weight = criterion.Weight, DisplayOrder = criterion.DisplayOrder };

    private static RubricStatus ToRubricStatus(string status) => status.ToUpperInvariant() switch { "ACTIVE" => RubricStatus.Active, "ARCHIVED" => RubricStatus.Archived, _ => RubricStatus.Draft };

    private static RoadmapItemResponse ToRoadmapResponse(WeeklyTask item, string courseCode) => new()
    {
        Id = item.Id, Title = item.Title, Description = item.Description, CourseCode = courseCode,
        WeekNumber = item.WeekNumber, Priority = item.Priority.ToString().ToUpper(),
        EstimatedHours = item.EstimatedHours, Tags = item.Tags,
    };

    private static string[] NormalizeTags(IEnumerable<string> tags) => tags
        .Select(tag => tag?.Trim()).Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()!;

    private static SubjectCheckpointResponse ToCheckpointResponse(Checkpoint checkpoint)
    {
        var rubric = checkpoint.Rubrics.FirstOrDefault(item => item.ClassId == null);
        return new SubjectCheckpointResponse
        {
            Number = checkpoint.CheckpointNumber, Title = checkpoint.Name, ShortDescription = checkpoint.Description,
            Requirements = JsonSerializer.Deserialize<string[]>(checkpoint.RequirementsJson) ?? Array.Empty<string>(),
            Rubrics = rubric?.Criteria.OrderBy(item => item.DisplayOrder).Select(item => new SubjectCriterionResponse
            {
                Key = string.IsNullOrWhiteSpace(item.Key) ? item.Name : item.Key, Label = item.Name, Description = item.Description, Weight = item.Weight,
                Levels = JsonSerializer.Deserialize<object[]>(item.LevelsJson) ?? Array.Empty<object>(),
            }).ToArray() ?? Array.Empty<SubjectCriterionResponse>(),
        };
    }

    private static string? ValidateCheckpoints(IEnumerable<SubjectCheckpointRequest> checkpoints)
    {
        var values = checkpoints.ToArray();
        if (values.Length == 0) return "At least one checkpoint is required.";
        if (values.Select(item => item.Number).Distinct().Count() != values.Length || values.Any(item => item.Number is < 1 or > 10)) return "Checkpoint numbers must be unique and range from 1 to 10.";
        foreach (var checkpoint in values)
        {
            if (string.IsNullOrWhiteSpace(checkpoint.Title)) return $"Checkpoint {checkpoint.Number} title is required.";
            if (checkpoint.Rubrics.Count == 0) return $"Checkpoint {checkpoint.Number} needs at least one rubric criterion.";
            if (checkpoint.Rubrics.Any(item => string.IsNullOrWhiteSpace(item.Key) || !System.Text.RegularExpressions.Regex.IsMatch(item.Key, "^[A-Za-z][A-Za-z0-9_-]*$"))) return $"Checkpoint {checkpoint.Number} contains an invalid rubric key.";
            if (checkpoint.Rubrics.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) return $"Checkpoint {checkpoint.Number} contains duplicate rubric keys.";
            if (checkpoint.Rubrics.Any(item => string.IsNullOrWhiteSpace(item.Label) || item.Weight <= 0 || item.Weight > 100)) return $"Checkpoint {checkpoint.Number} contains an invalid rubric criterion.";
            if (checkpoint.Rubrics.Sum(item => item.Weight) != 100) return $"Checkpoint {checkpoint.Number} rubric weights must total 100%.";
        }
        return null;
    }

    private static TaskPriority ToPriority(string priority) => priority.ToUpperInvariant() switch
    {
        "LOW" => TaskPriority.Low, "HIGH" => TaskPriority.High, "CRITICAL" => TaskPriority.Critical, _ => TaskPriority.Medium,
    };

    private static SubjectResponse ToSubjectResponse(Course course) => new()
    {
        Id = course.Id,
        SubjectCode = course.Code,
        SubjectName = course.Name,
        Status = course.Status == CourseStatus.Active ? "active" : "disabled",
    };

    private static SemesterResponse ToSemesterResponse(Semester semester) => new()
    {
        Semester = semester.Term switch
        {
            SemesterTerm.Spring => "SP",
            SemesterTerm.Summer => "SU",
            SemesterTerm.Fall => "FA",
            _ => throw new ArgumentOutOfRangeException(),
        },
        Year = semester.Year,
    };

    private static bool TryParseSubjectStatus(string? status, out CourseStatus? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(status)) return true;
        if (status.Equals("active", StringComparison.OrdinalIgnoreCase)) { result = CourseStatus.Active; return true; }
        if (status.Equals("disabled", StringComparison.OrdinalIgnoreCase)) { result = CourseStatus.Inactive; return true; }
        return false;
    }

    private static CourseStatus ToCourseStatus(string status) =>
        status.Equals("active", StringComparison.OrdinalIgnoreCase) ? CourseStatus.Active : CourseStatus.Inactive;

    private static bool TryParseSemesterTerm(string value, out SemesterTerm result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }
        result = value.ToUpperInvariant() switch
        {
            "SP" => SemesterTerm.Spring,
            "SU" => SemesterTerm.Summer,
            "FA" => SemesterTerm.Fall,
            _ => default,
        };
        return value.Equals("SP", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("SU", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("FA", StringComparison.OrdinalIgnoreCase);
    }

    private static SemesterTerm ToSemesterTerm(string value)
    {
        TryParseSemesterTerm(value, out var term);
        return term;
    }

    private static string TermName(SemesterTerm term) => term switch
    {
        SemesterTerm.Spring => "Spring",
        SemesterTerm.Summer => "Summer",
        SemesterTerm.Fall => "Fall",
        _ => throw new ArgumentOutOfRangeException(nameof(term)),
    };

    private sealed record StaffAssignment(Guid UserId, Guid ClassId, string ClassCode, string SubjectCode);
}
