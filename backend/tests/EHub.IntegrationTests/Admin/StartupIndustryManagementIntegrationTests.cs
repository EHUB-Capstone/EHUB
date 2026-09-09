using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Contracts.StartupIndustries;
using EHub.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace EHub.IntegrationTests.Admin;

[Collection("Sequential")]
public sealed class StartupIndustryManagementIntegrationTests
{
    private readonly HttpClient _client;

    public StartupIndustryManagementIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetIndustries_Should_Return_401_Without_AccessToken()
    {
        var response = await _client.GetAsync("/api/startup-industries");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetIndustries_Should_Return_403_For_Student()
    {
        var request = CreateAuthorizedRequest(
            HttpMethod.Get,
            "/api/startup-industries",
            await GetStudentTokenAsync());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_Should_Create_Update_Filter_And_Change_Industry_Status()
    {
        var token = await GetAdminTokenAsync();
        var uniqueName = $"Logistics {Guid.NewGuid():N}";
        var createRequest = CreateAuthorizedRequest(HttpMethod.Post, "/api/startup-industries", token);
        createRequest.Content = JsonContent.Create(new CreateStartupIndustryRequest
        {
            Name = uniqueName,
            Status = "active"
        });

        var createResponse = await _client.SendAsync(createRequest);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadFromJsonAsync<ApiResponse<StartupIndustryResponse>>();
        createBody!.Data.Should().NotBeNull();
        createBody.Data!.Name.Should().Be(uniqueName);

        var industryId = createBody.Data.Id;
        var filterRequest = CreateAuthorizedRequest(
            HttpMethod.Get,
            $"/api/startup-industries?search={Uri.EscapeDataString(uniqueName)}&status=active&sort=name-asc",
            token);
        var filterResponse = await _client.SendAsync(filterRequest);
        var filterBody = await filterResponse.Content.ReadFromJsonAsync<ApiResponse<StartupIndustryListResponse>>();
        filterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        filterBody!.Data!.Industries.Should().ContainSingle(item => item.Id == industryId);

        var updateRequest = CreateAuthorizedRequest(HttpMethod.Put, $"/api/startup-industries/{industryId}", token);
        updateRequest.Content = JsonContent.Create(new UpdateStartupIndustryRequest
        {
            Name = $"{uniqueName} Updated",
            Status = "active"
        });
        var updateResponse = await _client.SendAsync(updateRequest);
        updateResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await updateResponse.Content.ReadAsStringAsync());
        var updateBody = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<StartupIndustryResponse>>();
        updateBody!.Data!.Name.Should().Be($"{uniqueName} Updated");

        var statusRequest = CreateAuthorizedRequest(HttpMethod.Put, $"/api/startup-industries/{industryId}/status", token);
        statusRequest.Content = JsonContent.Create(new ChangeStartupIndustryStatusRequest { Status = "inactive" });
        var statusResponse = await _client.SendAsync(statusRequest);
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var statusBody = await statusResponse.Content.ReadFromJsonAsync<ApiResponse<StartupIndustryResponse>>();
        statusBody!.Data!.Status.Should().Be("inactive");
    }

    [Fact]
    public async Task CreateIndustry_Should_Reject_Duplicate_And_Empty_Name()
    {
        var token = await GetAdminTokenAsync();
        var uniqueName = $"Duplicate {Guid.NewGuid():N}";
        var payload = new CreateStartupIndustryRequest
        {
            Name = uniqueName,
            Status = "active"
        };

        var firstRequest = CreateAuthorizedRequest(HttpMethod.Post, "/api/startup-industries", token);
        firstRequest.Content = JsonContent.Create(payload);
        (await _client.SendAsync(firstRequest)).StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicateRequest = CreateAuthorizedRequest(HttpMethod.Post, "/api/startup-industries", token);
        duplicateRequest.Content = JsonContent.Create(payload);
        (await _client.SendAsync(duplicateRequest)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var invalidRequest = CreateAuthorizedRequest(HttpMethod.Post, "/api/startup-industries", token);
        invalidRequest.Content = JsonContent.Create(new CreateStartupIndustryRequest
        {
            Name = "",
            Status = "active"
        });
        (await _client.SendAsync(invalidRequest)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest
        {
            Email = "admin@ehub.test",
            Password = "Admin@123456"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return body!.Data!.AccessToken;
    }

    private async Task<string> GetStudentTokenAsync()
    {
        FakeEmailService.LastRegistrationOtp = null;
        var email = $"startup-industry-student-{Guid.NewGuid():N}@example.com";
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "Startup Industry Student",
            Email = email,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = "BIT_SE"
        });
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var verifyResponse = await _client.PostAsJsonAsync("/api/auth/register/verify-otp", new VerifyRegistrationOtpRequest
        {
            RegistrationId = registerBody!.Data!.RegistrationId!.Value,
            Otp = FakeEmailService.LastRegistrationOtp!
        });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest
        {
            Email = email,
            Password = "Password123"
        });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return loginBody!.Data!.AccessToken;
    }
}
