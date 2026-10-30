using System.Collections.Concurrent;
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
public sealed class AdminMentorCvUploadIntegrationTests(CustomWebApplicationFactory factory)
{
    private const string SyntheticCv = """
        I. THÔNG TIN CÁ NHÂN
        PRIVATE_PERSON_MARKER
        II. THÔNG TIN NGƯỜI THÂN
        PRIVATE_FAMILY_MARKER
        III. QUÁ TRÌNH ĐÀO TẠO
        Đại học Demo - Tài chính
        IV. KINH NGHIỆM LÀM VIỆC
        Demo Growth - Giám đốc Marketing
        Chiến lược marketing và điều phối dự án
        NGƯỜI KHAI
        PRIVATE_SIGNATURE_MARKER
        """;

    [Fact]
    public async Task AdminAttachesTheOriginalCvToTheSelectedMentorAndExtractsOnlyProfessionalEvidence()
    {
        var storage = new MemoryDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var selected = await Identity(SystemRoles.Mentor, withProfile: true, MentorType.Academic);
        var other = await Identity(SystemRoles.Mentor, withProfile: true);
        var counts = await Counts();
        var bytes = Docx(SyntheticCv);

        using var upload = Upload(admin.Token, selected.ProfileId!.Value, bytes, "selected-mentor.docx");
        using var response = await client.SendAsync(upload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("PRIVATE").And.NotContain("cvStorageUrl").And.NotContain("cvPublicId");
        var profile = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
        profile.Id.Should().Be(selected.ProfileId.Value);
        profile.UserId.Should().Be(selected.UserId);
        profile.CvFileName.Should().Be("selected-mentor.docx");
        profile.Bio.Should().Contain("Đại học Demo");
        profile.Experience.Should().Contain("Giám đốc Marketing");
        profile.Expertise.Should().Contain("Marketing").And.Contain("Manual skill").And.NotContain("Finance");
        profile.MentorType.Should().Be("IT");
        profile.TechnologySkills.Should().Equal("Manual TypeScript");
        profile.Tags.Should().Equal("Keep label");
        profile.Experiences.Should().ContainSingle().Which.Area.Should().Be("Facilitation");
        (await Counts()).Should().Be(counts, "uploading a CV never creates accounts or mentor profiles");

        var stored = await Stored(selected.ProfileId.Value);
        stored.CvStorageUrl.Should().NotBeNullOrWhiteSpace();
        stored.Type.Should().Be(MentorType.Academic);
        stored.User.Email.Should().Be(selected.Email);
        var untouched = await Stored(other.ProfileId!.Value);
        untouched.CvFileName.Should().BeNull();
        untouched.Bio.Should().Be("Original background");
        untouched.Experience.Should().Be("Original professional evidence");
        storage.Uploads.Should().ContainSingle().Which.MentorId.Should().Be(selected.ProfileId.Value);

        foreach (var token in new[] { admin.Token, selected.Token })
        {
            using var download = Authorized(HttpMethod.Get,
                $"/api/mentoring/profiles/{selected.ProfileId.Value}/documents/cv", token);
            using var downloaded = await client.SendAsync(download);
            downloaded.StatusCode.Should().Be(HttpStatusCode.OK);
            downloaded.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("selected-mentor.docx");
            (await downloaded.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
        }
    }

    [Theory]
    [InlineData(null, false, HttpStatusCode.Unauthorized)]
    [InlineData(SystemRoles.Student, false, HttpStatusCode.Forbidden)]
    [InlineData(SystemRoles.Lecturer, false, HttpStatusCode.Forbidden)]
    [InlineData(SystemRoles.Mentor, false, HttpStatusCode.Forbidden)]
    [InlineData(SystemRoles.Mentor, true, HttpStatusCode.Forbidden)]
    public async Task UploadRequiresAnAdminEvenWhenTheMentorOwnsTheTargetProfile(string? role, bool ownTarget, HttpStatusCode expected)
    {
        var storage = new MemoryDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var target = await Identity(SystemRoles.Mentor, withProfile: true);
        var actor = role is null ? null : ownTarget ? target : await Identity(role);

        using var request = Upload(actor?.Token, target.ProfileId!.Value, Docx(SyntheticCv));
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(expected);
        (await Stored(target.ProfileId.Value)).CvFileName.Should().BeNull();
        storage.Uploads.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownProfileReturnsNotFoundAndDoesNotStoreTheFileOrCreateAMentor()
    {
        var storage = new MemoryDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var counts = await Counts();

        using var request = Upload(admin.Token, Guid.NewGuid(), Docx(SyntheticCv));
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Counts()).Should().Be(counts);
        storage.Uploads.Should().BeEmpty();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("extension")]
    [InlineData("signature")]
    [InlineData("no-professional-sections")]
    [InlineData("too-large")]
    public async Task InvalidCvLeavesTheSelectedMentorAndStorageUntouched(string invalidCase)
    {
        var storage = new MemoryDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var target = await Identity(SystemRoles.Mentor, withProfile: true);
        var (bytes, name) = invalidCase switch
        {
            "missing" => ((byte[]?)null, "cv.docx"),
            "empty" => (Array.Empty<byte>(), "cv.docx"),
            "extension" => (Docx(SyntheticCv), "cv.exe"),
            "signature" => (Encoding.UTF8.GetBytes("not a Word file"), "cv.docx"),
            "no-professional-sections" => (Docx("Personal information\nPRIVATE_PERSON_MARKER\nSignature\nPRIVATE_SIGNATURE_MARKER"), "cv.docx"),
            _ => (new byte[10 * 1024 * 1024 + 1], "cv.pdf")
        };

        using var request = Upload(admin.Token, target.ProfileId!.Value, bytes, name);
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("PRIVATE").And.NotContain("Exception");
        var stored = await Stored(target.ProfileId.Value);
        stored.CvFileName.Should().BeNull();
        stored.Bio.Should().Be("Original background");
        stored.Experience.Should().Be("Original professional evidence");
        stored.TechnologySkills.Should().Equal("Manual TypeScript");
        storage.Uploads.Should().BeEmpty();
    }

    [Fact]
    public async Task ExistingCvRequiresExplicitReplacementAndRetainsItsOriginalFileUntilReplacementSucceeds()
    {
        var storage = new MemoryDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var target = await Identity(SystemRoles.Mentor, withProfile: true);
        var firstBytes = Docx(SyntheticCv);
        using (var first = Upload(admin.Token, target.ProfileId!.Value, firstBytes, "original.docx"))
        using (var saved = await client.SendAsync(first)) saved.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldId = (await Stored(target.ProfileId!.Value)).CvPublicId!;
        var replacement = Docx("Education\nDemo Technical Academy\nWork experience\nBuilt Python and PostgreSQL tools");

        using (var second = Upload(admin.Token, target.ProfileId.Value, replacement, "replacement.docx"))
        using (var rejected = await client.SendAsync(second)) rejected.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var retained = await Stored(target.ProfileId.Value);
        retained.CvFileName.Should().Be("original.docx");
        retained.CvPublicId.Should().Be(oldId);
        retained.Experience.Should().Contain("Giám đốc Marketing");
        storage.Files.Should().ContainSingle().Which.Value.Should().Equal(firstBytes);
        storage.Uploads.Should().ContainSingle();
        storage.DeletedIds.Should().BeEmpty();

        using (var second = Upload(admin.Token, target.ProfileId.Value, replacement, "replacement.docx", replaceExisting: true))
        using (var accepted = await client.SendAsync(second)) accepted.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await Stored(target.ProfileId.Value);
        updated.CvFileName.Should().Be("replacement.docx");
        updated.CvPublicId.Should().NotBe(oldId);
        updated.Experience.Should().Be("Built Python and PostgreSQL tools");
        updated.TechnologySkills.Should().Equal("Manual TypeScript");
        storage.DeletedIds.Should().Equal(oldId);
        storage.Files.Should().ContainSingle().Which.Value.Should().Equal(replacement);
    }

    [Fact]
    public async Task ConcurrentProfileEditReturnsConflictAndDeletesOnlyTheNewFile()
    {
        var storage = new MemoryDocuments();
        using var app = App(storage);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var target = await Identity(SystemRoles.Mentor, withProfile: true);
        var original = Docx(SyntheticCv);
        using (var first = Upload(admin.Token, target.ProfileId!.Value, original, "original.docx"))
        using (var saved = await client.SendAsync(first)) saved.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldId = (await Stored(target.ProfileId!.Value)).CvPublicId!;
        storage.BeforeUploadReturns = async () =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.MentorProfiles.SingleAsync(profile => profile.Id == target.ProfileId.Value)).Bio = "Concurrent saved background";
            await db.SaveChangesAsync();
        };

        using var request = Upload(admin.Token, target.ProfileId.Value,
            Docx("Work experience\nBuilt Python and PostgreSQL tools"), "conflicting.docx", replaceExisting: true);
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("Exception").And.NotContain("xmin");
        var stored = await Stored(target.ProfileId.Value);
        stored.CvFileName.Should().Be("original.docx");
        stored.CvPublicId.Should().Be(oldId);
        stored.Bio.Should().Be("Concurrent saved background");
        stored.Experience.Should().Contain("Giám đốc Marketing");
        storage.Uploads.Should().HaveCount(2);
        storage.DeletedIds.Should().ContainSingle().Which.Should().Be(storage.Uploads[1].PublicId);
        storage.DeletedIds.Should().NotContain(oldId);
        storage.Files.Should().ContainSingle().Which.Value.Should().Equal(original);
    }

    private WebApplicationFactory<Program> App(MemoryDocuments storage) => factory.WithWebHostBuilder(builder =>
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMentorDocumentStorageService>();
            services.AddSingleton<IMentorDocumentStorageService>(storage);
        }));

    private async Task<TestIdentity> Identity(string roleName, bool withProfile = false, MentorType type = MentorType.Enterprise)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.Roles.SingleAsync(row => row.Name == roleName);
        var email = $"admin-cv-{Guid.NewGuid():N}@example.test";
        var user = new User { FullName = "Synthetic admin CV mentor", Email = email, NormalizedEmail = email.ToUpperInvariant() };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        db.Users.Add(user);
        MentorProfile? profile = null;
        if (withProfile)
        {
            profile = new MentorProfile
            {
                User = user, Type = type, Bio = "Original background", Experience = "Original professional evidence",
                Expertise = ["Manual skill"], TechnologySkills = ["Manual TypeScript"], Tags = ["Keep label"],
                Experiences = [new MentorExperience { Kind = "Technology", Area = "Facilitation", Years = 2 }]
            };
            db.MentorProfiles.Add(profile);
        }
        await db.SaveChangesAsync();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [roleName]).Token;
        return new TestIdentity(user.Id, profile?.Id, email, token);
    }

    private async Task<(int Users, int Profiles)> Counts()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Users.CountAsync(), await db.MentorProfiles.CountAsync());
    }

    private async Task<MentorProfile> Stored(Guid profileId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().MentorProfiles.AsNoTracking()
            .Include(profile => profile.User).Include(profile => profile.Experiences).SingleAsync(profile => profile.Id == profileId);
    }

    private static HttpRequestMessage Upload(string? token, Guid mentorId, byte[]? bytes,
        string name = "cv.docx", bool? replaceExisting = null)
    {
        var request = Authorized(HttpMethod.Post, $"/api/mentoring/profiles/{mentorId}/documents/cv", token);
        var form = new MultipartFormDataContent();
        if (bytes is not null) form.Add(new ByteArrayContent(bytes), "file", name);
        if (replaceExisting.HasValue) form.Add(new StringContent(replaceExisting.Value ? "true" : "false"), "replaceExisting");
        request.Content = form;
        return request;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string? token)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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

    private sealed record TestIdentity(Guid UserId, Guid? ProfileId, string Email, string Token);

    private sealed class MemoryDocuments : IMentorDocumentStorageService
    {
        public ConcurrentDictionary<string, byte[]> Files { get; } = new();
        public List<(string PublicId, Guid MentorId)> Uploads { get; } = [];
        public List<string> DeletedIds { get; } = [];
        public Func<Task>? BeforeUploadReturns { get; set; }

        public async Task<Result<(string Url, string PublicId)>> UploadAsync(Stream content, string fileName, Guid mentorId,
            string kind, CancellationToken cancellationToken)
        {
            var id = Guid.NewGuid().ToString("N");
            using var bytes = new MemoryStream();
            await content.CopyToAsync(bytes, cancellationToken);
            Files[id] = bytes.ToArray();
            Uploads.Add((id, mentorId));
            if (BeforeUploadReturns is { } callback)
            {
                BeforeUploadReturns = null;
                await callback();
            }
            return Result.Success(($"https://storage.example.test/{id}", id));
        }

        public Task<Result<byte[]>> DownloadAsync(string url, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(Files[new Uri(url).Segments.Last()]));

        public Task DeleteAsync(string publicId, CancellationToken cancellationToken)
        {
            DeletedIds.Add(publicId);
            Files.TryRemove(publicId, out _);
            return Task.CompletedTask;
        }
    }
}
