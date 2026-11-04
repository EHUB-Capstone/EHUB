using System.IO;
using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using EHub.Contracts.Common;
using EHub.Contracts.ProjectData;
using EHub.Domain.Entities;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.ProjectData;

public sealed partial class ProjectDataIntegrationTests
{
    private static ProjectCompetitionEntryRequest Entry(string competition, int year, string stage, string? otherName = null) =>
        new() { Competition = competition, Year = year, Stage = stage, OtherName = otherName };

    [Fact]
    public async Task Admin_CanSetCompetitions_WhichAreListedFilteredSearchedAndLogged()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var search = $"search={world.Token}";
            var current = await CurrentAchievements(client, world, world.ProjectB);
            current.Competitions.Should().BeEmpty();

            var saved = await PutCompetitions(client, world.Admin, SystemRoles.Admin, world.ProjectB, current.RowVersion,
                Entry("fshark", 2026, "final"),
                Entry("SV.STARTUP", 2025, "Registered"),
                Entry("Others", 2026, "Qualifier", "  Tech   Cup "));
            saved.RowVersion.Should().NotBe(current.RowVersion);
            // Newest year first, then the order of the fixed list, with the free text shown as the name.
            saved.Competitions.Select(item => (item.Name, item.Year, item.Stage)).Should().Equal(
                ("FShark", 2026, "Final"), ("Tech Cup", 2026, "Qualifier"), ("SV.STARTUP", 2025, "Registered"));
            saved.Competitions.Single(item => item.Competition == "Others").OtherName.Should().Be("Tech Cup");

            var listed = await CurrentAchievements(client, world, world.ProjectB);
            listed.Competitions.Should().BeEquivalentTo(saved.Competitions, options => options.WithStrictOrdering());

            // One filter, or several alternatives; the table row still shows the whole record.
            (await Ids(client, world, $"{search}&competition=FShark")).Should().Equal(world.ProjectB);
            (await Ids(client, world, $"{search}&competition=DBC&competition=SV.STARTUP")).Should().Equal(world.ProjectB);
            (await Ids(client, world, $"{search}&competition=DBC")).Should().BeEmpty();
            (await Ids(client, world, $"{search}&competitionStage=Final&competitionYear=2026")).Should().Equal(world.ProjectB);
            // Stage and year have to belong to the same entry: SV.STARTUP reached Registered in 2025 only.
            (await Ids(client, world, $"{search}&competition=SV.STARTUP&competitionStage=Final")).Should().BeEmpty();
            (await Ids(client, world, $"{search}&competition=SV.STARTUP&competitionYear=2026")).Should().BeEmpty();
            (await Ids(client, world, $"{search}&competition=FShark&competitionStage=Final&competitionYear=2025")).Should().BeEmpty();
            (await Ids(client, world, $"{search}&competition=Others")).Should().Equal(world.ProjectB);
            (await Ids(client, world, "search=tech cup")).Should().Contain(world.ProjectB);
            (await Ids(client, world, $"{search}&competitionYear=2024")).Should().BeEmpty();

            // The same filters apply to the summary, like every other filter.
            (await GetSummary(client, world.Admin, SystemRoles.Admin, $"{search}&competition=FShark")).TotalGroups.Should().Be(1);

            var options = await GetOptions(client, world.Admin, SystemRoles.Admin);
            options.Competitions.Should().Equal(ProjectCompetitionNames.All);
            options.CompetitionStages.Should().Equal("Registered", "Qualifier", "Final");
            options.CompetitionYears.Should().Contain([2026, 2025]);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var log = await context.ProjectActivityLogs.AsNoTracking()
                    .SingleAsync(item => item.ProjectId == world.ProjectB && item.Action == "PROJECT_COMPETITIONS_CHANGED");
                log.ActorUserId.Should().Be(world.Admin.Id);
                log.Summary.Should().Contain("added FShark 2026 (Final)").And.Contain("added Tech Cup 2026 (Qualifier)");
                log.Summary.Length.Should().BeLessThanOrEqualTo(300);
                log.OccurredAtUtc.Kind.Should().Be(DateTimeKind.Utc);
            }

            // Saving the very same list again changes nothing.
            var again = await PutCompetitions(client, world.Admin, SystemRoles.Admin, world.ProjectB, saved.RowVersion,
                Entry("Others", 2026, "Qualifier", "tech cup"), Entry("FShark", 2026, "Final"), Entry("SV.STARTUP", 2025, "Registered"));
            again.RowVersion.Should().Be(saved.RowVersion);

            // Moving on a stage updates the entry; leaving one out removes it.
            var advanced = await PutCompetitions(client, world.Admin, SystemRoles.Admin, world.ProjectB, saved.RowVersion,
                Entry("FShark", 2026, "Final"), Entry("SV.STARTUP", 2025, "Final"));
            advanced.Competitions.Select(item => (item.Name, item.Stage)).Should().Equal(("FShark", "Final"), ("SV.STARTUP", "Final"));
            (await Ids(client, world, $"{search}&competition=Others")).Should().BeEmpty();
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                (await context.ProjectCompetitionEntries.CountAsync(item => item.ProjectId == world.ProjectB)).Should().Be(2);
                (await context.ProjectActivityLogs.AsNoTracking()
                    .Where(item => item.ProjectId == world.ProjectB && item.Action == "PROJECT_COMPETITIONS_CHANGED")
                    .Select(item => item.Summary).ToListAsync())
                    .Should().Contain(summary => summary.Contains("removed Tech Cup 2026 (Qualifier)") && summary.Contains("Registered to Final"));
            }

            (await PutCompetitions(client, world.Admin, SystemRoles.Admin, world.ProjectB, advanced.RowVersion))
                .Competitions.Should().BeEmpty();
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task Lecturer_CanEditCompetitionsOnlyOfAssignedClasses()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();

            var own = await CurrentAchievements(client, world, world.ProjectB);
            (await SendCompetitions(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectB, own.RowVersion, Entry("DBC", 2026, "Registered")))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            // A co-lecturer of class A may edit A but not the completed class B they do not teach.
            var projectA = await CurrentAchievements(client, world, world.ProjectA);
            (await SendCompetitions(client, world.CoLecturer, SystemRoles.Lecturer, world.ProjectA, projectA.RowVersion, Entry("SURF", 2026, "Final")))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            var projectB = await CurrentAchievements(client, world, world.ProjectB);
            (await SendCompetitions(client, world.CoLecturer, SystemRoles.Lecturer, world.ProjectB, projectB.RowVersion, Entry("SURF", 2026, "Final")))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // IDOR: a lecturer of another class cannot change this project, and nothing changed.
            projectA = await CurrentAchievements(client, world, world.ProjectA);
            (await SendCompetitions(client, world.Outsider, SystemRoles.Lecturer, world.ProjectA, projectA.RowVersion, Entry("DAVAS", 2026, "Final")))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CurrentAchievements(client, world, world.ProjectA)).Competitions.Select(item => item.Name).Should().Equal("SURF");

            // An administrator is not limited to any class.
            (await SendCompetitions(client, world.Admin, SystemRoles.Admin, world.ProjectOutsider,
                (await CurrentAchievements(client, world, world.ProjectOutsider)).RowVersion, Entry("DAVAS", 2026, "Final")))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            // Students and mentors are not staff; anonymous callers are rejected.
            foreach (var role in new[] { SystemRoles.Student, SystemRoles.Mentor })
            {
                (await SendCompetitions(client, world.Admin, role, world.ProjectB, "1", Entry("DBC", 2026, "Registered")))
                    .StatusCode.Should().Be(HttpStatusCode.Forbidden, role);
            }
            using var anonymous = new HttpRequestMessage(HttpMethod.Put, $"/api/project-data/{world.ProjectB}/competitions")
            {
                Content = JsonContent.Create(new UpdateProjectCompetitionsRequest { RowVersion = "1" }),
            };
            (await client.SendAsync(anonymous)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task Competitions_RejectInvalidInputStaleVersionsAndUnknownProjects()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var current = await CurrentAchievements(client, world, world.ProjectB);
            Task<HttpResponseMessage> Put(string rowVersion, params ProjectCompetitionEntryRequest[] entries) =>
                SendCompetitions(client, world.Admin, SystemRoles.Admin, world.ProjectB, rowVersion, entries);

            (await Put(current.RowVersion, Entry("Not a contest", 2026, "Final"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Put(current.RowVersion, Entry("DBC", 2026, "Winner"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Put(current.RowVersion, Entry("DBC", 2026, "1"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Put(current.RowVersion, Entry("DBC", 1999, "Final"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Put(current.RowVersion, Entry("DBC", 2101, "Final"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            // "Others" needs a name; a listed competition ignores any name sent with it.
            (await Put(current.RowVersion, Entry("Others", 2026, "Final"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Put(current.RowVersion, Entry("Others", 2026, "Final", "   "))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Put(current.RowVersion, Entry("Others", 2026, "Final", new string('x', 101)))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            // The same competition and year twice (names are compared ignoring case and spacing) is a mistake.
            (await Put(current.RowVersion, Entry("DBC", 2026, "Final"), Entry("dbc", 2026, "Registered"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Put(current.RowVersion, Entry("Others", 2026, "Final", "Tech Cup"), Entry("Others", 2026, "Final", "tech  cup")))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Put(current.RowVersion, Enumerable.Range(0, 31).Select(index => Entry("DBC", 2000 + index, "Final")).ToArray()))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Put("not-a-version", Entry("DBC", 2026, "Final"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await SendCompetitions(client, world.Admin, SystemRoles.Admin, Guid.NewGuid(), current.RowVersion, Entry("DBC", 2026, "Final")))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);

            var withName = await PutCompetitions(client, world.Admin, SystemRoles.Admin, world.ProjectB, current.RowVersion,
                new ProjectCompetitionEntryRequest { Competition = "DBC", OtherName = "ignored", Year = 2026, Stage = "Final" },
                Entry("Others", 2026, "Final", "Tech Cup"), Entry("Others", 2025, "Final", "Tech Cup"));
            withName.Competitions.Single(item => item.Competition == "DBC").OtherName.Should().BeNull();
            withName.Competitions.Count.Should().Be(3);

            // Nothing was stored by the rejected requests, and a stale version is a conflict.
            var stale = await Put(current.RowVersion, Entry("SURF", 2026, "Final"));
            stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await stale.Content.ReadFromJsonAsync<ApiResponse<object>>())!.Code.Should().Be("PROJECT_DATA_CONCURRENCY_CONFLICT");
            (await CurrentAchievements(client, world, world.ProjectB)).Competitions.Count.Should().Be(3);
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task ProjectDataExport_CoversEverySheetWithinTheCallersScopeAndFilters()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var search = $"search={world.Token}";
            var projectA = await CurrentAchievements(client, world, world.ProjectA);
            var projectB = await CurrentAchievements(client, world, world.ProjectB);
            var outsider = await CurrentAchievements(client, world, world.ProjectOutsider);
            await PutCompetitions(client, world.Admin, SystemRoles.Admin, world.ProjectA, projectA.RowVersion,
                Entry("FShark", 2026, "Final"), Entry("DBC", 2026, "Registered"));
            await PutCompetitions(client, world.Admin, SystemRoles.Admin, world.ProjectB, projectB.RowVersion,
                Entry("Others", 2025, "Qualifier", "=HYPERLINK(\"http://example.com\")"));
            await PutCompetitions(client, world.Admin, SystemRoles.Admin, world.ProjectOutsider, outsider.RowVersion,
                Entry("FShark", 2026, "Qualifier"));

            var all = await Export(client, world.Admin, SystemRoles.Admin, search);
            all.Sheets.Keys.Should().Equal(
                "Tổng quan", "Dự án", "Theo kỳ", "Theo môn", "Theo lớp", "Theo ngành", "Theo chuyên ngành",
                "Giảng viên", "Mentor", "Cuộc thi", "Tổng hợp cuộc thi");

            // Overview: who exported what, the filters, and the headline numbers.
            all.Metric("Tổng số nhóm (dự án)").Should().Be("4");
            all.Metric("Nhãn Potential").Should().Be("1");
            all.Metric("Nhãn Internally Funded").Should().Be("1");
            all.Metric("Nhãn Pitching").Should().Be("0");
            all.Metric("Có ít nhất một nhãn thành tích").Should().Be("1");
            all.Metric("Chưa có nhãn thành tích").Should().Be("3");
            all.Metric("Có tham dự cuộc thi").Should().Be("3");
            all.Metric("Vào vòng loại trở lên").Should().Be("3");
            all.Metric("Vào vòng chung kết").Should().Be("1");
            all.Metric("Chưa chọn ngành khởi nghiệp").Should().Be("3");
            all.Metric("Đã có điểm chính thức").Should().Be("0");
            all.Info("Người xuất").Should().Be(world.Admin.FullName);
            all.Info("Phạm vi dữ liệu").Should().Be("Toàn bộ dự án trong hệ thống");
            all.Info("Bộ lọc").Should().Contain("Tìm kiếm").And.Contain(world.Token);

            // One row per project, with the labels as 1/0 and the competitions spelled out.
            var projects = all.Data("Dự án");
            projects.Should().HaveCount(4);
            var rowA = projects.Single(row => row[5] == $"Alpha {world.Token} Health");
            (rowA[14], rowA[15], rowA[16], rowA[17], rowA[18], rowA[19]).Should().Be(("1", "1", "0", "0", "0", "0"));
            rowA[13].Should().BeEmpty("the project is not graded");
            rowA[23].Should().Be("2");
            rowA[24].Should().Contain("FShark 2026 (Vòng chung kết)").And.Contain("DBC 2026 (Đăng ký)");
            rowA[20].Should().BeEmpty();
            projects.Single(row => row[24].Contains("HYPERLINK"))[24].Should().Contain("Vòng loại");
            all.Data("Dự án").Select(row => row[0]).Should().Equal("1", "2", "3", "4");

            // Summaries add up to the same four groups.
            all.Data("Theo kỳ").Should().ContainSingle().Which[1].Should().Be("4");
            all.Data("Theo môn").Sum(row => int.Parse(row[2])).Should().Be(4);
            all.Data("Theo lớp").Sum(row => int.Parse(row[3])).Should().Be(4);
            all.Data("Theo ngành").Single(row => row[0] == "(Chưa chọn ngành)")[1].Should().Be("3");
            all.Data("Theo ngành").Should().Contain(row => row[0] == "Health Tech" && row[1] == "1");
            all.Data("Theo chuyên ngành").Should().NotBeEmpty();
            all.Data("Giảng viên").Sum(row => int.Parse(row[1])).Should().Be(4);
            all.Data("Giảng viên").Single(row => row[0] == world.Outsider.FullName)[1].Should().Be("1");

            var mentors = all.Data("Mentor");
            mentors.Should().Contain(row => row[0] == world.EnterpriseMentor.FullName && row[1] == "Doanh nghiệp" && row[2] == "1");
            mentors.Should().Contain(row => row[0] == world.HistoricalMentor.FullName);
            mentors.Select(row => row[0]).Should().NotContain(world.EarlyEndedMentor.FullName);

            // Competition sheets: newest year first, then the fixed list (DBC before FShark); a formula-like name is text.
            all.Data("Cuộc thi").Select(row => row[2]).Should().Equal("2026", "2026", "2026", "2025");
            all.Data("Cuộc thi")[0][1].Should().Be("DBC");
            all.Data("Cuộc thi").Select(row => (row[1], row[3])).Should().BeEquivalentTo(new[]
            {
                ("DBC", "Đăng ký"), ("FShark", "Vòng chung kết"), ("FShark", "Vòng loại"), ("=HYPERLINK(\"http://example.com\")", "Vòng loại"),
            });
            all.Data("Tổng hợp cuộc thi").Select(row => (row[0], row[1], row[5])).Should().Equal(
                ("DBC", "2026", "1"), ("FShark", "2026", "2"), ("Others", "2025", "1"), ("TỔNG", "", "4"));

            // A filter narrows every sheet the way it narrows the table, and the competition sheets list matching entries only.
            var potential = await Export(client, world.Admin, SystemRoles.Admin, $"{search}&achievement=Potential");
            potential.Data("Dự án").Should().ContainSingle();
            potential.Metric("Tổng số nhóm (dự án)").Should().Be("1");
            potential.Info("Bộ lọc").Should().Contain("Tìm kiếm");
            potential.Sheets["Tổng quan"].Select(row => row[1]).Should().Contain("Thành tích: Potential");

            var finals = await Export(client, world.Admin, SystemRoles.Admin, $"{search}&competition=FShark&competitionStage=Final");
            finals.Data("Dự án").Should().ContainSingle().Which[5].Should().Be($"Alpha {world.Token} Health");
            finals.Data("Dự án")[0][24].Should().Contain("DBC 2026", "the project row still shows its whole record");
            finals.Data("Cuộc thi").Select(row => row[1]).Should().Equal("FShark");
            finals.Sheets["Tổng quan"].Select(row => row[1]).Should().Contain("Cuộc thi: FShark").And.Contain("Vòng thi: Final");

            // A lecturer's report never contains classes they do not teach, and says so in its scope.
            var lecturer = await Export(client, world.Lecturer, SystemRoles.Lecturer, search);
            lecturer.Data("Dự án").Select(row => row[5]).Should().NotContain(name => name.StartsWith("Outsider"));
            lecturer.Data("Dự án").Count.Should().BeLessThan(4);
            lecturer.Data("Cuộc thi").Select(row => (row[1], row[3])).Should().NotContain(("FShark", "Vòng loại"));
            lecturer.Info("Phạm vi dữ liệu").Should().Be("Các lớp do giảng viên phụ trách");
            lecturer.Info("Người xuất").Should().Be(world.Lecturer.FullName);

            // Nothing matching is still a valid, empty workbook; bad filters are refused; other roles and anonymous callers too.
            var none = await Export(client, world.Admin, SystemRoles.Admin, $"{search}&competition=SURF");
            none.Data("Dự án").Should().BeEmpty();
            none.Metric("Tổng số nhóm (dự án)").Should().Be("0");
            (await Send(client, world.Admin, SystemRoles.Admin, HttpMethod.Get,
                $"/api/project-data/export?{search}&competition=Nope")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Send(client, world.Admin, SystemRoles.Student, HttpMethod.Get,
                "/api/project-data/export")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync("/api/project-data/export")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    private sealed class ExportedWorkbook(Dictionary<string, List<string[]>> sheets)
    {
        /// <summary>Every used row of every sheet, as text, in sheet order.</summary>
        public Dictionary<string, List<string[]>> Sheets { get; } = sheets;

        /// <summary>The rows under the header of a table sheet, without the one-cell explanatory notes under some tables.</summary>
        public List<string[]> Data(string sheet) =>
            Sheets[sheet].Skip(1).Where(row => row.Skip(1).Any(cell => cell.Length > 0)).ToList();

        /// <summary>The value of a headline number on the overview sheet.</summary>
        public string Metric(string name) => Sheets["Tổng quan"].Single(row => row[0] == name)[1];

        /// <summary>The value of a "label: value" line of the overview sheet (the first one when it repeats).</summary>
        public string Info(string label) => Sheets["Tổng quan"].First(row => row[0] == label)[1];
    }

    private async Task<ExportedWorkbook> Export(HttpClient client, User user, string role, string query)
    {
        var response = await Send(client, user, role, HttpMethod.Get, $"/api/project-data/export?{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().MatchRegex(@"^project-data-\d{8}\.xlsx$");

        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var workbook = new XLWorkbook(stream);
        var sheets = new Dictionary<string, List<string[]>>();
        foreach (var sheet in workbook.Worksheets)
        {
            var columns = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var rows = new List<string[]>();
            foreach (var row in sheet.RowsUsed())
            {
                row.Cells().Any(cell => cell.HasFormula).Should().BeFalse("exported text must never become a formula");
                rows.Add(Enumerable.Range(1, columns).Select(column => row.Cell(column).GetString()).ToArray());
            }

            sheets[sheet.Name] = rows;
        }

        return new ExportedWorkbook(sheets);
    }

    private async Task<HttpResponseMessage> SendCompetitions(
        HttpClient client, User user, string role, Guid projectId, string rowVersion, params ProjectCompetitionEntryRequest[] entries) =>
        await Send(client, user, role, HttpMethod.Put, $"/api/project-data/{projectId}/competitions",
            new UpdateProjectCompetitionsRequest { Entries = entries, RowVersion = rowVersion });

    private async Task<ProjectCompetitionsResponse> PutCompetitions(
        HttpClient client, User user, string role, Guid projectId, string rowVersion, params ProjectCompetitionEntryRequest[] entries)
    {
        var response = await SendCompetitions(client, user, role, projectId, rowVersion, entries);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectCompetitionsResponse>>();
        return body!.Data!;
    }
}
