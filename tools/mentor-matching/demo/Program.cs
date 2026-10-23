using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Application.Features.Mentoring;
using EHub.Contracts.Mentoring;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Mentoring;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Synthetic evaluation only: no production database, external storage or training.
var startedAtUtc = DateTime.UtcNow;
var totalTimer = Stopwatch.StartNew();
var root = FindRepositoryRoot(args.FirstOrDefault());
var directory = Path.Combine(root, "tools", "mentor-matching", "demo");
var mentorsPath = Path.Combine(directory, "mentors.json");
var inputFileName = args.Skip(1).FirstOrDefault() ?? "projects.json";
if (inputFileName is not ("projects.json" or "holdout-projects.json" or "semantic-projects.json" or "semantic-verification-projects.json"))
    throw new InvalidDataException("Choose the fixed synthetic regression or holdout input.");
var providerName = args.Skip(2).FirstOrDefault() ?? "Trained";
if (providerName is not ("Trained" or "Hybrid")) throw new InvalidDataException("Choose Trained or Hybrid.");
var projectsPath = Path.Combine(directory, inputFileName);
var modelPath = Path.Combine(root, "backend", "src", "EHub.Infrastructure", "Mentoring", "Models", "bootstrap-model.json");
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};
var mentorsDocument = JsonSerializer.Deserialize<MentorInputDocument>(await File.ReadAllTextAsync(mentorsPath), options)
    ?? throw new InvalidDataException("Missing synthetic mentor input.");
var projectsDocument = JsonSerializer.Deserialize<ProjectInputDocument>(await File.ReadAllTextAsync(projectsPath), options)
    ?? throw new InvalidDataException("Missing synthetic project input.");
if (!mentorsDocument.IsSynthetic || !projectsDocument.IsSynthetic)
    throw new InvalidDataException("This demo only accepts explicitly synthetic input.");
if (mentorsDocument.Mentors.Length == 0 || projectsDocument.Projects.Length == 0 ||
    mentorsDocument.Mentors.Select(x => x.Id).Distinct().Count() != mentorsDocument.Mentors.Length ||
    projectsDocument.Projects.Select(x => x.Id).Distinct().Count() != projectsDocument.Projects.Length ||
    mentorsDocument.Mentors.Any(x => x.Type is not ("Business" or "IT")))
    throw new InvalidDataException("Synthetic input contains missing, duplicated or invalid identities.");
var allMentorIds = mentorsDocument.Mentors.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
if (projectsDocument.Projects.SelectMany(x => x.ExpectedBusiness.Concat(x.ExpectedIT)).Any(x => !allMentorIds.Contains(x)))
    throw new InvalidDataException("An expectation references an unknown synthetic mentor.");

var modelHashBefore = Sha256(modelPath);
var implementationPaths = new[]
{
    "backend/src/EHub.Application/Features/Mentoring/MentorProfileHandler.cs",
    "backend/src/EHub.Application/Features/Mentoring/ExtractCv/MentorCvProfileParser.cs",
    "backend/src/EHub.Infrastructure/Mentoring/MentorCvTextReader.cs",
    "backend/src/EHub.Infrastructure/Mentoring/FileMentorFitRanker.cs",
    "backend/src/EHub.Infrastructure/Mentoring/LocalSemanticMentorReranker.cs",
    "tools/mentor-matching/semantic/encoder.py",
    "tools/mentor-matching/semantic/server.py",
    "tools/mentor-matching/semantic/prepare_model.py",
    "tools/mentor-matching/demo/Program.cs"
}.Concat(Directory.GetFiles(Path.Combine(root, "backend/src/EHub.Application/Features/Mentoring/TrainedRanking"), "*.cs")
    .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))).Order().ToArray();
var implementationHashes = implementationPaths.ToDictionary(path => path, path => Sha256(Path.Combine(root, path)));
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Mentoring:MatchingProvider"] = providerName,
    ["Mentoring:TrainedModelPath"] = modelPath
}).Build();
var ranker = new FileMentorFitRanker(configuration);
using var semanticClient = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
{ BaseAddress = LocalSemanticMentorReranker.ServiceUri(configuration), Timeout = TimeSpan.FromSeconds(40) };
IMentorSemanticReranker? semantic = providerName == "Hybrid" ? new LocalSemanticMentorReranker(semanticClient, configuration) : null;
var storage = new InMemoryMentorStorage();
var embeddings = new ForbiddenEmbeddingSearch();
var converter = new ForbiddenDocumentConverter();
await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase($"mentor-synthetic-demo-{Guid.NewGuid():N}").Options);
var reader = new MentorCvTextReader(converter);
var handler = new MentorProfileHandler(context, storage, embeddings, reader, ranker, semantic);
var lecturer = new User
{
    FullName = "Giảng viên mô phỏng",
    Email = "lecturer-demo@example.test",
    Status = UserStatus.Active
};
var semester = new Semester { Code = "DEMO2026", Name = "Học kỳ mô phỏng", Year = 2026 };
var classroom = new Class
{
    ClassCode = "STARTUP-DEMO",
    PrimaryLecturer = lecturer,
    PrimaryLecturerId = lecturer.Id,
    Semester = semester,
    Course = new Course { Code = "EXE-DEMO", Name = "Khởi nghiệp mô phỏng" },
    Status = ClassStatus.Active
};
context.Classes.Add(classroom);
var profiles = new Dictionary<string, MentorProfile>(StringComparer.Ordinal);
foreach (var input in mentorsDocument.Mentors)
{
    var user = new User
    {
        FullName = input.Name,
        Email = $"{input.Id.ToLowerInvariant()}@example.test",
        Status = UserStatus.Active
    };
    var profile = new MentorProfile
    {
        User = user,
        UserId = user.Id,
        MentorType = input.Type,
        Status = MentorProfileStatus.Active
    };
    profiles.Add(input.Id, profile);
    context.MentorProfiles.Add(profile);
    context.SemesterStaffAssignments.Add(new SemesterStaffAssignment
    {
        Semester = semester,
        User = user,
        UserId = user.Id,
        Role = SemesterStaffRole.Mentor,
        Status = SemesterStaffStatus.Active
    });
}
var teams = new Dictionary<string, Team>(StringComparer.Ordinal);
foreach (var input in projectsDocument.Projects)
{
    var team = new Team
    {
        TeamCode = input.Id,
        TeamName = input.Name,
        // The user supplies only a topic/title and one project description.
        Description = input.Description,
        Class = classroom
    };
    teams.Add(input.Id, team);
    context.Teams.Add(team);
}
await context.SaveChangesAsync();

var uploads = new List<UploadResult>();
foreach (var input in mentorsDocument.Mentors)
{
    var timer = Stopwatch.StartNew();
    var content = CreateDocx(input.CvText);
    await using var stream = new MemoryStream(content);
    var result = await handler.UploadDocumentAsync(profiles[input.Id].UserId, "cv", stream,
        $"{input.Id}-synthetic-cv.docx", content.Length, CancellationToken.None);
    var parsed = result.IsSuccess ? result.Value : null;
    var professionalText = parsed is null ? "" : ProfessionalText(parsed);
    uploads.Add(new UploadResult
    {
        MentorId = input.Id,
        Name = input.Name,
        Type = input.Type,
        Succeeded = result.IsSuccess,
        ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds,
        ErrorCode = result.IsFailure ? result.Error.Code : null,
        ErrorMessage = result.IsFailure ? result.Error.Message : null,
        SourceContainsPrivateMarker = input.CvText.Contains("PRIVATE_", StringComparison.OrdinalIgnoreCase),
        PrivateMarkerExcluded = !professionalText.Contains("PRIVATE_", StringComparison.OrdinalIgnoreCase),
        HasProfessionalText = !string.IsNullOrWhiteSpace(professionalText),
        Bio = parsed?.Bio,
        Experience = parsed?.Experience,
        Expertise = parsed?.Expertise ?? [],
        StartupDomains = parsed?.StartupDomains ?? [],
        TechnologySkills = parsed?.TechnologySkills ?? [],
        CvFileName = parsed?.CvFileName
    });
}
// Ensure recommendations read persisted profile fields rather than tracked upload responses.
context.ChangeTracker.Clear();
var mentorIdsByProfile = profiles.ToDictionary(x => x.Value.Id, x => x.Key);
var evaluations = new List<ProjectEvaluation>();
foreach (var input in projectsDocument.Projects)
{
    var timer = Stopwatch.StartNew();
    var result = await handler.RecommendAsync(teams[input.Id].Id, lecturer.Id,
        SystemRoles.Lecturer, CancellationToken.None);
    var recommendations = result.IsSuccess ? result.Value.ToArray() : [];
    var business = ToRanked(recommendations.Where(x => x.Mentor.MentorType == "Business"), mentorIdsByProfile);
    var it = ToRanked(recommendations.Where(x => x.Mentor.MentorType == "IT"), mentorIdsByProfile);
    var checks = input.Checks.Select(check => CheckEdgeCase(check, result.IsSuccess,
        result.IsFailure ? result.Error.Code : null, business, it, uploads)).ToArray();
    evaluations.Add(new ProjectEvaluation
    {
        Id = input.Id,
        Name = input.Name,
        Kind = input.Kind,
        Description = input.Description,
        ExpectationNote = input.ExpectationNote,
        Succeeded = result.IsSuccess,
        ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds,
        ErrorCode = result.IsFailure ? result.Error.Code : null,
        ErrorMessage = result.IsFailure ? result.Error.Message : null,
        Business = business,
        It = it,
        BusinessExpectation = Compare(input.ExpectedBusiness, business),
        ItExpectation = Compare(input.ExpectedIT, it),
        Checks = checks,
        PrivateMarkerExcludedFromReasons = recommendations.All(x =>
            x.Reasons.All(reason => !reason.Contains("PRIVATE_", StringComparison.OrdinalIgnoreCase)))
    });
}
var modelHashAfter = Sha256(modelPath);
var standardComparisons = evaluations.Where(x => x.Kind is "standard" or "holdout" or "semantic")
    .SelectMany(x => new[] { x.BusinessExpectation, x.ItExpectation })
    .Where(x => x.HasDeclaredExpectation).ToArray();
var edgeChecks = evaluations.SelectMany(x => x.Checks).ToArray();
totalTimer.Stop();
var report = new
{
    IsSynthetic = true,
    Description = "Chạy kiểm tra mô phỏng bằng hồ sơ CV và dự án tự tạo. Kỳ vọng tác giả được khai báo trước khi chạy, không phải nhãn chuẩn hoặc độ chính xác ngoài thực tế. Không huấn luyện hoặc chỉnh model bằng dữ liệu demo.",
    StartedAtUtc = startedAtUtc,
    CompletedAtUtc = DateTime.UtcNow,
    TotalElapsedMilliseconds = totalTimer.Elapsed.TotalMilliseconds,
    Pipeline = new
    {
        CvInput = "Synthetic DOCX generated in memory from mentors.json",
        Upload = nameof(MentorProfileHandler.UploadDocumentAsync),
        TextReader = nameof(MentorCvTextReader),
        Recommendation = nameof(MentorProfileHandler.RecommendAsync),
        Ranker = providerName == "Hybrid" ? "FileMentorFitRanker + local multilingual ONNX semantics" : nameof(FileMentorFitRanker),
        SemanticModel = providerName == "Hybrid" ? LocalSemanticMentorReranker.ModelId : null,
        SemanticRevision = providerName == "Hybrid" ? LocalSemanticMentorReranker.ModelRevision : null,
        Database = "Unique EF Core InMemory database; no production database",
        Storage = "In-memory fake; no external upload",
        RoleFiltering = "Business and IT lists are filtered from the same actual API-handler response",
        EmbeddingCalls = embeddings.Calls,
        DocumentConverterCalls = converter.Calls,
        ActualStorageUploadCount = storage.UploadCount,
        ProjectText = "Only Team.Description + TeamName; no Business/IT needs, technology, tags, rubric or expected IDs",
        TimingsNote = "Observed local handler timings, including in-memory persistence and first model load; not VPS latency or a production benchmark"
    },
    Model = new
    {
        Path = Path.GetRelativePath(root, modelPath).Replace('\\', '/'),
        Sha256Before = modelHashBefore,
        Sha256After = modelHashAfter,
        Unchanged = modelHashBefore == modelHashAfter,
        Retrained = false
    },
    Inputs = new
    {
        MentorsSha256 = Sha256(mentorsPath),
        ProjectsFile = inputFileName,
        ProjectsSha256 = Sha256(projectsPath),
        MentorCount = mentorsDocument.Mentors.Length,
        ProjectCount = projectsDocument.Projects.Length
    },
    Implementation = new
    {
        SourceHashes = implementationHashes,
        SourcesUnchangedDuringRun = implementationHashes.All(pair => Sha256(Path.Combine(root, pair.Key)) == pair.Value),
        ApplicationAssemblySha256 = Sha256(typeof(MentorProfileHandler).Assembly.Location),
        InfrastructureAssemblySha256 = Sha256(typeof(FileMentorFitRanker).Assembly.Location)
    },
    Summary = new
    {
        SuccessfulCvUploads = uploads.Count(x => x.Succeeded),
        CvUploadCount = uploads.Count,
        AllPrivateMarkersExcluded = uploads.All(x => x.PrivateMarkerExcluded) &&
            evaluations.All(x => x.PrivateMarkerExcludedFromReasons),
        StandardDeclaredRoleComparisons = standardComparisons.Length,
        StandardTop1ExpectationMatches = standardComparisons.Count(x => x.Top1Matches == true),
        StandardTop3ExpectationMatches = standardComparisons.Count(x => x.Top3Matches == true),
        EdgeChecksPassed = edgeChecks.Count(x => x.Passed),
        EdgeCheckCount = edgeChecks.Length,
        ScoreNote = "FitScore is the existing model's ranking score, not a calibrated probability of a correct match"
    },
    Uploads = uploads,
    Projects = evaluations
};
var outputName = inputFileName switch
{
    "projects.json" => "results.json",
    "holdout-projects.json" => "holdout-results.json",
    "semantic-verification-projects.json" => "semantic-verification-results.json",
    _ => "semantic-results.json"
};
var outputPath = Path.Combine(directory, (providerName == "Hybrid" ? "hybrid-" : "") + outputName);
await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, options) + Environment.NewLine,
    new UTF8Encoding(false));
Console.WriteLine($"Synthetic CV uploads: {uploads.Count(x => x.Succeeded)}/{uploads.Count}");
Console.WriteLine($"Standard role comparisons: top1 {standardComparisons.Count(x => x.Top1Matches == true)}/{standardComparisons.Length}, top3 {standardComparisons.Count(x => x.Top3Matches == true)}/{standardComparisons.Length}");
Console.WriteLine($"Edge checks: {edgeChecks.Count(x => x.Passed)}/{edgeChecks.Length}; embedding calls: {embeddings.Calls}; model unchanged: {modelHashBefore == modelHashAfter}");
Console.WriteLine($"Results: {outputPath}");
if (embeddings.Calls != 0 || converter.Calls != 0 || modelHashBefore != modelHashAfter ||
    uploads.Any(x => !x.Succeeded || !x.PrivateMarkerExcluded)) Environment.ExitCode = 1;

static string FindRepositoryRoot(string? supplied)
{
    if (!string.IsNullOrWhiteSpace(supplied))
    {
        var path = Path.GetFullPath(supplied);
        if (File.Exists(Path.Combine(path, "backend", "src", "EHub.Infrastructure", "EHub.Infrastructure.csproj"))) return path;
        throw new DirectoryNotFoundException("The supplied directory is not the E-HUB repository root.");
    }
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "backend", "src", "EHub.Infrastructure", "EHub.Infrastructure.csproj")))
                return directory.FullName;
    throw new DirectoryNotFoundException("Cannot find the E-HUB repository root.");
}

static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

static byte[] CreateDocx(string text)
{
    XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    var xml = new XDocument(new XElement(word + "document", new XElement(word + "body",
        text.Replace("\r", "", StringComparison.Ordinal).Split('\n').Select(line =>
            new XElement(word + "p", new XElement(word + "r", new XElement(word + "t", line)))))));
    using var memory = new MemoryStream();
    using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
    using (var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open(), new UTF8Encoding(false)))
        writer.Write(xml.ToString());
    return memory.ToArray();
}

static string ProfessionalText(MentorProfileResponse profile) => string.Join('\n',
    new[] { profile.Bio, profile.Experience }.Concat(profile.Expertise)
        .Concat(profile.StartupDomains).Concat(profile.TechnologySkills).Concat(profile.Tags)
        .Concat(profile.Experiences.Select(x => $"{x.Kind} {x.Area} {x.Level} {x.Notes}"))
        .Where(x => !string.IsNullOrWhiteSpace(x)));

static RankedMentor[] ToRanked(IEnumerable<MentorRecommendationResponse> items,
    IReadOnlyDictionary<Guid, string> ids) => items.Select((item, index) => new RankedMentor
    {
        Rank = index + 1,
        Id = ids[item.Mentor.Id],
        Name = item.Mentor.FullName,
        Type = item.Mentor.MentorType,
        FitScore = item.FitScore,
        Reasons = item.Reasons,
        ActiveTeamCount = item.ActiveTeamCount,
        HasCapacity = item.HasCapacity
    }).ToArray();

static RoleExpectation Compare(string[] expected, RankedMentor[] ranked) => new()
{
    ExpectedIds = expected,
    HasDeclaredExpectation = expected.Length > 0,
    ActualTop1 = ranked.FirstOrDefault()?.Id,
    ActualTop3 = ranked.Take(3).Select(x => x.Id).ToArray(),
    Top1Matches = expected.Length == 0 ? null : ranked.Take(1).Any(x => expected.Contains(x.Id)),
    Top3Matches = expected.Length == 0 ? null : ranked.Take(3).Any(x => expected.Contains(x.Id))
};

static EdgeCheck CheckEdgeCase(string check, bool succeeded, string? errorCode,
    RankedMentor[] business, RankedMentor[] it, IReadOnlyCollection<UploadResult> uploads)
{
    switch (check)
    {
        case "unsupported-specialty":
            return new(check, succeeded && business.Length == 0 && it.Length == 0,
                $"Returned {business.Length} Business and {it.Length} IT candidates; the API does not expose a separate specialty-abstention flag.");
        case "negated-project-need":
            return new(check, it.FirstOrDefault()?.Id == "I01",
                $"Expected C#/.NET backend mentor I01 first; actual first IT mentor: {it.FirstOrDefault()?.Id ?? "none"}.");
        case "negated-cv-expertise":
            var negatedCv = uploads.FirstOrDefault(x => x.MentorId == "I06");
            var aiInferred = negatedCv?.Expertise.Any(x => x.Equals("AI", StringComparison.OrdinalIgnoreCase)) == true;
            return new(check, negatedCv is { Succeeded: true } && !aiInferred,
                $"I06 CV explicitly denies AI experience; extracted AI expertise tag: {aiInferred}.");
        case "experience-over-degree":
            var financial = Array.FindIndex(business, x => x.Id == "B02");
            var degreeOnly = Array.FindIndex(business, x => x.Id == "B07");
            return new(check, financial >= 0 && degreeOnly >= 0 && financial < degreeOnly,
                $"B02 finance-work-experience rank: {financial + 1}; B07 finance-degree-only rank: {degreeOnly + 1}.");
        case "missing-project-description":
            return new(check, !succeeded && errorCode == ErrorCodes.CommonValidationError,
                $"Expected existing description validation; actual success: {succeeded}; error: {errorCode ?? "none"}.");
        case "business-only-no-it":
            return new(check, succeeded && business.Length > 0 && it.Length == 0,
                $"Business-only description returned {business.Length} Business and {it.Length} IT candidates.");
        case "react-native-not-react-web":
            return new(check, it.FirstOrDefault()?.Id == "I02" && it.All(x => x.Id != "I03"),
                $"Mobile mentor I02 expected; first IT: {it.FirstOrDefault()?.Id ?? "none"}; React web I03 returned: {it.Any(x => x.Id == "I03")}.");
        default:
            return new(check, false, "Unknown author-declared edge check; not silently treated as passed.");
    }
}

sealed class InMemoryMentorStorage : IMentorDocumentStorageService
{
    private readonly Dictionary<string, byte[]> _content = new(StringComparer.Ordinal);
    public int UploadCount { get; private set; }
    public async Task<Result<(string Url, string PublicId)>> UploadAsync(Stream content, string fileName,
        Guid mentorId, string kind, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var id = $"synthetic/{mentorId:N}/{kind}";
        var url = $"https://storage.example.test/{id}";
        _content[url] = buffer.ToArray();
        UploadCount++;
        return Result.Success((url, id));
    }
    public Task<Result<byte[]>> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_content.TryGetValue(url, out var bytes)
            ? Result.Success(bytes.ToArray())
            : Result.Failure<byte[]>(ErrorCodes.CommonNotFoundError, "Synthetic document was not found."));
    }
    public Task DeleteAsync(string publicId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _content.Remove($"https://storage.example.test/{publicId}");
        return Task.CompletedTask;
    }
}

sealed class ForbiddenEmbeddingSearch : IMentorEmbeddingSearch
{
    public int Calls { get; private set; }
    public Task<IReadOnlyDictionary<Guid, double>> SimilaritiesAsync(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, CancellationToken cancellationToken)
    {
        Calls++;
        throw new InvalidOperationException("The synthetic trained-model demo must not call an embedding provider.");
    }
}

sealed class ForbiddenDocumentConverter : IDocumentPreviewConverter
{
    public int Calls { get; private set; }
    public Task<Result<DocumentPreviewConversionResult>> ConvertToPdfAsync(byte[] sourceContent,
        string sourceExtension, CancellationToken cancellationToken = default)
    {
        Calls++;
        throw new InvalidOperationException("The synthetic DOCX demo must not call an external converter.");
    }
}

sealed class MentorInputDocument
{
    public bool IsSynthetic { get; init; }
    public MentorInput[] Mentors { get; init; } = [];
}
sealed class MentorInput
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Summary { get; init; } = "";
    public string CvText { get; init; } = "";
}
sealed class ProjectInputDocument
{
    public bool IsSynthetic { get; init; }
    public ProjectInput[] Projects { get; init; } = [];
}
sealed class ProjectInput
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Description { get; init; } = "";
    public string[] ExpectedBusiness { get; init; } = [];
    public string[] ExpectedIT { get; init; } = [];
    public string ExpectationNote { get; init; } = "";
    public string[] Checks { get; init; } = [];
}
sealed class UploadResult
{
    public string MentorId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public bool Succeeded { get; init; }
    public double ElapsedMilliseconds { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool SourceContainsPrivateMarker { get; init; }
    public bool PrivateMarkerExcluded { get; init; }
    public bool HasProfessionalText { get; init; }
    public string? Bio { get; init; }
    public string? Experience { get; init; }
    public string[] Expertise { get; init; } = [];
    public string[] StartupDomains { get; init; } = [];
    public string[] TechnologySkills { get; init; } = [];
    public string? CvFileName { get; init; }
}
sealed class RankedMentor
{
    public int Rank { get; init; }
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public int FitScore { get; init; }
    public string[] Reasons { get; init; } = [];
    public int ActiveTeamCount { get; init; }
    public bool HasCapacity { get; init; }
}
sealed class RoleExpectation
{
    public string[] ExpectedIds { get; init; } = [];
    public bool HasDeclaredExpectation { get; init; }
    public string? ActualTop1 { get; init; }
    public string[] ActualTop3 { get; init; } = [];
    public bool? Top1Matches { get; init; }
    public bool? Top3Matches { get; init; }
}
sealed record EdgeCheck(string Name, bool Passed, string Detail);
sealed class ProjectEvaluation
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Description { get; init; } = "";
    public string ExpectationNote { get; init; } = "";
    public bool Succeeded { get; init; }
    public double ElapsedMilliseconds { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public RankedMentor[] Business { get; init; } = [];
    public RankedMentor[] It { get; init; } = [];
    public RoleExpectation BusinessExpectation { get; init; } = new();
    public RoleExpectation ItExpectation { get; init; } = new();
    public EdgeCheck[] Checks { get; init; } = [];
    public bool PrivateMarkerExcludedFromReasons { get; init; }
}
