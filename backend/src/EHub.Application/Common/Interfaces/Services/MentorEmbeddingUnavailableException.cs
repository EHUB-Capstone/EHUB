namespace EHub.Application.Common.Interfaces.Services;

public sealed class MentorEmbeddingUnavailableException : Exception
{
    public MentorEmbeddingUnavailableException() : base("Mentor embedding service is unavailable.") { }
}
