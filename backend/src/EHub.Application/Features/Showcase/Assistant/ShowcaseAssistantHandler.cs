using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Showcase;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Assistant;

/// <summary>
/// Answers student questions about published showcases. The model only sees public showcase fields, and every project
/// card in the reply is rebuilt from the database from a reference the model picked, so it cannot invent a project or link.
/// </summary>
public sealed class ShowcaseAssistantHandler(
    IApplicationDbContext context,
    IShowcaseAssistantModel model,
    IValidator<ShowcaseAssistantChatRequest> validator,
    IDateTimeProvider time,
    IStoredShowcaseAssistantKey? storedKey = null) : IShowcaseAssistantHandler
{
    internal const int MaximumCatalogSize = 120;
    internal const int MaximumCandidates = 500;
    internal const int MaximumHistoryTurnsSent = 6;
    internal const int MaximumRecommendations = 6;
    private const int MaximumAnswerLength = 1200;
    private const int MaximumReasonLength = 200;
    private const int MaximumSuggestionLength = 80;
    private const int MaximumSuggestions = 3;
    private static readonly Regex Url = new(@"(https?://|www\.)\S+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public async Task<Result<ShowcaseAssistantReplyDto>> AskAsync(
        ShowcaseAssistantChatRequest request, string? apiKey, Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return Result.Failure<ShowcaseAssistantReplyDto>(ErrorCodes.CommonUnauthorizedError, "Bạn cần đăng nhập để dùng trợ lý.");
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<ShowcaseAssistantReplyDto>(ErrorCodes.ShowcaseValidationError, validation.Errors[0].ErrorMessage);

        // The key is always the caller's own. One sent with this request wins; otherwise the one they chose to keep on the server
        // (encrypted, and only ever opened here for this call). Refuse before any database or provider work when there is none or
        // it is malformed. A key pasted with a trailing space or newline is still the user's key.
        var key = apiKey?.Trim();
        if (string.IsNullOrEmpty(key) && storedKey is not null)
            key = await storedKey.RevealAsync(userId, cancellationToken);
        if (string.IsNullOrEmpty(key))
            return Result.Failure<ShowcaseAssistantReplyDto>(ErrorCodes.ShowcaseAssistantKeyRequired, "Hãy nhập API key Gemini của bạn để dùng trợ lý.");
        if (!ShowcaseAssistantApiKey.IsWellFormed(key))
            return Result.Failure<ShowcaseAssistantReplyDto>(ErrorCodes.ShowcaseAssistantKeyInvalid, "API key không đúng định dạng. Hãy dán lại đúng key từ Google AI Studio.");

        var message = request.Message.Trim();
        var history = request.History.TakeLast(MaximumHistoryTurnsSent)
            .Select(turn => new ShowcaseAssistantTurn(turn.Role, Truncate(turn.Content.Trim(), 600))).ToList();
        var (total, rows) = await LoadCandidatesAsync(cancellationToken);
        if (total == 0)
            return Result.Success(new ShowcaseAssistantReplyDto
            {
                Answer = "Hiện chưa có dự án nào được công bố trên Showcase, nên mình chưa có gì để gợi ý. Bạn quay lại sau nhé!"
            });

        var selected = Select(rows, message, history);
        var byRef = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<ShowcaseAssistantCatalogEntry>(selected.Count);
        foreach (var row in selected)
        {
            var reference = $"P{entries.Count + 1}";
            byRef[reference] = row;
            entries.Add(row.ToEntry(reference));
        }

        ShowcaseAssistantModelReply reply;
        try
        {
            reply = await model.AskAsync(
                new ShowcaseAssistantModelInput(message, history, entries, total, DateOnly.FromDateTime(time.UtcNow)), key, cancellationToken);
        }
        catch (ShowcaseAssistantKeyRejectedException exception)
        {
            return Result.Failure<ShowcaseAssistantReplyDto>(ErrorCodes.ShowcaseAssistantKeyInvalid, exception.UserMessage);
        }
        catch (ShowcaseAssistantUnavailableException exception)
        {
            return Result.Failure<ShowcaseAssistantReplyDto>(ErrorCodes.ShowcaseAssistantUnavailable, exception.UserMessage);
        }

        var projects = reply.Picks
            .Select(pick => (Ref: pick.Ref?.Trim() ?? string.Empty, pick.Reason))
            .Where(pick => byRef.ContainsKey(pick.Ref))
            .DistinctBy(pick => pick.Ref, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumRecommendations)
            .Select(pick => byRef[pick.Ref].ToDto(Clean(pick.Reason, MaximumReasonLength)))
            .ToList();
        var answer = Clean(reply.Answer, MaximumAnswerLength);
        if (answer.Length == 0)
            answer = projects.Count > 0 ? "Đây là những dự án phù hợp nhất mình tìm được." : "Mình chưa tìm được dự án phù hợp với câu hỏi này.";
        var suggestions = reply.Suggestions.Select(item => Clean(item, MaximumSuggestionLength)).Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(MaximumSuggestions).ToList();
        return Result.Success(new ShowcaseAssistantReplyDto { Answer = answer, Projects = projects, Suggestions = suggestions });
    }

    private async Task<(int Total, List<Candidate> Rows)> LoadCandidatesAsync(CancellationToken cancellationToken)
    {
        // Same lineage rule as showcase discovery: the newest Project of a lineage is the page's public profile.
        var latest = context.Projects.AsNoTracking().Where(project => project.Id == context.Projects
            .Where(other => other.ProjectLineageId == project.ProjectLineageId)
            .OrderByDescending(other => other.CreatedAt).ThenByDescending(other => other.Id)
            .Select(other => other.Id).First());
        var published = from showcase in context.ShowcaseProjects.AsNoTracking()
                        join project in latest on showcase.ProjectLineageId equals project.ProjectLineageId
                        where showcase.Status == ShowcaseStatus.Published
                        select new { Showcase = showcase, Project = project };
        var total = await published.CountAsync(cancellationToken);
        var rows = await published.OrderByDescending(row => row.Showcase.PublishedAt).ThenBy(row => row.Showcase.Id)
            .Take(MaximumCandidates)
            .Select(row => new Candidate
            {
                Id = row.Showcase.Id,
                Name = row.Project.Name,
                Description = row.Project.Description,
                Problem = row.Project.Problem,
                Solution = row.Project.Solution,
                TargetUsers = row.Project.TargetUsers,
                Highlights = row.Showcase.Highlights,
                LogoUrl = row.Showcase.LogoUrl,
                Stage = row.Showcase.DevelopmentStage.ToString(),
                Industries = row.Project.ProjectTags.Where(tag => tag.TagType == ProjectTagType.StartupField)
                    .OrderBy(tag => tag.TagName).Select(tag => tag.TagName).ToList(),
                SemesterId = row.Project.Team.Class.SemesterId,
                Team = row.Project.Team.TeamName,
                Likes = row.Showcase.LikeCount,
                Views = row.Showcase.ViewCount,
                Comments = row.Showcase.CommentCount,
                Featured = row.Showcase.IsFeatured,
                PublishedAt = row.Showcase.PublishedAt
            }).ToListAsync(cancellationToken);
        // Looked up separately so a page is never dropped because its class has no semester row.
        var semesters = await context.Semesters.AsNoTracking().ToDictionaryAsync(semester => semester.Id, semester => semester.Code, cancellationToken);
        foreach (var row in rows) row.Semester = semesters.GetValueOrDefault(row.SemesterId, string.Empty);
        return (total, rows);
    }

    /// <summary>Keeps the prompt small: with more than <see cref="MaximumCatalogSize"/> showcases, send the best keyword matches.</summary>
    private static List<Candidate> Select(List<Candidate> rows, string message, IReadOnlyList<ShowcaseAssistantTurn> history)
    {
        if (rows.Count <= MaximumCatalogSize) return rows;
        var terms = Tokens(message + " " + string.Join(' ', history.Where(turn => turn.Role == "user").Select(turn => turn.Content)));
        return rows.Select(row => (Row: row, Score: row.Score(terms)))
            .OrderByDescending(item => item.Score).ThenByDescending(item => item.Row.Likes).ThenByDescending(item => item.Row.PublishedAt)
            .Take(MaximumCatalogSize).Select(item => item.Row).ToList();
    }

    internal static HashSet<string> Tokens(string text) => Fold(text)
        .Split([' ', ',', '.', ';', ':', '!', '?', '(', ')', '/', '-', '"', '\''], StringSplitOptions.RemoveEmptyEntries)
        .Where(token => token.Length >= 2).ToHashSet(StringComparer.Ordinal);

    /// <summary>Lower-cases and strips Vietnamese diacritics so "giáo dục" matches "giao duc".</summary>
    internal static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];

    /// <summary>Model text is shown as plain text; links are removed so catalog content cannot smuggle a URL into the chat.</summary>
    private static string Clean(string? text, int length)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        try { text = Url.Replace(text, "[liên kết đã ẩn]"); }
        catch (RegexMatchTimeoutException) { return string.Empty; }
        return Truncate(text.Trim(), length);
    }

    private sealed class Candidate
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? Problem { get; init; }
        public string? Solution { get; init; }
        public string? TargetUsers { get; init; }
        public string? Highlights { get; init; }
        public string? LogoUrl { get; init; }
        public string Stage { get; init; } = string.Empty;
        public List<string> Industries { get; init; } = [];
        public Guid SemesterId { get; init; }
        public string Semester { get; set; } = string.Empty;
        public string Team { get; init; } = string.Empty;
        public int Likes { get; init; }
        public int Views { get; init; }
        public int Comments { get; init; }
        public bool Featured { get; init; }
        public DateTime? PublishedAt { get; init; }

        public int Score(HashSet<string> terms)
        {
            var name = Tokens(Name);
            var industries = Tokens(string.Join(' ', Industries));
            var body = Tokens($"{Description} {Problem} {Solution} {TargetUsers} {Highlights}");
            return terms.Sum(term => (name.Contains(term) ? 3 : 0) + (industries.Contains(term) ? 3 : 0) + (body.Contains(term) ? 1 : 0));
        }

        public ShowcaseAssistantCatalogEntry ToEntry(string reference) => new(
            reference, Name, Truncate(Description ?? string.Empty, 400), Shorten(Problem), Shorten(Solution), Shorten(TargetUsers), Shorten(Highlights),
            Stage, Industries, Semester, Team, Likes, Views, Comments, Featured, PublishedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        public ShowcaseAssistantProjectDto ToDto(string reason) => new()
        {
            Id = Id, ProjectName = Name, Description = Truncate(Description ?? string.Empty, 200), LogoUrl = LogoUrl, DevelopmentStage = Stage,
            TeamName = Team, Industries = Industries, LikeCount = Likes, ViewCount = Views, Reason = reason
        };

        private static string? Shorten(string? text) => string.IsNullOrWhiteSpace(text) ? null : Truncate(text.Trim(), 250);
    }
}
