using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Application.Features.Mentoring;
using EHub.Application.Features.Mentoring.UploadCv;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Mentoring;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class AdminMentorCvUploadTests
{
    private const string ProfessionalCv = """
        Personal information
        PRIVATE_PERSON_MARKER
        Education
        Demo Academy - Software Engineering
        Work experience
        Built backend services using C# and PostgreSQL.
        Professional skills
        Software engineering and Cloud.
        Family
        PRIVATE_FAMILY_MARKER
        """;

    [Theory]
    [InlineData(false, SystemRoles.Admin, false, ErrorCodes.CommonUnauthorizedError)]
    [InlineData(true, SystemRoles.Admin, true, ErrorCodes.CommonUnauthorizedError)]
    [InlineData(true, SystemRoles.Mentor, false, ErrorCodes.ClassAccessDenied)]
    [InlineData(true, SystemRoles.Lecturer, false, ErrorCodes.ClassAccessDenied)]
    [InlineData(true, SystemRoles.Student, false, ErrorCodes.ClassAccessDenied)]
    public async Task UploadRequiresAuthenticatedAdminBeforeReadingFile(bool authenticated, string role,
        bool emptyUserId, string code)
    {
        await using var db = Context();
        var reader = Substitute.For<IMentorCvTextReader>();
        var storage = Documents();
        var handler = Handler(db, storage, User(role, authenticated, emptyUserId), reader);
        using var content = new MemoryStream(Docx(ProfessionalCv));

        var result = await handler.UploadAsync(Guid.NewGuid(), content, "cv.docx", content.Length, false, default);

        result.Error.Code.Should().Be(code);
        content.Position.Should().Be(0);
        await reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default!, default);
        await storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default, default!, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrAccountlessProfileCannotReceiveCv(bool accountless)
    {
        await using var db = Context();
        var targetId = Guid.NewGuid();
        if (accountless)
        {
            var orphan = new MentorProfile { UserId = Guid.NewGuid(), Type = MentorType.Enterprise };
            db.MentorProfiles.Add(orphan);
            await db.SaveChangesAsync();
            targetId = orphan.Id;
        }
        var storage = Documents();
        using var content = new MemoryStream(Docx(ProfessionalCv));

        var result = await Handler(db, storage).UploadAsync(targetId, content, "cv.docx", content.Length, false, default);

        result.Error.Code.Should().Be(ErrorCodes.CommonNotFoundError);
        content.Position.Should().Be(0);
        await storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default, default!, default);
    }

    [Fact]
    public async Task AdminAttachesOriginalCvToSelectedMentorAndOnlyExtractsProfessionalMetadata()
    {
        await using var db = Context();
        var selected = Profile("selected@example.test", MentorType.Academic);
        var other = Profile("other@example.test");
        db.MentorProfiles.AddRange(selected, other);
        await db.SaveChangesAsync();
        var storage = Documents();
        var bytes = Docx(ProfessionalCv);
        byte[]? original = null;
        storage.UploadAsync(Arg.Any<Stream>(), "cv.docx", selected.Id, "cv", Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                using var copy = new MemoryStream();
                call.ArgAt<Stream>(0).CopyTo(copy);
                original = copy.ToArray();
                return Result.Success(("https://storage.example.test/new", "new-cv"));
            });
        using var content = new MemoryStream(bytes);

        var result = await Handler(db, storage).UploadAsync(selected.Id, content, "cv.docx", bytes.Length, false, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(selected.Id);
        result.Value.CvFileName.Should().Be("cv.docx");
        result.Value.Experience.Should().Contain("backend").And.NotContain("PRIVATE");
        result.Value.Bio.Should().Contain("Demo Academy").And.NotContain("PRIVATE");
        result.Value.MentorType.Should().Be("IT");
        result.Value.TechnologySkills.Should().Equal("Manual technology");
        result.Value.Tags.Should().Equal("Manual tag");
        original.Should().Equal(bytes, "the private original document is stored intact, while matching uses only professional extraction");
        db.ChangeTracker.Clear();
        var unchanged = await db.MentorProfiles.AsNoTracking().SingleAsync(profile => profile.Id == other.Id);
        unchanged.CvFileName.Should().BeNull();
        unchanged.Experience.Should().Be("Previously reviewed experience");
    }

    [Fact]
    public async Task ExistingCvRequiresExplicitReplacementBeforeAnyExtractionOrStorage()
    {
        await using var db = Context();
        var profile = Profile("existing@example.test", withCv: true);
        db.MentorProfiles.Add(profile);
        await db.SaveChangesAsync();
        var reader = Substitute.For<IMentorCvTextReader>();
        var storage = Documents();
        using var content = new MemoryStream(Docx(ProfessionalCv));

        var result = await Handler(db, storage, reader: reader)
            .UploadAsync(profile.Id, content, "cv.docx", content.Length, false, default);

        result.Error.Code.Should().Be(ErrorCodes.MentorProfileConflict);
        content.Position.Should().Be(0);
        profile.CvPublicId.Should().Be("old-cv");
        await reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default!, default);
        await storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default, default!, default);
        await storage.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Fact]
    public async Task ExplicitReplacementSavesNewCvBeforeDeletingOldDocument()
    {
        await using var db = Context();
        var profile = Profile("replace@example.test", withCv: true);
        db.MentorProfiles.Add(profile);
        await db.SaveChangesAsync();
        var storage = Documents();
        storage.DeleteAsync("old-cv", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            db.MentorProfiles.AsNoTracking().Single(item => item.Id == profile.Id).CvPublicId.Should().Be("new-cv");
            return Task.CompletedTask;
        });
        using var content = new MemoryStream(Docx(ProfessionalCv));

        var result = await Handler(db, storage).UploadAsync(profile.Id, content, "cv.docx", content.Length, true, default);

        result.IsSuccess.Should().BeTrue();
        await storage.Received(1).DeleteAsync("old-cv", Arg.Any<CancellationToken>());
        await storage.DidNotReceive().DeleteAsync("new-cv", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("cv.exe", false)]
    [InlineData("cv.pdf", false)]
    [InlineData("cv.docx", true)]
    public async Task InvalidOrUnextractableFilePreservesExistingCvAndProfile(string name, bool privateOnly)
    {
        await using var db = Context();
        var profile = Profile("invalid@example.test", withCv: true);
        db.MentorProfiles.Add(profile);
        await db.SaveChangesAsync();
        var storage = Documents();
        var bytes = privateOnly ? Docx("Personal information\nPRIVATE_PERSON_MARKER") : Encoding.UTF8.GetBytes("invalid signature");
        using var content = new MemoryStream(bytes);

        var result = await Handler(db, storage).UploadAsync(profile.Id, content, name, bytes.Length, true, default);

        result.IsFailure.Should().BeTrue();
        db.ChangeTracker.Clear();
        var unchanged = await db.MentorProfiles.SingleAsync();
        unchanged.CvPublicId.Should().Be("old-cv");
        unchanged.CvFileName.Should().Be("old.pdf");
        unchanged.Experience.Should().Be("Previously reviewed experience");
        await storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default, default!, default);
        await storage.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Fact]
    public async Task ActualStreamSizeIsBoundedEvenIfDeclaredLengthIsSmall()
    {
        await using var db = Context();
        var profile = Profile("oversize@example.test", withCv: true);
        db.MentorProfiles.Add(profile);
        await db.SaveChangesAsync();
        var storage = Documents();
        using var content = new CountingReadStream(11 * 1024 * 1024);

        var result = await Handler(db, storage).UploadAsync(profile.Id, content, "cv.docx", 1, true, default);

        result.Error.Code.Should().Be(ErrorCodes.CommonValidationError);
        content.BytesRead.Should().Be(10 * 1024 * 1024 + 1);
        profile.CvPublicId.Should().Be("old-cv");
        await storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default, default!, default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedSaveRemovesOnlyNewUploadAndKeepsPreviouslySavedCv(bool concurrency)
    {
        var interceptor = new FailedSaveInterceptor();
        await using var db = Context(interceptor);
        var profile = Profile("concurrent@example.test", withCv: true);
        db.MentorProfiles.Add(profile);
        await db.SaveChangesAsync();
        interceptor.Failure = concurrency ? new DbUpdateConcurrencyException("Synthetic concurrent edit") : new DbUpdateException("Synthetic database failure");
        var storage = Documents();
        using var content = new MemoryStream(Docx(ProfessionalCv));
        var handler = Handler(db, storage);

        if (concurrency)
        {
            var result = await handler.UploadAsync(profile.Id, content, "cv.docx", content.Length, true, default);
            result.Error.Code.Should().Be(ErrorCodes.MentorProfileConflict);
            result.Error.Message.Should().NotContain("Synthetic");
        }
        else
        {
            Func<Task> action = async () => await handler.UploadAsync(profile.Id, content, "cv.docx", content.Length, true, default);
            await action.Should().ThrowAsync<DbUpdateException>();
        }

        db.ChangeTracker.Entries().Should().BeEmpty();
        var unchanged = await db.MentorProfiles.AsNoTracking().SingleAsync();
        unchanged.CvPublicId.Should().Be("old-cv");
        unchanged.Experience.Should().Be("Previously reviewed experience");
        await storage.Received(1).DeleteAsync("new-cv", CancellationToken.None);
        await storage.DidNotReceive().DeleteAsync("old-cv", Arg.Any<CancellationToken>());
    }

    private static AdminMentorCvUploadHandler Handler(AppDbContext db, IMentorDocumentStorageService storage,
        ICurrentUserService? user = null, IMentorCvTextReader? reader = null) => new(db, user ?? User(),
            new MentorProfileHandler(db, storage, Substitute.For<IMentorEmbeddingSearch>(),
                reader ?? new MentorCvTextReader(Substitute.For<IDocumentPreviewConverter>())));

    private static ICurrentUserService User(string role = SystemRoles.Admin, bool authenticated = true, bool emptyUserId = false)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.IsAuthenticated.Returns(authenticated);
        user.UserId.Returns(emptyUserId ? Guid.Empty : Guid.NewGuid());
        user.Roles.Returns([role]);
        return user;
    }

    private static IMentorDocumentStorageService Documents()
    {
        var storage = Substitute.For<IMentorDocumentStorageService>();
        storage.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<Guid>(), "cv", Arg.Any<CancellationToken>())
            .Returns(Result.Success(("https://storage.example.test/new", "new-cv")));
        return storage;
    }

    private static AppDbContext Context(FailedSaveInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new AppDbContext(options.Options);
    }

    private static MentorProfile Profile(string email, MentorType type = MentorType.Enterprise, bool withCv = false) => new()
    {
        User = new User { FullName = "Synthetic mentor", Email = email, NormalizedEmail = email }, Type = type,
        Experience = "Previously reviewed experience", Expertise = ["Manual skill"],
        TechnologySkills = ["Manual technology"], Tags = ["Manual tag"],
        CvFileName = withCv ? "old.pdf" : null, CvPublicId = withCv ? "old-cv" : null,
        CvStorageUrl = withCv ? "https://storage.example.test/old" : null
    };

    private static byte[] Docx(string text)
    {
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var xml = new XDocument(new XElement(word + "document", new XElement(word + "body",
            text.Split('\n').Select(line => new XElement(word + "p", new XElement(word + "r", new XElement(word + "t", line)))))));
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open(), new UTF8Encoding(false)))
            writer.Write(xml.ToString());
        return memory.ToArray();
    }

    private sealed class FailedSaveInterceptor : SaveChangesInterceptor
    {
        public Exception? Failure { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Failure is null ? ValueTask.FromResult(result) : ValueTask.FromException<InterceptionResult<int>>(Failure);
    }

    private sealed class CountingReadStream(long length) : Stream
    {
        public long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = (int)Math.Min(buffer.Length, length - BytesRead);
            buffer.Span[..count].Clear();
            BytesRead += count;
            return ValueTask.FromResult(count);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
