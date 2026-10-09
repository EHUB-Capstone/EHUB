namespace EHub.Contracts.Mentors;

// The full profile of one mentor, as the admin sees it.
public sealed class MentorProfileResponse
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? AvatarUrl { get; init; }
    // "Enterprise" or "Academic". It is fixed once the mentor is created.
    public string MentorType { get; init; } = string.Empty;
    // "Active", "Inactive" or "Unavailable". Only Active mentors can be assigned to teams.
    public string Status { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Expertise { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> StartupDomains { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> TechnologySkills { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> MentorTags { get; init; } = Array.Empty<string>();
    public string? Bio { get; init; }
    public string? AvailabilityNote { get; init; }
    public string? Organization { get; init; }
    public string? Department { get; init; }
    public string? JobTitle { get; init; }
    public string? ContractType { get; init; }
    public string? EducationLevel { get; init; }
    public string? CurrentAddress { get; init; }
    public string? LinkedInUrl { get; init; }
    public string? FptEmail { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    // Teams the mentor mentors right now, so the admin sees the effect of making them Inactive or Unavailable.
    public int ActiveTeamCount { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

// Replaces the editable part of a profile. Name, email and phone belong to the account and are edited there.
public sealed class UpdateMentorProfileRequest
{
    public string RowVersion { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Expertise { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> StartupDomains { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> TechnologySkills { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> MentorTags { get; init; } = Array.Empty<string>();
    public string? Bio { get; init; }
    public string? AvailabilityNote { get; init; }
    public string? Organization { get; init; }
    public string? Department { get; init; }
    public string? JobTitle { get; init; }
    public string? ContractType { get; init; }
    public string? EducationLevel { get; init; }
    public string? CurrentAddress { get; init; }
    public string? LinkedInUrl { get; init; }
    public string? FptEmail { get; init; }
    public DateOnly? DateOfBirth { get; init; }
}

// The searchable labels of a mentor, shown to anyone who picks a mentor (admin and lecturers).
public sealed class MentorTagsDto
{
    public IReadOnlyCollection<string> Expertise { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> StartupDomains { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> TechnologySkills { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> MentorTags { get; init; } = Array.Empty<string>();
}

// Every tag already in use, most used first, so the editor can suggest one spelling instead of many.
public sealed class MentorTagSuggestionsResponse
{
    public IReadOnlyCollection<string> Expertise { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> StartupDomains { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> TechnologySkills { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> MentorTags { get; init; } = Array.Empty<string>();
}
