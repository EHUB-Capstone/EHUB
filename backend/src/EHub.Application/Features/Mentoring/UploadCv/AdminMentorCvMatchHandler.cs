using System.IO.Compression;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Mentoring;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Mentoring.UploadCv;

public interface IAdminMentorCvMatchHandler
{
    Task<Result<MentorCvMatchResponse>> MatchAsync(Stream content, string fileName, long length,
        CancellationToken cancellationToken);
}

public sealed class AdminMentorCvMatchHandler(IApplicationDbContext context, ICurrentUserService currentUser,
    IMentorCvTextReader reader) : IAdminMentorCvMatchHandler
{
    private const int MaximumDocumentBytes = 10 * 1024 * 1024;

    public async Task<Result<MentorCvMatchResponse>> MatchAsync(Stream content, string fileName, long length,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null || currentUser.UserId == Guid.Empty)
            return Result.Failure<MentorCvMatchResponse>(ErrorCodes.CommonUnauthorizedError,
                "An authenticated administrator is required.");
        if (!currentUser.Roles.Contains(SystemRoles.Admin))
            return Result.Failure<MentorCvMatchResponse>(ErrorCodes.ClassAccessDenied,
                "Only an administrator can match mentor CVs.");
        if (length is <= 0 or > MaximumDocumentBytes)
            return Invalid("Choose a non-empty CV up to 10 MB.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".doc" or ".docx"))
            return Invalid("Only PDF, DOC and DOCX documents are accepted.");

        await using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = await content.ReadAsync(chunk.AsMemory(0,
                   (int)Math.Min(chunk.Length, MaximumDocumentBytes - buffer.Length + 1)), cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaximumDocumentBytes) return Invalid("CV must be 10 MB or smaller.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        var bytes = buffer.ToArray();
        if (buffer.Length != length || !ValidDocument(bytes, extension))
            return Invalid("Document content does not match its format.");

        var text = await reader.ReadAsync(bytes, extension, cancellationToken);
        if (text.IsFailure) return Result.Failure<MentorCvMatchResponse>(text.Error);
        cancellationToken.ThrowIfCancellationRequested();
        if (text.Value.Length > 60_000) return Invalid("The CV exceeds the supported text limit.");
        // The professional parser intentionally removes contacts. Matching reads only
        // email addresses from the original extracted text and never persists that text.
        var emails = MentorCvEmailParser.Parse(text.Value);
        if (emails.IsAmbiguous) return Preview(MentorCvMatchStatuses.Ambiguous, emails.Values);
        if (emails.Values.Length == 0) return Preview(MentorCvMatchStatuses.NoEmail, emails.Values);

        var ids = await (from profile in context.MentorProfiles.AsNoTracking()
                         join user in context.Users.AsNoTracking() on profile.UserId equals user.Id
                         where emails.Values.Contains(user.Email.Trim().ToLower())
                         select profile.Id).Distinct().Take(2).ToArrayAsync(cancellationToken);
        return ids.Length switch
        {
            1 => Preview(MentorCvMatchStatuses.Matched, emails.Values, ids[0]),
            > 1 => Preview(MentorCvMatchStatuses.Ambiguous, emails.Values),
            _ => Preview(MentorCvMatchStatuses.NoMatch, emails.Values)
        };
    }

    private static Result<MentorCvMatchResponse> Preview(string status, string[] emails, Guid? id = null) =>
        Result.Success(new MentorCvMatchResponse { Status = status, MentorProfileId = id, DetectedEmails = emails });

    private static Result<MentorCvMatchResponse> Invalid(string message) =>
        Result.Failure<MentorCvMatchResponse>(ErrorCodes.CommonValidationError, message);

    private static bool ValidDocument(byte[] bytes, string extension)
    {
        if (extension == ".pdf") return bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
        if (extension == ".doc") return bytes.Length >= 8 &&
            bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 });
        if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4B) return false;
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            return archive.GetEntry("word/document.xml") is not null;
        }
        catch (InvalidDataException) { return false; }
    }
}
