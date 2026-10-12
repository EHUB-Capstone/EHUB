using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Auth;
using EHub.Contracts.Classes;
using EHub.Contracts.Common;
using EHub.Contracts.Mentors;
using EHub.Contracts.Subjects;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Admin;

[Collection("Sequential")]
public sealed class MentorAssignmentExportIntegrationTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Export_ShouldReturnThreeSheetsWithTheMentorsInEffectAndASummary()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedExportSemesterAsync(2064);

        var response = await GetExportAsync(seed.SemesterId, token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be($"{seed.SemesterCode}_mentor_assignments.xlsx");
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        workbook.Worksheets.Select(item => item.Name).Should().Equal("EXE101", "EXE201", "Tổng hợp");

        var exe101 = workbook.Worksheet("EXE101");
        exe101.Cell(1, 6).GetString().Should().Be($"Group {seed.GroupSuffix}");
        exe101.Cell(2, 4).GetString().Should().Be("EXE101");
        exe101.Cell(2, 10).GetString().Should().Be("Export Enterprise One");
        exe101.Cell(2, 11).GetString().Should().Be("Export Academic One");
        exe101.Cell(3, 10).GetString().Should().BeEmpty();

        var exe201 = workbook.Worksheet("EXE201");
        var firstRows = exe201.RowsUsed().Skip(1).Select(row => (row.Cell(10).GetString(), row.Cell(11).GetString())).Where(item => item.Item1 != string.Empty || item.Item2 != string.Empty).ToArray();
        firstRows.Should().BeEquivalentTo(new[]
        {
            ("Export Enterprise One", "Export Academic One"),
            ("Export Enterprise Two", "Chưa phân công"),
            ("Export Enterprise Two", "Export Academic One")
        });
        exe201.Column(5).CellsUsed().Skip(1).Select(cell => cell.GetString()).Distinct().Should().HaveCount(2, "every class of the semester is exported, not only one");
        exe101.RowsUsed().Should().HaveCount(3, "the header and the two students of the only EXE101 team of this semester");
        exe201.Cells().Select(cell => cell.GetString()).Should().NotContain("Export Enterprise Replaced", "an assignment that ended mid-semester is not in effect");

        var summary = workbook.Worksheet("Tổng hợp");
        Counts(summary, "Export Enterprise One").Should().Equal("1", "1", "2");
        Counts(summary, "Export Enterprise Two").Should().Equal("0", "2", "2");
        Counts(summary, "Export Academic One").Should().Equal("1", "2", "3");
        Counts(summary, "Export Idle Mentor").Should().Equal("0", "0", "0");
        summary.Cells().Select(cell => cell.GetString()).Should().NotContain("Export Enterprise Replaced");
        var totals = summary.Rows().Where(row => row.Cell(2).GetString() == "TỔNG").ToArray();
        totals.Should().HaveCount(2);
        totals[0].Cells(3, 5).Select(cell => cell.GetString()).Should().Equal("1", "3", "4");
        // One EXE201 team has no academic mentor, so the academic totals are one lower than the enterprise ones.
        totals[1].Cells(3, 5).Select(cell => cell.GetString()).Should().Equal("1", "2", "3");
    }

    [Fact]
    public async Task Export_ShouldBeDeniedWithoutAnAdministrator()
    {
        var seed = await SeedExportSemesterAsync(2067);
        using var anonymous = await _client.GetAsync($"/api/admin/mentors/assignments/export?semesterId={seed.SemesterId}");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lecturerRole = await context.Roles.SingleAsync(role => role.Name == SystemRoles.Lecturer);
        var email = $"export-denied-{Guid.NewGuid():N}@example.com";
        var lecturer = new User { FullName = "Export Denied Lecturer", Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = UserStatus.Active };
        context.Users.Add(lecturer);
        context.UserRoles.Add(new UserRole { UserId = lecturer.Id, User = lecturer, RoleId = lecturerRole.Id, Role = lecturerRole, AssignedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var lecturerToken = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(lecturer, [SystemRoles.Lecturer]).Token;

        using var forbidden = await GetExportAsync(seed.SemesterId, lecturerToken);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var missing = await GetExportAsync(Guid.NewGuid(), await GetAdminTokenAsync());
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Export_ShouldProduceHeadersOnlyForASemesterWithoutClasses()
    {
        Guid semesterId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var semester = new Semester { Code = $"EM{Guid.NewGuid():N}"[..10], Name = "Empty export", Term = SemesterTerm.Summer, Year = 2068, Status = SemesterStatus.Planned };
            context.Semesters.Add(semester);
            await context.SaveChangesAsync();
            semesterId = semester.Id;
        }

        using var response = await GetExportAsync(semesterId, await GetAdminTokenAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        workbook.Worksheets.Select(item => item.Name).Should().Equal("EXE101", "EXE201", "Tổng hợp");
        workbook.Worksheet("EXE101").LastRowUsed()!.RowNumber().Should().Be(1);
        workbook.Worksheet("EXE201").LastRowUsed()!.RowNumber().Should().Be(1);
    }

    // Stage 5: the real class completion ends the mentor assignments; the semester export must still show those
    // mentors, and the next semester must be able to keep them for the continuing EXE201 team.
    [Fact]
    public async Task CompletingAnExe101Class_ShouldKeepItsMentorsInTheExportAndAvailableForTheContinuingTeam()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2065, 2066);

        // Complete the EXE101 class through the real endpoint.
        using var complete = new HttpRequestMessage(HttpMethod.Post, $"/api/classes/{seed.PreviousClassId}/complete")
        {
            Content = JsonContent.Create(new ChangeClassLifecycleRequest { RowVersion = seed.PreviousClassVersion.ToString(), Reason = "End of EXE101" })
        };
        complete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var completeResponse = await _client.SendAsync(complete);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK, await completeResponse.Content.ReadAsStringAsync());

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var completedClass = await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.PreviousClassId);
            var assignments = await context.MentorAssignments.AsNoTracking().Where(item => item.TeamId == seed.PreviousTeamId).ToListAsync();
            completedClass.Status.Should().Be(ClassStatus.Completed);
            assignments.Should().HaveCount(2).And.OnlyContain(item => item.Status == MentorAssignmentStatus.Ended && item.EndedAt == completedClass.CompletedAtUtc);
        }

        // The export of the completed semester still lists the mentors that the team ended with.
        using var export = await GetExportAsync(seed.PreviousSemesterId, token);
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        using var workbook = new XLWorkbook(await export.Content.ReadAsStreamAsync());
        var sheet = workbook.Worksheet("EXE101");
        sheet.Cell(2, 10).GetString().Should().Be("Lifecycle Enterprise");
        sheet.Cell(2, 11).GetString().Should().Be("Lifecycle Academic");
        Counts(workbook.Worksheet("Tổng hợp"), "Lifecycle Enterprise").Should().Equal("1", "0", "1");

        // The next semester keeps both mentors for the continuing EXE201 team, because they are active there.
        using var preview = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/allocations/preview")
        {
            Content = JsonContent.Create(new PreviewMentorAllocationRequest { SemesterId = seed.TargetSemesterId, ClassIds = [seed.TargetClassId], Seed = 1 })
        };
        preview.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var previewResponse = await _client.SendAsync(preview);
        var body = await previewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>();
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = body!.Data!.Assignments.Where(item => item.TeamId == seed.TargetTeamId).ToArray();
        rows.Should().HaveCount(2).And.OnlyContain(item => item.Source == MentorAllocationSources.Retained);
        rows.Select(item => item.MentorProfileId).Should().BeEquivalentTo([seed.EnterpriseMentorId, seed.AcademicMentorId]);
        body.Data.MentorLoads.Single(item => item.MentorProfileId == seed.EnterpriseMentorId).TotalBefore.Should().Be(0, "the ended EXE101 assignment no longer counts as a current team");
    }

    [Fact]
    public async Task CompletedClass_ShouldStillShowItsMentors_AndGiveThemAReadOnlyHistoryWithoutOpeningTheTeam()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2077, 2078);
        using (var complete = new HttpRequestMessage(HttpMethod.Post, $"/api/classes/{seed.PreviousClassId}/complete")
        {
            Content = JsonContent.Create(new ChangeClassLifecycleRequest { RowVersion = seed.PreviousClassVersion.ToString(), Reason = "End of EXE101" })
        })
        {
            complete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            (await _client.SendAsync(complete)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 1. The teams of the completed class still list the mentors they finished with.
        using var teamsRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/classes/{seed.PreviousClassId}/teams");
        teamsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var teams = await (await _client.SendAsync(teamsRequest)).Content.ReadFromJsonAsync<ApiResponse<List<TeamDto>>>();
        var finished = teams!.Data!.Single(item => item.Id == seed.PreviousTeamId);
        finished.CurrentMentorAssignments.Select(item => item.Mentor.MentorProfileId)
            .Should().BeEquivalentTo([seed.EnterpriseMentorId, seed.AcademicMentorId]);

        // 2. The mentor gets a history entry for it, marked as ended with the class.
        string enterpriseToken, strangerToken;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var enterpriseUser = await context.MentorProfiles.AsNoTracking().Where(item => item.Id == seed.EnterpriseMentorId).Select(item => item.User).SingleAsync();
            enterpriseToken = jwt.GenerateAccessToken(enterpriseUser, [SystemRoles.Mentor]).Token;
            var admin = await context.Users.AsNoTracking().SingleAsync(item => item.NormalizedEmail == "admin@ehub.test");
            strangerToken = jwt.GenerateAccessToken(admin, [SystemRoles.Admin]).Token;
        }
        async Task<HttpResponseMessage> History(string? bearer)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/mentors/me/assignment-history");
            if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            return await _client.SendAsync(request);
        }
        var ownHistory = await History(enterpriseToken);
        var items = (await ownHistory.Content.ReadFromJsonAsync<ApiResponse<List<MentorHistoryItemDto>>>())!.Data!;
        ownHistory.StatusCode.Should().Be(HttpStatusCode.OK);
        items.Should().ContainSingle(item => item.TeamId == seed.PreviousTeamId && item.EndedBecause == "ClassCompleted" && item.Slot == "Enterprise");

        // 3. A mentor sees only their own assignments, and history does not reopen the team for them.
        (await History(null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await History(strangerToken)).StatusCode.Should().NotBe(HttpStatusCode.OK, "only mentors have a mentoring history");
        using var openTeam = new HttpRequestMessage(HttpMethod.Get, $"/api/teams/{seed.PreviousTeamId}");
        openTeam.Headers.Authorization = new AuthenticationHeaderValue("Bearer", enterpriseToken);
        (await _client.SendAsync(openTeam)).StatusCode.Should().NotBe(HttpStatusCode.OK, "an ended mentor no longer has access to the team itself");
    }

    // ---------------- mentors without an account (temporary mentors) ----------------

    [Fact]
    public async Task TemporaryMentor_ShouldFillAnEmptySlot_ShowOnTheTeam_AndKeepOtherMentorsOutOfTheSlot()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2111, 2112);
        var first = await CreateDraftAsync("Temp Lecturer A", MentorType.Academic);
        var second = await CreateDraftAsync("Temp Lecturer B", MentorType.Academic);

        var candidates = await GetJsonAsync<List<MentorCandidateDto>>(token, $"/api/classes/{seed.TargetClassId}/mentor-candidates");
        candidates.Should().Contain(item => item.IsTemporary && item.Mentor.MentorProfileId == first && item.Mentor.IsTemporary && item.Mentor.MentorType == "Academic");

        var assigned = await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = first, Temporary = true });
        assigned.StatusCode.Should().Be(HttpStatusCode.OK, await assigned.Content.ReadAsStringAsync());
        var dto = (await assigned.Content.ReadFromJsonAsync<ApiResponse<MentorAssignmentDto>>())!.Data!;
        dto.Mentor.IsTemporary.Should().BeTrue();
        dto.Mentor.FullName.Should().EndWith("(no email yet)");
        dto.Slot.Should().Be("Academic");

        var team = (await GetJsonAsync<List<TeamDto>>(token, $"/api/classes/{seed.TargetClassId}/teams")).Single(item => item.Id == seed.TargetTeamId);
        team.CurrentMentorAssignments.Should().ContainSingle(item => item.Mentor.IsTemporary && item.Slot == "Academic");

        // Neither a registered mentor nor another temporary mentor may share the slot; the same one again changes nothing.
        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = seed.AcademicMentorId }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = second, Temporary = true }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = first, Temporary = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.TemporaryMentorAssignments.CountAsync(item => item.TeamId == seed.TargetTeamId && item.Status == MentorAssignmentStatus.Active)).Should().Be(1);
        (await context.MentorAssignments.CountAsync(item => item.TeamId == seed.TargetTeamId && item.Slot == MentorType.Academic && item.Status == MentorAssignmentStatus.Active)).Should().Be(0);

        // The Needs information list tells how many teams rely on this mentor until an email arrives.
        var firstName = await context.MentorImportDrafts.AsNoTracking().Where(item => item.Id == first).Select(item => item.FullName).SingleAsync();
        var listed = await GetJsonAsync<IncompleteMentorListResponse>(token, $"/api/admin/mentors/incomplete?limit=50&search={Uri.EscapeDataString(firstName)}");
        listed.Mentors.Should().ContainSingle(item => item.FullName == firstName && item.ActiveTeamCount == 1);
    }

    [Fact]
    public async Task TemporaryMentor_CanBeReplacedByARegisteredMentorAndBack_AndCanBeEnded()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2113, 2114);
        var first = await CreateDraftAsync("Swap Lecturer A", MentorType.Academic);
        var second = await CreateDraftAsync("Swap Lecturer B", MentorType.Academic);
        var assigned = await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = first, Temporary = true });
        var temporary = (await assigned.Content.ReadFromJsonAsync<ApiResponse<MentorAssignmentDto>>())!.Data!;

        // temporary -> registered
        var toReal = await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments/replace",
            new ReplaceMentorRequest { AssignmentId = temporary.AssignmentId, MentorProfileId = seed.AcademicMentorId, Reason = "Account created" });
        toReal.StatusCode.Should().Be(HttpStatusCode.OK, await toReal.Content.ReadAsStringAsync());
        var real = (await toReal.Content.ReadFromJsonAsync<ApiResponse<MentorAssignmentDto>>())!.Data!;
        real.Mentor.IsTemporary.Should().BeFalse();

        // registered -> temporary
        var toTemporary = await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments/replace",
            new ReplaceMentorRequest { AssignmentId = real.AssignmentId, MentorProfileId = second, Temporary = true, Reason = "Swapped back" });
        toTemporary.StatusCode.Should().Be(HttpStatusCode.OK, await toTemporary.Content.ReadAsStringAsync());
        var again = (await toTemporary.Content.ReadFromJsonAsync<ApiResponse<MentorAssignmentDto>>())!.Data!;
        again.Mentor.IsTemporary.Should().BeTrue();

        // wrong type is refused and changes nothing
        var enterpriseDraft = await CreateDraftAsync("Swap Enterprise", MentorType.Enterprise);
        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments/replace",
            new ReplaceMentorRequest { AssignmentId = again.AssignmentId, MentorProfileId = enterpriseDraft, Temporary = true, Reason = "Wrong kind" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments/end", new EndMentorAssignmentRequest { AssignmentId = again.AssignmentId, Reason = "No longer needed" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.TemporaryMentorAssignments.CountAsync(item => item.TeamId == seed.TargetTeamId && item.Status == MentorAssignmentStatus.Active)).Should().Be(0);
        (await context.TemporaryMentorAssignments.CountAsync(item => item.TeamId == seed.TargetTeamId && item.Status == MentorAssignmentStatus.Ended)).Should().Be(2, "history is kept");
        (await context.MentorAssignments.CountAsync(item => item.TeamId == seed.TargetTeamId && item.Slot == MentorType.Academic && item.Status == MentorAssignmentStatus.Ended)).Should().Be(1);
    }

    [Fact]
    public async Task TemporaryMentor_BatchAssignIsAllOrNothing_AndRegisteredMentorsCannotTakeTheSlot()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2115, 2116);
        var draft = await CreateDraftAsync("Batch Lecturer", MentorType.Academic);
        var url = $"/api/classes/{seed.TargetClassId}/mentor-assignments/batch";

        var first = await PostAsync(token, url, new AssignMentorBatchRequest { MentorProfileId = draft, Temporary = true, TeamIds = [seed.TargetTeamId] });
        var repeat = await PostAsync(token, url, new AssignMentorBatchRequest { MentorProfileId = draft, Temporary = true, TeamIds = [seed.TargetTeamId] });
        var registered = await PostAsync(token, url, new AssignMentorBatchRequest { MentorProfileId = seed.AcademicMentorId, TeamIds = [seed.TargetTeamId] });
        var foreign = await PostAsync(token, url, new AssignMentorBatchRequest { MentorProfileId = draft, Temporary = true, TeamIds = [Guid.NewGuid()] });

        (await first.Content.ReadFromJsonAsync<ApiResponse<AssignMentorBatchResponse>>())!.Data!.AssignedCount.Should().Be(1);
        (await repeat.Content.ReadFromJsonAsync<ApiResponse<AssignMentorBatchResponse>>())!.Data!.AlreadyAssignedCount.Should().Be(1);
        registered.StatusCode.Should().Be(HttpStatusCode.Conflict);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AllocationPreview_ShouldTreatATemporaryMentorAsTheHolderOfItsSlot()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2117, 2118);
        var draft = await CreateDraftAsync("Held Lecturer", MentorType.Academic);
        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = draft, Temporary = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await PostAsync(token, "/api/admin/mentors/allocations/preview", new PreviewMentorAllocationRequest { SemesterId = seed.TargetSemesterId, ClassIds = [seed.TargetClassId], Seed = 1 });
        var preview = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Assignments.Should().NotContain(item => item.TeamId == seed.TargetTeamId && item.MentorType == "Academic", "the slot is already held");
        preview.Assignments.Should().Contain(item => item.TeamId == seed.TargetTeamId && item.MentorType == "Enterprise" && item.Source == MentorAllocationSources.Retained);
        preview.ExistingAssignments.Should().Contain(item => item.TeamId == seed.TargetTeamId && item.MentorType == "Academic" && item.MentorName.EndsWith("(no email yet)"));
        preview.Unfilled.Should().NotContain(item => item.TeamId == seed.TargetTeamId && item.MentorType == "Academic");
    }

    [Fact]
    public async Task TemporaryMentor_ShouldBeExportedWithAMarker_ThenBecomeTheRealMentorWhenTheEmailArrives()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2119, 2120);
        var name = $"Convert Lecturer {Guid.NewGuid().ToString("N")[..6]}";
        var draft = await CreateDraftAsync(name, MentorType.Academic, makeUnique: false);
        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = draft, Temporary = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using (var export = await GetExportAsync(seed.TargetSemesterId, token))
        {
            using var workbook = new XLWorkbook(await export.Content.ReadAsStreamAsync());
            workbook.Worksheet("EXE201").Cell(2, 11).GetString().Should().Be($"{name} (chưa có email)");
            Counts(workbook.Worksheet("Tổng hợp"), $"{name} (chưa có email)").Should().Equal("0", "1", "1");
        }

        var email = $"convert-{Guid.NewGuid():N}@example.edu.vn";
        var commit = await ImportMasterAsync(token, email, name);
        commit.TemporaryAssignmentsConverted.Should().Be(1);
        commit.DraftCompletedCount.Should().Be(1);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.TemporaryMentorAssignments.SingleAsync(item => item.DraftId == draft)).Status.Should().Be(MentorAssignmentStatus.Ended);
        var real = await context.MentorAssignments.AsNoTracking().Include(item => item.MentorProfile).ThenInclude(profile => profile.User)
            .SingleAsync(item => item.TeamId == seed.TargetTeamId && item.Slot == MentorType.Academic && item.Status == MentorAssignmentStatus.Active);
        real.MentorProfile.User.NormalizedEmail.Should().Be(email);
        real.EndedAt.Should().BeNull();

        using var after = await GetExportAsync(seed.TargetSemesterId, token);
        using var workbookAfter = new XLWorkbook(await after.Content.ReadAsStreamAsync());
        workbookAfter.Worksheet("EXE201").Cell(2, 11).GetString().Should().Be(name, "the marker disappears once the mentor has an account");
    }

    [Fact]
    public async Task CompletingAClass_ShouldEndItsTemporaryMentorsToo_AndKeepThemInEffectForTheCompletedClass()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2121, 2122);
        var name = $"Finish Lecturer {Guid.NewGuid().ToString("N")[..6]}";
        var draft = await CreateDraftAsync(name, MentorType.Academic, makeUnique: false);
        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = draft, Temporary = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        uint version;
        using (var scope = factory.Services.CreateScope())
            version = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Classes.AsNoTracking().Where(item => item.Id == seed.TargetClassId).Select(item => item.Version).SingleAsync();

        var complete = await PostAsync(token, $"/api/classes/{seed.TargetClassId}/complete", new ChangeClassLifecycleRequest { RowVersion = version.ToString(), Reason = "End of term" });
        complete.StatusCode.Should().Be(HttpStatusCode.OK, await complete.Content.ReadAsStringAsync());

        using var checkScope = factory.Services.CreateScope();
        var context = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var completedAt = (await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.TargetClassId)).CompletedAtUtc;
        var ended = await context.TemporaryMentorAssignments.AsNoTracking().SingleAsync(item => item.DraftId == draft);
        ended.Status.Should().Be(MentorAssignmentStatus.Ended);
        ended.EndedAt.Should().Be(completedAt);

        var team = (await GetJsonAsync<List<TeamDto>>(token, $"/api/classes/{seed.TargetClassId}/teams")).Single(item => item.Id == seed.TargetTeamId);
        team.CurrentMentorAssignments.Should().Contain(item => item.Mentor.IsTemporary && item.Mentor.FullName.StartsWith(name));
    }

    [Fact]
    public async Task SemesterTemporaryMentor_ShouldBeAddedListedAndToggled_ByAnAdminOnly()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2050, 2093);
        var year = 2093;
        var draft = await CreateDraftAsync("Participant Lecturer", MentorType.Academic);

        var candidates = await GetJsonAsync<TeachingStaffCandidateListResponse>(token, "/api/subjects/teaching-staff/candidates");
        candidates.Candidates.Should().Contain(item => item.IsTemporary && item.UserId == draft);

        var request = new AddSemesterTeachingStaffBatchRequest { Semester = "SP", Year = year, Role = "MENTOR", Temporary = true, UserIds = [draft] };
        var added = (await (await PostAsync(token, "/api/subjects/teaching-staff/batch", request)).Content.ReadFromJsonAsync<ApiResponse<AddSemesterTeachingStaffBatchResponse>>())!.Data!;
        added.AddedCount.Should().Be(1);
        var again = (await (await PostAsync(token, "/api/subjects/teaching-staff/batch", request)).Content.ReadFromJsonAsync<ApiResponse<AddSemesterTeachingStaffBatchResponse>>())!.Data!;
        again.AlreadyInListCount.Should().Be(1);

        var list = await GetJsonAsync<TeachingStaffListResponse>(token, $"/api/subjects/teaching-staff?semester=SP&year={year}");
        var row = list.Staff.Single(item => item.DraftId == draft);
        row.IsTemporary.Should().BeTrue();
        row.Status.Should().Be("Active");

        var off = await PutAsync(token, $"/api/subjects/teaching-staff/temporary-mentors/{row.Id}", new UpdateSemesterTeachingStaffRequest { Status = "Inactive", RowVersion = row.RowVersion });
        off.StatusCode.Should().Be(HttpStatusCode.OK, await off.Content.ReadAsStringAsync());
        var stale = await PutAsync(token, $"/api/subjects/teaching-staff/temporary-mentors/{row.Id}", new UpdateSemesterTeachingStaffRequest { Status = "Active", RowVersion = row.RowVersion });
        stale.StatusCode.Should().NotBe(HttpStatusCode.OK, "the row version is outdated");

        using var anonymous = new HttpRequestMessage(HttpMethod.Put, $"/api/subjects/teaching-staff/temporary-mentors/{row.Id}") { Content = JsonContent.Create(new UpdateSemesterTeachingStaffRequest { Status = "Active", RowVersion = row.RowVersion }) };
        (await _client.SendAsync(anonymous)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SemesterTemporaryMentor_ShouldNotBeDeactivatedWhileItHoldsATeam()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2051, 2094);
        var draft = await CreateDraftAsync("Busy Lecturer", MentorType.Academic);
        await PostAsync(token, "/api/subjects/teaching-staff/batch", new AddSemesterTeachingStaffBatchRequest { Semester = "SP", Year = 2094, Role = "MENTOR", Temporary = true, UserIds = [draft] });
        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = draft, Temporary = true })).StatusCode.Should().Be(HttpStatusCode.OK);

        var row = (await GetJsonAsync<TeachingStaffListResponse>(token, "/api/subjects/teaching-staff?semester=SP&year=2094")).Staff.Single(item => item.DraftId == draft);
        row.ClassCount.Should().Be(1);
        var response = await PutAsync(token, $"/api/subjects/teaching-staff/temporary-mentors/{row.Id}", new UpdateSemesterTeachingStaffRequest { Status = "Inactive", RowVersion = row.RowVersion });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Balanced")]
    [InlineData("Random")]
    public async Task Allocation_ShouldUseParticipatingTemporaryMentors_AndSkipThemWhenExcluded(string strategy)
    {
        var token = await GetAdminTokenAsync();
        var years = strategy == "Balanced" ? (2052, 2095) : (2053, 2096);
        var seed = await SeedLifecycleAsync(years.Item1, years.Item2);
        var draft = await CreateDraftAsync("Pool Lecturer", MentorType.Academic);
        await PostAsync(token, "/api/subjects/teaching-staff/batch", new AddSemesterTeachingStaffBatchRequest { Semester = "SP", Year = years.Item2, Role = "MENTOR", Temporary = true, UserIds = [draft] });

        // A team with no previous mentors, so both its slots need a mentor.
        Guid freshTeamId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = await NewSeedContextAsync(scope);
            var targetClass = await context.Context.Classes.Include(item => item.Course).Include(item => item.Semester).SingleAsync(item => item.Id == seed.TargetClassId);
            var fresh = context.Team(targetClass, "Fresh", 2);
            await context.Context.SaveChangesAsync();
            freshTeamId = fresh.Id;
        }

        var excluded = (await (await PostAsync(token, "/api/admin/mentors/allocations/preview", new PreviewMentorAllocationRequest { SemesterId = seed.TargetSemesterId, ClassIds = [seed.TargetClassId], Seed = 7, Strategy = strategy, IncludeTemporaryMentors = false }))
            .Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;
        excluded.Assignments.Should().NotContain(item => item.IsTemporary);
        excluded.Assignments.Should().Contain(item => item.TeamId == freshTeamId && item.MentorType == "Academic" && item.MentorProfileId == seed.AcademicMentorId);

        var response = await PostAsync(token, "/api/admin/mentors/allocations/preview", new PreviewMentorAllocationRequest { SemesterId = seed.TargetSemesterId, ClassIds = [seed.TargetClassId], Seed = 7, Strategy = strategy });
        var preview = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var academicRow = preview.Assignments.Single(item => item.TeamId == freshTeamId && item.MentorType == "Academic");
        if (strategy == "Balanced")
        {
            academicRow.IsTemporary.Should().BeTrue("the participant has no teams yet, so balanced picks them first");
            academicRow.MentorProfileId.Should().Be(draft);
            academicRow.MentorName.Should().EndWith("(no email yet)");

            var commit = await PostAsync(token, "/api/admin/mentors/allocations/commit", new CommitMentorAllocationRequest { SessionId = preview.SessionId });
            commit.StatusCode.Should().Be(HttpStatusCode.OK, await commit.Content.ReadAsStringAsync());
            using var check = factory.Services.CreateScope();
            var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.TemporaryMentorAssignments.AsNoTracking().SingleAsync(item => item.TeamId == freshTeamId)).DraftId.Should().Be(draft);
            (await db.MentorAssignments.AsNoTracking().AnyAsync(item => item.TeamId == freshTeamId && item.Slot == MentorType.Academic && item.Status == MentorAssignmentStatus.Active)).Should().BeFalse();
        }
        else
        {
            preview.Unfilled.Should().NotContain(item => item.TeamId == freshTeamId && item.MentorType == "Academic");
        }
    }

    [Fact]
    public async Task SemesterClasses_ShouldCountOpenMentorSlotsPerClass_AndRequireAnAdministrator()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2054, 2097);

        var before = (await GetJsonAsync<MentorSemesterClassListResponse>(token, $"/api/admin/mentors/semesters/{seed.TargetSemesterId}/classes")).Classes.Single(item => item.ClassId == seed.TargetClassId);
        before.TeamCount.Should().Be(1);
        before.MissingEnterpriseCount.Should().Be(1);
        before.MissingAcademicCount.Should().Be(1);
        before.TemporarySlotCount.Should().Be(0);

        var draft = await CreateDraftAsync("Counted Lecturer", MentorType.Academic);
        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = draft, Temporary = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = seed.EnterpriseMentorId }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var after = (await GetJsonAsync<MentorSemesterClassListResponse>(token, $"/api/admin/mentors/semesters/{seed.TargetSemesterId}/classes")).Classes.Single(item => item.ClassId == seed.TargetClassId);
        after.MissingEnterpriseCount.Should().Be(0);
        after.MissingAcademicCount.Should().Be(0, "a temporary mentor fills the slot");
        after.TemporarySlotCount.Should().Be(1);

        using var anonymous = await _client.GetAsync($"/api/admin/mentors/semesters/{seed.TargetSemesterId}/classes");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        string lecturerToken;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.MentorProfiles.AsNoTracking().Where(item => item.Id == seed.AcademicMentorId).Select(item => item.User).SingleAsync();
            lecturerToken = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [SystemRoles.Lecturer]).Token;
        }
        using var forbidden = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/mentors/semesters/{seed.TargetSemesterId}/classes");
        forbidden.Headers.Authorization = new AuthenticationHeaderValue("Bearer", lecturerToken);
        (await _client.SendAsync(forbidden)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var missing = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/mentors/semesters/{Guid.NewGuid()}/classes");
        missing.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await _client.SendAsync(missing)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ManualAssignmentAfterAPreview_ShouldRejectTheOldPreview_AndANewPreviewShouldRespectIt()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2055, 2098);

        // A team with no previous mentors, so the preview proposes both of its slots.
        Guid freshTeamId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = await NewSeedContextAsync(scope);
            var targetClass = await context.Context.Classes.Include(item => item.Course).Include(item => item.Semester).SingleAsync(item => item.Id == seed.TargetClassId);
            var fresh = context.Team(targetClass, "ManualAfter", 2);
            await context.Context.SaveChangesAsync();
            freshTeamId = fresh.Id;
        }

        async Task<MentorAllocationPreviewResponse> PreviewAsync() =>
            (await (await PostAsync(token, "/api/admin/mentors/allocations/preview", new PreviewMentorAllocationRequest { SemesterId = seed.TargetSemesterId, ClassIds = [seed.TargetClassId], Seed = 3 }))
                .Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;

        var oldPreview = await PreviewAsync();
        oldPreview.Assignments.Should().Contain(item => item.TeamId == freshTeamId && item.MentorType == "Academic");
        oldPreview.Assignments.Should().Contain(item => item.TeamId == freshTeamId && item.MentorType == "Enterprise");

        // Someone fills the academic slot by hand through the class dialog before the preview is saved.
        (await PostAsync(token, $"/api/teams/{freshTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = seed.AcademicMentorId }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var rejected = await PostAsync(token, "/api/admin/mentors/allocations/commit", new CommitMentorAllocationRequest { SessionId = oldPreview.SessionId });
        rejected.StatusCode.Should().Be(HttpStatusCode.Conflict, await rejected.Content.ReadAsStringAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var active = await db.MentorAssignments.AsNoTracking().Where(item => item.TeamId == freshTeamId && item.Status == MentorAssignmentStatus.Active).ToListAsync();
            active.Should().ContainSingle("nothing from the old preview may be saved").Which.Slot.Should().Be(MentorType.Academic);
        }

        // A fresh preview treats the hand-made assignment as current and only fills the open slot.
        var newPreview = await PreviewAsync();
        newPreview.Assignments.Should().NotContain(item => item.TeamId == freshTeamId && item.MentorType == "Academic");
        newPreview.ExistingAssignments.Should().Contain(item => item.TeamId == freshTeamId && item.MentorType == "Academic" && item.MentorProfileId == seed.AcademicMentorId);
        newPreview.Assignments.Should().Contain(item => item.TeamId == freshTeamId && item.MentorType == "Enterprise");
        var saved = await PostAsync(token, "/api/admin/mentors/allocations/commit", new CommitMentorAllocationRequest { SessionId = newPreview.SessionId });
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());

        using var verify = factory.Services.CreateScope();
        var finalState = await verify.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments.AsNoTracking()
            .Where(item => item.TeamId == freshTeamId && item.Status == MentorAssignmentStatus.Active).ToListAsync();
        finalState.Should().HaveCount(2);
        finalState.Select(item => item.Slot).Should().BeEquivalentTo([MentorType.Academic, MentorType.Enterprise]);
        finalState.Single(item => item.Slot == MentorType.Academic).MentorProfileId.Should().Be(seed.AcademicMentorId);
    }

    [Fact]
    public async Task ReplacingAMentor_ShouldNotifyTheLecturerAndTheFormerMentor_ButNotTheAdminWhoDidIt()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2056, 2100);
        var (lecturerUserId, formerMentorUserId) = await UseLecturerAsync(seed);

        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = seed.AcademicMentorId }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var assignment = await CurrentAssignmentIdAsync(seed.TargetTeamId);
        var draft = await CreateDraftAsync("Replacement Lecturer", MentorType.Academic);
        var reason = "Mentor is overloaded: 6 teams";
        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments/replace", new ReplaceMentorRequest { AssignmentId = assignment, MentorProfileId = draft, Temporary = true, Reason = reason }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var notifications = await DispatchMentorChangesAsync(seed.TargetClassId);
        var lecturerMessage = notifications.Single(item => item.RecipientUserId == lecturerUserId);
        lecturerMessage.Body.Should().Contain("was replaced by Replacement Lecturer").And.Contain(reason);
        lecturerMessage.Body.Should().NotContain("(no email yet)");
        var formerMessage = notifications.Single(item => item.RecipientUserId == formerMentorUserId);
        formerMessage.Title.Should().StartWith("You are no longer mentoring team");
        formerMessage.Body.Should().Contain(reason);
        notifications.Should().HaveCount(2, "the admin who made the change is not told about it");
    }

    [Fact]
    public async Task EndingAMentor_ShouldNotifyTheLecturerAndTheFormerMentorWithTheReason()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2057, 2092);
        var (lecturerUserId, formerMentorUserId) = await UseLecturerAsync(seed);

        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = seed.AcademicMentorId }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var assignment = await CurrentAssignmentIdAsync(seed.TargetTeamId);
        var reason = "Mentor is no longer available";
        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments/end", new EndMentorAssignmentRequest { AssignmentId = assignment, Reason = reason }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var notifications = await DispatchMentorChangesAsync(seed.TargetClassId);
        notifications.Single(item => item.RecipientUserId == lecturerUserId).Body.Should().Contain("was removed").And.Contain(reason);
        notifications.Single(item => item.RecipientUserId == formerMentorUserId).Body.Should().Contain(reason);
    }

    [Fact]
    public async Task ReplacingAMentorInAnAllocationPreview_ShouldNotifyTheLecturerAndTheFormerMentor()
    {
        var token = await GetAdminTokenAsync();
        var seed = await SeedLifecycleAsync(2058, 2091);
        var (lecturerUserId, formerMentorUserId) = await UseLecturerAsync(seed);
        (await PostAsync(token, $"/api/teams/{seed.TargetTeamId}/mentor-assignments", new AssignMentorRequest { MentorProfileId = seed.AcademicMentorId }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = await CreateDraftAsync("Preview Replacement", MentorType.Academic);
        await PostAsync(token, "/api/subjects/teaching-staff/batch", new AddSemesterTeachingStaffBatchRequest { Semester = "SP", Year = 2091, Role = "MENTOR", Temporary = true, UserIds = [draft] });
        var reason = "Rebalance mentor workload";

        var edit = new MentorAllocationEdit { TeamId = seed.TargetTeamId, MentorType = "Academic", MentorProfileId = draft, Replace = true, Reason = reason };
        var previewResponse = await PostAsync(token, "/api/admin/mentors/allocations/preview", new PreviewMentorAllocationRequest { SemesterId = seed.TargetSemesterId, ClassIds = [seed.TargetClassId], Seed = 5, Edits = [edit] });
        var preview = (await previewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;
        preview.Assignments.Should().Contain(item => item.TeamId == seed.TargetTeamId && item.MentorType == "Academic" && item.MentorProfileId == draft);
        var commit = await PostAsync(token, "/api/admin/mentors/allocations/commit", new CommitMentorAllocationRequest { SessionId = preview.SessionId });
        commit.StatusCode.Should().Be(HttpStatusCode.OK, await commit.Content.ReadAsStringAsync());

        var notifications = await DispatchMentorChangesAsync(seed.TargetClassId);
        notifications.Single(item => item.RecipientUserId == lecturerUserId).Body.Should().Contain("was replaced by Preview Replacement").And.Contain(reason);
        notifications.Single(item => item.RecipientUserId == formerMentorUserId).Body.Should().Contain(reason);
        notifications.Should().HaveCount(2);
    }

    // The class lecturer must be someone other than the admin who acts, and not the mentor being replaced.
    private async Task<(Guid LecturerUserId, Guid FormerMentorUserId)> UseLecturerAsync(LifecycleSeed seed)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lecturerUserId = await context.MentorProfiles.Where(item => item.Id == seed.EnterpriseMentorId).Select(item => item.UserId).SingleAsync();
        var formerMentorUserId = await context.MentorProfiles.Where(item => item.Id == seed.AcademicMentorId).Select(item => item.UserId).SingleAsync();
        var targetClass = await context.Classes.SingleAsync(item => item.Id == seed.TargetClassId);
        targetClass.PrimaryLecturerId = lecturerUserId;
        await context.SaveChangesAsync();
        return (lecturerUserId, formerMentorUserId);
    }

    private async Task<Guid> CurrentAssignmentIdAsync(Guid teamId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments.AsNoTracking()
            .Where(item => item.TeamId == teamId && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
            .Select(item => item.Id).SingleAsync();
    }

    private async Task<List<Notification>> DispatchMentorChangesAsync(Guid classId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var events = await context.OutboxMessages.Where(item => item.AggregateId == classId && item.Type == "Team.MentorChanged.v1").ToListAsync();
        events.Should().NotBeEmpty();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxEventDispatcher>();
        foreach (var message in events)
        {
            await dispatcher.DispatchAsync(message);
            await dispatcher.DispatchAsync(message); // a retry must not create duplicates
        }
        await context.SaveChangesAsync();
        var ids = events.Select(item => item.EventId).ToArray();
        return await context.Notifications.AsNoTracking().Where(item => ids.Contains(item.SourceEventId!.Value)).ToListAsync();
    }

    private async Task<HttpResponseMessage> PutAsync(string token, string url, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<Guid> CreateDraftAsync(string name, MentorType type, bool makeUnique = true)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unique = Guid.NewGuid().ToString("N")[..6];
        var fullName = makeUnique ? $"{name} {unique}" : name;
        var draft = new MentorImportDraft { Type = type, FullName = fullName, NormalizedFullName = fullName.ToLowerInvariant(), Status = MentorImportDraftStatus.NeedsCompletion };
        context.MentorImportDrafts.Add(draft);
        await context.SaveChangesAsync();
        return draft.Id;
    }

    private async Task<HttpResponseMessage> PostAsync(string token, string url, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<T> GetJsonAsync<T>(string token, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResponse<T>>())!.Data!;
    }

    // Imports a two-sheet mentor workbook into the master list: the lecturer mentor with an email, plus a dummy enterprise row.
    private async Task<MentorImportCommitResponse> ImportMasterAsync(string token, string academicEmail, string academicName)
    {
        using var workbook = new XLWorkbook();
        var enterprise = workbook.Worksheets.Add("DS Mentor_FA26");
        string[] enterpriseHeaders = ["STT", "Họ và tên", "Email"];
        for (var index = 0; index < enterpriseHeaders.Length; index++) enterprise.Cell(1, index + 1).Value = enterpriseHeaders[index];
        enterprise.Cell(2, 1).Value = "1";
        enterprise.Cell(2, 2).Value = $"Filler Enterprise {Guid.NewGuid().ToString("N")[..6]}";
        enterprise.Cell(2, 3).Value = $"filler-{Guid.NewGuid():N}@example.com";
        var academic = workbook.Worksheets.Add("Mentor IT_FA26");
        string[] academicHeaders = ["STT", "Email công việc", "Họ tên"];
        for (var index = 0; index < academicHeaders.Length; index++) academic.Cell(1, index + 1).Value = academicHeaders[index];
        academic.Cell(2, 1).Value = "1";
        academic.Cell(2, 2).Value = academicEmail;
        academic.Cell(2, 3).Value = academicName;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(stream.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", "mentors.xlsx");
        using var preview = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/master-imports/preview") { Content = content };
        preview.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var previewResponse = await _client.SendAsync(preview);
        var session = (await previewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>())!.Data!;
        session.CanCommit.Should().BeTrue(await previewResponse.Content.ReadAsStringAsync());

        var commit = await PostAsync(token, "/api/admin/mentors/imports/commit", new CommitMentorImportRequest { SessionId = session.SessionId });
        commit.StatusCode.Should().Be(HttpStatusCode.OK, await commit.Content.ReadAsStringAsync());
        return (await commit.Content.ReadFromJsonAsync<ApiResponse<MentorImportCommitResponse>>())!.Data!;
    }

    private static string[] Counts(IXLWorksheet summary, string mentorName)
    {
        var row = summary.Rows().First(item => item.Cell(2).GetString() == mentorName);
        return row.Cells(3, 5).Select(cell => cell.GetString()).ToArray();
    }

    private async Task<HttpResponseMessage> GetExportAsync(Guid semesterId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/mentors/assignments/export?semesterId={semesterId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = "admin@ehub.test", Password = "Admin@123456" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return body!.Data!.AccessToken;
    }

    private sealed record ExportSeed(Guid SemesterId, string SemesterCode, string GroupSuffix);

    private sealed record LifecycleSeed(
        Guid PreviousSemesterId, Guid TargetSemesterId, Guid PreviousClassId, uint PreviousClassVersion, Guid PreviousTeamId,
        Guid TargetClassId, Guid TargetTeamId, Guid EnterpriseMentorId, Guid AcademicMentorId);

    private sealed class SeedContext(AppDbContext context, Guid adminId, Role mentorRole, string suffix)
    {
        public AppDbContext Context { get; } = context;
        public Guid AdminId { get; } = adminId;

        public Course Course(string code)
        {
            var existing = Context.Courses.Local.FirstOrDefault(item => item.Code == code) ?? Context.Courses.SingleOrDefault(item => item.Code == code);
            if (existing is not null) return existing;
            var course = new Course { Code = code, Name = code, Status = CourseStatus.Active };
            Context.Courses.Add(course);
            return course;
        }

        public Semester Semester(string prefix, SemesterTerm term, int year, SemesterStatus status = SemesterStatus.Planned)
        {
            var semester = new Semester { Code = $"{prefix}{suffix}", Name = $"{prefix} {suffix}", Term = term, Year = year, Status = status };
            Context.Semesters.Add(semester);
            return semester;
        }

        public Class Class(Course course, Semester semester, int index, ClassStatus status)
        {
            var @class = new Class
            {
                SemesterId = semester.Id, Semester = semester, CourseId = course.Id, Course = course, ClassCode = $"{course.Code}_{suffix}_{index}",
                Slug = $"{course.Code}-{suffix}-{index}".ToLowerInvariant(), ClassIndex = index, Status = status, PrimaryLecturerId = AdminId,
                ScheduleJson = "[{\"dayOfWeek\":1,\"startTime\":\"08:00\",\"endTime\":\"10:00\"}]", CreatedById = AdminId
            };
            Context.Classes.Add(@class);
            return @class;
        }

        public Team Team(Class @class, string name, int students, Team? previous = null)
        {
            var team = new Team
            {
                ClassId = @class.Id, Class = @class, TeamCode = $"{@class.ClassCode}_{name}", TeamName = name, Status = TeamStatus.Active, CreatedById = AdminId,
                PreviousTeamId = previous?.Id, TeamLineageId = previous?.TeamLineageId ?? Guid.NewGuid()
            };
            for (var index = 0; index < students; index++)
            {
                var roll = $"{suffix}{@class.ClassIndex}{name.GetHashCode() & 0xff:x2}{index}";
                var student = new Student
                {
                    RollNumber = roll, NormalizedRollNumber = roll, FullName = $"Student {roll}", Email = $"{roll}@example.com".ToLowerInvariant(),
                    MajorCode = "SE", Status = StudentStatus.Active, CreatedBy = AdminId
                };
                var enrollment = new ClassStudent
                {
                    ClassId = @class.Id, Class = @class, StudentId = student.Id, Student = student, SemesterId = @class.SemesterId,
                    CourseId = @class.CourseId, MajorCodeAtEnrollment = "SE", EnrollmentStatus = EnrollmentStatus.Active, CountsTowardCourseSemesterLimit = true
                };
                team.TeamMembers.Add(new TeamMember
                {
                    Team = team, TeamId = team.Id, ClassId = @class.Id, StudentId = student.Id, ClassStudent = enrollment,
                    CountsTowardActiveTeam = true, CreatedById = AdminId
                });
                Context.Students.Add(student);
                Context.ClassStudents.Add(enrollment);
            }

            Context.Teams.Add(team);
            return team;
        }

        public MentorProfile Mentor(MentorType type, string name, Semester? activeIn, string? contract = null)
        {
            var email = $"{name.Replace(' ', '-').ToLowerInvariant()}-{suffix}@example.com";
            var user = new User { FullName = name, Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = UserStatus.Active };
            var profile = new MentorProfile { UserId = user.Id, User = user, Type = type, ContractType = contract, Status = MentorProfileStatus.Active, CreatedBy = AdminId };
            Context.Users.Add(user);
            Context.UserRoles.Add(new UserRole { UserId = user.Id, User = user, RoleId = mentorRole.Id, Role = mentorRole, AssignedAt = DateTime.UtcNow, AssignedBy = AdminId });
            Context.MentorProfiles.Add(profile);
            if (activeIn is not null) ActivateIn(profile, activeIn);
            return profile;
        }

        public void ActivateIn(MentorProfile profile, Semester semester) =>
            Context.SemesterStaffAssignments.Add(new SemesterStaffAssignment
            {
                SemesterId = semester.Id, Semester = semester, UserId = profile.UserId, User = profile.User,
                Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active, CreatedBy = AdminId
            });

        public MentorAssignment Assign(Team team, MentorProfile mentor, MentorAssignmentStatus status = MentorAssignmentStatus.Active, DateTime? endedAt = null, int minutesAgo = 0)
        {
            var assignment = new MentorAssignment
            {
                TeamId = team.Id, Team = team, MentorProfileId = mentor.Id, MentorProfile = mentor, AssignedById = AdminId,
                AssignedAt = DateTime.UtcNow.AddMinutes(-minutesAgo), EndedAt = endedAt, Slot = mentor.Type, Status = status, CreatedBy = AdminId
            };
            Context.MentorAssignments.Add(assignment);
            return assignment;
        }
    }

    private async Task<SeedContext> NewSeedContextAsync(IServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminId = await context.Users.Where(item => item.NormalizedEmail == "admin@ehub.test").Select(item => item.Id).SingleAsync();
        var mentorRole = await context.Roles.SingleAsync(item => item.Name == SystemRoles.Mentor);
        return new SeedContext(context, adminId, mentorRole, Guid.NewGuid().ToString("N")[..8]);
    }

    private async Task<ExportSeed> SeedExportSemesterAsync(int year)
    {
        using var scope = factory.Services.CreateScope();
        var seed = await NewSeedContextAsync(scope);
        var semester = seed.Semester("EX", SemesterTerm.Spring, year);
        var exe101 = seed.Course("EXE101");
        var exe201 = seed.Course("EXE201");
        var class101 = seed.Class(exe101, semester, 1, ClassStatus.Active);
        var class201 = seed.Class(exe201, semester, 1, ClassStatus.Active);
        var team101 = seed.Team(class101, "Alpha", 2);
        var teamA = seed.Team(class201, "Beta", 2);
        var teamB = seed.Team(class201, "Gamma", 1);
        var class201Second = seed.Class(exe201, semester, 2, ClassStatus.Active);
        var teamC = seed.Team(class201Second, "Delta", 1);

        var enterpriseOne = seed.Mentor(MentorType.Enterprise, "Export Enterprise One", semester, "Thỉnh giảng");
        var enterpriseTwo = seed.Mentor(MentorType.Enterprise, "Export Enterprise Two", semester, "Khoán");
        var replaced = seed.Mentor(MentorType.Enterprise, "Export Enterprise Replaced", null, "Khoán");
        var academicOne = seed.Mentor(MentorType.Academic, "Export Academic One", semester);
        seed.Mentor(MentorType.Enterprise, "Export Idle Mentor", semester, "Khoán");

        seed.Assign(team101, enterpriseOne, minutesAgo: 10);
        seed.Assign(team101, academicOne, minutesAgo: 10);
        seed.Assign(teamA, replaced, MentorAssignmentStatus.Ended, endedAt: DateTime.UtcNow.AddMinutes(-20), minutesAgo: 60);
        seed.Assign(teamA, enterpriseOne, minutesAgo: 10);
        seed.Assign(teamA, academicOne, minutesAgo: 10);
        seed.Assign(teamB, enterpriseTwo, minutesAgo: 10);
        seed.Assign(teamC, enterpriseTwo, minutesAgo: 10);
        seed.Assign(teamC, academicOne, minutesAgo: 10);
        await seed.Context.SaveChangesAsync();
        return new ExportSeed(semester.Id, semester.Code, $"{semester.Code.Where(char.IsLetter).Aggregate(string.Empty, (text, letter) => text + letter).ToUpperInvariant()}{new string(semester.Code.Where(char.IsDigit).ToArray())[^2..]}");
    }

    private async Task<LifecycleSeed> SeedLifecycleAsync(int previousYear, int targetYear)
    {
        using var scope = factory.Services.CreateScope();
        var seed = await NewSeedContextAsync(scope);
        var previous = seed.Semester("LP", SemesterTerm.Fall, previousYear);
        var target = seed.Semester("LT", SemesterTerm.Spring, targetYear);
        var class101 = seed.Class(seed.Course("EXE101"), previous, 1, ClassStatus.Active);
        var class201 = seed.Class(seed.Course("EXE201"), target, 1, ClassStatus.Active);
        var previousTeam = seed.Team(class101, "Lifecycle", 2);
        var targetTeam = seed.Team(class201, "LifecycleNext", 2, previousTeam);

        var enterprise = seed.Mentor(MentorType.Enterprise, "Lifecycle Enterprise", previous, "Thỉnh giảng");
        var academic = seed.Mentor(MentorType.Academic, "Lifecycle Academic", previous);
        seed.ActivateIn(enterprise, target);
        seed.ActivateIn(academic, target);
        seed.Assign(previousTeam, enterprise, minutesAgo: 30);
        seed.Assign(previousTeam, academic, minutesAgo: 30);
        await seed.Context.SaveChangesAsync();

        var version = await seed.Context.Classes.AsNoTracking().Where(item => item.Id == class101.Id).Select(item => item.Version).SingleAsync();
        return new LifecycleSeed(previous.Id, target.Id, class101.Id, version, previousTeam.Id, class201.Id, targetTeam.Id, enterprise.Id, academic.Id);
    }
}
