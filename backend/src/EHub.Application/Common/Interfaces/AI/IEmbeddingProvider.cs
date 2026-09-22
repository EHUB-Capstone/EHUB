namespace EHub.Application.Common.Interfaces.AI;

public sealed record EmbeddingVector(IReadOnlyList<float> Values);

public interface IEmbeddingProvider
{
    string ProviderName { get; }
    string ModelName { get; }
    int Dimension { get; }

    Task<IReadOnlyList<EmbeddingVector>> GenerateAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}

public sealed class EmbeddingProviderException : Exception
{
    public EmbeddingProviderException(
        string errorCode,
        string message,
        bool isTransient,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        IsTransient = isTransient;
    }

    public string ErrorCode { get; }
    public bool IsTransient { get; }
}
