using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using EHub.Application.Features.Mentoring.TrainedRanking;
using EHub.Domain.Entities;

// Offline evaluation: never opens a database or calls an embedding/semantic service.
var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    WriteIndented = true
};
try
{
    var arguments = ReadArguments(args);
    var output = Path.GetFullPath(arguments["--output"]);
    var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    if (arguments.Where(x => x.Key != "--output").Any(x => Path.GetFullPath(x.Value).Equals(output, pathComparison)))
        throw new InvalidDataException("Output must not replace an input file.");
    var profilesBytes = await File.ReadAllBytesAsync(arguments["--profiles"]);
    var projectsBytes = await File.ReadAllBytesAsync(arguments["--projects"]);
    var modelBytes = await File.ReadAllBytesAsync(arguments["--model"]);
    if (modelBytes.Length > MentorRankingModel.MaximumArtifactBytes)
        throw new InvalidDataException("Model artifact exceeds the runtime limit.");
    var utf8 = new UTF8Encoding(false, true);
    var profiles = new List<MentorInput>();
    foreach (var line in utf8.GetString(profilesBytes).TrimStart('\uFEFF').Split('\n'))
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        var input = JsonSerializer.Deserialize<MentorInput>(line, jsonOptions)
            ?? throw new InvalidDataException("Invalid profile row.");
        if (!ValidId(input.MentorRef) || !ValidProfile(input.Profile))
            throw new InvalidDataException("A profile has an invalid reference or role.");
        profiles.Add(input);
    }
    if (profiles.Count == 0 || profiles.Select(x => x.MentorRef).Distinct(StringComparer.Ordinal).Count() != profiles.Count)
        throw new InvalidDataException("Profiles must be non-empty and have unique references.");

    using var projectsJson = JsonDocument.Parse(projectsBytes);
    var projectArray = projectsJson.RootElement.ValueKind == JsonValueKind.Array
        ? projectsJson.RootElement : projectsJson.RootElement.GetProperty("projects");
    var projects = projectArray.Deserialize<ProjectInput[]>(jsonOptions)
        ?? throw new InvalidDataException("Invalid projects input.");
    if (projects.Length == 0 || projects.Any(x => x is null || !ValidId(x.ProjectId) ||
        string.IsNullOrWhiteSpace(x.Title) || string.IsNullOrWhiteSpace(x.Description)) ||
        projects.Select(x => x.ProjectId).Distinct(StringComparer.Ordinal).Count() != projects.Length)
        throw new InvalidDataException("Projects need unique references, titles and descriptions.");

    var labels = new Dictionary<(string Project, string Mentor), int>();
    byte[]? labelsBytes = null;
    if (arguments.TryGetValue("--labels", out var labelsPath))
    {
        labelsBytes = await File.ReadAllBytesAsync(labelsPath);
        foreach (var line in utf8.GetString(labelsBytes).TrimStart('\uFEFF').Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var label = JsonSerializer.Deserialize<LabelInput>(line, jsonOptions)
                ?? throw new InvalidDataException("Invalid label row.");
            if (label.Relevance is null or < 0 or > 3 || !labels.TryAdd((label.ProjectId, label.MentorRef), label.Relevance.Value))
                throw new InvalidDataException("Labels must be unique integers from 0 through 3.");
        }
        var expected = projects.SelectMany(project => profiles.Select(profile => (project.ProjectId, profile.MentorRef))).ToHashSet();
        if (!expected.SetEquals(labels.Keys))
            throw new InvalidDataException("Evaluation requires exactly one label for every project/candidate pair.");
    }

    var coldTimer = Stopwatch.StartNew();
    var model = MentorRankingModel.FromJson(utf8.GetString(modelBytes).TrimStart('\uFEFF'));
    var scorer = new TrainedMentorFitScorer(model);
    var runtimeProfiles = profiles.ToDictionary(x => x.MentorRef, x => ToRuntime(x.Profile!), StringComparer.Ordinal);
    var candidates = runtimeProfiles.Values.ToArray();
    // Warm once to separate model parsing/JIT from the measured per-project calls.
    _ = scorer.Rank(ProjectText(projects[0]), candidates, CancellationToken.None);
    coldTimer.Stop();
    var evaluations = new List<ProjectEvaluation>();
    foreach (var project in projects)
    {
        var timer = Stopwatch.StartNew();
        var results = scorer.Rank(ProjectText(project), candidates, CancellationToken.None);
        timer.Stop();
        var ranked = profiles.Select(input =>
        {
            var result = results[runtimeProfiles[input.MentorRef].Id];
            return new RankedCandidate(input.MentorRef, result.Score, result.RankingValue ?? result.Score,
                result.HasEvidence, result.Reasons,
                labels.Count > 0 ? labels[(project.ProjectId, input.MentorRef)] : null);
        }).OrderByDescending(x => x.RankingValue).ThenBy(x => x.MentorRef, StringComparer.Ordinal).ToArray();
        var recommended = ranked.Where(x => x.HasEvidence).ToArray();
        evaluations.Add(new ProjectEvaluation(project.ProjectId, project.Title,
            timer.Elapsed.TotalMilliseconds, recommended, ranked,
            labels.Count > 0 ? Measure(ranked, recommended) : null));
    }
    var times = evaluations.Select(x => x.ElapsedMilliseconds).Order().ToArray();
    var measured = evaluations.Where(x => x.Metrics is not null).Select(x => x.Metrics!).ToArray();
    var noFit = measured.Where(x => x.ExpectedPositiveCount == 0).ToArray();
    var report = new
    {
        schema = "ehub-mentor-ranker-evaluation-v1",
        createdAtUtc = DateTime.UtcNow,
        execution = "Actual MentorRankingModel.FromJson + TrainedMentorFitScorer; no database or external service",
        projectTextOrder = "description + newline + title",
        candidatePolicy = "Caller supplies eligibility; recommendations retain runtime HasEvidence; ties use mentorRef ascending",
        metricDefinition = "NDCG@5 uses grades 0-3 and gain 2^grade-1; relevant means grade >=2; Precision@3 denominator is 3; no ideal gain yields null NDCG",
        model = new { sha256 = Hash(modelBytes), model.TrainingSource, model.SuitabilityValidated },
        inputs = new { profilesSha256 = Hash(profilesBytes), projectsSha256 = Hash(projectsBytes), labelsSha256 = labelsBytes is null ? null : Hash(labelsBytes) },
        implementation = new
        {
            applicationAssemblySha256 = Hash(await File.ReadAllBytesAsync(typeof(TrainedMentorFitScorer).Assembly.Location)),
            evaluatorAssemblySha256 = Hash(await File.ReadAllBytesAsync(typeof(ProjectInput).Assembly.Location))
        },
        summary = new
        {
            projectCount = projects.Length, mentorCount = profiles.Count, labeledPairs = labels.Count,
            modelParseAndWarmupMilliseconds = coldTimer.Elapsed.TotalMilliseconds,
            warmMedianMilliseconds = Percentile(times, 0.5), warmP95Milliseconds = Percentile(times, 0.95),
            abstainedProjectCount = evaluations.Count(x => x.Recommendations.Length == 0),
            meanNdcgAt5 = Mean(measured.Select(x => x.NdcgAt5)),
            meanRecallAt3 = Mean(measured.Select(x => x.RecallAt3)),
            meanPrecisionAt3 = Mean(measured.Where(x => x.ExpectedRelevantCount > 0).Select(x => (double?)x.PrecisionAt3)),
            top1RelevantRate = Mean(measured.Where(x => x.ExpectedRelevantCount > 0).Select(x => (double?)(x.Top1Relevant ? 1 : 0))),
            noDocumentedFitProjectCount = noFit.Length,
            noDocumentedFitAbstentionRate = Mean(noFit.Select(x => (double?)(x.Abstained ? 1 : 0)))
        },
        projects = evaluations
    };
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, jsonOptions), new UTF8Encoding(false));
    Console.WriteLine(JsonSerializer.Serialize(new { projects = projects.Length, mentors = profiles.Count, labeledPairs = labels.Count,
        modelSha256 = Hash(modelBytes), abstainedProjects = evaluations.Count(x => x.Recommendations.Length == 0) }));
    return 0;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or
    InvalidDataException or ArgumentException or KeyNotFoundException or InvalidOperationException)
{
    // Do not echo malformed input, professional profile content, or environment paths.
    Console.Error.WriteLine("Evaluation failed: check CLI flags, file access, model validity, unique IDs and complete integer labels.");
    return 1;
}

static Dictionary<string, string> ReadArguments(string[] args)
{
    var valid = new HashSet<string>(["--profiles", "--projects", "--model", "--output", "--labels"], StringComparer.Ordinal);
    var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
    if (args.Length % 2 != 0) throw new ArgumentException("Flags need values.");
    for (var i = 0; i < args.Length; i += 2)
        if (!valid.Contains(args[i]) || string.IsNullOrWhiteSpace(args[i + 1]) || !parsed.TryAdd(args[i], args[i + 1]))
            throw new ArgumentException("Unknown, empty or duplicate flag.");
    if (new[] { "--profiles", "--projects", "--model", "--output" }.Any(key => !parsed.ContainsKey(key)))
        throw new ArgumentException("Missing required flags.");
    return parsed;
}
static bool ValidId(string? value) => value is not null && Regex.IsMatch(value, @"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,79}\z");
static bool ValidProfile(ProfileInput? input) => input is not null && input.MentorType is "Business" or "IT" &&
    new[] { input.Expertise, input.StartupDomains, input.TechnologySkills, input.Tags }.All(values => values is null || values.All(value => value is not null)) &&
    (input.Experiences is null || input.Experiences.All(value => value is not null));
static string ProjectText(ProjectInput project) => project.Description + "\n" + project.Title;
static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
static double Percentile(double[] sorted, double percentile) => sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * percentile) - 1, 0, sorted.Length - 1)];
static double? Mean(IEnumerable<double?> values)
{
    var present = values.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
    return present.Length == 0 ? null : present.Average();
}
static MentorProfile ToRuntime(ProfileInput input) => new()
{
    MentorType = input.MentorType, Bio = input.Bio, Experience = input.Experience,
    Expertise = input.Expertise ?? [], StartupDomains = input.StartupDomains ?? [],
    TechnologySkills = input.TechnologySkills ?? [], Tags = input.Tags ?? [],
    Experiences = (input.Experiences ?? []).Select(x => new MentorExperience { Area = x.Area ?? "", Notes = x.Notes }).ToArray()
};
static ProjectMetrics Measure(RankedCandidate[] all, RankedCandidate[] recommendations)
{
    static double Gain(int grade) => Math.Pow(2, grade) - 1;
    static double Dcg(IEnumerable<int> grades) => grades.Take(5).Select((grade, index) => Gain(grade) / Math.Log2(index + 2)).Sum();
    var ideal = Dcg(all.Select(x => x.Relevance!.Value).OrderDescending());
    var relevant = all.Count(x => x.Relevance >= 2);
    var found = recommendations.Take(3).Count(x => x.Relevance >= 2);
    return new ProjectMetrics(ideal > 0 ? Dcg(recommendations.Select(x => x.Relevance!.Value)) / ideal : null,
        relevant > 0 ? found / (double)relevant : null, found / 3d,
        recommendations.FirstOrDefault()?.Relevance >= 2, relevant, all.Count(x => x.Relevance > 0), recommendations.Length == 0);
}

sealed record MentorInput(string MentorRef, ProfileInput? Profile);
sealed record ProfileInput(string MentorType, string? Bio, string? Experience, string[]? Expertise,
    string[]? StartupDomains, string[]? TechnologySkills, string[]? Tags, ExperienceInput[]? Experiences);
sealed record ExperienceInput(string? Area, string? Notes);
sealed record ProjectInput(string ProjectId, string Title, string Description);
sealed record LabelInput(string ProjectId, string MentorRef, int? Relevance);
sealed record RankedCandidate(string MentorRef, int Score, double RankingValue, bool HasEvidence, string[] Reasons, int? Relevance);
sealed record ProjectMetrics(double? NdcgAt5, double? RecallAt3, double PrecisionAt3, bool Top1Relevant,
    int ExpectedRelevantCount, int ExpectedPositiveCount, bool Abstained);
sealed record ProjectEvaluation(string ProjectId, string Title, double ElapsedMilliseconds,
    RankedCandidate[] Recommendations, RankedCandidate[] AllCandidates, ProjectMetrics? Metrics);
