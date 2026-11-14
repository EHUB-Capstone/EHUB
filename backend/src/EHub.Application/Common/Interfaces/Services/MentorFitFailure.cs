namespace EHub.Application.Common.Interfaces.Services;

public enum MentorFitFailure
{
    Unknown,
    Configuration,
    InputLimit,
    InvalidInput,
    Authentication,
    Permission,
    Quota,
    ModelUnavailable,
    ServiceUnavailable,
    RequestRejected,
    Timeout,
    Network,
    InvalidResponse
}
