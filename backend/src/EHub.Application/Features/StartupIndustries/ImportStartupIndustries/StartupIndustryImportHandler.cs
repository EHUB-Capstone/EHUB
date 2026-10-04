using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.StartupIndustries;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.StartupIndustries.ImportStartupIndustries;

public sealed class StartupIndustryImportHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IStartupIndustryImportHandler
{
    private const long MaximumFileSize = 5 * 1024 * 1024;

    public async Task<Result<StartupIndustryImportPreviewResponse>> PreviewAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(file, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<StartupIndustryImportPreviewResponse>(validation.Error);
        }

        var rows = validation.Value;
        return Result.Success(new StartupIndustryImportPreviewResponse
        {
            TotalRows = rows.Count,
            ValidRowsCount = rows.Count(row => row.IsValid),
            ErrorRowsCount = rows.Count(row => !row.IsValid),
            Rows = rows.Select(row => new StartupIndustryImportRowPreview
            {
                RowNumber = row.RowNumber,
                Name = row.Name,
                Description = row.Description,
                IsValid = row.IsValid,
                Status = row.Status,
                ErrorMessage = row.ErrorMessage
            }).ToArray()
        });
    }

    public async Task<Result<StartupIndustryImportResponse>> ImportAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(file, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<StartupIndustryImportResponse>(validation.Error);
        }

        var candidates = validation.Value;
        var invalidRows = candidates.Where(candidate => !candidate.IsValid).ToArray();
        if (invalidRows.Length > 0)
        {
            var hasFileError = invalidRows.Any(row => row.ErrorCode == ErrorCodes.StartupIndustryImportFileInvalid);
            return Result.Failure<StartupIndustryImportResponse>(new Error(
                hasFileError
                    ? ErrorCodes.StartupIndustryImportFileInvalid
                    : ErrorCodes.StartupIndustryImportConflict,
                invalidRows.Length == 1
                    ? $"Row {invalidRows[0].RowNumber}: {invalidRows[0].ErrorMessage}"
                    : $"The workbook contains {invalidRows.Length} invalid rows. Preview the file and resolve every error before importing."));
        }

        var industries = candidates.Select(candidate => new StartupIndustry
        {
            Name = candidate.Name,
            NormalizedName = candidate.NormalizedName,
            Description = candidate.Description,
            Status = StartupIndustryStatus.Active,
            CreatedBy = currentUser.UserId
        }).ToArray();

        await context.StartupIndustries.AddRangeAsync(industries, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new StartupIndustryImportResponse
        {
            ImportedCount = industries.Length,
            Industries = industries.Select(ToResponse).ToArray()
        });
    }

    private async Task<Result<List<StartupIndustryImportCandidate>>> ValidateAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0 || file.Length > MaximumFileSize)
        {
            return InvalidFile<List<StartupIndustryImportCandidate>>(
                "Select a non-empty Excel file not exceeding 5 MB.");
        }

        var security = ExcelWorkbookSecurity.Validate(file);
        if (security.IsFailure)
        {
            return InvalidFile<List<StartupIndustryImportCandidate>>(security.Error.Message);
        }

        var parsed = StartupIndustryImportWorkbookParser.Parse(file);
        if (parsed.IsFailure)
        {
            return Result.Failure<List<StartupIndustryImportCandidate>>(parsed.Error);
        }

        var candidates = parsed.Value;
        var normalizedNames = candidates
            .Where(candidate => candidate.IsValid)
            .Select(candidate => candidate.NormalizedName)
            .ToArray();
        if (normalizedNames.Length == 0)
        {
            return Result.Success(candidates);
        }

        var existing = await context.StartupIndustries
            .AsNoTracking()
            .Where(industry => normalizedNames.Contains(industry.NormalizedName))
            .Select(industry => new { industry.NormalizedName, industry.Name })
            .ToArrayAsync(cancellationToken);
        var existingByNormalizedName = existing.ToDictionary(
            industry => industry.NormalizedName,
            industry => industry.Name,
            StringComparer.Ordinal);

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!candidate.IsValid ||
                !existingByNormalizedName.TryGetValue(candidate.NormalizedName, out var existingName))
            {
                continue;
            }

            candidates[index] = candidate with
            {
                IsValid = false,
                Status = "Error",
                ErrorMessage = $"Industry '{existingName}' already exists.",
                ErrorCode = ErrorCodes.StartupIndustryImportConflict
            };
        }

        return Result.Success(candidates);
    }

    private static StartupIndustryResponse ToResponse(StartupIndustry industry) => new()
    {
        Id = industry.Id,
        Name = industry.Name,
        Description = industry.Description,
        Status = "active"
    };

    private static Result<T> InvalidFile<T>(string message) =>
        Result.Failure<T>(new Error(
            ErrorCodes.StartupIndustryImportFileInvalid,
            message));
}
