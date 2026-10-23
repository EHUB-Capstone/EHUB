namespace EHub.Application.Common.Interfaces.Services;

public sealed class MentorFitRankerUnavailableException : Exception
{
    public MentorFitRankerUnavailableException() : base("Mentor ranking model is unavailable.") { }
}
