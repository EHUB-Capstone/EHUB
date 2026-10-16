using Pgvector;

namespace EHub.Infrastructure.Persistence;

public sealed class MentorEmbedding
{
    public Guid MentorProfileId { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public Vector Embedding { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}
