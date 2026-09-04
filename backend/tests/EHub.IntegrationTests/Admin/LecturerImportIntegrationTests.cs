using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Contracts.Users;
using EHub.Domain.Enums;
using EHub.IntegrationTests.Common;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Admin;

[Collection("Sequential")]
public sealed class LecturerImportIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LecturerImportIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Preview_ShouldReturn401_WhenNoTokenIsProvided()
    {
        using var request = CreatePreviewRequest(CreateLecturerWorkbook("unauthorized@example.org"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Preview_ShouldReturn403_WhenLecturerTokenIsProvided()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lecturer = await context.Users
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .FirstAsync(user => user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Lecturer));
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(lecturer, [SystemRoles.Lecturer])
            .Token;
        using var request = CreatePreviewRequest(CreateLecturerWorkbook("forbidden@example.org"), token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminImport_ShouldCreateActiveLecturer_AndAllowGoogleLoginForAnyEmailDomain()
    {
        var adminToken = await GetAdminTokenAsync();
        var email = $"lecturer-import-{Guid.NewGuid():N}@independent.edu";
        using var previewRequest = CreatePreviewRequest(CreateLecturerWorkbook(email), adminToken);

        var previewResponse = await _client.SendAsync(previewRequest);
        var previewBody = await previewResponse.Content
            .ReadFromJsonAsync<ApiResponse<LecturerImportPreviewResponse>>();

        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        previewBody!.Data!.CanCommit.Should().BeTrue();
        previewBody.Data.ReadyCount.Should().Be(1);
        previewBody.Data.Rows.Should().ContainSingle(row => row.GoogleEmail == email);

        using var commitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/users/import-lecturers/commit")
        {
            Content = JsonContent.Create(new CommitLecturerImportRequest
            {
                SessionId = previewBody.Data.SessionId
            })
        };
        commitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var commitResponse = await _client.SendAsync(commitRequest);
        var commitBody = await commitResponse.Content
            .ReadFromJsonAsync<ApiResponse<LecturerImportCommitResponse>>();

        commitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        commitBody!.Data!.CreatedCount.Should().Be(1);
        commitBody.Data.ErrorCount.Should().Be(0);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var imported = await context.Users
                .AsNoTracking()
                .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                .SingleAsync(user => user.NormalizedEmail == email);
            imported.Status.Should().Be(UserStatus.Active);
            imported.IsEmailVerified.Should().BeFalse();
            imported.UserRoles.Should().ContainSingle(userRole => userRole.Role.Name == SystemRoles.Lecturer);
        }

        var googleResponse = await _client.PostAsJsonAsync(
            "/api/auth/google",
            new GoogleLoginRequest { IdToken = email });
        var googleBody = await googleResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();

        googleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        googleBody!.Data!.User.Email.Should().Be(email);
        googleBody.Data.User.Roles.Should().ContainSingle(role => role == SystemRoles.Lecturer);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var imported = await context.Users.AsNoTracking()
                .SingleAsync(user => user.NormalizedEmail == email);
            imported.IsEmailVerified.Should().BeTrue();
        }
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new EmailPasswordLoginRequest
            {
                Email = "admin@ehub.test",
                Password = "Admin@123456"
            });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return body!.Data!.AccessToken;
    }

    private static HttpRequestMessage CreatePreviewRequest(byte[] workbook, string? token = null)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", "lecturers.xlsx");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/import-lecturers/preview")
        {
            Content = content
        };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private static byte[] CreateLecturerWorkbook(string googleEmail)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        worksheet.Cell(1, 1).Value = "STT";
        worksheet.Cell(1, 2).Value = "Tên Giảng Viên";
        worksheet.Cell(1, 3).Value = "Vị trí";
        worksheet.Cell(1, 4).Value = "Email";
        worksheet.Cell(1, 6).Value = "Roles";
        worksheet.Cell(2, 1).Value = 1;
        worksheet.Cell(2, 2).Value = "Integration Lecturer";
        worksheet.Cell(2, 3).Value = "Lecturer";
        worksheet.Cell(2, 4).Value = "contact@example.org";
        worksheet.Cell(2, 5).Value = googleEmail;
        worksheet.Cell(2, 6).Value = "Lecturer";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
