using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.Common;

public static class ClassStateRules
{
    public static bool IsReadOnly(ClassStatus status) =>
        status is ClassStatus.Completed or ClassStatus.Archived;

    public static bool IsOperational(ClassStatus status) =>
        status is ClassStatus.Draft or ClassStatus.Active;

    public static bool ParticipatesInScheduleConflict(ClassStatus status) =>
        status is ClassStatus.Draft or ClassStatus.Active;

    public static Error? GetMutationError(ClassStatus status) => status switch
    {
        ClassStatus.Completed => new Error(ErrorCodes.ClassCompleted, "The class is completed and read-only."),
        ClassStatus.Archived => new Error(ErrorCodes.ClassArchived, "The class is archived and read-only."),
        _ => null
    };
}
