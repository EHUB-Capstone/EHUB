using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Contracts.Common;
using EHub.Contracts.Mentoring;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Mentoring;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit.Abstractions;

namespace EHub.IntegrationTests.Mentoring;

[Collection("Sequential")]
public sealed class MentorCvUploadIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
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
    public async Task UploadExtractsProfessionalDataAndFeedsMatchingWithoutPrivateSections()
    {
        var ollama = new RecordingOllama();
        using var app = App(ollama);
        using var client = app.CreateClient();
        var mentor = await Identity(SystemRoles.Mentor, withProfile: true);
        var lecturer = await Identity(SystemRoles.Lecturer);
        var teamId = await Team(mentor.Id, lecturer.Id);

        var cvBytes = Docx(SyntheticCv);
        using var upload = Upload(mentor.Token, cvBytes);
        using var response = await client.SendAsync(upload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
        profile.Experience.Should().Contain("Giám đốc Marketing").And.NotContain("PRIVATE");
        profile.Bio.Should().Contain("Đại học Demo");
        profile.Expertise.Should().Contain("Marketing").And.NotContain("Finance");
        profile.TechnologySkills.Should().BeEmpty();

        using var recommend = Authorized(HttpMethod.Get, $"/api/mentoring/teams/{teamId}/recommendations", lecturer.Token);
        using var recommendations = await client.SendAsync(recommend);
        recommendations.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await recommendations.Content.ReadFromJsonAsync<ApiResponse<MentorRecommendationResponse[]>>())!.Data!;
        items.Should().ContainSingle();
        items[0].Mentor.Id.Should().Be(profile.Id);
        ollama.Inputs.Should().Contain(text => text.Contains("Giám đốc Marketing"));
        ollama.Inputs.Should().NotContain(text => text.Contains("PRIVATE") || text.Contains("@example.test"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.MentorEmbeddings.AsNoTracking().AnyAsync(x => x.MentorProfileId == profile.Id)).Should().BeTrue();
        output.WriteLine($"Upload CV: 200; extracted expertise: {string.Join(", ", profile.Expertise)}; matching: 200; score: {items[0].FitScore}; vector cached; private sections excluded.");

        using var download = Authorized(HttpMethod.Get, $"/api/mentoring/profiles/{profile.Id}/documents/cv", mentor.Token);
        using var downloaded = await client.SendAsync(download);
        downloaded.StatusCode.Should().Be(HttpStatusCode.OK);
        (await downloaded.Content.ReadAsByteArrayAsync()).Should().Equal(cvBytes);
    }

    [Fact]
    public async Task TrainedProviderRanksUploadedProfessionalEvidenceWithoutOllamaOrVectorCache()
    {
        var ollama = new RecordingOllama();
        using var app = App(ollama, trained: true);
        using var client = app.CreateClient();
        app.Services.GetRequiredService<IMentorFitRanker>().Should().BeOfType<FileMentorFitRanker>();
        var mentor = await Identity(SystemRoles.Mentor, withProfile: true);
        var lecturer = await Identity(SystemRoles.Lecturer);
        var teamId = await Team(mentor.Id, lecturer.Id);
        const string syntheticSkillsCv = "Education\nDemo Academy\nTechnical skills\nMarketing, sales and negotiation\nPersonal information\nPRIVATE_PERSON_MARKER";
        using var upload = Upload(mentor.Token, Docx(syntheticSkillsCv));
        using var uploaded = await client.SendAsync(upload);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = (await uploaded.Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
        profile.Bio.Should().Contain("Marketing, sales and negotiation").And.NotContain("PRIVATE");
        profile.TechnologySkills.Should().BeEmpty();
        profile.MentorType.Should().Be("Business");

        using var request = Authorized(HttpMethod.Get, $"/api/mentoring/teams/{teamId}/recommendations", lecturer.Token);
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var recommendation = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorRecommendationResponse[]>>())!.Data!.Single();
        recommendation.Mentor.Id.Should().Be(profile.Id);
        recommendation.FitScore.Should().BeInRange(1, 100);
        recommendation.Reasons.Should().Contain(reason => reason.Contains("Marketing", StringComparison.OrdinalIgnoreCase));
        recommendation.Reasons.Should().NotContain(reason => reason.Contains("PRIVATE"));
        ollama.Inputs.Should().BeEmpty();
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().MentorEmbeddings.AsNoTracking()
            .AnyAsync(x => x.MentorProfileId == profile.Id)).Should().BeFalse();
    }

    [LocalSemanticFact]
    public async Task HybridProviderUsesRealLocalOnnxAndUploadedExperienceThroughTheApi()
    {
        var ollama = new RecordingOllama();
        using var app = App(ollama, trained: true, hybrid: true);
        using var client = app.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(60);
        var mentor = await Identity(SystemRoles.Mentor, withProfile: true);
        var lecturer = await Identity(SystemRoles.Lecturer);
        var teamId = await Team(mentor.Id, lecturer.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var team = await db.Teams.SingleAsync(x => x.Id == teamId);
            team.TeamName = "Synthetic cash planning";
            team.Description = "Nhóm muốn biết còn duy trì được bao lâu với số tiền đang có. Cần tính các khoản phải chi mỗi tháng trước khi nhận thêm vốn.";
            await db.SaveChangesAsync();
        }
        using var upload = Upload(mentor.Token, Docx("Work experience\nQuản lý tài chính cho nhóm sáng lập. Lập kế hoạch dòng tiền, ngân sách và thời gian duy trì hoạt động."));
        using var uploaded = await client.SendAsync(upload);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        using var request = Authorized(HttpMethod.Get, $"/api/mentoring/teams/{teamId}/recommendations", lecturer.Token);
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorRecommendationResponse[]>>())!.Data!;
        items.Should().ContainSingle();
        items[0].Reasons.Should().Contain(x => x.Contains("Đối chiếu ngữ nghĩa với kinh nghiệm"));
        ollama.Inputs.Should().BeEmpty();
    }

    [Fact]
    public async Task TrainedProviderReturnsNoSuggestionWhenTheUploadedCvExplicitlyDeniesTheRequiredExpertise()
    {
        var ollama = new RecordingOllama();
        using var app = App(ollama, trained: true);
        using var client = app.CreateClient();
        var mentor = await Identity(SystemRoles.Mentor, withProfile: true);
        var lecturer = await Identity(SystemRoles.Lecturer);
        var teamId = await Team(mentor.Id, lecturer.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var profile = await db.MentorProfiles.SingleAsync(x => x.UserId == mentor.Id);
            profile.MentorType = "IT";
            profile.Expertise = ["AI"]; // Previously inferred tag must not override the new CV denial.
            (await db.Teams.SingleAsync(x => x.Id == teamId)).Description = "AI và computer vision để phân loại ảnh";
            await db.SaveChangesAsync();
        }
        using var upload = Upload(mentor.Token, Docx("Work experience\nBackend Node.js. Không có kinh nghiệm AI, computer vision hoặc machine learning."));
        using var uploaded = await client.SendAsync(upload);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);

        using var request = Authorized(HttpMethod.Get, $"/api/mentoring/teams/{teamId}/recommendations", lecturer.Token);
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ApiResponse<MentorRecommendationResponse[]>>())!.Data.Should().BeEmpty();
        ollama.Inputs.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized, false)]
    [InlineData(SystemRoles.Lecturer, HttpStatusCode.Forbidden, false)]
    [InlineData(SystemRoles.Mentor, HttpStatusCode.Forbidden, false)]
    [InlineData(SystemRoles.Student, HttpStatusCode.Forbidden, false)]
    [InlineData(null, HttpStatusCode.Unauthorized, true)]
    [InlineData(SystemRoles.Lecturer, HttpStatusCode.Forbidden, true)]
    [InlineData(SystemRoles.Mentor, HttpStatusCode.Forbidden, true)]
    [InlineData(SystemRoles.Student, HttpStatusCode.Forbidden, true)]
    public async Task TrainedRecommendationsEnforceAuthenticationRoleAndClassScope(string? role, HttpStatusCode expected, bool hybrid)
    {
        using var app = App(trained: true, hybrid: hybrid);
        using var client = app.CreateClient();
        var mentor = await Identity(SystemRoles.Mentor, withProfile: true);
        var lecturer = await Identity(SystemRoles.Lecturer);
        var teamId = await Team(mentor.Id, lecturer.Id);
        var token = role is null ? null : (await Identity(role)).Token;
        using var request = Authorized(HttpMethod.Get, $"/api/mentoring/teams/{teamId}/recommendations", token);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ExplicitMissingTrainedModelReturns503WithoutLeakingItsPathOrUsingOllama()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ehub-private-model-marker-{Guid.NewGuid():N}", "missing.json");
        var ollama = new RecordingOllama();
        using var app = App(ollama, trained: true, trainedModelPath: path);
        using var client = app.CreateClient();
        var mentor = await Identity(SystemRoles.Mentor, withProfile: true);
        var lecturer = await Identity(SystemRoles.Lecturer);
        var teamId = await Team(mentor.Id, lecturer.Id);
        using var request = Authorized(HttpMethod.Get, $"/api/mentoring/teams/{teamId}/recommendations", lecturer.Token);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(path).And.NotContain("ehub-private-model-marker").And.NotContain("Exception");
        var error = JsonSerializer.Deserialize<ApiResponse<object>>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        error.Code.Should().Be(ErrorCodes.MentorMatchingUnavailable);
        error.Message.Should().Be("Mô hình gợi ý mentor tạm thời chưa sẵn sàng.");
        ollama.Inputs.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(SystemRoles.Student, HttpStatusCode.Forbidden)]
    [InlineData(SystemRoles.Lecturer, HttpStatusCode.Forbidden)]
    [InlineData(SystemRoles.Admin, HttpStatusCode.Forbidden)]
    public async Task UploadEnforcesAuthenticationAndMentorRole(string? role, HttpStatusCode expected)
    {
        using var app = App();
        using var client = app.CreateClient();
        var token = role is null ? null : (await Identity(role)).Token;
        using var upload = Upload(token, Docx(SyntheticCv));
        using var response = await client.SendAsync(upload);
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task InvalidCvFailsWithoutReplacingThePreviousUpload()
    {
        using var app = App();
        using var client = app.CreateClient();
        var mentor = await Identity(SystemRoles.Mentor, withProfile: true);
        using var valid = Upload(mentor.Token, Docx(SyntheticCv));
        (await client.SendAsync(valid)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var invalid = Upload(mentor.Token, "%PDF-invalid"u8.ToArray(), "invalid.pdf");
        (await client.SendAsync(invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var get = Authorized(HttpMethod.Get, "/api/mentoring/profile", mentor.Token);
        var stored = (await (await client.SendAsync(get)).Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
        stored.CvFileName.Should().Be("cv.docx");
        stored.Experience.Should().Contain("Giám đốc Marketing");
        var outsider = await Identity(SystemRoles.Mentor, withProfile: true);
        using var forbidden = Authorized(HttpMethod.Get, $"/api/mentoring/profiles/{stored.Id}/documents/cv", outsider.Token);
        (await client.SendAsync(forbidden)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ManualSkillsStayUnchangedWhenCvIsUploadedReplacedOrRejected()
    {
        using var app = App();
        using var client = app.CreateClient();
        var mentor = await Identity(SystemRoles.Mentor, withProfile: true);

        using var initialSkills = UpdateSkills(mentor.Token, ["Negotiation", "React"]);
        (await client.SendAsync(initialSkills)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var editSkills = UpdateSkills(mentor.Token, [" Negotiation ", "Facilitation"]);
        using var edited = await client.SendAsync(editSkills);
        edited.StatusCode.Should().Be(HttpStatusCode.OK);
        var manualProfile = (await edited.Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
        manualProfile.TechnologySkills.Should().Equal("Negotiation", "Facilitation");

        using var first = Upload(mentor.Token, Docx(SyntheticCv));
        using var uploaded = await client.SendAsync(first);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        (await uploaded.Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!
            .TechnologySkills.Should().Equal("Negotiation", "Facilitation");

        const string replacement = "Education\nDemo University - Software engineering\nWork experience\nBuilt Python and PostgreSQL tools\nSkills: AWS, Azure";
        using var replace = Upload(mentor.Token, Docx(replacement), "replacement.docx");
        using var replaced = await client.SendAsync(replace);
        replaced.StatusCode.Should().Be(HttpStatusCode.OK);
        var replacementProfile = (await replaced.Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
        replacementProfile.TechnologySkills.Should().Equal("Negotiation", "Facilitation");
        replacementProfile.Bio.Should().Be("Demo University - Software engineering\nAWS, Azure");
        replacementProfile.Experience.Should().Be("Built Python and PostgreSQL tools");
        // Preserve the manually entered Finance tag; an education-only degree is
        // kept in Bio without being promoted to declared professional expertise.
        replacementProfile.Expertise.Should().Contain("Finance").And.NotContain("Software engineering");

        using var invalid = Upload(mentor.Token, "%PDF-invalid"u8.ToArray(), "invalid.pdf");
        (await client.SendAsync(invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var get = Authorized(HttpMethod.Get, "/api/mentoring/profile", mentor.Token);
        using var storedResponse = await client.SendAsync(get);
        storedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = (await storedResponse.Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
        stored.TechnologySkills.Should().Equal("Negotiation", "Facilitation");
        stored.CvFileName.Should().Be("replacement.docx");
        stored.Experience.Should().Be("Built Python and PostgreSQL tools");
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("empty")]
    [InlineData("too-long")]
    [InlineData("too-many")]
    public async Task InvalidManualSkillsDoNotReplaceSavedSkills(string invalidCase)
    {
        using var app = App();
        using var client = app.CreateClient();
        var mentor = await Identity(SystemRoles.Mentor, withProfile: true);
        using var valid = UpdateSkills(mentor.Token, ["Negotiation"]);
        (await client.SendAsync(valid)).StatusCode.Should().Be(HttpStatusCode.OK);
        string[] invalidSkills = invalidCase switch
        {
            "duplicate" => ["React", " react "],
            "empty" => [" "],
            "too-long" => [new string('a', 81)],
            _ => Enumerable.Range(1, 21).Select(index => $"Skill {index}").ToArray()
        };
        using var invalid = UpdateSkills(mentor.Token, invalidSkills);
        (await client.SendAsync(invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var get = Authorized(HttpMethod.Get, "/api/mentoring/profile", mentor.Token);
        var stored = (await (await client.SendAsync(get)).Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
        stored.TechnologySkills.Should().Equal("Negotiation");
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(SystemRoles.Student, HttpStatusCode.Forbidden)]
    [InlineData(SystemRoles.Lecturer, HttpStatusCode.Forbidden)]
    [InlineData(SystemRoles.Admin, HttpStatusCode.Forbidden)]
    public async Task ManualSkillsUpdateEnforcesAuthenticationAndMentorRole(string? role, HttpStatusCode expected)
    {
        using var app = App();
        using var client = app.CreateClient();
        var token = role is null ? null : (await Identity(role)).Token;
        using var update = UpdateSkills(token, ["Negotiation"]);
        (await client.SendAsync(update)).StatusCode.Should().Be(expected);
    }

    [RealOllamaFact]
    public async Task LiveBgeM3ReceivesExtractedExperienceAndStoresVector()
    {
        using var app = App();
        using var client = app.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(60);
        var mentor = await Identity(SystemRoles.Mentor, withProfile: true);
        var lecturer = await Identity(SystemRoles.Lecturer);
        var teamId = await Team(mentor.Id, lecturer.Id);
        using var upload = Upload(mentor.Token, Docx(SyntheticCv));
        (await client.SendAsync(upload)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var request = Authorized(HttpMethod.Get, $"/api/mentoring/teams/{teamId}/recommendations", lecturer.Token);
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorRecommendationResponse[]>>())!.Data!.Single();
        item.Mentor.Experience.Should().Contain("Giám đốc Marketing");
        item.FitScore.Should().BeInRange(1, 100);
        using var scope = factory.Services.CreateScope();
        var vector = await scope.ServiceProvider.GetRequiredService<AppDbContext>().MentorEmbeddings.AsNoTracking()
            .SingleAsync(x => x.MentorProfileId == item.Mentor.Id);
        vector.ModelName.Should().Be("bge-m3");
        vector.Embedding.ToArray().Should().HaveCount(1024);
        output.WriteLine($"REAL BGE-M3: CV upload 200; recommendations 200; fit score {item.FitScore}; vector 1024 dimensions persisted in PostgreSQL.");
    }

    private WebApplicationFactory<Program> App(RecordingOllama? ollama = null, bool trained = false,
        string? trainedModelPath = null, bool hybrid = false) => factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mentoring:MatchingProvider"] = hybrid ? "Hybrid" : trained ? "Trained" : "Ollama",
                ["Mentoring:TrainedModelPath"] = trainedModelPath
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMentorDocumentStorageService>();
            services.AddSingleton<IMentorDocumentStorageService, MemoryDocuments>();
            // Minimal-host configuration overrides arrive after Program registers services.
            // Exercise the real file provider here; separate unit tests verify provider selection.
            services.RemoveAll<IMentorFitRanker>();
            if (trained)
                services.AddSingleton<IMentorFitRanker>(provider =>
                    new FileMentorFitRanker(provider.GetRequiredService<IConfiguration>()));
            services.RemoveAll<IMentorSemanticReranker>();
            if (hybrid)
                services.AddHttpClient<IMentorSemanticReranker, LocalSemanticMentorReranker>(client =>
                {
                    client.BaseAddress = new Uri("http://127.0.0.1:8091/");
                    client.Timeout = TimeSpan.FromSeconds(40);
                }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });
            if (ollama is null) return;
            services.RemoveAll<IMentorEmbeddingSearch>();
            services.AddScoped<IMentorEmbeddingSearch>(provider => new OllamaMentorEmbeddingSearch(
                provider.GetRequiredService<AppDbContext>(), new HttpClient(ollama, disposeHandler: false)
                { BaseAddress = new Uri("http://localhost:11434/") }, provider.GetRequiredService<IConfiguration>()));
        });
    });

    private async Task<(Guid Id, string Token)> Identity(string roleName, bool withProfile = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.Roles.SingleAsync(x => x.Name == roleName);
        var email = $"cv-{Guid.NewGuid():N}@example.test";
        var user = new User { FullName = "Synthetic CV Test", Email = email, NormalizedEmail = email.ToUpperInvariant() };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        db.Users.Add(user);
        if (withProfile) db.MentorProfiles.Add(new MentorProfile { User = user, Expertise = ["Manual skill"] });
        await db.SaveChangesAsync();
        return (user.Id, scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [roleName]).Token);
    }

    private async Task<Guid> Team(Guid mentorId, Guid lecturerId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var year = (await db.Semesters.MaxAsync(x => (int?)x.Year) ?? 2026) + 1;
        var semester = new Semester { Code = $"CV{suffix}", Name = "Synthetic CV Semester", Year = year, Term = SemesterTerm.Fall };
        var team = new Team { TeamCode = $"CV{suffix}", TeamName = "Synthetic marketing project",
            Description = "Marketing strategy and finance for a startup product",
            Class = new Class { ClassCode = $"CV{suffix}", Slug = $"cv-{suffix}", ClassIndex = 1,
                CourseId = (await db.Courses.FirstAsync()).Id, Semester = semester, PrimaryLecturerId = lecturerId } };
        db.Teams.Add(team);
        db.SemesterStaffAssignments.Add(new SemesterStaffAssignment { Semester = semester, UserId = mentorId,
            Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active });
        await db.SaveChangesAsync();
        return team.Id;
    }

    private static HttpRequestMessage Upload(string? token, byte[] bytes, string fileName = "cv.docx")
    {
        var request = Authorized(HttpMethod.Post, "/api/mentoring/profile/documents/cv", token);
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", fileName);
        request.Content = form;
        return request;
    }

    private static HttpRequestMessage UpdateSkills(string? token, string[] skills)
    {
        var request = Authorized(HttpMethod.Put, "/api/mentoring/profile", token);
        request.Content = JsonContent.Create(new UpdateMentorProfileRequest
        {
            MentorType = "Business", Expertise = ["Finance"], TechnologySkills = skills
        });
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

    private sealed class MemoryDocuments : IMentorDocumentStorageService
    {
        private readonly Dictionary<string, byte[]> files = [];
        public async Task<Result<(string Url, string PublicId)>> UploadAsync(Stream content, string fileName, Guid mentorId,
            string kind, CancellationToken cancellationToken)
        {
            var id = Guid.NewGuid().ToString("N");
            using var bytes = new MemoryStream();
            await content.CopyToAsync(bytes, cancellationToken);
            files[id] = bytes.ToArray();
            return Result.Success(($"https://storage.example.test/{id}", id));
        }
        public Task<Result<byte[]>> DownloadAsync(string url, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(files[new Uri(url).Segments.Last()]));
        public Task DeleteAsync(string publicId, CancellationToken cancellationToken)
        { files.Remove(publicId); return Task.CompletedTask; }
    }

    private sealed class RecordingOllama : HttpMessageHandler
    {
        public List<string> Inputs { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var inputs = json.RootElement.GetProperty("input").EnumerateArray().Select(x => x.GetString()!).ToArray();
            Inputs.AddRange(inputs);
            var vectors = inputs.Select(_ => { var vector = new float[1024]; vector[0] = 1; return vector; }).ToArray();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { embeddings = vectors }) };
        }
    }
}

public sealed class RealOllamaFactAttribute : FactAttribute
{
    public RealOllamaFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("EHUB_TEST_REAL_OLLAMA") != "1")
            Skip = "Set EHUB_TEST_REAL_OLLAMA=1 with local Ollama/bge-m3 running to test the actual model.";
    }
}

public sealed class LocalSemanticFactAttribute : FactAttribute
{
    public LocalSemanticFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("EHUB_TEST_LOCAL_SEMANTIC") != "1")
            Skip = "Set EHUB_TEST_LOCAL_SEMANTIC=1 with the pinned local ONNX service running to test real semantic inference.";
    }
}
