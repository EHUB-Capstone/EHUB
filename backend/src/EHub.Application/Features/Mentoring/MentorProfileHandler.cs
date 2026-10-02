using System.IO.Compression;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Contracts.Mentoring;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Mentoring;

public sealed class MentorProfileHandler(IApplicationDbContext context, IMentorDocumentStorageService storage,
    IMentorEmbeddingSearch embeddingSearch) : IMentorProfileHandler
{
    public async Task<Result<MentorProfileResponse>> UploadDocumentAsync(Guid userId, string kind, Stream content,
        string fileName, long length, CancellationToken cancellationToken)
    {
        if (kind is not ("cv" or "portfolio") || length is <= 0 or > 10 * 1024 * 1024)
            return Result.Failure<MentorProfileResponse>(ErrorCodes.CommonValidationError, "Document must be a CV or portfolio under 10 MB.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".docx"))
            return Result.Failure<MentorProfileResponse>(ErrorCodes.CommonValidationError, "Only PDF and DOCX documents are accepted.");
        var profile = await context.MentorProfiles.Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null)
            return Result.Failure<MentorProfileResponse>(ErrorCodes.CommonNotFoundError, "Mentor profile was not found.");
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length != length || !ValidDocument(buffer.ToArray(), extension))
            return Result.Failure<MentorProfileResponse>(ErrorCodes.CommonValidationError, "Document content does not match its format.");
        buffer.Position = 0;
        var safeName = Path.GetFileName(fileName);
        if (safeName.Length > 255) safeName = safeName[^255..];
        var uploaded = await storage.UploadAsync(buffer, safeName, profile.Id, kind, cancellationToken);
        if (uploaded.IsFailure) return Result.Failure<MentorProfileResponse>(uploaded.Error);
        var previousId = kind == "cv" ? profile.CvPublicId : profile.PortfolioPublicId;
        if (kind == "cv")
        {
            profile.CvStorageUrl = uploaded.Value.Url; profile.CvPublicId = uploaded.Value.PublicId;
            profile.CvFileName = safeName;
        }
        else
        {
            profile.PortfolioStorageUrl = uploaded.Value.Url; profile.PortfolioPublicId = uploaded.Value.PublicId;
            profile.PortfolioFileName = safeName;
        }
        try { await context.SaveChangesAsync(cancellationToken); }
        catch
        {
            await storage.DeleteAsync(uploaded.Value.PublicId, cancellationToken);
            throw;
        }
        if (!string.IsNullOrWhiteSpace(previousId)) await storage.DeleteAsync(previousId, cancellationToken);
        return Result.Success(Map(profile));
    }

    public async Task<Result<(byte[] Content, string FileName, string ContentType)>> DownloadDocumentAsync(Guid mentorId,
        string kind, Guid userId, string role, CancellationToken cancellationToken)
    {
        if (kind is not ("cv" or "portfolio"))
            return Result.Failure<(byte[], string, string)>(ErrorCodes.CommonValidationError, "Invalid document type.");
        var profile = await context.MentorProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == mentorId, cancellationToken);
        if (profile is null) return Result.Failure<(byte[], string, string)>(ErrorCodes.CommonNotFoundError, "Mentor was not found.");
        var allowed = profile.UserId == userId || role == SystemRoles.Admin ||
            (role == SystemRoles.Lecturer && await context.SemesterStaffAssignments.AsNoTracking().AnyAsync(s =>
                s.UserId == profile.UserId && s.Role == SemesterStaffRole.Mentor && s.Status == SemesterStaffStatus.Active &&
                context.Classes.Any(c => c.SemesterId == s.SemesterId && c.PrimaryLecturerId == userId), cancellationToken));
        if (!allowed) return Forbidden<(byte[], string, string)>();
        var url = kind == "cv" ? profile.CvStorageUrl : profile.PortfolioStorageUrl;
        var name = kind == "cv" ? profile.CvFileName : profile.PortfolioFileName;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name))
            return Result.Failure<(byte[], string, string)>(ErrorCodes.CommonNotFoundError, "Document was not found.");
        var downloaded = await storage.DownloadAsync(url, cancellationToken);
        if (downloaded.IsFailure) return Result.Failure<(byte[], string, string)>(downloaded.Error);
        var contentType = name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" :
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        return Result.Success((downloaded.Value, name, contentType));
    }

    private static bool ValidDocument(byte[] bytes, string extension)
    {
        if (extension == ".pdf") return bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
        if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4B) return false;
        try { using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            return zip.GetEntry("word/document.xml") is not null; }
        catch (InvalidDataException) { return false; }
    }

    public async Task<Result<MentorProfileResponse>> GetMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await context.MentorProfiles.AsNoTracking().Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        return profile is null
            ? Result.Failure<MentorProfileResponse>(ErrorCodes.CommonNotFoundError, "Mentor profile was not found.")
            : Result.Success(Map(profile));
    }

    public async Task<Result<MentorProfileResponse>> UpdateMineAsync(Guid userId, UpdateMentorProfileRequest request, CancellationToken cancellationToken)
    {
        if (request.MentorType is not ("Business" or "IT") || request.Expertise.Length is 0 or > 20 ||
            request.Expertise.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 80) ||
            (request.Bio?.Length ?? 0) > 2000 || (request.Experience?.Length ?? 0) > 4000 ||
            (request.Organization?.Length ?? 0) > 200 ||
            !ValidHttpsUrl(request.LinkedInUrl, 500) || !ValidHttpsUrl(request.PortfolioUrl, 1000))
            return Result.Failure<MentorProfileResponse>(ErrorCodes.CommonValidationError, "Mentor profile contains invalid fields.");

        var profile = await context.MentorProfiles.Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null)
            return Result.Failure<MentorProfileResponse>(ErrorCodes.CommonNotFoundError, "Mentor profile was not found.");

        if (profile.MentorType != request.MentorType)
            return Result.Failure<MentorProfileResponse>(ErrorCodes.CommonValidationError,
                "Mentor type is managed by the administrator.");
        profile.Expertise = request.Expertise.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        profile.Bio = request.Bio?.Trim();
        profile.Experience = request.Experience?.Trim();
        profile.Organization = request.Organization?.Trim();
        profile.LinkedInUrl = request.LinkedInUrl?.Trim();
        profile.PortfolioUrl = request.PortfolioUrl?.Trim();
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(profile));
    }

    public async Task<Result<IReadOnlyCollection<MentorProfileResponse>>> GetDirectoryAsync(Guid userId, string role, CancellationToken cancellationToken)
    {
        if (!IsStaff(role)) return Forbidden<IReadOnlyCollection<MentorProfileResponse>>();
        var query = context.MentorProfiles.AsNoTracking().Include(x => x.User).AsQueryable();
        if (role == SystemRoles.Lecturer)
            query = query.Where(x => x.Status == MentorProfileStatus.Active && x.User.Status == UserStatus.Active &&
                context.SemesterStaffAssignments.Any(s => s.UserId == x.UserId &&
                s.Role == SemesterStaffRole.Mentor && s.Status == SemesterStaffStatus.Active &&
                context.Classes.Any(c => c.SemesterId == s.SemesterId && c.PrimaryLecturerId == userId)));
        var profiles = await query.OrderBy(x => x.User.FullName).ToListAsync(cancellationToken);
        var ids = profiles.Select(x => x.Id).ToArray();
        var assignments = await context.MentorAssignments.AsNoTracking()
            .Where(x => ids.Contains(x.MentorProfileId)).Select(x => new { x.Id, x.MentorProfileId, x.Status, x.EndedAt })
            .ToListAsync(cancellationToken);
        var assignmentIds = assignments.Select(x => x.Id).ToArray();
        var sessions = await context.MentoringSessions.AsNoTracking()
            .Where(x => assignmentIds.Contains(x.MentorAssignmentId)).Select(x => new { x.Id, x.MentorAssignmentId })
            .ToListAsync(cancellationToken);
        var sessionIds = sessions.Select(x => x.Id).ToArray();
        var ratings = await context.MentoringFeedback.AsNoTracking()
            .Where(x => sessionIds.Contains(x.MentoringSessionId)).Select(x => new { x.MentoringSessionId, x.Rating })
            .ToListAsync(cancellationToken);
        var result = profiles.Select(profile =>
        {
            var mentorAssignments = assignments.Where(x => x.MentorProfileId == profile.Id).ToArray();
            var mentorSessionIds = sessions.Where(x => mentorAssignments.Any(a => a.Id == x.MentorAssignmentId)).Select(x => x.Id).ToHashSet();
            var mentorRatings = ratings.Where(x => mentorSessionIds.Contains(x.MentoringSessionId)).Select(x => x.Rating).ToArray();
            return Map(profile, mentorAssignments.Count(x => x.Status == MentorAssignmentStatus.Active && x.EndedAt == null),
                mentorAssignments.Length, mentorSessionIds.Count, mentorRatings.Length > 0 ? Math.Round(mentorRatings.Average(), 1) : null);
        }).ToArray();
        return Result.Success<IReadOnlyCollection<MentorProfileResponse>>(result);
    }

    public async Task<Result<IReadOnlyCollection<MentorRecommendationResponse>>> RecommendAsync(Guid teamId, Guid userId, string role, CancellationToken cancellationToken)
    {
        var team = await context.Teams.AsNoTracking().Include(x => x.Class).Include(x => x.Project)
            .ThenInclude(x => x!.ProjectTags).Include(x => x.ProjectDirection)
            .FirstOrDefaultAsync(x => x.Id == teamId, cancellationToken);
        if (team is null)
            return Result.Failure<IReadOnlyCollection<MentorRecommendationResponse>>(ErrorCodes.TeamNotFound, "Team was not found.");
        if (role != SystemRoles.Admin && !(role == SystemRoles.Lecturer && team.Class.PrimaryLecturerId == userId))
            return Forbidden<IReadOnlyCollection<MentorRecommendationResponse>>();

        var profiles = await context.MentorProfiles.AsNoTracking().Include(x => x.User)
            .Where(x => x.Status == MentorProfileStatus.Active && x.User.Status == UserStatus.Active &&
                context.SemesterStaffAssignments.Any(s => s.SemesterId == team.Class.SemesterId &&
                    s.UserId == x.UserId && s.Role == SemesterStaffRole.Mentor && s.Status == SemesterStaffStatus.Active))
            .ToListAsync(cancellationToken);
        var counts = await context.MentorAssignments.AsNoTracking()
            .Where(x => x.Status == MentorAssignmentStatus.Active && x.EndedAt == null)
            .GroupBy(x => x.MentorProfileId).Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

        var occupiedSlots = await context.MentorAssignments.AsNoTracking()
            .Where(x => x.TeamId == teamId && x.Status == MentorAssignmentStatus.Active && x.EndedAt == null)
            .Select(x => x.Slot).ToListAsync(cancellationToken);
        profiles = profiles.Where(x => !occupiedSlots.Contains(x.Type)).ToList();
        var projectDetails = new[] { team.Project?.Technology, team.Project?.StartupField,
            team.ProjectDirection?.Summary, team.Project?.Description, team.Project?.Problem,
            team.Project?.Solution, team.Description }.Where(x => !string.IsNullOrWhiteSpace(x))
            .Concat(team.Project?.ProjectTags.Select(x => x.TagName) ?? []).ToArray();
        if (projectDetails.Length == 0)
            return Result.Failure<IReadOnlyCollection<MentorRecommendationResponse>>(
                ErrorCodes.CommonValidationError, "Cần mô tả hoặc tag dự án trước khi gợi ý mentor.");
        var projectText = string.Join(' ', projectDetails.Concat(
            new[] { team.ProjectDirection?.Title, team.Project?.Name, team.TeamName }
                .Where(x => !string.IsNullOrWhiteSpace(x))));
        IReadOnlyDictionary<Guid, double> similarities;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        try { similarities = await embeddingSearch.SimilaritiesAsync(projectText, profiles, deadline.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<IReadOnlyCollection<MentorRecommendationResponse>>(
                ErrorCodes.MentorMatchingUnavailable, "Dịch vụ đang chuẩn bị dữ liệu mentor. Vui lòng thử lại.");
        }
        catch (MentorEmbeddingUnavailableException)
        {
            return Result.Failure<IReadOnlyCollection<MentorRecommendationResponse>>(
                ErrorCodes.MentorMatchingUnavailable, "Dịch vụ gợi ý mentor tạm thời chưa sẵn sàng.");
        }
        var items = profiles.Select(profile =>
        {
            var count = counts.GetValueOrDefault(profile.Id);
            var fit = MentorFitScorer.Score(profile, projectText,
                similarities.GetValueOrDefault(profile.Id), count);
            return new MentorRecommendationResponse { Mentor = Map(profile), FitScore = fit.Score, Reasons = fit.Reasons,
                ActiveTeamCount = count, HasCapacity = fit.HasCapacity };
        }).OrderByDescending(x => x.HasCapacity).ThenByDescending(x => x.FitScore)
            .ThenBy(x => x.Mentor.FullName).ToArray();
        return Result.Success<IReadOnlyCollection<MentorRecommendationResponse>>(items);
    }

    private static bool ValidHttpsUrl(string? value, int maxLength) => string.IsNullOrWhiteSpace(value) ||
        (value.Length <= maxLength && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
    private static bool IsStaff(string role) => role is SystemRoles.Admin or SystemRoles.Lecturer;
    private static Result<T> Forbidden<T>() => Result.Failure<T>(ErrorCodes.ClassAccessDenied, "Access to mentor profiles is denied.");
    private static MentorProfileResponse Map(MentorProfile x, int activeTeams = 0, int totalAssignments = 0,
        int totalSessions = 0, double? averageRating = null) => new() { Id = x.Id, UserId = x.UserId,
        FullName = x.User.FullName, MentorType = x.MentorType, Expertise = x.Expertise,
        Bio = x.Bio, Experience = x.Experience, Organization = x.Organization,
        LinkedInUrl = x.LinkedInUrl, PortfolioUrl = x.PortfolioUrl, CvFileName = x.CvFileName,
        PortfolioFileName = x.PortfolioFileName,
        MaxTeams = null, Status = x.Status.ToString(), ActiveTeamCount = activeTeams,
        TotalAssignments = totalAssignments, TotalSessions = totalSessions, AverageFeedbackRating = averageRating };
}
