using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using EHub.Application.Common.Interfaces.Persistence;
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
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<(byte[], string, string)>(
                new Error("Classes.AccessDenied", "You do not have permission to export class roster."));
        }

        var targetClass = await _context.Classes
            .AsNoTracking()
            .Include(c => c.ClassLecturers)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            return Result.Failure<(byte[], string, string)>(
                new Error("Classes.NotFound", "The requested class was not found."));
        }

        if (isLecturer)
        {
            var isAssigned = targetClass.PrimaryLecturerId == currentUserId ||
                             targetClass.ClassLecturers.Any(cl => cl.LecturerId == currentUserId);

            if (!isAssigned)
            {
                return Result.Failure<(byte[], string, string)>(
                    new Error("Classes.AccessDenied", "You can only export roster for classes assigned to you."));
            }
        }

        var roster = await _context.ClassStudents
            .AsNoTracking()
            .Include(cs => cs.Student)
            .Include(cs => cs.TeamMembers)
            .ThenInclude(tm => tm.Team)
            .Where(cs => cs.ClassId == classId)
            .OrderBy(cs => cs.Student.RollNumber)
            .ThenBy(cs => cs.Student.FullName)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Class Roster");

        // Headers
        worksheet.Cell(1, 1).Value = "STT";
        worksheet.Cell(1, 2).Value = "StudentCode";
        worksheet.Cell(1, 3).Value = "FullName";
        worksheet.Cell(1, 4).Value = "Email";
        worksheet.Cell(1, 5).Value = "MajorCode";
        worksheet.Cell(1, 6).Value = "EnrollmentStatus";
        worksheet.Cell(1, 7).Value = "TeamName";
        worksheet.Cell(1, 8).Value = "IsTeamLeader";
        worksheet.Cell(1, 9).Value = "JoinedAt";

        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

        int rowIndex = 2;
        int stt = 1;

        foreach (var cs in roster)
        {
            var activeTeamMember = cs.TeamMembers.FirstOrDefault(tm => tm.Team != null && tm.Team.Status == TeamStatus.Active);

            worksheet.Cell(rowIndex, 1).Value = stt++;
            worksheet.Cell(rowIndex, 2).Value = cs.Student.RollNumber ?? string.Empty;
            worksheet.Cell(rowIndex, 3).Value = cs.Student.FullName;
            worksheet.Cell(rowIndex, 4).Value = cs.Student.Email ?? string.Empty;
            worksheet.Cell(rowIndex, 5).Value = cs.MajorCodeAtEnrollment;
            worksheet.Cell(rowIndex, 6).Value = cs.EnrollmentStatus.ToString();
            worksheet.Cell(rowIndex, 7).Value = activeTeamMember?.Team?.TeamName ?? "N/A";
            worksheet.Cell(rowIndex, 8).Value = activeTeamMember?.RoleInTeam == TeamMemberRole.Leader ? "Yes" : "No";
            worksheet.Cell(rowIndex, 9).Value = cs.CreatedAt.ToString("yyyy-MM-dd HH:mm");

            rowIndex++;
        }

        worksheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var bytes = ms.ToArray();

        var fileName = $"{targetClass.ClassCode}_students.xlsx";
        var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        return Result.Success((bytes, contentType, fileName));
    }
}
