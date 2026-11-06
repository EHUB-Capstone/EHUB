using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Manage;

public sealed class ShowcaseManageHandler(
    IApplicationDbContext context,
    IImageStorageService imageStorage,
    IDateTimeProvider dateTimeProvider,
    IValidator<SaveShowcaseRequest> validator) : IShowcaseManageHandler
{
    public const long MaximumImageBytes = 5 * 1024 * 1024;
    public const int MaximumMediaItems = 8;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };

    public async Task<Result<IReadOnlyCollection<MyShowcaseEntryDto>>> GetMineAsync(
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!ShowcaseAccess.IsStudent(role)) return Denied<IReadOnlyCollection<MyShowcaseEntryDto>>();

        var projects = await ShowcaseAccess.OwnedProjects(context, userId)
            .OrderBy(project => project.Name)
            .Select(project => new
            {
                project.Id,
                project.TeamId,
                project.ProjectLineageId,
                project.Name,
                project.Description,
                project.Problem,
                project.TargetUsers,
                project.Solution,
                project.Team.TeamName
            })
            .ToListAsync(cancellationToken);

        var lineageIds = projects.Select(project => project.ProjectLineageId).ToList();
        var showcases = await context.ShowcaseProjects.AsNoTracking()
            .Include(item => item.Links).Include(item => item.Media)
            .Where(item => lineageIds.Contains(item.ProjectLineageId))
            .ToListAsync(cancellationToken);

        IReadOnlyCollection<MyShowcaseEntryDto> entries = projects.Select(project =>
        {
            var showcase = showcases.FirstOrDefault(item => item.ProjectLineageId == project.ProjectLineageId);
            var profile = ShowcaseMapper.ToProfile(project.Name, project.Description, project.Problem, project.Solution, project.TargetUsers);
            return new MyShowcaseEntryDto
            {
                ProjectLineageId = project.ProjectLineageId,
                ProjectId = project.Id,
                TeamId = project.TeamId,
                TeamName = project.TeamName,
                Profile = profile,
                Showcase = showcase is null ? null : ShowcaseMapper.ToDto(showcase, profile, canEdit: true)
            };
        }).ToArray();

        return Result.Success(entries);
    }

    public async Task<Result<ShowcaseDto>> CreateAsync(
        SaveShowcaseRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!ShowcaseAccess.IsStudent(role)) return Denied<ShowcaseDto>();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Invalid<ShowcaseDto>(validation.Errors[0].ErrorMessage);
        if (request.ProjectLineageId is not { } lineageId || lineageId == Guid.Empty)
            return Invalid<ShowcaseDto>("A project must be selected.");

        // Unknown and foreign lineages are indistinguishable to the caller so ids cannot be probed.
        if (!await ShowcaseAccess.OwnsLineageAsync(context, lineageId, userId, cancellationToken))
            return NotFound<ShowcaseDto>();
        if (await context.ShowcaseProjects.AnyAsync(item => item.ProjectLineageId == lineageId, cancellationToken))
            return Result.Failure<ShowcaseDto>(ErrorCodes.ShowcaseAlreadyExists, "This project already has a showcase page.");

        var showcase = new ShowcaseProject
        {
            ProjectLineageId = lineageId,
            Status = ShowcaseStatus.Draft,
            CreatedBy = userId
        };
        ApplyContent(showcase, request);
        foreach (var link in BuildLinks(request)) showcase.Links.Add(link);
        context.ShowcaseProjects.Add(showcase);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Concurrent create for the same lineage hits the unique index.
            context.ClearChanges();
            return Result.Failure<ShowcaseDto>(ErrorCodes.ShowcaseAlreadyExists, "This project already has a showcase page.");
        }

        return Result.Success(ShowcaseMapper.ToDto(showcase, await ProfileOfAsync(showcase.ProjectLineageId, cancellationToken), canEdit: true));
    }

    public async Task<Result<ShowcaseDto>> UpdateAsync(
        Guid showcaseId, SaveShowcaseRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!ShowcaseAccess.IsStudent(role)) return Denied<ShowcaseDto>();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Invalid<ShowcaseDto>(validation.Errors[0].ErrorMessage);

        var loaded = await LoadEditableAsync(showcaseId, userId, role, cancellationToken);
        if (loaded.IsFailure) return Result.Failure<ShowcaseDto>(loaded.Error);
        var showcase = loaded.Value;

        if (request.RowVersion.HasValue && request.RowVersion.Value != showcase.Version)
            return Changed<ShowcaseDto>();

        ApplyContent(showcase, request);
        var previousLinks = showcase.Links.ToList();
        context.ShowcaseLinks.RemoveRange(previousLinks);
        foreach (var link in previousLinks) showcase.Links.Remove(link);
        foreach (var link in BuildLinks(request))
        {
            // Links have generated keys. Adding them explicitly lets EF track the inserts and populate showcase.Links.
            link.ShowcaseProjectId = showcase.Id;
            context.ShowcaseLinks.Add(link);
        }
        showcase.UpdatedBy = userId;

        // Edits to a live page keep it published, so the audit trail records who changed it.
        if (showcase.Status == ShowcaseStatus.Published)
        {
            context.ShowcaseReviewHistories.Add(new ShowcaseReviewHistory
            {
                ShowcaseProjectId = showcase.Id,
                FromStatus = ShowcaseStatus.Published,
                ToStatus = ShowcaseStatus.Published,
                Note = "Content edited by the project team.",
                ActorId = userId,
                CreatedAt = dateTimeProvider.UtcNow
            });
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ClearChanges();
            return Changed<ShowcaseDto>();
        }

        return Result.Success(ShowcaseMapper.ToDto(showcase, await ProfileOfAsync(showcase.ProjectLineageId, cancellationToken), canEdit: true));
    }

    public async Task<Result<ShowcaseMediaDto>> UploadMediaAsync(
        Guid showcaseId, Stream content, string fileName, long length, Guid userId, string role,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadEditableAsync(showcaseId, userId, role, cancellationToken);
        if (loaded.IsFailure) return Result.Failure<ShowcaseMediaDto>(loaded.Error);
        var showcase = loaded.Value;
        if (showcase.Media.Count >= MaximumMediaItems)
            return Invalid<ShowcaseMediaDto>($"A showcase can have at most {MaximumMediaItems} images.");

        var image = await ReadImageAsync(content, fileName, length, cancellationToken);
        if (image.IsFailure) return Result.Failure<ShowcaseMediaDto>(image.Error);

        var upload = await UploadAsync(showcase.Id, image.Value, cancellationToken);
        if (upload.IsFailure) return Result.Failure<ShowcaseMediaDto>(upload.Error);

        var media = new ShowcaseMedia
        {
            ShowcaseProjectId = showcase.Id,
            MediaType = ShowcaseMediaType.Image,
            Url = upload.Value,
            SortOrder = showcase.Media.Count == 0 ? 0 : showcase.Media.Max(item => item.SortOrder) + 1,
            CreatedAt = dateTimeProvider.UtcNow
        };
        context.ShowcaseMedia.Add(media);
        showcase.UpdatedBy = userId;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ShowcaseMapper.ToDto(media));
    }

    public async Task<Result> DeleteMediaAsync(
        Guid showcaseId, Guid mediaId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadEditableAsync(showcaseId, userId, role, cancellationToken);
        if (loaded.IsFailure) return Result.Failure(loaded.Error.Code, loaded.Error.Message);
        var media = loaded.Value.Media.FirstOrDefault(item => item.Id == mediaId);
        if (media is null) return Result.Failure(ErrorCodes.ShowcaseNotFound, "The image was not found.");

        context.ShowcaseMedia.Remove(media);
        loaded.Value.UpdatedBy = userId;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ShowcaseDto>> UploadLogoAsync(
        Guid showcaseId, Stream content, string fileName, long length, Guid userId, string role,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadEditableAsync(showcaseId, userId, role, cancellationToken);
        if (loaded.IsFailure) return Result.Failure<ShowcaseDto>(loaded.Error);
        var showcase = loaded.Value;

        var image = await ReadImageAsync(content, fileName, length, cancellationToken);
        if (image.IsFailure) return Result.Failure<ShowcaseDto>(image.Error);
        var upload = await UploadAsync(showcase.Id, image.Value, cancellationToken);
        if (upload.IsFailure) return Result.Failure<ShowcaseDto>(upload.Error);

        showcase.LogoUrl = upload.Value;
        showcase.UpdatedBy = userId;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ShowcaseMapper.ToDto(showcase, await ProfileOfAsync(showcase.ProjectLineageId, cancellationToken), canEdit: true));
    }

    /// <summary>Loads a tracked showcase the caller's team owns and that is not locked for review.</summary>
    private async Task<Result<ShowcaseProject>> LoadEditableAsync(
        Guid showcaseId, Guid userId, string role, CancellationToken cancellationToken)
    {
        if (!ShowcaseAccess.IsStudent(role)) return Denied<ShowcaseProject>();

        var showcase = await context.ShowcaseProjects
            .Include(item => item.Links).Include(item => item.Media)
            .FirstOrDefaultAsync(item => item.Id == showcaseId, cancellationToken);
        if (showcase is null || !await ShowcaseAccess.OwnsLineageAsync(context, showcase.ProjectLineageId, userId, cancellationToken))
            return NotFound<ShowcaseProject>();
        if (showcase.Status == ShowcaseStatus.PendingReview)
            return Result.Failure<ShowcaseProject>(ErrorCodes.ShowcaseInvalidState, "The page cannot be edited while it is waiting for review.");

        return Result.Success(showcase);
    }

    private async Task<ShowcaseProfileDto?> ProfileOfAsync(Guid lineageId, CancellationToken cancellationToken) =>
        (await ShowcaseAccess.LatestProjectAsync(context, lineageId, cancellationToken))?.Profile;

    private static void ApplyContent(ShowcaseProject showcase, SaveShowcaseRequest request)
    {
        showcase.Highlights = Clean(request.Highlights);
        showcase.DevelopmentStage = Enum.Parse<ShowcaseDevelopmentStage>(request.DevelopmentStage, true);
        showcase.ShowTeamMembers = request.ShowTeamMembers;
    }

    private static IEnumerable<ShowcaseLink> BuildLinks(SaveShowcaseRequest request) =>
        request.Links.Select((link, index) => new ShowcaseLink
        {
            LinkType = Enum.Parse<ShowcaseLinkType>(link.LinkType, true),
            Label = link.Label.Trim(),
            Url = link.Url.Trim(),
            SortOrder = index
        });

    private async Task<Result<string>> UploadAsync(Guid showcaseId, ValidatedImage image, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(image.Bytes, writable: false);
        var upload = await imageStorage.UploadShowcaseImageAsync(stream, $"showcase{image.Extension}", showcaseId, cancellationToken);
        return upload.IsSuccess
            ? Result.Success(upload.Value.SecureUrl)
            : Result.Failure<string>(upload.Error);
    }

    private static async Task<Result<ValidatedImage>> ReadImageAsync(
        Stream content, string fileName, long length, CancellationToken cancellationToken)
    {
        if (length <= 0 || length > MaximumImageBytes)
            return Invalid<ValidatedImage>("Images must be larger than 0 and at most 5 MB.");
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (!ImageExtensions.Contains(extension))
            return Invalid<ValidatedImage>("Only PNG, JPG and WEBP images are accepted.");

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0 || buffer.Length > MaximumImageBytes)
            return Invalid<ValidatedImage>("Images must be larger than 0 and at most 5 MB.");

        var bytes = buffer.ToArray();
        return HasImageSignature(bytes, extension)
            ? Result.Success(new ValidatedImage(bytes, extension.ToLowerInvariant()))
            : Invalid<ValidatedImage>("The file does not match its declared image format.");
    }

    private static bool HasImageSignature(byte[] bytes, string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
        ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        ".webp" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<T> Denied<T>() =>
        Result.Failure<T>(ErrorCodes.ShowcaseAccessDenied, "Only students can manage a showcase page.");

    private static Result<T> NotFound<T>() =>
        Result.Failure<T>(ErrorCodes.ShowcaseNotFound, "The showcase page was not found.");

    private static Result<T> Invalid<T>(string message) =>
        Result.Failure<T>(ErrorCodes.ShowcaseValidationError, message);

    private static Result<T> Changed<T>() =>
        Result.Failure<T>(ErrorCodes.ShowcaseConcurrencyConflict, "The showcase page was changed by someone else. Reload and try again.");

    private sealed record ValidatedImage(byte[] Bytes, string Extension);
}
