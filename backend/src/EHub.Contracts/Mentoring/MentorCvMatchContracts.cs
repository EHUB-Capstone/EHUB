namespace EHub.Contracts.Mentoring;

public static class MentorCvMatchStatuses
{
    public const string Matched = "Matched";
    public const string NoEmail = "NoEmail";
    public const string NoMatch = "NoMatch";
    public const string Ambiguous = "Ambiguous";
}

// Only the admin reviewing the CV receives its detected email addresses.
// Matching does not save the document, identities or extracted profile data.
public sealed class MentorCvMatchResponse
{
    public string Status { get; init; } = string.Empty;
    public Guid? MentorProfileId { get; init; }
    public string[] DetectedEmails { get; init; } = [];
}
