using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;
using FluentValidation;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

public sealed class RequestCheckpointSummaryRequestValidator : AbstractValidator<RequestCheckpointSummaryRequest>
{
    public RequestCheckpointSummaryRequestValidator()
    {
        RuleFor(request => request.FileIds)
            .NotEmpty().WithMessage("Choose at least one file to summarise.")
            .Must(ids => ids.Count <= SubmissionFileLimits.MaxActiveFilesPerCheckpoint)
            .WithMessage($"Choose at most {SubmissionFileLimits.MaxActiveFilesPerCheckpoint} files.")
            .Must(ids => ids.Distinct().Count() == ids.Count && ids.All(id => id != Guid.Empty))
            .WithMessage("Each file can be chosen only once.");
    }
}
