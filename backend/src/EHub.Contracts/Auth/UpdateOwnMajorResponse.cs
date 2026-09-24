namespace EHub.Contracts.Auth;

public sealed class UpdateOwnMajorResponse
{
    public string MajorCode { get; init; } = string.Empty;
    public int UpdatedEnrollmentCount { get; init; }
    public IReadOnlyCollection<Guid> UpdatedClassIds { get; init; } = Array.Empty<Guid>();
}
