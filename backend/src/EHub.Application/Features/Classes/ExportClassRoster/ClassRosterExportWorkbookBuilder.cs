using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Classes.ExportClassRoster;

internal sealed record ClassRosterExportSection(
    Class Class,
    IReadOnlyCollection<ClassStudent> Roster);

internal static class ClassRosterExportWorkbookBuilder
{
    internal const string WorksheetName = "Class Roster";
    internal const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    internal static byte[] Build(IReadOnlyCollection<ClassRosterExportSection> sections)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(WorksheetName);
        var semesterCode = sections.FirstOrDefault()?.Class.Semester?.Code;

        WriteHeader(worksheet, $"Group {ShortenSemesterCode(semesterCode)}");

        var rowIndex = 2;
        foreach (var section in sections)
        {
            rowIndex = WriteRoster(worksheet, rowIndex, section);
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteHeader(IXLWorksheet worksheet, string groupHeader)
    {
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
    }

    private static int WriteRoster(
        IXLWorksheet worksheet,
        int startRowIndex,
        ClassRosterExportSection section)
    {
        var rosterRows = section.Roster
            .Select(enrollment =>
            {
                var activeTeamMember = enrollment.TeamMembers
                    .FirstOrDefault(member =>
                        member.CountsTowardActiveTeam &&
                        member.Team != null &&
                        member.Team.Status == TeamStatus.Active);
                var team = activeTeamMember?.Team;
                return new
                {
                    Enrollment = enrollment,
                    Team = team,
                    Project = team?.Project
                };
            })
            .OrderBy(row => row.Team == null ? 1 : 0)
            .ThenBy(row => row.Team?.Id)
            .ThenBy(row => row.Enrollment.Student.RollNumber)
            .ThenBy(row => row.Enrollment.Student.FullName)
            .ToList();

        var rowIndex = startRowIndex;
        Guid? lastTeamId = null;

        foreach (var row in rosterRows)
        {
            var enrollment = row.Enrollment;
            var project = row.Project;
            var isFirstRowOfTeam = row.Team == null || row.Team.Id != lastTeamId;

            worksheet.Cell(rowIndex, 1).Value = enrollment.Student.RollNumber ?? string.Empty;
            worksheet.Cell(rowIndex, 2).Value = enrollment.Student.FullName;
            worksheet.Cell(rowIndex, 3).Value = enrollment.MajorCodeAtEnrollment;
            worksheet.Cell(rowIndex, 4).Value = section.Class.Course?.Code ?? string.Empty;
            worksheet.Cell(rowIndex, 5).Value = section.Class.ClassCode;
            worksheet.Cell(rowIndex, 6).Value = string.Empty;
            worksheet.Cell(rowIndex, 10).Value = string.Empty;
            worksheet.Cell(rowIndex, 11).Value = string.Empty;

            var zaloCell = worksheet.Cell(rowIndex, 9);
            if (isFirstRowOfTeam)
            {
                worksheet.Cell(rowIndex, 7).Value = project?.Name ?? string.Empty;
                worksheet.Cell(rowIndex, 8).Value = project?.Description ?? string.Empty;

                var zaloUrl = project?.ZaloGroupUrl;
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
            }
            else
            {
                worksheet.Cell(rowIndex, 7).Value = string.Empty;
                worksheet.Cell(rowIndex, 8).Value = string.Empty;
                zaloCell.Value = string.Empty;
            }

            lastTeamId = row.Team?.Id;
            rowIndex++;
        }

        return rowIndex;
    }

    private static string ShortenSemesterCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var letters = new string(code.Where(char.IsLetter).ToArray());
        var digits = new string(code.Where(char.IsDigit).ToArray());
        if (digits.Length >= 2)
        {
            digits = digits[^2..];
        }

        return letters + digits;
    }
}
