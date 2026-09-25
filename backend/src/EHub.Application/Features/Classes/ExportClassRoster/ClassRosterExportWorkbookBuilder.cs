using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using EHub.Application.Features.Classes.Common;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Classes.ExportClassRoster;

internal sealed record ClassRosterExportSection(
    Class Class,
    IReadOnlyCollection<ClassStudent> Roster);

internal static class ClassRosterExportWorkbookBuilder
{
    internal const string WorksheetName = "Class Roster";
    internal const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const double ProjectNameMaxWidth = 25;
    private const double DescriptionMaxWidth = 50;

    internal static byte[] Build(
        IReadOnlyCollection<ClassRosterExportSection> sections,
        IReadOnlyDictionary<string, string>? registeredMajorByEmail = null)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(WorksheetName);
        var semesterCode = sections.FirstOrDefault()?.Class.Semester?.Code;

        WriteHeader(worksheet, $"Group {ShortenSemesterCode(semesterCode)}");

        var rowIndex = 2;
        foreach (var section in sections)
        {
            rowIndex = WriteRoster(worksheet, rowIndex, section, registeredMajorByEmail);
        }

        ApplyColumnSizing(worksheet, rowIndex - 1);
        ApplyBorders(worksheet, rowIndex - 1);

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
        ClassRosterExportSection section,
        IReadOnlyDictionary<string, string>? registeredMajorByEmail)
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
            var profileMajorCode = string.IsNullOrWhiteSpace(enrollment.Student.MajorCode)
                ? null
                : enrollment.Student.MajorCode.Trim().ToUpperInvariant();
            if (!MajorCodes.IsValid(profileMajorCode) &&
                !string.IsNullOrWhiteSpace(enrollment.Student.Email) &&
                registeredMajorByEmail?.TryGetValue(enrollment.Student.Email, out var registeredMajorCode) == true)
            {
                profileMajorCode = registeredMajorCode;
            }
            worksheet.Cell(rowIndex, 3).Value = StudentEnrollmentRules.ResolveEffectiveMajorCode(
                enrollment.MajorCodeAtEnrollment, profileMajorCode) ?? string.Empty;
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

    private static void ApplyBorders(IXLWorksheet worksheet, int lastRowIndex)
    {
        const int firstColumn = 1;
        const int lastColumn = 11; // RollNumber ... Mentor - GV

        var range = worksheet.Range(1, firstColumn, lastRowIndex, lastColumn);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void ApplyColumnSizing(IXLWorksheet worksheet, int lastRowIndex)
    {
        // Tự co giãn các cột theo nội dung, riêng Project Name / Description giới hạn độ rộng tối đa
        // và bật wrap text để nội dung dài xuống dòng thay vì kéo giãn cột.
        worksheet.Columns().AdjustToContents();

        var projectNameColumn = worksheet.Column(7);
        if (projectNameColumn.Width > ProjectNameMaxWidth)
        {
            projectNameColumn.Width = ProjectNameMaxWidth;
        }
        projectNameColumn.Style.Alignment.WrapText = true;

        var descriptionColumn = worksheet.Column(8);
        if (descriptionColumn.Width > DescriptionMaxWidth)
        {
            descriptionColumn.Width = DescriptionMaxWidth;
        }
        descriptionColumn.Style.Alignment.WrapText = true;

        // ClosedXML's Rows().AdjustToContents() không tính đúng chiều cao dòng khi có wrap text,
        // nên tự ước lượng số dòng cần thiết cho từng row và set Height thủ công.
        const double defaultRowHeight = 15d; // chiều cao ~1 dòng với font mặc định (Calibri 11)

        for (var rowNumber = 2; rowNumber <= lastRowIndex; rowNumber++)
        {
            var projectText = worksheet.Cell(rowNumber, 7).GetString();
            var descriptionText = worksheet.Cell(rowNumber, 8).GetString();

            var lineCount = Math.Max(
                EstimateWrappedLineCount(projectText, ProjectNameMaxWidth),
                EstimateWrappedLineCount(descriptionText, DescriptionMaxWidth));

            if (lineCount > 1)
            {
                worksheet.Row(rowNumber).Height = lineCount * defaultRowHeight;
            }
        }
    }

    private static int EstimateWrappedLineCount(string text, double columnWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 1;
        }

        // Ước lượng số ký tự vừa một dòng theo độ rộng cột (xấp xỉ cho font Calibri 11 mặc định của Excel)
        var charsPerLine = Math.Max(10, (int)(columnWidth * 1.8));
        var lineCount = 0;

        foreach (var paragraph in text.Split('\n'))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                lineCount++;
                continue;
            }

            var linesInParagraph = 1;
            var currentLineLength = 0;

            foreach (var word in words)
            {
                var wordLength = word.Length + 1; // +1 cho khoảng trắng
                if (currentLineLength + wordLength > charsPerLine)
                {
                    linesInParagraph++;
                    currentLineLength = wordLength;
                }
                else
                {
                    currentLineLength += wordLength;
                }
            }

            lineCount += linesInParagraph;
        }

        return Math.Max(1, lineCount);
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
