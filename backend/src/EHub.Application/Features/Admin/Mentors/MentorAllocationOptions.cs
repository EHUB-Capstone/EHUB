namespace EHub.Application.Features.Admin.Mentors;

/// <summary>
/// How the Academic (IT) mentor slot is filled when an admin uses the AI strategy.
/// "Balanced" (default) asks the model to match only the Enterprise (business) mentors; each IT slot goes, without AI, to
/// the IT mentor with the fewest teams (ties at random), so team counts stay even. This halves the number of AI requests.
/// "Random" picks IT mentors at random whatever their load (counts can be very uneven). "Ai" asks the model to match both
/// mentor types.
/// </summary>
public sealed class MentorAllocationOptions
{
    public const string SectionName = "Mentoring:Allocation";
    public const string Ai = "Ai";
    public const string Balanced = "Balanced";
    public const string Random = "Random";

    public string AcademicMethod { get; set; } = Balanced;

    /// <summary>
    /// How many times a background AI allocation is retried by itself when the provider is only temporarily
    /// unavailable (overload, timeout, network, unusable output). Batches that already finished are reused from cache.
    /// </summary>
    public int MaxProviderRetries { get; set; } = 2;

    /// <summary>Wait before each retry. It must outlast the one-minute cool-down applied to a model after a failure.</summary>
    public int ProviderRetryDelaySeconds { get; set; } = 65;

    public bool IsValid() => TryParse(AcademicMethod, out _) && MaxProviderRetries is >= 0 and <= 5 &&
        ProviderRetryDelaySeconds is >= 0 and <= 300;

    internal AllocationStrategy AcademicStrategy => TryParse(AcademicMethod, out var strategy) ? strategy : AllocationStrategy.Ai;

    private static bool TryParse(string? value, out AllocationStrategy strategy)
    {
        var text = value?.Trim();
        if (string.Equals(text, Ai, StringComparison.OrdinalIgnoreCase)) strategy = AllocationStrategy.Ai;
        else if (string.Equals(text, Balanced, StringComparison.OrdinalIgnoreCase)) strategy = AllocationStrategy.Balanced;
        else if (string.Equals(text, Random, StringComparison.OrdinalIgnoreCase)) strategy = AllocationStrategy.Random;
        else
        {
            strategy = AllocationStrategy.Ai;
            return false;
        }
        return true;
    }
}
