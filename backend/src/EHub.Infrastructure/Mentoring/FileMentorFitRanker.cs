using System.Security;
using System.Text;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.TrainedRanking;
using EHub.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace EHub.Infrastructure.Mentoring;

/// <summary>Loads the CPU model once. An invalid explicit model never falls back to another provider.</summary>
public sealed class FileMentorFitRanker : IMentorFitRanker
{
    private readonly Lazy<TrainedMentorFitScorer> _scorer;

    public FileMentorFitRanker(IConfiguration configuration)
    {
        var path = configuration["Mentoring:TrainedModelPath"];
        _scorer = new Lazy<TrainedMentorFitScorer>(() => new TrainedMentorFitScorer(ReadModel(path)),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyDictionary<Guid, MentorFitRanking> Rank(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _scorer.Value.Rank(projectText, mentors, cancellationToken);
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
            throw new MentorFitRankerUnavailableException();
        }
    }
}
