namespace EHub.Application.Common.Interfaces.Services;

public sealed class MentorFitRankerUnavailableException : Exception
{
    public MentorFitRankerUnavailableException() : base("Mentor ranking model is unavailable.") { }

    /// <summary>Keeps the cause for server logs; API errors only ever use the fixed message.</summary>
    public MentorFitRankerUnavailableException(Exception innerException)
        : base("Mentor ranking model is unavailable.", innerException) { }
}
