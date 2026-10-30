using System.IO.Compression;
using System.Text;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.UploadCv;
using EHub.Contracts.Mentoring;
using EHub.Domain.Entities;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class AdminMentorCvMatchTests
{
    [Theory]
    [InlineData(false, SystemRoles.Admin, false, ErrorCodes.CommonUnauthorizedError)]
    [InlineData(true, SystemRoles.Admin, true, ErrorCodes.CommonUnauthorizedError)]
    [InlineData(true, SystemRoles.Mentor, false, ErrorCodes.ClassAccessDenied)]
    [InlineData(true, SystemRoles.Lecturer, false, ErrorCodes.ClassAccessDenied)]
    [InlineData(true, SystemRoles.Student, false, ErrorCodes.ClassAccessDenied)]
    public async Task MatchingRequiresAuthenticatedAdminBeforeReadingTheCv(bool authenticated, string role,
        bool emptyIdentity, string expectedCode)
    {
        await using var db = Context();
        var reader = Reader("mentor@example.test");
        using var stream = new MemoryStream(Docx());

        var result = await new AdminMentorCvMatchHandler(db, User(role, authenticated, emptyIdentity), reader)
            .MatchAsync(stream, "cv.docx", stream.Length, default);

        result.Error.Code.Should().Be(expectedCode);
        stream.Position.Should().Be(0);
        await reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default!, default);
    }

    [Theory]
    [InlineData("Contact: MENTOR@EXAMPLE.TEST.")]
    [InlineData("Email: 'mentor@example.test'")]
    [InlineData("Email: (mailto:mentor@example.test), alternate unregistered@example.test")]
    [InlineData("mentor@example.test\nMENTOR@example.test\nmentor@example.test")]
    public async Task UniqueImportedEmailMatchesWithoutSavingOrExposingThePrivateCv(string text)
    {
        await using var db = Context();
        var profile = Profile("mentor@example.test");
        db.MentorProfiles.Add(profile);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        const string privateMarker = "PRIVATE_CV_BODY_MARKER";
        var reader = Reader(text + "\nPersonal information\n" + privateMarker);
        using var stream = new MemoryStream(Docx());

        var result = await Handler(db, reader).MatchAsync(stream, "someone-else.docx", stream.Length, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(MentorCvMatchStatuses.Matched);
        result.Value.MentorProfileId.Should().Be(profile.Id);
        result.Value.DetectedEmails.Should().Contain("mentor@example.test").And.OnlyContain(email => email == email.ToLowerInvariant());
        JsonSerializer.Serialize(result.Value).Should().NotContain(privateMarker).And.NotContain("Personal information");
        db.ChangeTracker.Entries().Should().BeEmpty("the preview uses only AsNoTracking queries and never saves identities or files");
        (await db.Users.CountAsync()).Should().Be(1);
        (await db.MentorProfiles.AsNoTracking().SingleAsync()).CvFileName.Should().BeNull();
        (await db.MentorProfiles.AsNoTracking().SingleAsync()).Experience.Should().Be("Reviewed professional experience");
    }

    [Theory]
    [InlineData("Education\nDemo Academy", MentorCvMatchStatuses.NoEmail)]
    [InlineData("unregistered@example.test", MentorCvMatchStatuses.NoMatch)]
    public async Task MissingOrUnknownEmailsNeverFallBackToTheFilename(string text, string status)
    {
        await using var db = Context();
        db.MentorProfiles.Add(Profile("mentor@example.test"));
        await db.SaveChangesAsync();
        using var stream = new MemoryStream(Docx());

        var result = await Handler(db, Reader(text)).MatchAsync(stream, "Synthetic Mentor.docx", stream.Length, default);

        result.Value.Status.Should().Be(status);
        result.Value.MentorProfileId.Should().BeNull();
    }

    [Fact]
    public async Task MoreThanOneMatchedMentorIsAmbiguousRegardlessOfFilename()
    {
        await using var db = Context();
        db.MentorProfiles.AddRange(Profile("first@example.test"), Profile("second@example.test"));
        await db.SaveChangesAsync();
        using var stream = new MemoryStream(Docx());

        var result = await Handler(db, Reader("FIRST@example.test, second@example.test, unknown@example.test"))
            .MatchAsync(stream, "first@example.test.docx", stream.Length, default);

        result.Value.Status.Should().Be(MentorCvMatchStatuses.Ambiguous);
        result.Value.MentorProfileId.Should().BeNull();
        result.Value.DetectedEmails.Should().HaveCount(3);
    }

    [Fact]
    public async Task RepeatedImportedEmailAcrossProfilesDoesNotChooseTheFirstProfile()
    {
        await using var db = Context();
        db.MentorProfiles.AddRange(Profile("mentor@example.test"), Profile("MENTOR@EXAMPLE.TEST"));
        await db.SaveChangesAsync();
        using var stream = new MemoryStream(Docx());

        var result = await Handler(db, Reader("mentor@example.test")).MatchAsync(stream, "cv.docx", stream.Length, default);

        result.Value.Status.Should().Be(MentorCvMatchStatuses.Ambiguous);
        result.Value.MentorProfileId.Should().BeNull();
    }

    [Fact]
    public async Task MatchingUsesAnExistingAccountBackedProfileAndIgnoresUnrelatedUsers()
    {
        await using var db = Context();
        db.Users.Add(new User { FullName = "Synthetic non-mentor", Email = "user@example.test", NormalizedEmail = "user@example.test" });
        db.MentorProfiles.Add(new MentorProfile { UserId = Guid.NewGuid() });
        await db.SaveChangesAsync();
        using var stream = new MemoryStream(Docx());

        var result = await Handler(db, Reader("user@example.test")).MatchAsync(stream, "cv.docx", stream.Length, default);

        result.Value.Status.Should().Be(MentorCvMatchStatuses.NoMatch);
        result.Value.MentorProfileId.Should().BeNull();
    }

    [Theory]
    [InlineData("ment.or@gmail.com")]
    [InlineData("mentor+advisor@gmail.com")]
    [InlineData("mentor@other.test")]
    public async Task GmailAliasesAndDomainsAreNeverSilentlyRewritten(string email)
    {
        await using var db = Context();
        db.MentorProfiles.Add(Profile("mentor@gmail.com"));
        await db.SaveChangesAsync();
        using var stream = new MemoryStream(Docx());

        var result = await Handler(db, Reader(email)).MatchAsync(stream, "cv.docx", stream.Length, default);

        result.Value.Status.Should().Be(MentorCvMatchStatuses.NoMatch);
        result.Value.DetectedEmails.Should().Equal(email);
    }

    [Fact]
    public async Task ARegisteredPlusAliasMatchesOnlyItsLiteralImportedAddress()
    {
        await using var db = Context();
        var target = Profile("mentor+advisor@gmail.com");
        db.MentorProfiles.Add(target);
        await db.SaveChangesAsync();
        using var stream = new MemoryStream(Docx());

        var result = await Handler(db, Reader("Contact: <MENTOR+ADVISOR@GMAIL.COM>"))
            .MatchAsync(stream, "cv.docx", stream.Length, default);

        result.Value.MentorProfileId.Should().Be(target.Id);
        result.Value.DetectedEmails.Should().Equal("mentor+advisor@gmail.com");
    }

    [Fact]
    public async Task ApostrophesInsideTheMailboxRemainPartOfTheImportedEmail()
    {
        await using var db = Context();
        var target = Profile("o'connor@example.test");
        db.MentorProfiles.Add(target);
        await db.SaveChangesAsync();
        using var stream = new MemoryStream(Docx());

        var result = await Handler(db, Reader("Email: 'O'CONNOR@EXAMPLE.TEST'"))
            .MatchAsync(stream, "cv.docx", stream.Length, default);

        result.Value.MentorProfileId.Should().Be(target.Id);
        result.Value.DetectedEmails.Should().Equal("o'connor@example.test");
    }

    [Theory]
    [InlineData("mentor@@example.test")]
    [InlineData("bad..local@example.test")]
    [InlineData(".mentor@example.test")]
    [InlineData("mentor@example.test@other.test")]
    public async Task MalformedAddressesDoNotMatchValidSubstrings(string text)
    {
        await using var db = Context();
        db.MentorProfiles.Add(Profile("mentor@example.test"));
        await db.SaveChangesAsync();
        using var stream = new MemoryStream(Docx());

        var result = await Handler(db, Reader(text)).MatchAsync(stream, "cv.docx", stream.Length, default);

        result.Value.Status.Should().Be(MentorCvMatchStatuses.NoEmail);
        result.Value.MentorProfileId.Should().BeNull();
    }

    [Fact]
    public async Task ExcessEmailsCannotTurnATruncatedListIntoAUniqueMatch()
    {
        await using var db = Context();
        db.MentorProfiles.Add(Profile("mentor@example.test"));
        await db.SaveChangesAsync();
        var text = "mentor@example.test " + string.Join(' ', Enumerable.Range(1, 20).Select(index => $"email{index}@example.test"));
        using var stream = new MemoryStream(Docx());

        var result = await Handler(db, Reader(text)).MatchAsync(stream, "cv.docx", stream.Length, default);

        result.Value.Status.Should().Be(MentorCvMatchStatuses.Ambiguous);
        result.Value.MentorProfileId.Should().BeNull();
        result.Value.DetectedEmails.Should().HaveCount(20);
    }

    [Theory]
    [InlineData("cv.exe", 1, false)]
    [InlineData("cv.docx", 0, false)]
    [InlineData("cv.pdf", 1, false)]
    [InlineData("cv.docx", 1, true)]
    public async Task InvalidFilesNeverReachTheTextReader(string name, int length, bool oversized)
    {
        await using var db = Context();
        var reader = Reader("mentor@example.test");
        var bytes = oversized ? new byte[10 * 1024 * 1024 + 7] : new byte[1];
        using var stream = new MemoryStream(bytes);

        var result = await Handler(db, reader).MatchAsync(stream, name, length, default);

        result.Error.Code.Should().Be(ErrorCodes.CommonValidationError);
        await reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default!, default);
    }

    [Fact]
    public async Task ReaderUnavailabilityIsReturnedWithoutSavingOrExposingTheCv()
    {
        await using var db = Context();
        var reader = Substitute.For<IMentorCvTextReader>();
        reader.ReadAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<string>(ErrorCodes.MentorCvExtractionUnavailable, "CV reader is temporarily unavailable."));
        using var stream = new MemoryStream(Docx());

        var result = await Handler(db, reader).MatchAsync(stream, "cv.docx", stream.Length, default);

        result.Error.Code.Should().Be(ErrorCodes.MentorCvExtractionUnavailable);
        (await db.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CancellationIsPropagatedBeforeAnyEmailMatching()
    {
        await using var db = Context();
        using var stream = new MemoryStream(Docx());
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var action = () => Handler(db, Reader("mentor@example.test"))
            .MatchAsync(stream, "cv.docx", stream.Length, cancelled.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        (await db.Users.CountAsync()).Should().Be(0);
    }

    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ICurrentUserService User(string role = SystemRoles.Admin, bool authenticated = true, bool emptyIdentity = false)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.IsAuthenticated.Returns(authenticated);
        user.UserId.Returns(emptyIdentity ? Guid.Empty : Guid.NewGuid());
        user.Roles.Returns([role]);
        return user;
    }
    private static IMentorCvTextReader Reader(string text)
    {
        var reader = Substitute.For<IMentorCvTextReader>();
        reader.ReadAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success(text));
        return reader;
    }
    private static AdminMentorCvMatchHandler Handler(AppDbContext context, IMentorCvTextReader reader) => new(context, User(), reader);
    private static MentorProfile Profile(string email) => new()
    {
        User = new User { FullName = "Synthetic Mentor", Email = email, NormalizedEmail = email },
        Experience = "Reviewed professional experience"
    };
    private static byte[] Docx()
    {
        using var bytes = new MemoryStream();
        using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open(), Encoding.UTF8))
            writer.Write("<document>synthetic fixture</document>");
        return bytes.ToArray();
    }
}
