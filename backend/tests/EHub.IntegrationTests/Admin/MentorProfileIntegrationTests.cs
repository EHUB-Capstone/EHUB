using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Contracts.Mentors;
using EHub.Contracts.Subjects;
using EHub.Contracts.Users;
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
public sealed class MentorProfileIntegrationTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreatingAMentor_ShouldSaveTheFirstProfileFields_AndShowThemOnTheDetail()
    {
        var token = await GetAdminTokenAsync();
        var created = await CreateMentorAsync(token, expertise: ["  Marketing ", "Fundraising  pitch"], bio: "Ten years in startups.", note: "Weekday afternoons");

        created.IsSuccessStatusCode.Should().BeTrue(await created.Content.ReadAsStringAsync());
        var user = (await created.Content.ReadFromJsonAsync<ApiResponse<ManagedUserResponse>>())!.Data!;
        user.MentorProfileId.Should().NotBeNull("the user list needs the profile id to open the detail page");

        var detail = await GetProfileAsync(token, user.MentorProfileId!.Value);
        var profile = (await detail.Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        profile.FullName.Should().Be(user.Name);
        profile.Email.Should().Be(user.Email);
        profile.MentorType.Should().Be("Academic");
        profile.Status.Should().Be("Active");
        profile.Expertise.Should().Equal("Marketing", "Fundraising pitch");
        profile.Bio.Should().Be("Ten years in startups.");
        profile.AvailabilityNote.Should().Be("Weekday afternoons");
        profile.ActiveTeamCount.Should().Be(0);
        profile.RowVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreatingAMentor_ShouldRejectInvalidProfileFields_AndSaveNothing()
    {
        var token = await GetAdminTokenAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];

        var duplicate = await CreateMentorAsync(token, expertise: ["AI", "ai"], email: $"dup-{unique}@example.com");
        var tooLong = await CreateMentorAsync(token, bio: new string('x', 2001), email: $"long-{unique}@example.com");
        var tooMany = await CreateMentorAsync(token, expertise: Enumerable.Range(1, 21).Select(index => $"Skill {index}").ToArray(), email: $"many-{unique}@example.com");

        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        tooMany.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.Users.IgnoreQueryFilters().AnyAsync(item => item.NormalizedEmail.Contains(unique))).Should().BeFalse("a rejected request creates no account");
    }

    [Fact]
    public async Task UpdatingAProfile_ShouldSaveNewValues_ReturnTheLatestData_AndKeepTheMentorType()
    {
        var token = await GetAdminTokenAsync();
        var profileId = await CreateMentorAndGetProfileIdAsync(token);
        var before = await ReadProfileAsync(token, profileId);

        var response = await UpdateProfileAsync(token, profileId, Update(before, request => request with
        {
            Status = "Unavailable",
            Expertise = ["Product", "UX"],
            Bio = "New background",
            AvailabilityNote = "Only on Fridays",
            Organization = "Acme",
            JobTitle = "CTO",
            LinkedInUrl = "https://www.linkedin.com/in/someone",
            FptEmail = "Someone@FPT.edu.vn",
            DateOfBirth = new DateOnly(1990, 5, 1)
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
        after.Status.Should().Be("Unavailable");
        after.Expertise.Should().Equal("Product", "UX");
        after.Bio.Should().Be("New background");
        after.AvailabilityNote.Should().Be("Only on Fridays");
        after.Organization.Should().Be("Acme");
        after.FptEmail.Should().Be("someone@fpt.edu.vn");
        after.MentorType.Should().Be(before.MentorType, "the type of a mentor is fixed");
        after.RowVersion.Should().NotBe(before.RowVersion);
        (await ReadProfileAsync(token, profileId)).Bio.Should().Be("New background");
    }

    [Fact]
    public async Task UpdatingAProfile_ShouldRejectBadDataWithAMessage_AndKeepTheStoredProfile()
    {
        var token = await GetAdminTokenAsync();
        var profileId = await CreateMentorAndGetProfileIdAsync(token);
        var before = await ReadProfileAsync(token, profileId);

        var cases = new Dictionary<string, UpdateMentorProfileRequest>
        {
            ["status"] = Update(before, request => request with { Status = "Retired" }),
            ["background"] = Update(before, request => request with { Bio = new string('b', 2001) }),
            ["availability"] = Update(before, request => request with { AvailabilityNote = new string('a', 501) }),
            ["duplicate expertise"] = Update(before, request => request with { Expertise = ["Sales", "sales"] }),
            ["short expertise"] = Update(before, request => request with { Expertise = ["A"] }),
            ["linkedin"] = Update(before, request => request with { LinkedInUrl = "javascript:alert(1)" }),
            ["fpt email"] = Update(before, request => request with { FptEmail = "not-an-email" }),
            ["birth date"] = Update(before, request => request with { DateOfBirth = new DateOnly(1800, 1, 1) }),
            ["row version"] = Update(before, request => request with { RowVersion = "abc" })
        };
        foreach (var (name, request) in cases)
        {
            var response = await UpdateProfileAsync(token, profileId, request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, name);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            body!.Message.Should().NotBeNullOrWhiteSpace(name);
        }

        (await ReadProfileAsync(token, profileId)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task UpdatingAProfile_WithAStaleVersion_ShouldBeRejectedAsAConflict()
    {
        var token = await GetAdminTokenAsync();
        var profileId = await CreateMentorAndGetProfileIdAsync(token);
        var original = await ReadProfileAsync(token, profileId);
        (await UpdateProfileAsync(token, profileId, Update(original, request => request with { Bio = "First editor" }))).StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await UpdateProfileAsync(token, profileId, Update(original, request => request with { Bio = "Second editor" }));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadProfileAsync(token, profileId)).Bio.Should().Be("First editor", "the second editor must not overwrite the first");
    }

    [Fact]
    public async Task MentorProfiles_ShouldBeDeniedWithoutAnAdministrator_AndReportUnknownProfiles()
    {
        var token = await GetAdminTokenAsync();
        var profileId = await CreateMentorAndGetProfileIdAsync(token);
        var profile = await ReadProfileAsync(token, profileId);
        var lecturerToken = await CreateLecturerTokenAsync();

        (await GetProfileAsync(null, profileId)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await UpdateProfileAsync(null, profileId, Update(profile, request => request))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await GetProfileAsync(lecturerToken, profileId)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await UpdateProfileAsync(lecturerToken, profileId, Update(profile, request => request with { Bio = "Hacked" }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetProfileAsync(token, Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await UpdateProfileAsync(token, Guid.NewGuid(), Update(profile, request => request))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadProfileAsync(token, profileId)).Bio.Should().NotBe("Hacked");
    }

    [Fact]
    public async Task UpdatingAProfile_ShouldSaveEveryKindOfTag_AndRejectBadOrDuplicatedOnes()
    {
        var token = await GetAdminTokenAsync();
        var profileId = await CreateMentorAndGetProfileIdAsync(token);
        var before = await ReadProfileAsync(token, profileId);

        var saved = await UpdateProfileAsync(token, profileId, Update(before, request => request with
        {
            Expertise = ["Marketing"],
            StartupDomains = [" FinTech ", "EdTech"],
            TechnologySkills = ["React", ".NET", "Machine   learning"],
            MentorTags = ["Alumni"]
        }));
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = (await saved.Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
        after.StartupDomains.Should().Equal("FinTech", "EdTech");
        after.TechnologySkills.Should().Equal("React", ".NET", "Machine learning");
        after.MentorTags.Should().Equal("Alumni");

        var cases = new Dictionary<string, Func<EditableProfile, EditableProfile>>
        {
            ["duplicate domain"] = request => request with { StartupDomains = ["FinTech", "fintech"] },
            ["duplicate technology"] = request => request with { TechnologySkills = ["React", "REACT"] },
            ["duplicate tag"] = request => request with { MentorTags = ["Alumni", "alumni"] },
            ["short domain"] = request => request with { StartupDomains = ["X"] },
            ["long technology"] = request => request with { TechnologySkills = [new string('t', 51)] },
            ["too many tags"] = request => request with { MentorTags = Enumerable.Range(1, 21).Select(index => $"Tag {index}").ToArray() }
        };
        foreach (var (name, change) in cases)
        {
            var response = await UpdateProfileAsync(token, profileId, Update(after, change));
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, name);
        }
        var stored = await ReadProfileAsync(token, profileId);
        stored.StartupDomains.Should().Equal("FinTech", "EdTech");
        stored.TechnologySkills.Should().Equal("React", ".NET", "Machine learning");
    }

    [Fact]
    public async Task TagSuggestions_ShouldListEachKindMostUsedFirstWithOneSpelling_ForAdminsOnly()
    {
        var token = await GetAdminTokenAsync();
        var tag = $"Zeta{Guid.NewGuid():N}"[..14];
        foreach (var spelling in new[] { tag, tag, tag.ToUpperInvariant() })
        {
            var profileId = await CreateMentorAndGetProfileIdAsync(token);
            var profile = await ReadProfileAsync(token, profileId);
            (await UpdateProfileAsync(token, profileId, Update(profile, request => request with { TechnologySkills = [spelling] }))).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/mentor-profiles/tag-suggestions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        var suggestions = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorTagSuggestionsResponse>>())!.Data!;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        suggestions.TechnologySkills.Where(item => item.Equals(tag, StringComparison.OrdinalIgnoreCase)).Should().ContainSingle("spellings that differ by case are listed once").Which.Should().Be(tag, "the most used spelling wins");

        (await _client.GetAsync("/api/admin/mentor-profiles/tag-suggestions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var forbidden = new HttpRequestMessage(HttpMethod.Get, "/api/admin/mentor-profiles/tag-suggestions");
        forbidden.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await CreateLecturerTokenAsync());
        (await _client.SendAsync(forbidden)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheMentorPicker_ShouldCarryTheTagsOfEachMentor()
    {
        var token = await GetAdminTokenAsync();
        var profileId = await CreateMentorAndGetProfileIdAsync(token);
        var profile = await ReadProfileAsync(token, profileId);
        (await UpdateProfileAsync(token, profileId, Update(profile, request => request with
        {
            Expertise = ["Pricing"], StartupDomains = ["HealthTech"], TechnologySkills = ["Python"], MentorTags = ["Industry"]
        }))).StatusCode.Should().Be(HttpStatusCode.OK);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/subjects/teaching-staff/candidates");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        var candidates = (await response.Content.ReadFromJsonAsync<ApiResponse<TeachingStaffCandidateListResponse>>())!.Data!.Candidates;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var mentor = candidates.Single(item => item.UserId == profile.UserId && item.Role == "MENTOR");
        mentor.Tags.Should().NotBeNull();
        mentor.Tags!.Expertise.Should().Equal("Pricing");
        mentor.Tags.StartupDomains.Should().Equal("HealthTech");
        mentor.Tags.TechnologySkills.Should().Equal("Python");
        mentor.Tags.MentorTags.Should().Equal("Industry");
    }

    // ---- helpers ----

    private static UpdateMentorProfileRequest Update(MentorProfileResponse profile, Func<EditableProfile, EditableProfile> change)
    {
        var edited = change(new EditableProfile(
            profile.RowVersion, profile.Status, profile.Expertise, profile.StartupDomains, profile.TechnologySkills, profile.MentorTags, profile.Bio, profile.AvailabilityNote, profile.Organization,
            profile.Department, profile.JobTitle, profile.ContractType, profile.EducationLevel, profile.CurrentAddress,
            profile.LinkedInUrl, profile.FptEmail, profile.DateOfBirth));
        return new UpdateMentorProfileRequest
        {
            RowVersion = edited.RowVersion, Status = edited.Status, Expertise = edited.Expertise, StartupDomains = edited.StartupDomains,
            TechnologySkills = edited.TechnologySkills, MentorTags = edited.MentorTags, Bio = edited.Bio,
            AvailabilityNote = edited.AvailabilityNote, Organization = edited.Organization, Department = edited.Department,
            JobTitle = edited.JobTitle, ContractType = edited.ContractType, EducationLevel = edited.EducationLevel,
            CurrentAddress = edited.CurrentAddress, LinkedInUrl = edited.LinkedInUrl, FptEmail = edited.FptEmail, DateOfBirth = edited.DateOfBirth
        };
    }

    private sealed record EditableProfile(
        string RowVersion, string Status, IReadOnlyCollection<string> Expertise, IReadOnlyCollection<string> StartupDomains,
        IReadOnlyCollection<string> TechnologySkills, IReadOnlyCollection<string> MentorTags, string? Bio, string? AvailabilityNote, string? Organization,
        string? Department, string? JobTitle, string? ContractType, string? EducationLevel, string? CurrentAddress,
        string? LinkedInUrl, string? FptEmail, DateOnly? DateOfBirth);

    private async Task<HttpResponseMessage> CreateMentorAsync(
        string token, string[]? expertise = null, string? bio = null, string? note = null, string? email = null)
    {
        var unique = Guid.NewGuid().ToString("N")[..10];
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new SaveManagedUserRequest
            {
                Name = $"Profile Mentor {unique}", Email = email ?? $"profile-{unique}@example.com", Password = "Temporary123",
                Role = "MENTOR", MentorType = "Academic", Status = "APPROVED", Expertise = expertise, Bio = bio, AvailabilityNote = note
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<Guid> CreateMentorAndGetProfileIdAsync(string token)
    {
        var response = await CreateMentorAsync(token);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ManagedUserResponse>>())!.Data!.MentorProfileId!.Value;
    }

    private async Task<HttpResponseMessage> GetProfileAsync(string? token, Guid profileId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/mentor-profiles/{profileId}");
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<MentorProfileResponse> ReadProfileAsync(string token, Guid profileId)
    {
        var response = await GetProfileAsync(token, profileId);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
    }

    private async Task<HttpResponseMessage> UpdateProfileAsync(string? token, Guid profileId, UpdateMentorProfileRequest body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/mentor-profiles/{profileId}") { Content = JsonContent.Create(body) };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = "admin@ehub.test", Password = "Admin@123456" });
        return (await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!.AccessToken;
    }

    private async Task<string> CreateLecturerTokenAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await context.Roles.SingleAsync(item => item.Name == SystemRoles.Lecturer);
        var email = $"profile-lecturer-{Guid.NewGuid():N}@example.com";
        var lecturer = new User { FullName = "Profile Lecturer", Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = UserStatus.Active };
        context.Users.Add(lecturer);
        context.UserRoles.Add(new UserRole { UserId = lecturer.Id, User = lecturer, RoleId = role.Id, Role = role, AssignedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        return scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(lecturer, [SystemRoles.Lecturer]).Token;
    }
}
