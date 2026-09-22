namespace EHub.Application.Features.ProposalAnalyses;

public sealed class ProposalAnalysisProcessingException : Exception
{
    public ProposalAnalysisProcessingException(
        string errorCode,
        string message,
        bool isTransient = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        IsTransient = isTransient;
    }

    public string ErrorCode { get; }
    public bool IsTransient { get; }
}
