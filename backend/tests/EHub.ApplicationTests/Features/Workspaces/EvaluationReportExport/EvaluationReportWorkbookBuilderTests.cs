using ClosedXML.Excel;
using EHub.Application.Features.Workspaces.EvaluationReportExport;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.EvaluationReportExport;

public sealed class EvaluationReportWorkbookBuilderTests
{
    [Fact]
    public void Build_WritesDynamicHeadersAndPersonalScores()
    {
        var courseId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var checkpointId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var firstStudentId = Guid.NewGuid();
        var secondStudentId = Guid.NewGuid();
        var columns = new[]
        {
            new EvaluationReportColumn(courseId, assessmentId, "Business Model", 0),
            new EvaluationReportColumn(courseId, checkpointId, "Checkpoint 2", 10_002),
        };
        var students = new[]
        {
            new EvaluationReportStudentRow("SE001", courseId, projectId, firstStudentId,
                new HashSet<Guid> { assessmentId, checkpointId }),
            new EvaluationReportStudentRow("SE002", courseId, projectId, secondStudentId,
                new HashSet<Guid> { assessmentId, checkpointId }),
        };
        var scores = new Dictionary<(Guid, Guid, Guid), decimal>
        {
            [(projectId, assessmentId, firstStudentId)] = 8.25m,
            [(projectId, checkpointId, firstStudentId)] = 7.5m,
            [(projectId, assessmentId, secondStudentId)] = 9m,
            [(projectId, checkpointId, secondStudentId)] = 6.75m,
        };

        var bytes = EvaluationReportWorkbookBuilder.Build(columns, students, scores);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheet(EvaluationReportWorkbookBuilder.WorksheetName);
        worksheet.Cell(1, 1).GetString().Should().Be("MSSV");
        worksheet.Cell(1, 2).GetString().Should().Be("Business Model");
        worksheet.Cell(1, 3).GetString().Should().Be("Checkpoint 2");
        worksheet.Cell(2, 1).GetString().Should().Be("SE001");
        worksheet.Cell(2, 2).GetValue<decimal>().Should().Be(8.25m);
        worksheet.Cell(2, 3).GetValue<decimal>().Should().Be(7.5m);
        worksheet.Cell(3, 2).GetValue<decimal>().Should().Be(9m);
        worksheet.Cell(3, 3).GetValue<decimal>().Should().Be(6.75m);
    }

    [Fact]
    public void Build_LeavesComponentsOutsideStudentScopeBlank()
    {
        var courseId = Guid.NewGuid();
        var firstCheckpointId = Guid.NewGuid();
        var secondCheckpointId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var columns = new[]
        {
            new EvaluationReportColumn(courseId, firstCheckpointId, "Checkpoint 1", 10_001),
            new EvaluationReportColumn(courseId, secondCheckpointId, "Checkpoint 2", 10_002),
        };
        var students = new[]
        {
            new EvaluationReportStudentRow("SE001", courseId, projectId, studentId,
                new HashSet<Guid> { firstCheckpointId }),
        };
        var scores = new Dictionary<(Guid, Guid, Guid), decimal>
        {
            [(projectId, firstCheckpointId, studentId)] = 8m,
            [(projectId, secondCheckpointId, studentId)] = 9m,
        };

        var bytes = EvaluationReportWorkbookBuilder.Build(columns, students, scores);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheet(EvaluationReportWorkbookBuilder.WorksheetName);
        worksheet.Cell(2, 2).GetValue<decimal>().Should().Be(8m);
        worksheet.Cell(2, 3).IsEmpty().Should().BeTrue();
    }
}
