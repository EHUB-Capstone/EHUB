namespace EHub.Application.Features.Mentoring.Scoring;

/// <summary>Initial business policy, configurable and not fitted/calibrated from mentor outcome labels.</summary>
public sealed class MentorScoringOptions
{
    public const string SectionName = "Mentoring:Scoring";
    public int ExperienceWeight { get; set; } = 35;
    public int ExpertiseWeight { get; set; } = 25;
    public int StartupDomainWeight { get; set; } = 15;
    public int TechnologyWeight { get; set; } = 20;
    public int BioWeight { get; set; } = 5;

    // Seventy profile points plus at most fifteen history and fifteen achievement points.
    public int ProfilePoints => 70;
    public int[] CriterionWeights => [ExperienceWeight, ExpertiseWeight, StartupDomainWeight, TechnologyWeight, BioWeight];
    public bool IsValid() => CriterionWeights.All(weight => weight is >= 0 and <= 100) && CriterionWeights.Sum() > 0;
}
