using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using EHub.Application.Features.Classes.ExportClassRoster;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.ExportClassRoster;

public sealed class ClassRosterExportWorkbookBuilderTests
{
    [Fact]
    public void Build_OneClass_PreservesLecturerWorksheetHeadersAndStyle()
    {
        var section = CreateSection("EXE101", 1, "SE001");

        using var workbook = OpenWorkbook(ClassRosterExportWorkbookBuilder.Build([section]));
        var worksheet = workbook.Worksheet(ClassRosterExportWorkbookBuilder.WorksheetName);

        workbook.Worksheets.Should().ContainSingle();
        worksheet.Row(1).Cells(1, 11).Select(cell => cell.GetString()).Should().Equal(
            "RollNumber", "Fullname", "Chuyên ngành", "SubjectCode", "GroupName", "Group FA26",
            "Project Name", "Description", "Zalo Link", "Mentor", "Mentor - GV");
        worksheet.Row(1).Style.Font.Bold.Should().BeTrue();
        worksheet.Cell(1, 1).Style.Fill.BackgroundColor.Should().Be(XLColor.FromHtml("#F1F5F9"));
        worksheet.Cell(2, 1).GetString().Should().Be("SE001");
        worksheet.Cell(2, 4).GetString().Should().Be("EXE101");
        worksheet.Cell(2, 5).GetString().Should().Be("EXE101-1");
    }

    [Fact]
    public void Build_MultipleClasses_WritesAllRowsSequentiallyIntoExactlyOneWorksheet()
    {
        var sections = new List<ClassRosterExportSection>
        {
            CreateSection("EXE101", 1, "SE001"),
            CreateSection("EXE101", 2, "SE002"),
            CreateSection("EXE101", 10, "SE010"),
            CreateSection("EXE201", 1, "SE201")
        };

        using var workbook = OpenWorkbook(ClassRosterExportWorkbookBuilder.Build(sections));
        var worksheet = workbook.Worksheet(ClassRosterExportWorkbookBuilder.WorksheetName);

        workbook.Worksheets.Should().ContainSingle();
        worksheet.Range(2, 5, 5, 5).Cells().Select(cell => cell.GetString()).Should().Equal(
            "EXE101-1", "EXE101-2", "EXE101-10", "EXE201-1");
        worksheet.Range(2, 1, 5, 1).Cells().Select(cell => cell.GetString()).Should().Equal(
            "SE001", "SE002", "SE010", "SE201");
    }

    [Fact]
    public void Build_TeamRows_PreservesProjectAndZaloFormattingOnFirstTeamRowOnly()
    {
        var section = CreateTeamSection();

        using var workbook = OpenWorkbook(ClassRosterExportWorkbookBuilder.Build([section]));
        var worksheet = workbook.Worksheet(ClassRosterExportWorkbookBuilder.WorksheetName);

        worksheet.Cell(2, 7).GetString().Should().Be("Project Alpha");
        worksheet.Cell(2, 8).GetString().Should().Be("Project description");
        worksheet.Cell(2, 9).GetString().Should().Be("https://zalo.me/g/alpha");
        worksheet.Cell(2, 9).HasHyperlink.Should().BeTrue();
        worksheet.Cell(2, 9).Style.Font.FontColor.Should().Be(XLColor.Blue);
        worksheet.Cell(2, 9).Style.Font.Underline.Should().Be(XLFontUnderlineValues.Single);
        worksheet.Cell(3, 7).GetString().Should().BeEmpty();
        worksheet.Cell(3, 8).GetString().Should().BeEmpty();
        worksheet.Cell(3, 9).GetString().Should().BeEmpty();
    }

    [Fact]
    public void Build_MajorMatchesClassRosterEnrollmentProfileAndRegisteredEmailFallback()
    {
        var section = CreateSection("EXE201", 8, "DE180182");
        var enrollment = section.Roster.Single();
        enrollment.MajorCodeAtEnrollment = "UNDECLARED";
        enrollment.Student.MajorCode = "BIT_SE";

        using (var workbook = OpenWorkbook(ClassRosterExportWorkbookBuilder.Build([section])))
        {
            workbook.Worksheet(ClassRosterExportWorkbookBuilder.WorksheetName)
                .Cell(2, 3).GetString().Should().Be("BIT_SE");
        }

        enrollment.Student.MajorCode = null;
        enrollment.Student.Email = "student@example.test";
        using (var workbook = OpenWorkbook(ClassRosterExportWorkbookBuilder.Build(
            [section], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["student@example.test"] = "BBA_MC"
            })))
        {
            workbook.Worksheet(ClassRosterExportWorkbookBuilder.WorksheetName)
                .Cell(2, 3).GetString().Should().Be("BBA_MC");
        }

        enrollment.MajorCodeAtEnrollment = "BBA_FIN";
        using var officialWorkbook = OpenWorkbook(ClassRosterExportWorkbookBuilder.Build([section]));
        officialWorkbook.Worksheet(ClassRosterExportWorkbookBuilder.WorksheetName)
            .Cell(2, 3).GetString().Should().Be("BBA_FIN");
    }

    private static ClassRosterExportSection CreateSection(string courseCode, int classIndex, string rollNumber)
    {
        var semester = CreateSemester();
        var course = new Course { Code = courseCode, Name = courseCode };
        var @class = new Class
        {
            Course = course,
            CourseId = course.Id,
            Semester = semester,
            SemesterId = semester.Id,
            ClassIndex = classIndex,
            ClassCode = $"{courseCode}-{classIndex}",
            Slug = $"{courseCode}-{classIndex}".ToLowerInvariant()
        };
        var student = new Student { RollNumber = rollNumber, FullName = $"Student {rollNumber}" };
        var enrollment = new ClassStudent
        {
            Class = @class,
            ClassId = @class.Id,
            Student = student,
            StudentId = student.Id,
            SemesterId = semester.Id,
            CourseId = course.Id,
            MajorCodeAtEnrollment = "SE",
            EnrollmentStatus = EnrollmentStatus.Active
        };
        return new ClassRosterExportSection(@class, [enrollment]);
    }

    private static ClassRosterExportSection CreateTeamSection()
    {
        var section = CreateSection("EXE101", 1, "SE001");
        var team = new Team
        {
            Class = section.Class,
            ClassId = section.Class.Id,
            TeamCode = "EXE101-1-T1",
            TeamName = "Team 1",
            Status = TeamStatus.Active
        };
        team.Project = new Project
        {
            Team = team,
            TeamId = team.Id,
            Name = "Project Alpha",
            Description = "Project description",
            ZaloGroupUrl = "https://zalo.me/g/alpha"
        };

        var firstEnrollment = section.Roster.Single();
        AddTeamMembership(firstEnrollment, team);
        var secondStudent = new Student { RollNumber = "SE002", FullName = "Student SE002" };
        var secondEnrollment = new ClassStudent
        {
            Class = section.Class,
            ClassId = section.Class.Id,
            Student = secondStudent,
            StudentId = secondStudent.Id,
            SemesterId = section.Class.SemesterId,
            CourseId = section.Class.CourseId,
            MajorCodeAtEnrollment = "SE",
            EnrollmentStatus = EnrollmentStatus.Active
        };
        AddTeamMembership(secondEnrollment, team);

        return new ClassRosterExportSection(section.Class, [firstEnrollment, secondEnrollment]);
    }

    private static void AddTeamMembership(ClassStudent enrollment, Team team)
    {
        enrollment.TeamMembers.Add(new TeamMember
        {
            Team = team,
            TeamId = team.Id,
            ClassStudent = enrollment,
            ClassId = enrollment.ClassId,
            StudentId = enrollment.StudentId,
            CountsTowardActiveTeam = true
        });
    }

    private static Semester CreateSemester() => new()
    {
        Code = "FA2026",
        Name = "Fall 2026",
        Term = SemesterTerm.Fall,
        Year = 2026
    };

    private static XLWorkbook OpenWorkbook(byte[] bytes) => new(new MemoryStream(bytes));
}
