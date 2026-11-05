using ClosedXML.Excel;
using EHub.Application.Features.ProjectData.Common;
using EHub.Application.Features.ProjectData.GetProjectDataTrends;
using EHub.Contracts.ProjectData;
using EHub.Domain.Enums;

namespace EHub.Application.Features.ProjectData.ExportProjectData;

/// <summary>
/// Builds the project data report. Every number is derived from the same project rows as the table, so the sheets
/// always agree with each other and with the screen:
/// overview, one row per project, summaries by semester / subject / class / industry / major / lecturer / mentor,
/// and the competition entries (with a count per competition, year and stage).
/// </summary>
internal static class ProjectDataExportWorkbookBuilder
{
    internal const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    internal const string OverviewSheet = "Tổng quan";
    internal const string ProjectsSheet = "Dự án";
    internal const string SemesterSheet = "Theo kỳ";
    internal const string SubjectSheet = "Theo môn";
    internal const string ClassSheet = "Theo lớp";
    internal const string IndustrySheet = "Theo ngành";
    internal const string MajorSheet = "Theo chuyên ngành";
    internal const string LecturerSheet = "Giảng viên";
    internal const string MentorSheet = "Mentor";
    internal const string CompetitionSheet = "Cuộc thi";
    internal const string CompetitionSummarySheet = "Tổng hợp cuộc thi";

    internal const string NoIndustryLabel = "(Chưa chọn ngành)";
    internal const string NoLecturerLabel = "(Chưa phân công)";
    internal const string DateTimeFormat = "yyyy-mm-dd hh:mm";

    internal static string StageLabel(CompetitionStage stage) => stage switch
    {
        CompetitionStage.Registered => "Đăng ký",
        CompetitionStage.Qualifier => "Vòng loại",
        _ => "Vòng chung kết",
    };

    internal static byte[] Build(ProjectDataExportModel model)
    {
        using var workbook = new XLWorkbook();
        WriteOverview(workbook.Worksheets.Add(OverviewSheet), model);
        WriteProjects(workbook.Worksheets.Add(ProjectsSheet), model.Projects);
        WriteTrends(workbook, model.Projects);
        WriteIndustries(workbook.Worksheets.Add(IndustrySheet), model.Projects);
        WriteMajors(workbook.Worksheets.Add(MajorSheet), model.Projects);
        WriteLecturers(workbook.Worksheets.Add(LecturerSheet), model.Projects);
        WriteMentors(workbook.Worksheets.Add(MentorSheet), model.Projects);
        WriteCompetitions(workbook.Worksheets.Add(CompetitionSheet), model.CompetitionRows);
        WriteCompetitionSummary(workbook.Worksheets.Add(CompetitionSummarySheet), model.CompetitionRows);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ---- Statistics shared by the summary sheets ------------------------------------------------------------------

    private static readonly string[] LabelNames = [.. ProjectAchievementNames.All];

    private static bool[] LabelFlags(ExportProject project) =>
        [project.IsPotential, project.IsInternallyFunded, project.IsExternallyFunded, project.IsAwarded, project.IsPitching, project.IsDemo];

    /// <summary>What every summary row says about a set of projects.</summary>
    private sealed record Stats(
        int Groups, int Graded, decimal? AverageScore, int[] Labels, int InCompetitions, int Finalists)
    {
        internal static Stats Of(IReadOnlyCollection<ExportProject> projects)
        {
            var scores = projects.Where(project => project.Score.HasValue).Select(project => project.Score!.Value).ToArray();
            var labels = new int[LabelNames.Length];
            foreach (var project in projects)
            {
                var flags = LabelFlags(project);
                for (var index = 0; index < flags.Length; index++)
                    if (flags[index]) labels[index]++;
            }

            return new Stats(
                projects.Count,
                scores.Length,
                scores.Length == 0 ? null : Math.Round(scores.Average(), 2, MidpointRounding.AwayFromZero),
                labels,
                projects.Count(project => project.Competitions.Count > 0),
                projects.Count(project => project.Competitions.Any(entry => entry.Stage == CompetitionStage.Final)));
        }

        /// <summary>Groups, graded groups, average score, one count per label, groups in a competition, finalists.</summary>
        internal object?[] Cells() =>
            [Groups, Graded, AverageScore, .. Labels.Cast<object?>(), InCompetitions, Finalists];
    }

    private static readonly string[] StatHeaders =
    [
        "Số nhóm", "Đã có điểm", "Điểm TB", .. LabelNames, "Có thi cuộc thi", "Vào chung kết",
    ];

    private static string[] WithStatHeaders(params string[] leading) => [.. leading, .. StatHeaders];

    // ---- Sheets ---------------------------------------------------------------------------------------------------

    private static void WriteOverview(IXLWorksheet sheet, ProjectDataExportModel model)
    {
        var projects = model.Projects;
        var total = projects.Count;
        var row = 1;

        sheet.Cell(row, 1).Value = "BÁO CÁO DỮ LIỆU DỰ ÁN E-HUB";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.FontSize = 14;
        row += 2;

        void Info(string label, string value)
        {
            SetText(sheet.Cell(row, 1), label);
            sheet.Cell(row, 1).Style.Font.Bold = true;
            SetText(sheet.Cell(row, 2), value);
            row++;
        }

        Info("Xuất lúc (UTC)", model.ExportedAtUtc.ToString("yyyy-MM-dd HH:mm"));
        Info("Người xuất", model.ExportedBy);
        Info("Phạm vi dữ liệu", model.Scope);
        if (model.FilterLines.Count == 0)
        {
            Info("Bộ lọc", "Không áp dụng bộ lọc");
        }
        else
        {
            for (var index = 0; index < model.FilterLines.Count; index++)
                Info(index == 0 ? "Bộ lọc" : string.Empty, model.FilterLines[index]);
        }

        row++;
        var table = new List<object?[]>();
        void Metric(string name, int count, string note = "") =>
            table.Add([name, count, total == 0 ? 0d : (double)count / total, note]);

        var stats = Stats.Of(projects);
        Metric("Tổng số nhóm (dự án)", total);
        Metric("Đã có điểm chính thức", stats.Graded, "Mọi thành phần điểm của môn đã được công bố");
        table.Add(["Điểm chính thức trung bình (thang 10)", stats.AverageScore, null, "Chỉ tính các nhóm đã có điểm"]);
        for (var index = 0; index < LabelNames.Length; index++)
            Metric($"Nhãn {LabelNames[index]}", stats.Labels[index], "Các nhãn độc lập, một nhóm có thể có nhiều nhãn");
        Metric("Có ít nhất một nhãn thành tích", projects.Count(project => LabelFlags(project).Any(flag => flag)));
        Metric("Chưa có nhãn thành tích", projects.Count(project => !LabelFlags(project).Any(flag => flag)));
        Metric("Có tham dự cuộc thi", stats.InCompetitions, "Tính trên mọi cuộc thi của dự án");
        Metric("Vào vòng loại trở lên", projects.Count(project => project.Competitions.Any(entry => entry.Stage >= CompetitionStage.Qualifier)));
        Metric("Vào vòng chung kết", stats.Finalists);
        Metric("Chưa chọn ngành khởi nghiệp", projects.Count(project => project.Industries.Count == 0));
        Metric("Chưa có mentor (cả hai vị trí)", projects.Count(project => project.Mentor.Length == 0 && project.AcademicMentor.Length == 0));
        Metric("Chưa có giảng viên", projects.Count(project => project.Lecturer.Length == 0));

        var firstRow = row;
        WriteHeader(sheet, row, ["Chỉ số", "Số nhóm / Giá trị", "Tỷ lệ trên tổng số nhóm", "Ghi chú"]);
        row++;
        foreach (var cells in table)
        {
            WriteRow(sheet, row, cells);
            row++;
        }

        sheet.Range(firstRow + 1, 3, row - 1, 3).Style.NumberFormat.Format = "0.0%";
        sheet.Column(1).Width = 44;
        sheet.Column(2).Width = 22;
        sheet.Column(3).Width = 24;
        sheet.Column(4).Width = 60;
    }

    private static void WriteProjects(IXLWorksheet sheet, IReadOnlyList<ExportProject> projects)
    {
        var headers = new[]
        {
            "STT", "Kỳ học", "Môn", "Lớp", "Nhóm", "Dự án", "Mô tả", "Ngành khởi nghiệp", "Giảng viên", "Mentor", "Mentor - GV",
            "Số thành viên", "Chuyên ngành thành viên", "Điểm chính thức",
        }.Concat(LabelNames).Concat(["Ghi chú thành tích", "Cập nhật thành tích (UTC)", "Cập nhật bởi", "Số cuộc thi", "Cuộc thi"]).ToArray();
        WriteHeader(sheet, 1, headers);

        for (var index = 0; index < projects.Count; index++)
        {
            var project = projects[index];
            WriteRow(sheet, index + 2,
            [
                index + 1,
                project.SemesterCode,
                project.SubjectCode,
                project.ClassCode,
                project.Groups,
                project.ProjectName,
                project.Description ?? string.Empty,
                string.Join(", ", project.Industries),
                project.Lecturer,
                project.Mentor,
                project.AcademicMentor,
                project.MemberCount,
                string.Join(", ", project.Majors),
                project.Score,
                .. LabelFlags(project).Select(flag => (object?)(flag ? 1 : 0)),
                project.AchievementNote ?? string.Empty,
                project.AchievementsUpdatedAtUtc,
                project.AchievementsUpdatedBy ?? string.Empty,
                project.Competitions.Count,
                string.Join("; ", project.Competitions.Select(entry => $"{entry.Name} {entry.Year} ({StageLabel(entry.Stage)})")),
            ]);
        }

        // Numbers the analyst will sum or sort stay numbers; the score and date get their own format.
        if (projects.Count > 0)
        {
            sheet.Range(2, 14, projects.Count + 1, 14).Style.NumberFormat.Format = "0.00";
            sheet.Range(2, 22, projects.Count + 1, 22).Style.NumberFormat.Format = DateTimeFormat;
        }

        Finish(sheet, headers.Length, projects.Count);
    }

    private static void WriteTrends(XLWorkbook workbook, IReadOnlyList<ExportProject> projects)
    {
        var trend = ProjectDataTrendBuilder.Build(projects.Select(project => new TrendProject(
            project.ProjectId,
            project.SemesterCode,
            project.SemesterYear,
            project.Term,
            project.SubjectCode,
            project.ClassId,
            project.ClassCode,
            project.IsPotential,
            project.IsInternallyFunded,
            project.IsExternallyFunded,
            project.IsAwarded,
            project.IsPitching,
            project.IsDemo,
            project.Score)).ToArray());

        string[] common = ["Số nhóm", "Đã có điểm", "Điểm TB", "Thay đổi điểm", .. LabelNames];

        object?[] Cells(ProjectDataTrendRowResponse row) =>
        [
            row.TotalGroups, row.GradedGroups, row.AverageScore, row.AverageScoreChange,
            row.PotentialGroups, row.InternallyFundedGroups, row.ExternallyFundedGroups, row.AwardedGroups,
            row.PitchingGroups, row.DemoGroups,
        ];

        void Sheet(string name, string[] leading, IEnumerable<ProjectDataTrendRowResponse> rows, Func<ProjectDataTrendRowResponse, object?[]> lead)
        {
            var sheet = workbook.Worksheets.Add(name);
            string[] headers = [.. leading, .. common];
            WriteHeader(sheet, 1, headers);
            var list = rows.ToArray();
            for (var index = 0; index < list.Length; index++)
                WriteRow(sheet, index + 2, [.. lead(list[index]), .. Cells(list[index])]);
            if (list.Length > 0)
            {
                sheet.Range(2, leading.Length + 3, list.Length + 1, leading.Length + 4).Style.NumberFormat.Format = "0.00";
            }

            Finish(sheet, headers.Length, list.Length);
        }

        Sheet(SemesterSheet, ["Kỳ học"], trend.Semesters, row => [row.SemesterCode]);
        Sheet(SubjectSheet, ["Kỳ học", "Môn"], trend.Subjects, row => [row.SemesterCode, row.SubjectCode]);
        Sheet(ClassSheet, ["Kỳ học", "Môn", "Lớp"], trend.Classes, row => [row.SemesterCode, row.SubjectCode, row.ClassCode]);
    }

    private static void WriteIndustries(IXLWorksheet sheet, IReadOnlyList<ExportProject> projects)
    {
        // A project can have up to three industries, so it counts once under each and the shares add up to more than 100%.
        var groups = projects
            .SelectMany(project => project.Industries.Count == 0
                ? new[] { (Name: NoIndustryLabel, Project: project) }
                : project.Industries.Select(name => (Name: name, Project: project)))
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key == NoIndustryLabel)
            .ThenByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        WriteSummaryTable(sheet, "Ngành khởi nghiệp", projects.Count, groups.Select(group => (group.First().Name, (IReadOnlyCollection<ExportProject>)group.Select(item => item.Project).ToArray())));
        WriteNote(sheet, groups.Length + 3, "Một nhóm có tối đa 3 ngành nên được tính ở mỗi ngành; tỷ lệ cộng lại có thể vượt 100%.");
    }

    private static void WriteMajors(IXLWorksheet sheet, IReadOnlyList<ExportProject> projects)
    {
        var groups = projects
            .SelectMany(project => project.Majors.Select(major => (Major: major, Project: project)))
            .GroupBy(item => item.Major, StringComparer.Ordinal)
            .OrderBy(group => group.Key == ProjectDataDistributionRules.UnspecifiedMajor)
            .ThenByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();

        WriteSummaryTable(sheet, "Chuyên ngành", projects.Count, groups.Select(group => (group.Key, (IReadOnlyCollection<ExportProject>)group.Select(item => item.Project).ToArray())));
        WriteNote(sheet, groups.Length + 3, "Một nhóm được tính ở mỗi chuyên ngành có thành viên của nhóm; UNSPECIFIED là chuyên ngành thiếu hoặc không hợp lệ.");
    }

    private static void WriteLecturers(IXLWorksheet sheet, IReadOnlyList<ExportProject> projects)
    {
        var groups = projects
            .GroupBy(project => project.Lecturer.Length == 0 ? NoLecturerLabel : project.Lecturer, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(group => group.Key == NoLecturerLabel)
            .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        WriteSummaryTable(sheet, "Giảng viên", projects.Count, groups.Select(group => (group.Key, (IReadOnlyCollection<ExportProject>)group.ToArray())));
        WriteNote(sheet, groups.Length + 3, "Giảng viên chính của lớp, giống cột Lecturer trên bảng dữ liệu.");
    }

    private static void WriteMentors(IXLWorksheet sheet, IReadOnlyList<ExportProject> projects)
    {
        var slots = projects
            .SelectMany(project => new[]
            {
                (Slot: "Doanh nghiệp", Name: project.Mentor, Project: project),
                (Slot: "Học thuật", Name: project.AcademicMentor, Project: project),
            })
            .Where(item => item.Name.Length > 0)
            .GroupBy(item => (item.Slot, Name: item.Name.ToLowerInvariant()))
            .OrderBy(group => group.Key.Slot, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        string[] headers = WithStatHeaders("Mentor", "Vị trí");
        WriteHeader(sheet, 1, headers);
        for (var index = 0; index < slots.Length; index++)
        {
            var stats = Stats.Of(slots[index].Select(item => item.Project).ToArray());
            WriteRow(sheet, index + 2, [slots[index].First().Name, slots[index].Key.Slot, .. stats.Cells()]);
        }

        if (slots.Length > 0) sheet.Range(2, 5, slots.Length + 1, 5).Style.NumberFormat.Format = "0.00";
        Finish(sheet, headers.Length, slots.Length);
        WriteNote(sheet, slots.Length + 3, "Mentor đang hiển thị của từng nhóm, giống bảng dữ liệu; một nhóm được tính cho cả hai vị trí nếu có đủ hai mentor.");
    }

    /// <summary>A first column naming the group, then the shared statistics, then the share of all groups.</summary>
    private static void WriteSummaryTable(
        IXLWorksheet sheet,
        string firstHeader,
        int totalGroups,
        IEnumerable<(string Name, IReadOnlyCollection<ExportProject> Projects)> groups)
    {
        var list = groups.ToArray();
        string[] headers = [.. WithStatHeaders(firstHeader), "Tỷ lệ trên tổng số nhóm"];
        WriteHeader(sheet, 1, headers);
        for (var index = 0; index < list.Length; index++)
        {
            var stats = Stats.Of(list[index].Projects);
            WriteRow(sheet, index + 2,
            [
                list[index].Name, .. stats.Cells(), totalGroups == 0 ? 0d : (double)stats.Groups / totalGroups,
            ]);
        }

        if (list.Length > 0)
        {
            sheet.Range(2, 4, list.Length + 1, 4).Style.NumberFormat.Format = "0.00";
            sheet.Range(2, headers.Length, list.Length + 1, headers.Length).Style.NumberFormat.Format = "0.0%";
        }

        Finish(sheet, headers.Length, list.Length);
    }

    private static void WriteCompetitions(IXLWorksheet sheet, IReadOnlyList<ProjectCompetitionExportRow> rows)
    {
        string[] headers = ["STT", "Cuộc thi", "Năm", "Trạng thái", "Dự án", "Kỳ học", "Môn", "Lớp", "Nhóm", "Giảng viên", "Mentor", "Mentor - GV"];
        WriteHeader(sheet, 1, headers);

        var ordered = rows
            .OrderByDescending(row => row.Year)
            .ThenBy(row => ProjectCompetitionMapping.IndexOf(row.Competition))
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.SemesterCode, StringComparer.Ordinal)
            .ThenBy(row => row.ClassCode, StringComparer.Ordinal)
            .ThenBy(row => row.ProjectName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            var row = ordered[index];
            WriteRow(sheet, index + 2,
            [
                index + 1, row.Name, row.Year, StageLabel(row.Stage), row.ProjectName, row.SemesterCode, row.SubjectCode,
                row.ClassCode, row.Groups, row.Lecturer, row.Mentor, row.AcademicMentor,
            ]);
        }

        Finish(sheet, headers.Length, ordered.Length);
    }

    private static void WriteCompetitionSummary(IXLWorksheet sheet, IReadOnlyList<ProjectCompetitionExportRow> rows)
    {
        string[] headers =
        [
            "Cuộc thi", "Năm", StageLabel(CompetitionStage.Registered), StageLabel(CompetitionStage.Qualifier),
            StageLabel(CompetitionStage.Final), "Tổng",
        ];
        WriteHeader(sheet, 1, headers);

        // "Others" are told apart by name in the list, but counted together here as one competition.
        var groups = rows
            .GroupBy(row => (row.Competition, row.Year))
            .OrderByDescending(group => group.Key.Year)
            .ThenBy(group => ProjectCompetitionMapping.IndexOf(group.Key.Competition))
            .ToArray();

        int Count(IEnumerable<ProjectCompetitionExportRow> items, CompetitionStage stage) => items.Count(row => row.Stage == stage);

        for (var index = 0; index < groups.Length; index++)
        {
            var group = groups[index];
            WriteRow(sheet, index + 2,
            [
                group.Key.Competition, group.Key.Year, Count(group, CompetitionStage.Registered),
                Count(group, CompetitionStage.Qualifier), Count(group, CompetitionStage.Final), group.Count(),
            ]);
        }

        if (groups.Length > 0)
        {
            var totalRow = groups.Length + 2;
            WriteRow(sheet, totalRow,
            [
                "TỔNG", null, Count(rows, CompetitionStage.Registered), Count(rows, CompetitionStage.Qualifier),
                Count(rows, CompetitionStage.Final), rows.Count,
            ]);
            sheet.Range(totalRow, 1, totalRow, headers.Length).Style.Font.Bold = true;
        }

        Finish(sheet, headers.Length, groups.Length);
    }

    // ---- Writing helpers ------------------------------------------------------------------------------------------

    private static void WriteHeader(IXLWorksheet sheet, int row, string[] headers)
    {
        for (var column = 0; column < headers.Length; column++)
            SetText(sheet.Cell(row, column + 1), headers[column]);

        var header = sheet.Range(row, 1, row, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF3F9");
        header.Style.Alignment.WrapText = true;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        if (row == 1) sheet.SheetView.FreezeRows(1);
    }

    /// <summary>
    /// Writes one row. Text is always plain text (never read as a formula, whatever a user typed), numbers stay numbers,
    /// and a null leaves the cell empty.
    /// </summary>
    private static void WriteRow(IXLWorksheet sheet, int row, object?[] cells)
    {
        for (var column = 0; column < cells.Length; column++)
        {
            var cell = sheet.Cell(row, column + 1);
            switch (cells[column])
            {
                case null:
                    break;
                case string text:
                    SetText(cell, text);
                    break;
                case int number:
                    cell.Value = number;
                    break;
                case decimal number:
                    cell.Value = number;
                    break;
                case double number:
                    cell.Value = number;
                    break;
                case DateTime date:
                    cell.Value = date;
                    break;
                default:
                    SetText(cell, cells[column]!.ToString() ?? string.Empty);
                    break;
            }
        }
    }

    private static void WriteNote(IXLWorksheet sheet, int row, string text)
    {
        SetText(sheet.Cell(row, 1), text);
        sheet.Cell(row, 1).Style.Font.Italic = true;
        sheet.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#64748B");
    }

    private static void Finish(IXLWorksheet sheet, int columns, int dataRows)
    {
        if (dataRows > 0) sheet.Range(1, 1, dataRows + 1, columns).SetAutoFilter();
        sheet.Columns(1, columns).AdjustToContents(1, Math.Min(dataRows + 2, 200), 8, 60);
    }

    private static void SetText(IXLCell cell, string value)
    {
        cell.SetValue(value);
        cell.Style.NumberFormat.Format = "@";
    }
}
