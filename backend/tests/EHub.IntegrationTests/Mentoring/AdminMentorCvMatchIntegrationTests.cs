using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Xml.Linq;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Contracts.Common;
using EHub.Contracts.Mentoring;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EHub.IntegrationTests.Mentoring;

[Collection("Sequential")]
public sealed class AdminMentorCvMatchIntegrationTests(CustomWebApplicationFactory factory)
{
    private const string MatchUrl = "/api/mentoring/profiles/cv-match";

    [Fact]
    public async Task AdminMatchesTheContactEmailFromAnArbitrarilyNamedCvWithoutSavingOrExposingItsOtherContents()
    {
        var storage = new NoDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var target = await Identity(SystemRoles.Mentor, withProfile: true, type: MentorType.Academic);
        var other = await Identity(SystemRoles.Mentor, withProfile: true);
        var before = await Counts();
        var original = await Snapshot(target.ProfileId!.Value);
        var otherOriginal = await Snapshot(other.ProfileId!.Value);
        var text = $"""
            Personal information
            PRIVATE_PERSON_MARKER
            Email: <{target.Email.ToUpperInvariant()}>.
            Family information
            PRIVATE_FAMILY_MARKER
            Education
            PRIVATE_UNRELATED_BACKGROUND_MARKER
            Work experience
            PRIVATE_UNRELATED_PROFESSIONAL_MARKER
            """;

        using var request = Match(admin.Token, Docx(text), "unrelated-filename.docx");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("PRIVATE")
            .And.NotContain("cvStorageUrl").And.NotContain("cvPublicId").And.NotContain("Original background");
        var result = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorCvMatchResponse>>())!.Data!;
        result.Status.Should().Be("Matched");
        result.MentorProfileId.Should().Be(target.ProfileId.Value);
        result.DetectedEmails.Should().Equal(target.Email);
        (await Counts()).Should().Be(before);
        (await Snapshot(target.ProfileId.Value)).Should().BeEquivalentTo(original);
        (await Snapshot(other.ProfileId.Value)).Should().BeEquivalentTo(otherOriginal);
        storage.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContactOnlyCvCanMatchWithoutRunningTheProfessionalProfileParserOrUploadingTheDocument(bool quoted)
    {
        var storage = new NoDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var target = await Identity(SystemRoles.Mentor, withProfile: true);
        var original = await Snapshot(target.ProfileId!.Value);

        var contact = quoted ? $"'{target.Email}'" : target.Email;
        using var request = Match(admin.Token, Docx($"Personal information\nEmail: {contact}\nPRIVATE_PERSON_MARKER"));
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorCvMatchResponse>>())!.Data!;
        result.Status.Should().Be("Matched");
        result.MentorProfileId.Should().Be(target.ProfileId);
        (await Snapshot(target.ProfileId.Value)).Should().BeEquivalentTo(original);
        storage.Calls.Should().Be(0);
    }

    [Fact]
    public async Task RepeatedEmailAndUnknownContactsStillSelectOnlyTheSingleExistingMentor()
    {
        var storage = new NoDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var target = await Identity(SystemRoles.Mentor, withProfile: true);
        var unknown = $"unknown-{Guid.NewGuid():N}@example.test";
        var text = $"Email: {target.Email}\nAlternate: {target.Email.ToUpperInvariant()}\nContact: {unknown}";

        using var request = Match(admin.Token, Docx(text));
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorCvMatchResponse>>())!.Data!;
        result.Status.Should().Be("Matched");
        result.MentorProfileId.Should().Be(target.ProfileId);
        result.DetectedEmails.Should().BeEquivalentTo([target.Email, unknown]);
        storage.Calls.Should().Be(0);
    }

    [Fact]
    public async Task TwoExistingMentorEmailsAreAmbiguousAndNeverSelectOneByFilenameOrOrder()
    {
        var storage = new NoDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var first = await Identity(SystemRoles.Mentor, withProfile: true);
        var second = await Identity(SystemRoles.Mentor, withProfile: true);
        var before = await Counts();

        using var request = Match(admin.Token, Docx($"Email: {second.Email}\nReference: {first.Email}"),
            "Synthetic CV match mentor.docx");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorCvMatchResponse>>())!.Data!;
        result.Status.Should().Be("Ambiguous");
        result.MentorProfileId.Should().BeNull();
        result.DetectedEmails.Should().BeEquivalentTo([first.Email, second.Email]);
        (await Counts()).Should().Be(before);
        storage.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("no-email", "NoEmail")]
    [InlineData("unknown-email", "NoMatch")]
    [InlineData("account-without-mentor-profile", "NoMatch")]
    public async Task MissingOrUnregisteredMentorEmailRequiresManualSelection(string source, string expectedStatus)
    {
        var storage = new NoDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var nonMentor = await Identity(SystemRoles.Student);
        var text = source switch
        {
            "no-email" => "Work experience\nMarketing strategy and finance. PRIVATE_BODY_MARKER",
            "account-without-mentor-profile" => $"Contact email: {nonMentor.Email}",
            _ => $"Email: unknown-{Guid.NewGuid():N}@example.test"
        };
        var before = await Counts();

        using var request = Match(admin.Token, Docx(text), "CV Synthetic Mentor.docx");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("PRIVATE");
        var result = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorCvMatchResponse>>())!.Data!;
        result.Status.Should().Be(expectedStatus);
        result.MentorProfileId.Should().BeNull();
        if (source == "no-email") result.DetectedEmails.Should().BeEmpty();
        (await Counts()).Should().Be(before);
        storage.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("dots")]
    [InlineData("plus")]
    [InlineData("same-domain")]
    public async Task MatchingDoesNotGuessGmailAliasesOrMatchOnlyTheDomain(string transformation)
    {
        var storage = new NoDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var local = $"cv.match.{Guid.NewGuid():N}";
        await Identity(SystemRoles.Mentor, withProfile: true, email: local + "@gmail.com");
        var candidate = transformation switch
        {
            "dots" => local.Replace(".", string.Empty) + "@gmail.com",
            "plus" => local + "+cv@gmail.com",
            _ => $"different-{Guid.NewGuid():N}@gmail.com"
        };

        using var request = Match(admin.Token, Docx($"Email: {candidate}"));
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorCvMatchResponse>>())!.Data!;
        result.Status.Should().Be("NoMatch");
        result.MentorProfileId.Should().BeNull();
        result.DetectedEmails.Should().Contain(candidate);
        storage.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ExcessEmailAddressesNeverProduceAnAutomaticTargetFromATruncatedList()
    {
        var storage = new NoDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var target = await Identity(SystemRoles.Mentor, withProfile: true);
        var suffix = Guid.NewGuid().ToString("N");
        var emails = new[] { target.Email }.Concat(Enumerable.Range(0, 20).Select(index => $"contact-{index}-{suffix}@example.test"));

        using var request = Match(admin.Token, Docx(string.Join('\n', emails)));
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorCvMatchResponse>>())!.Data!;
        result.Status.Should().Be("Ambiguous");
        result.MentorProfileId.Should().BeNull();
        result.DetectedEmails.Length.Should().BeLessThanOrEqualTo(20);
        storage.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(SystemRoles.Student, HttpStatusCode.Forbidden)]
    [InlineData(SystemRoles.Lecturer, HttpStatusCode.Forbidden)]
    [InlineData(SystemRoles.Mentor, HttpStatusCode.Forbidden)]
    public async Task ReadingCvEmailsRequiresAnAdministrator(string? role, HttpStatusCode expected)
    {
        var storage = new NoDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var target = await Identity(SystemRoles.Mentor, withProfile: true);
        var actor = role is null ? null : await Identity(role);
        var before = await Counts();

        using var request = Match(actor?.Token, Docx($"Email: {target.Email}"));
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(expected);
        (await Counts()).Should().Be(before);
        storage.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("extension")]
    [InlineData("signature")]
    [InlineData("too-large")]
    public async Task InvalidDocumentsReturnValidationErrorsWithoutSavingOrLeakingTheirContents(string source)
    {
        var storage = new NoDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var (bytes, name) = source switch
        {
            "missing" => ((byte[]?)null, "cv.docx"),
            "empty" => (Array.Empty<byte>(), "cv.docx"),
            "extension" => (Docx("PRIVATE_BODY_MARKER"), "cv.exe"),
            "signature" => (Encoding.UTF8.GetBytes("PRIVATE_BODY_MARKER"), "cv.docx"),
            _ => (new byte[10 * 1024 * 1024 + 1], "cv.pdf")
        };
        var before = await Counts();

        using var request = Match(admin.Token, bytes, name);
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("PRIVATE").And.NotContain("Exception");
        (await Counts()).Should().Be(before);
        storage.Calls.Should().Be(0);
    }

    private WebApplicationFactory<Program> App(NoDocuments storage) => factory.WithWebHostBuilder(builder =>
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMentorDocumentStorageService>();
            services.AddSingleton<IMentorDocumentStorageService>(storage);
        }));

    private async Task<TestIdentity> Identity(string roleName, bool withProfile = false,
        MentorType type = MentorType.Enterprise, string? email = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.Roles.SingleAsync(row => row.Name == roleName);
        email ??= $"cv-match-{Guid.NewGuid():N}@example.test";
        var user = new User { FullName = "Synthetic CV match mentor", Email = email, NormalizedEmail = email.ToUpperInvariant() };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        db.Users.Add(user);
        MentorProfile? profile = null;
        if (withProfile)
        {
            profile = new MentorProfile
            {
                User = user, Type = type, Bio = "Original background", Experience = "Original professional evidence",
                Expertise = ["Manual skill"], TechnologySkills = ["Manual TypeScript"], Tags = ["Keep label"],
                CvStorageUrl = "https://storage.example.test/existing-synthetic-cv", CvPublicId = "existing-synthetic-cv",
                CvFileName = "existing-cv.docx"
            };
            db.MentorProfiles.Add(profile);
        }
        await db.SaveChangesAsync();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [roleName]).Token;
        return new TestIdentity(profile?.Id, email, token);
    }

    private async Task<(int Users, int Profiles, int Assignments, int Outbox)> Counts()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Users.CountAsync(), await db.MentorProfiles.CountAsync(),
            await db.MentorAssignments.CountAsync(), await db.OutboxMessages.CountAsync());
    }

    private async Task<ProfileSnapshot> Snapshot(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var profile = await scope.ServiceProvider.GetRequiredService<AppDbContext>().MentorProfiles.AsNoTracking()
            .SingleAsync(row => row.Id == id);
        return new ProfileSnapshot(profile.Version, profile.Type, profile.Bio, profile.Experience,
            profile.CvFileName, profile.CvPublicId, profile.CvStorageUrl, profile.Expertise,
            profile.TechnologySkills, profile.Tags, profile.UpdatedAt);
    }

    private static HttpRequestMessage Match(string? token, byte[]? bytes, string name = "cv.docx")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, MatchUrl);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var form = new MultipartFormDataContent();
        if (bytes is not null) form.Add(new ByteArrayContent(bytes), "file", name);
        request.Content = form;
        return request;
    }

    private static byte[] Docx(string text)
    {
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var document = new XElement(word + "document", new XElement(word + "body", text.Split('\n')
            .Select(line => new XElement(word + "p", new XElement(word + "r", new XElement(word + "t", line))))));
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open(), new UTF8Encoding(false)))
            writer.Write(document.ToString());
        return memory.ToArray();
    }

    private sealed record TestIdentity(Guid? ProfileId, string Email, string Token);
    private sealed record ProfileSnapshot(uint Version, MentorType Type, string? Bio, string? Experience,
        string? CvFileName, string? CvPublicId, string? CvStorageUrl, string[] Expertise,
        string[] TechnologySkills, string[] Tags, DateTime? UpdatedAt);

    private sealed class NoDocuments : IMentorDocumentStorageService
    {
        public int Calls { get; private set; }
        public Task<Result<(string Url, string PublicId)>> UploadAsync(Stream content, string fileName,
            Guid mentorId, string kind, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("A CV match preview must never upload a document.");
        }

        public Task<Result<byte[]>> DownloadAsync(string url, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("A CV match preview must never download a stored document.");
        }

        public Task DeleteAsync(string publicId, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("A CV match preview must never delete a document.");
        }
    }
}
