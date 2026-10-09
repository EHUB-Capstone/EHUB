using ClosedXML.Excel;
using EHub.Application.Features.Admin.Mentors.ExportAssignments;
using EHub.Application.Features.Classes.ExportClassRoster;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Admin.Mentors;

public sealed class MentorAssignmentExportWorkbookBuilderTests
{
    private static readonly Semester Semester = new() { Code = "FA2026", Name = "Fall 2026", Term = SemesterTerm.Fall, Year = 2026 };

    [Fact]
    public void Build_AlwaysProducesTheThreeSheetsInOrder_EvenWithoutData()
    {
        using var workbook = Open(MentorAssignmentExportWorkbookBuilder.Build("FA2026", [], [], null, [], []));

        workbook.Worksheets.Select(item => item.Name).Should().Equal("EXE101", "EXE201", "Tổng hợp");
        foreach (var name in new[] { "EXE101", "EXE201" })
        {
            workbook.Worksheet(name).Row(1).Cells(1, 11).Select(cell => cell.GetString()).Should().Equal(
                "RollNumber", "Fullname", "Chuyên ngành", "SubjectCode", "GroupName", "Group FA26",
                "Project Name", "Description", "Zalo Link", "Mentor", "Mentor - GV");
            workbook.Worksheet(name).LastRowUsed()!.RowNumber().Should().Be(1);
        }
    }

    [Fact]
    public void Build_WritesTheMentorsOnTheFirstRowOfEachTeamAndMarksMissingOnesAsUnassigned()
    {
        var covered = CreateTeamSection("EXE201", "EXE201-1", teamName: "Covered", students: 2);
        var half = CreateTeamSection("EXE201", "EXE201-2", teamName: "Half covered", students: 2);
        var none = CreateTeamSection("EXE201", "EXE201-3", teamName: "Nobody", students: 1);
        var mentorsByTeam = new Dictionary<Guid, ClassRosterMentorNames>
        {
            [TeamId(covered)] = new("Enterprise One", "Academic One"),
            [TeamId(half)] = new("Enterprise Two", string.Empty)
        };
        ClassRosterExportSection WithMentors(ClassRosterExportSection section) => section with { MentorsByTeam = mentorsByTeam };

        using var workbook = Open(MentorAssignmentExportWorkbookBuilder.Build(
            "FA2026", [], [WithMentors(covered), WithMentors(half), WithMentors(none)], null, [], []));
        var sheet = workbook.Worksheet("EXE201");

        // Rows: 2-3 covered team, 4-5 half covered team, 6 team without mentors.
        sheet.Cell(2, 10).GetString().Should().Be("Enterprise One");
        sheet.Cell(2, 11).GetString().Should().Be("Academic One");
        sheet.Cell(3, 10).GetString().Should().BeEmpty("only the first row of a team carries the mentors");
        sheet.Cell(3, 11).GetString().Should().BeEmpty();
        sheet.Cell(4, 10).GetString().Should().Be("Enterprise Two");
        sheet.Cell(4, 11).GetString().Should().Be("Chưa phân công");
        sheet.Cell(6, 10).GetString().Should().Be("Chưa phân công");
        sheet.Cell(6, 11).GetString().Should().Be("Chưa phân công");
        sheet.Cell(2, 4).GetString().Should().Be("EXE201");
        workbook.Worksheet("EXE101").LastRowUsed()!.RowNumber().Should().Be(1);
    }

    [Fact]
    public void Build_SummaryCountsTeamsPerMentorAndSubjectAndListsIdleMentors()
    {
        var enterpriseA = new ExportMentor(Guid.NewGuid(), "Enterprise A", MentorType.Enterprise, "Thỉnh giảng");
        var enterpriseB = new ExportMentor(Guid.NewGuid(), "Enterprise B", MentorType.Enterprise, "Khoán");
        var idle = new ExportMentor(Guid.NewGuid(), "Enterprise Idle", MentorType.Enterprise, "Khoán");
        var academicA = new ExportMentor(Guid.NewGuid(), "Academic A", MentorType.Academic, null);
        var academicB = new ExportMentor(Guid.NewGuid(), "Academic B", MentorType.Academic, null);
        ExportTeamMentor[] teamMentors =
        [
            // Three teams, each with one enterprise and one academic mentor.
            new(enterpriseA.Id, "EXE101"), new(academicA.Id, "EXE101"),
            new(enterpriseA.Id, "EXE201"), new(academicA.Id, "EXE201"),
            new(enterpriseB.Id, "EXE201"), new(academicB.Id, "EXE201")
        ];

        using var workbook = Open(MentorAssignmentExportWorkbookBuilder.Build(
            "FA2026", [], [], null, [academicB, idle, enterpriseB, academicA, enterpriseA], teamMentors));
        var sheet = workbook.Worksheet("Tổng hợp");

        sheet.Row(1).Cells(1, 6).Select(cell => cell.GetString()).Should().Equal(
            "STT", "Họ và tên", "Nhóm EXE101", "Nhóm EXE201", "Tổng", "Loại HĐ");
        RowValues(sheet, 2).Should().Equal("1", "Enterprise A", "1", "1", "2", "Thỉnh giảng");
        RowValues(sheet, 3).Should().Equal("2", "Enterprise B", "0", "1", "1", "Khoán");
        RowValues(sheet, 4).Should().Equal("3", "Enterprise Idle", "0", "0", "0", "Khoán");
        RowValues(sheet, 5).Should().Equal(string.Empty, "TỔNG", "1", "2", "3", string.Empty);

        sheet.Cell(7, 1).GetString().Should().Be("GIẢNG VIÊN IT");
        sheet.Cell(7, 1).Style.Fill.BackgroundColor.Should().Be(XLColor.Yellow);
        RowValues(sheet, 8).Take(5).Should().Equal("1", "Academic A", "1", "1", "2");
        RowValues(sheet, 9).Take(5).Should().Equal("2", "Academic B", "0", "1", "1");
        RowValues(sheet, 10).Take(5).Should().Equal(string.Empty, "TỔNG", "1", "2", "3");
    }

    [Fact]
    public void Build_SummaryTotalsOfBothMentorTypesMatchWhenEveryTeamHasBothMentors()
    {
        var enterprise = Enumerable.Range(1, 3).Select(index => new ExportMentor(Guid.NewGuid(), $"E{index}", MentorType.Enterprise, "Khoán")).ToArray();
        var academic = Enumerable.Range(1, 2).Select(index => new ExportMentor(Guid.NewGuid(), $"A{index}", MentorType.Academic, null)).ToArray();
        var teamMentors = new List<ExportTeamMentor>();
        for (var team = 0; team < 7; team++)
        {
            var subject = team < 3 ? "EXE101" : "EXE201";
            teamMentors.Add(new ExportTeamMentor(enterprise[team % 3].Id, subject));
            teamMentors.Add(new ExportTeamMentor(academic[team % 2].Id, subject));
        }

        using var workbook = Open(MentorAssignmentExportWorkbookBuilder.Build(
            "FA2026", [], [], null, [.. enterprise, .. academic], teamMentors));
        var sheet = workbook.Worksheet("Tổng hợp");

        var totals = sheet.Rows().Where(row => row.Cell(2).GetString() == "TỔNG").Select(row => RowValues(sheet, row.RowNumber()).Skip(2).Take(3).ToArray()).ToArray();
        totals.Should().HaveCount(2);
        totals[0].Should().Equal("3", "4", "7");
        totals[1].Should().Equal(totals[0]);
    }

    private static string[] RowValues(IXLWorksheet sheet, int row) =>
        sheet.Row(row).Cells(1, 6).Select(cell => cell.GetString()).ToArray();

    private static Guid TeamId(ClassRosterExportSection section) =>
        section.Roster.First().TeamMembers.First().Team.Id;

    private static ClassRosterExportSection CreateTeamSection(string courseCode, string classCode, string teamName, int students)
    {
        var course = new Course { Code = courseCode, Name = courseCode };
        var @class = new Class
        {
            Course = course, CourseId = course.Id, Semester = Semester, SemesterId = Semester.Id,
            ClassIndex = 1, ClassCode = classCode, Slug = classCode.ToLowerInvariant()
        };
        var team = new Team { Class = @class, ClassId = @class.Id, TeamCode = $"{classCode}-T", TeamName = teamName, Status = TeamStatus.Active };
        var roster = new List<ClassStudent>();
        for (var index = 0; index < students; index++)
        {
            var student = new Student { RollNumber = $"{classCode}-S{index}", FullName = $"Student {classCode} {index}" };
            var enrollment = new ClassStudent
            {
                Class = @class, ClassId = @class.Id, Student = student, StudentId = student.Id,
                SemesterId = Semester.Id, CourseId = course.Id, MajorCodeAtEnrollment = "SE", EnrollmentStatus = EnrollmentStatus.Active
            };
            enrollment.TeamMembers.Add(new TeamMember
            {
                Team = team, TeamId = team.Id, ClassStudent = enrollment, ClassId = @class.Id, StudentId = student.Id, CountsTowardActiveTeam = true
            });
            roster.Add(enrollment);
        }

        return new ClassRosterExportSection(@class, roster);
    }

    private static XLWorkbook Open(byte[] bytes) => new(new MemoryStream(bytes));
}
