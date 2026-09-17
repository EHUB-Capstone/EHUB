using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.ExportClassRoster;

public sealed class ExportClassRosterQueryHandler : IExportClassRosterQueryHandler
{
    private readonly IApplicationDbContext _context;

    public ExportClassRosterQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> HandleAsync(
        Guid classId,
        ExportClassRosterRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<(byte[], string, string)>(
                new Error(ErrorCodes.ClassAccessDenied, "You do not have permission to export class roster."));
        }

        var targetClass = await _context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Semester)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);


        if (targetClass == null)
        {
            return Result.Failure<(byte[], string, string)>(
                new Error(ErrorCodes.ClassNotFound, "The requested class was not found."));
        }

        if (isLecturer)
        {
            if (targetClass.PrimaryLecturerId != currentUserId)
            {
                return Result.Failure<(byte[], string, string)>(
                    new Error(ErrorCodes.ClassAccessDenied, "You can only export roster for classes assigned to you."));
            }
        }

        var normalizedScope = request.Scope.Trim();
        if (!string.Equals(normalizedScope, "Active", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedScope, "History", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<(byte[], string, string)>(
                new Error(ErrorCodes.ClassValidationError, "Export scope must be Active or History."));
        }

        EnrollmentStatus? status;
        if (string.Equals(normalizedScope, "Active", StringComparison.OrdinalIgnoreCase))
        {
            status = EnrollmentStatus.Active;
            if (!string.IsNullOrWhiteSpace(request.Status) &&
                !string.Equals(request.Status, nameof(EnrollmentStatus.Active), StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<(byte[], string, string)>(
                    new Error(ErrorCodes.ClassValidationError, "Active export scope only accepts Active enrollment status."));
            }
        }
        else if (!ClassRosterFilters.TryParseStatus(request.Status, out status))
        {
            return Result.Failure<(byte[], string, string)>(
                new Error(ErrorCodes.ClassValidationError, "Enrollment status must be Active, Dropped, or Completed."));
        }

        var rosterQuery = _context.ClassStudents
            .AsNoTracking()
            .Include(cs => cs.Student)
            .Include(cs => cs.TeamMembers)
                .ThenInclude(tm => tm.Team)
                    .ThenInclude(t => t.Project)
            .Where(cs => cs.ClassId == classId);


        rosterQuery = ClassRosterFilters.Apply(rosterQuery, request.Search, request.MajorCode, status);

        var roster = await rosterQuery
            .OrderBy(cs => cs.Student.RollNumber)
            .ThenBy(cs => cs.Student.FullName)
            .ToListAsync(cancellationToken);

        var shortSemesterCode = ShortenSemesterCode(targetClass.Semester?.Code);
        var groupHeader = $"Group {shortSemesterCode}";

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Class Roster");

        // Headers
        worksheet.Cell(1, 1).Value = "RollNumber";
        worksheet.Cell(1, 2).Value = "Fullname";
        worksheet.Cell(1, 3).Value = "Chuyên ngành";
        worksheet.Cell(1, 4).Value = "SubjectCode";
        worksheet.Cell(1, 5).Value = "GroupName";
        worksheet.Cell(1, 6).Value = groupHeader;
        worksheet.Cell(1, 7).Value = "Project Name";
        worksheet.Cell(1, 8).Value = "Description";
        worksheet.Cell(1, 9).Value = "Zalo Link";
        worksheet.Cell(1, 10).Value = "Mentor";
        worksheet.Cell(1, 11).Value = "Mentor - GV";

        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

        int rowIndex = 2;

        foreach (var cs in roster)
        {
            var activeTeamMember = cs.TeamMembers
                .FirstOrDefault(tm => tm.CountsTowardActiveTeam && tm.Team != null && tm.Team.Status == TeamStatus.Active);
            var project = activeTeamMember?.Team?.Project;

            worksheet.Cell(rowIndex, 1).Value = cs.Student.RollNumber ?? string.Empty;
            worksheet.Cell(rowIndex, 2).Value = cs.Student.FullName;
            worksheet.Cell(rowIndex, 3).Value = cs.MajorCodeAtEnrollment;
            worksheet.Cell(rowIndex, 4).Value = targetClass.Course?.Code ?? string.Empty; // Mã môn học (ví dụ EXE101)
            worksheet.Cell(rowIndex, 5).Value = targetClass.ClassCode;                   // Tên lớp (ví dụ EXE101_1)
            worksheet.Cell(rowIndex, 6).Value = string.Empty;                            // Để trống, chỉ header là "Group SU26"
            worksheet.Cell(rowIndex, 7).Value = project?.Name ?? string.Empty;             // Project Name
            worksheet.Cell(rowIndex, 8).Value = project?.Description ?? string.Empty;      // Description
            worksheet.Cell(rowIndex, 10).Value = string.Empty;                            // Mentor (để trống)
            worksheet.Cell(rowIndex, 11).Value = string.Empty;                            // Mentor - GV (để trống)

            var zaloUrl = project?.ZaloGroupUrl;
            var zaloCell = worksheet.Cell(rowIndex, 9);
            if (!string.IsNullOrWhiteSpace(zaloUrl))
            {
                zaloCell.Value = zaloUrl;
                zaloCell.SetHyperlink(new XLHyperlink(zaloUrl));
                zaloCell.Style.Font.FontColor = XLColor.Blue;
                zaloCell.Style.Font.Underline = XLFontUnderlineValues.Single;
            }
            else
            {
                zaloCell.Value = string.Empty;
            }

            rowIndex++;
        }


        worksheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var bytes = ms.ToArray();

        var scopeSuffix = string.Equals(normalizedScope, "Active", StringComparison.OrdinalIgnoreCase)
            ? "active"
            : "history";
        var fileName = $"{targetClass.ClassCode}_students_{scopeSuffix}.xlsx";
        var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        return Result.Success((bytes, contentType, fileName));
    }

    private static string ShortenSemesterCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;

        // Tách phần chữ (mùa: SP/SU/FA...) và phần số (năm)
        var letters = new string(code.Where(char.IsLetter).ToArray());
        var digits = new string(code.Where(char.IsDigit).ToArray());

        if (digits.Length >= 2)
            digits = digits[^2..]; // lấy 2 số cuối, ví dụ 2026 -> 26

        return letters + digits;
    }
}