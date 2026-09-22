using System.Security.Cryptography;
using System.Text.Json;
using EHub.Application.Common.Interfaces.AI;
using EHub.Application.Features.ProposalAnalyses;
using EHub.Contracts.ProjectProposals;
using EHub.Domain.Enums;
using EHub.Infrastructure.Options;
using EHub.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace EHub.ProposalEvaluation;

public static class Program
{
    private const string DatasetSchemaVersion = "proposal-golden-dataset-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = true
    };
    private static readonly JsonSerializerOptions FingerprintJsonOptions = new(JsonOptions)
    {
        WriteIndented = false
    };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = EvaluationOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            var datasetPath = Path.GetFullPath(options.DatasetPath);
            var datasetJson = await File.ReadAllTextAsync(datasetPath);
            var dataset = JsonSerializer.Deserialize<GoldenDataset>(datasetJson, JsonOptions)
                ?? throw new InvalidOperationException("Golden dataset is empty.");
            Validate(dataset);

            using var providerContext = CreateProvider(options.Provider);
            var report = await EvaluateAsync(
                dataset,
                Convert.ToHexString(SHA256.HashData(
                    JsonSerializer.SerializeToUtf8Bytes(dataset, FingerprintJsonOptions))).ToLowerInvariant(),
                providerContext.Provider,
                options.K);

            PrintReport(report);
            if (!string.IsNullOrWhiteSpace(options.JsonOutputPath))
            {
                var outputPath = Path.GetFullPath(options.JsonOutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
                Console.WriteLine($"JSON report: {outputPath}");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Evaluation failed: {exception.Message}");
            return 1;
        }
    }

    private static async Task<GoldenEvaluationReport> EvaluateAsync(
        GoldenDataset dataset,
        string datasetSha256,
        IEmbeddingProvider provider,
        int k)
    {
        var textBuilder = new ProposalEmbeddingTextBuilder();
        var fieldTextBuilder = new ProposalFieldEmbeddingTextBuilder();
        var overallTexts = dataset.Proposals
            .Select(proposal => textBuilder.Build(proposal.Snapshot).Text)
            .ToArray();
        var overallVectors = await provider.GenerateAsync(overallTexts);
        ValidateVectors(overallVectors, dataset.Proposals.Count, provider.Dimension);

        var fieldInputs = dataset.Proposals
            .SelectMany(proposal => fieldTextBuilder.Build(proposal.Snapshot)
                .Select(field => new FieldInput(proposal.Id, field.Field, field.Text)))
            .ToArray();
        var fieldVectors = await provider.GenerateAsync(fieldInputs.Select(input => input.Text).ToArray());
        ValidateVectors(fieldVectors, fieldInputs.Length, provider.Dimension);

        var embedded = dataset.Proposals
            .Select((proposal, index) => new EmbeddedProposal(
                proposal.Id,
                proposal.Snapshot,
                overallVectors[index].Values,
                new Dictionary<ProjectProposalSemanticField, IReadOnlyList<float>>()))
            .ToDictionary(proposal => proposal.Id, StringComparer.Ordinal);
        for (var index = 0; index < fieldInputs.Length; index++)
            embedded[fieldInputs[index].ProposalId].FieldVectors[fieldInputs[index].Field] = fieldVectors[index].Values;

        var caseReports = new List<GoldenEvaluationCaseReport>(dataset.Cases.Count);
        foreach (var goldenCase in dataset.Cases)
        {
            var query = embedded[goldenCase.QueryProposalId];
            var candidates = embedded.Values
                .Where(candidate => !string.Equals(candidate.Id, query.Id, StringComparison.Ordinal))
                .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
                .ToArray();
            var lexical = ProposalLexicalSimilarity.Calculate(
                query.Snapshot,
                candidates.Select(candidate => candidate.Snapshot).ToArray());
            var scores = candidates.Select((candidate, index) => Score(query, candidate, lexical[index])).ToArray();

            var semanticRanking = scores
                .OrderByDescending(item => item.WeightedSemantic)
                .ThenByDescending(item => item.OverallSemantic)
                .ThenBy(item => item.ProposalId, StringComparer.Ordinal)
                .Select(item => item.ProposalId)
                .ToArray();
            var tfIdfRanking = scores
                .OrderByDescending(item => item.TfIdf)
                .ThenByDescending(item => item.Jaccard)
                .ThenBy(item => item.ProposalId, StringComparer.Ordinal)
                .Select(item => item.ProposalId)
                .ToArray();
            var fullHybridRanking = scores
                .OrderByDescending(item => item.Hybrid)
                .ThenByDescending(item => item.WeightedSemantic)
                .ThenBy(item => item.ProposalId, StringComparer.Ordinal)
                .Select(item => item.ProposalId)
                .ToArray();

            var productionCandidates = candidates
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Overall = CosineSimilarity.Calculate(query.OverallVector, candidate.OverallVector)
                })
                .OrderByDescending(item => item.Overall)
                .ThenBy(item => item.Candidate.Id, StringComparer.Ordinal)
                .Take(10)
                .ToArray();
            var productionLexical = ProposalLexicalSimilarity.Calculate(
                query.Snapshot,
                productionCandidates.Select(item => item.Candidate.Snapshot).ToArray());
            var productionRanking = productionCandidates
                .Select((item, index) => Score(query, item.Candidate, productionLexical[index]))
                .OrderByDescending(item => item.Hybrid)
                .ThenByDescending(item => item.WeightedSemantic)
                .ThenByDescending(item => item.OverallSemantic)
                .ThenBy(item => item.ProposalId, StringComparer.Ordinal)
                .Select(item => item.ProposalId)
                .ToArray();

            var grades = goldenCase.RelevanceGrades;
            caseReports.Add(new GoldenEvaluationCaseReport(
                goldenCase.Id,
                goldenCase.QueryProposalId,
                EvaluateMethod(semanticRanking, grades, k),
                EvaluateMethod(tfIdfRanking, grades, k),
                EvaluateMethod(fullHybridRanking, grades, k),
                EvaluateMethod(productionRanking, grades, k)));
        }

        return new GoldenEvaluationReport(
            DatasetSchemaVersion,
            dataset.Name,
            dataset.LabelStatus,
            datasetSha256,
            DateTime.UtcNow,
            provider.ProviderName,
            provider.ModelName,
            provider.Dimension,
            k,
            dataset.Proposals.Count,
            dataset.Cases.Count,
            ProposalEmbeddingTextBuilder.CurrentSchemaVersion,
            ProposalFieldEmbeddingTextBuilder.CurrentSchemaVersion,
            WeightedSemanticSimilarity.CurrentVersion,
            WeightedSemanticSimilarity.CurrentWeights,
            ProposalLexicalSimilarity.CurrentVersion,
            HybridProposalSimilarity.CurrentVersion,
            HybridProposalSimilarity.CurrentWeights,
            Aggregate(caseReports.Select(report => report.EmbeddingOnly)),
            Aggregate(caseReports.Select(report => report.TfIdfOnly)),
            Aggregate(caseReports.Select(report => report.HybridFullCorpus)),
            Aggregate(caseReports.Select(report => report.ProductionTwoStageHybrid)),
            caseReports);
    }

    private static ScoredProposal Score(
        EmbeddedProposal query,
        EmbeddedProposal candidate,
        ProposalLexicalSimilarityScores lexical)
    {
        double Field(ProjectProposalSemanticField field) => CosineSimilarity.Calculate(
            query.FieldVectors[field],
            candidate.FieldVectors[field]);
        var fieldScores = new ProposalFieldSimilarityScores(
            Field(ProjectProposalSemanticField.Problem),
            Field(ProjectProposalSemanticField.Solution),
            Field(ProjectProposalSemanticField.TargetCustomers),
            Field(ProjectProposalSemanticField.ValueAndApproach));
        var weightedSemantic = WeightedSemanticSimilarity.Calculate(fieldScores);
        return new ScoredProposal(
            candidate.Id,
            CosineSimilarity.Calculate(query.OverallVector, candidate.OverallVector),
            weightedSemantic,
            lexical.TfIdf,
            lexical.Jaccard,
            HybridProposalSimilarity.Calculate(weightedSemantic, lexical.TfIdf, lexical.Jaccard));
    }

    private static GoldenMethodCaseResult EvaluateMethod(
        IReadOnlyList<string> ranking,
        IReadOnlyDictionary<string, int> relevanceGrades,
        int k)
    {
        var metrics = ProposalRankingMetrics.Calculate(ranking, relevanceGrades, k);
        return new GoldenMethodCaseResult(
            ranking.Take(k).ToArray(),
            metrics.PrecisionAtK,
            metrics.RecallAtK,
            metrics.NdcgAtK);
    }

    private static GoldenMethodSummary Aggregate(IEnumerable<GoldenMethodCaseResult> results)
    {
        var items = results.ToArray();
        return new GoldenMethodSummary(
            items.Average(item => item.PrecisionAtK),
            items.Average(item => item.RecallAtK),
            items.Average(item => item.NdcgAtK));
    }

    private static void Validate(GoldenDataset dataset)
    {
        if (!string.Equals(dataset.SchemaVersion, DatasetSchemaVersion, StringComparison.Ordinal))
            throw new InvalidOperationException($"Dataset schema must be {DatasetSchemaVersion}.");
        if (string.IsNullOrWhiteSpace(dataset.Name) || string.IsNullOrWhiteSpace(dataset.LabelStatus))
            throw new InvalidOperationException("Dataset name and label status are required.");
        if (dataset.Proposals.Count is < 30 or > 50)
            throw new InvalidOperationException("Golden dataset must contain between 30 and 50 proposals.");
        if (dataset.Cases.Count is < 10 or > 20)
            throw new InvalidOperationException("Golden dataset must contain between 10 and 20 evaluation cases.");

        var proposalIds = dataset.Proposals.Select(proposal => proposal.Id).ToArray();
        if (proposalIds.Any(string.IsNullOrWhiteSpace)
            || proposalIds.Distinct(StringComparer.Ordinal).Count() != proposalIds.Length)
            throw new InvalidOperationException("Proposal identifiers must be non-empty and unique.");
        if (dataset.Proposals.Any(proposal => string.IsNullOrWhiteSpace(proposal.Snapshot.Title)
            || string.IsNullOrWhiteSpace(proposal.Snapshot.Problem)
            || string.IsNullOrWhiteSpace(proposal.Snapshot.Solution)
            || string.IsNullOrWhiteSpace(proposal.Snapshot.TargetCustomers)
            || string.IsNullOrWhiteSpace(proposal.Snapshot.ValueProposition)))
            throw new InvalidOperationException("Every proposal requires the core comparison fields.");

        var knownIds = proposalIds.ToHashSet(StringComparer.Ordinal);
        var caseIds = dataset.Cases.Select(item => item.Id).ToArray();
        if (caseIds.Any(string.IsNullOrWhiteSpace)
            || caseIds.Distinct(StringComparer.Ordinal).Count() != caseIds.Length)
            throw new InvalidOperationException("Evaluation case identifiers must be non-empty and unique.");
        foreach (var goldenCase in dataset.Cases)
        {
            if (!knownIds.Contains(goldenCase.QueryProposalId))
                throw new InvalidOperationException($"Unknown query proposal: {goldenCase.QueryProposalId}.");
            if (goldenCase.RelevanceGrades.Count == 0
                || goldenCase.RelevanceGrades.Any(pair => !knownIds.Contains(pair.Key)
                    || string.Equals(pair.Key, goldenCase.QueryProposalId, StringComparison.Ordinal)
                    || pair.Value is < 1 or > 3))
                throw new InvalidOperationException($"Case {goldenCase.Id} has invalid relevance grades.");
        }
    }

    private static void ValidateVectors(
        IReadOnlyList<EmbeddingVector> vectors,
        int expectedCount,
        int expectedDimension)
    {
        if (vectors.Count != expectedCount
            || vectors.Any(vector => vector.Values.Count != expectedDimension
                || vector.Values.Any(value => !float.IsFinite(value))))
            throw new InvalidOperationException("Embedding provider returned invalid vectors.");
    }

    private static ProviderContext CreateProvider(string providerName)
    {
        if (string.Equals(providerName, "local", StringComparison.OrdinalIgnoreCase))
            return new ProviderContext(new DeterministicLocalEmbeddingProvider(), null);
        if (!string.Equals(providerName, "gemini", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Provider must be 'local' or 'gemini'.");

        var apiKey = Environment.GetEnvironmentVariable("AI__Embedding__ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AI__Embedding__ApiKey is required for Gemini evaluation.");
        var model = Environment.GetEnvironmentVariable("AI__Embedding__Model") ?? "gemini-embedding-2";
        var dimension = int.TryParse(Environment.GetEnvironmentVariable("AI__Embedding__Dimension"), out var parsedDimension)
            ? parsedDimension
            : 768;
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
            Timeout = TimeSpan.FromSeconds(60)
        };
        var provider = new GeminiEmbeddingProvider(httpClient, Options.Create(new ProposalEmbeddingOptions
        {
            Provider = "Gemini",
            ApiKey = apiKey,
            Model = model,
            Dimension = dimension,
            TimeoutSeconds = 60
        }));
        return new ProviderContext(provider, httpClient);
    }

    private static void PrintReport(GoldenEvaluationReport report)
    {
        Console.WriteLine($"Dataset: {report.DatasetName} / {report.DatasetSchemaVersion} ({report.ProposalCount} proposals, {report.CaseCount} cases)");
        Console.WriteLine($"Labels: {report.LabelStatus}");
        Console.WriteLine($"Embedding: {report.EmbeddingProvider}/{report.EmbeddingModel} ({report.EmbeddingDimension} dimensions)");
        Console.WriteLine($"K = {report.K}");
        Console.WriteLine("Method                         Precision@K  Recall@K  NDCG@K");
        PrintMethod("Embedding-only", report.EmbeddingOnly);
        PrintMethod("TF-IDF-only", report.TfIdfOnly);
        PrintMethod("Hybrid/full corpus", report.HybridFullCorpus);
        PrintMethod("Production two-stage hybrid", report.ProductionTwoStageHybrid);
    }

    private static void PrintMethod(string label, GoldenMethodSummary result) =>
        Console.WriteLine($"{label,-30} {result.PrecisionAtK,11:F3}  {result.RecallAtK,8:F3}  {result.NdcgAtK,6:F3}");

    private static void PrintHelp()
    {
        Console.WriteLine("EHUB proposal ranking Golden Dataset evaluator");
        Console.WriteLine("  --dataset <path>       Golden Dataset JSON path");
        Console.WriteLine("  --provider local|gemini (default: local)");
        Console.WriteLine("  --k <number>           Evaluation cutoff (default: 3)");
        Console.WriteLine("  --json <path>          Optional JSON report output");
        Console.WriteLine("  --help                 Show this help");
        Console.WriteLine("Gemini mode reads AI__Embedding__ApiKey, AI__Embedding__Model and AI__Embedding__Dimension from environment variables and never prints the key.");
    }

    private sealed record FieldInput(string ProposalId, ProjectProposalSemanticField Field, string Text);
    private sealed record EmbeddedProposal(
        string Id,
        ProjectProposalSnapshotDto Snapshot,
        IReadOnlyList<float> OverallVector,
        Dictionary<ProjectProposalSemanticField, IReadOnlyList<float>> FieldVectors);
    private sealed record ScoredProposal(
        string ProposalId,
        double OverallSemantic,
        double WeightedSemantic,
        double TfIdf,
        double Jaccard,
        double Hybrid);

    private sealed class ProviderContext(IEmbeddingProvider provider, IDisposable? disposable) : IDisposable
    {
        public IEmbeddingProvider Provider { get; } = provider;
        public void Dispose() => disposable?.Dispose();
    }
}

public sealed record GoldenDataset(
    string SchemaVersion,
    string Name,
    string LabelStatus,
    IReadOnlyList<GoldenProposal> Proposals,
    IReadOnlyList<GoldenEvaluationCase> Cases);

public sealed record GoldenProposal(string Id, ProjectProposalSnapshotDto Snapshot);

public sealed record GoldenEvaluationCase(
    string Id,
    string QueryProposalId,
    IReadOnlyDictionary<string, int> RelevanceGrades);

public sealed record GoldenMethodCaseResult(
    IReadOnlyList<string> TopProposalIds,
    double PrecisionAtK,
    double RecallAtK,
    double NdcgAtK);

public sealed record GoldenEvaluationCaseReport(
    string CaseId,
    string QueryProposalId,
    GoldenMethodCaseResult EmbeddingOnly,
    GoldenMethodCaseResult TfIdfOnly,
    GoldenMethodCaseResult HybridFullCorpus,
    GoldenMethodCaseResult ProductionTwoStageHybrid);

public sealed record GoldenMethodSummary(
    double PrecisionAtK,
    double RecallAtK,
    double NdcgAtK);

public sealed record GoldenEvaluationReport(
    string DatasetSchemaVersion,
    string DatasetName,
    string LabelStatus,
    string DatasetSha256,
    DateTime GeneratedAtUtc,
    string EmbeddingProvider,
    string EmbeddingModel,
    int EmbeddingDimension,
    int K,
    int ProposalCount,
    int CaseCount,
    string TextSchemaVersion,
    string FieldTextSchemaVersion,
    string FieldScoringVersion,
    ProposalFieldSimilarityWeights FieldWeights,
    string LexicalScoringVersion,
    string HybridScoringVersion,
    ProposalHybridSimilarityWeights HybridWeights,
    GoldenMethodSummary EmbeddingOnly,
    GoldenMethodSummary TfIdfOnly,
    GoldenMethodSummary HybridFullCorpus,
    GoldenMethodSummary ProductionTwoStageHybrid,
    IReadOnlyList<GoldenEvaluationCaseReport> Cases);

public sealed record EvaluationOptions(
    string DatasetPath,
    string Provider,
    int K,
    string? JsonOutputPath,
    bool ShowHelp)
{
    public static EvaluationOptions Parse(string[] args)
    {
        var dataset = Path.Combine(AppContext.BaseDirectory, "Data", "proposal-golden-dataset-v1.json");
        var provider = "local";
        var k = 3;
        string? json = null;
        var showHelp = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--dataset": dataset = RequiredValue(args, ref index); break;
                case "--provider": provider = RequiredValue(args, ref index); break;
                case "--k":
                    if (!int.TryParse(RequiredValue(args, ref index), out k) || k is < 1 or > 10)
                        throw new ArgumentException("--k must be between 1 and 10.");
                    break;
                case "--json": json = RequiredValue(args, ref index); break;
                case "--help" or "-h": showHelp = true; break;
                default: throw new ArgumentException($"Unknown argument: {args[index]}.");
            }
        }
        return new EvaluationOptions(dataset, provider, k, json, showHelp);
    }

    private static string RequiredValue(string[] args, ref int index)
    {
        if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
            throw new ArgumentException("An option value is missing.");
        return args[index];
    }
}
