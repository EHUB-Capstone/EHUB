using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Services;

/// <summary>Rejects explicit profile contradictions locally; never supplies semantic fit scores.</summary>
public interface IMentorEvidenceGuard
{
    IReadOnlySet<Guid> FindContradictions(string projectText, IReadOnlyCollection<MentorProfile> mentors,
        CancellationToken cancellationToken);
}
