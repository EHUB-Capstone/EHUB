using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;
using FluentValidation;

namespace EHub.Application.Features.Workspaces.CheckpointFiles;

public sealed class InitiateCheckpointFileUploadRequestValidator : AbstractValidator<InitiateCheckpointFileUploadRequest>
{
    public InitiateCheckpointFileUploadRequestValidator()
    {
        RuleFor(request => request.FileName)
            .NotEmpty().WithMessage("A file name is required.")
            .Must(name => Path.GetFileName(name.Trim()).Length is > 0 and <= 256)
            .WithMessage("The file name must be between 1 and 256 characters.")
            .Must(name => CheckpointFileTypes.TryGetContentType(Path.GetExtension(name.Trim()), out _))
            .WithMessage("Only PDF, DOCX, and PPTX files are accepted.");

        RuleFor(request => request.Size)
            .GreaterThan(0).WithMessage("The file is empty.")
            .LessThanOrEqualTo(SubmissionFileLimits.MaxFileSizeBytes)
            .WithMessage($"Files must not exceed {SubmissionFileLimits.MaxFileSizeBytes / (1024 * 1024)} MB.");

        // Browsers send an empty or generic type for some Office files; the stored type is always derived from the extension.
        RuleFor(request => request.ContentType)
            .Must((request, contentType) => IsCompatible(request.FileName, contentType))
            .WithMessage("The file type does not match its extension.");
    }

    private static bool IsCompatible(string fileName, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType) ||
            contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return CheckpointFileTypes.TryGetContentType(Path.GetExtension(fileName.Trim()), out var expected) &&
            expected.Equals(contentType.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
