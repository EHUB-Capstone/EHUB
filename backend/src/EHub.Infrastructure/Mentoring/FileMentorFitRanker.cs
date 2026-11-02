using System.Security;
using System.Text;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.TrainedRanking;
using EHub.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.Mentoring;

/// <summary>Loads the CPU model once. An invalid explicit model never falls back to another provider.</summary>
public sealed class FileMentorFitRanker : IMentorFitRanker
{
    private readonly Lazy<TrainedMentorFitScorer> _scorer;

    public FileMentorFitRanker(IConfiguration configuration, ILogger<FileMentorFitRanker>? logger = null)
    {
        var path = configuration["Mentoring:TrainedModelPath"];
        // PublicationOnly does not cache a failed load: once the file is fixed, the next request
        // succeeds without restarting the backend. A second concurrent load is cheap.
        _scorer = new Lazy<TrainedMentorFitScorer>(() => Load(path, logger),
            LazyThreadSafetyMode.PublicationOnly);
    }

    public IReadOnlyDictionary<Guid, MentorFitRanking> Rank(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, CancellationToken cancellationToken) =>
        Rank(projectText, mentors, MentorRankingScope.MentorRole, cancellationToken);

    public IReadOnlyDictionary<Guid, MentorFitRanking> Rank(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, MentorRankingScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _scorer.Value.Rank(projectText, mentors, scope, cancellationToken);
    }

    private static TrainedMentorFitScorer Load(string? path, ILogger? logger)
    {
        try
        {
            return new TrainedMentorFitScorer(ReadModel(path));
        }
        catch (MentorFitRankerUnavailableException exception)
        {
            // Operators need the cause (e.g. a model only root can read); the API never sees it.
            logger?.LogError(exception, "The mentor ranking model could not be loaded. AI mentor allocation is unavailable.");
            throw;
        }
    }

    internal static MentorRankingModel ReadModel(string? path)
    {
        try
        {
            using var stream = string.IsNullOrWhiteSpace(path)
                ? typeof(FileMentorFitRanker).Assembly.GetManifestResourceStream("EHub.Mentoring.BootstrapModel.json")
                : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream is null || stream.Length > MentorRankingModel.MaximumArtifactBytes)
                throw new MentorFitRankerUnavailableException();
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true),
                detectEncodingFromByteOrderMarks: true);
            var characters = new char[MentorRankingModel.MaximumArtifactBytes + 1];
            var count = 0;
            int read;
            while (count < characters.Length &&
                   (read = reader.Read(characters, count, characters.Length - count)) > 0) count += read;
            if (count > MentorRankingModel.MaximumArtifactBytes) throw new MentorFitRankerUnavailableException();
            return MentorRankingModel.FromJson(new string(characters, 0, count));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            InvalidDataException or ArgumentException or SecurityException)
        {
            // Do not expose configured paths or artifact content in an API error.
            throw new MentorFitRankerUnavailableException(exception);
        }
    }
}
