using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Services;

public interface IMentorEmbeddingSearch
{
    Task<IReadOnlyDictionary<Guid, double>> SimilaritiesAsync(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, CancellationToken cancellationToken);
}
