using EHub.Application.Common.Interfaces.Services;
using EHub.Domain.Entities;

namespace EHub.Application.Features.Mentoring.TrainedRanking;

public sealed class TrainedMentorFitScorer(MentorRankingModel model) : IMentorFitRanker
{
    public IReadOnlyDictionary<Guid, MentorFitRanking> Rank(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, CancellationToken cancellationToken) =>
        Rank(projectText, mentors, MentorRankingScope.MentorRole, cancellationToken);

    public IReadOnlyDictionary<Guid, MentorFitRanking> Rank(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, MentorRankingScope scope, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));
        cancellationToken.ThrowIfCancellationRequested();
        var projects = mentors.Select(x => x.MentorType).Distinct().ToDictionary(role => role,
            role => MentorRankingFeatures.PrepareProject(model, projectText, role, scope));
        var output = new Dictionary<Guid, MentorFitRanking>(mentors.Count);
        foreach (var mentor in mentors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var evidence = MentorRankingFeatures.ExtractEvidence(model, projects[mentor.MentorType], mentor);
            var hasEvidence = evidence.HasEvidence;
            var score = 0;
            var rankingValue = 0d;
            if (hasEvidence)
            {
                var linear = model.Bias;
                for (var i = 0; i < evidence.Values.Length; i++) linear += model.Weights[i] * evidence.Values[i];
                // This is a ranking score; synthetic training does not calibrate match success.
                rankingValue = 100d / (1d + Math.Exp(-linear));
                score = (int)Math.Round(rankingValue, MidpointRounding.AwayFromZero);
            }

            var reasons = hasEvidence ? evidence.Reasons.ToList() : [];
            if (!hasEvidence) reasons.Add("Chưa tìm thấy bằng chứng chuyên môn phù hợp trong hồ sơ");
            if (!model.SuitabilityValidated)
                reasons.Add("Mô hình chưa được kiểm chứng bằng đánh giá ghép mentor thực tế");
            output[mentor.Id] = new MentorFitRanking(score, reasons.ToArray(), hasEvidence, rankingValue);
        }
        return output;
    }
}
