using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Application.Features.Showcase.Manage;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class ShowcaseManageHandlerTests
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    private readonly IImageStorageService _storage = Substitute.For<IImageStorageService>();

    public ShowcaseManageHandlerTests()
    {
        _storage.UploadShowcaseImageAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(Result.Success(new ImageUploadResult("https://cdn.example.test/showcase.png")));
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyProjectsOfActiveTeamMembersWithTheirLiveProfile()
    {
        await using var db = Context();
        var mine = await SeedProjectAsync(db);
        await SeedProjectAsync(db);

        var result = await Handler(db).GetMineAsync(mine.UserId, SystemRoles.Student);

        result.IsSuccess.Should().BeTrue();
        var entry = result.Value.Should().ContainSingle().Subject;
        entry.ProjectLineageId.Should().Be(mine.LineageId);
        entry.Showcase.Should().BeNull();
        entry.TeamId.Should().NotBeEmpty();
        entry.Profile.ProjectName.Should().Be("Smart Garden");
        entry.Profile.Description.Should().Be("Waters your plants while you travel.");
        entry.Profile.Problem.Should().Be("Plants die");
        entry.Profile.Solution.Should().Be("A sensor and a pump");
        entry.Profile.TargetUsers.Should().Be("Busy students");
    }

    [Fact]
    public async Task ShowcaseShowsTheCurrentProjectProfile_NotACopyTakenAtCreation()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var showcase = await SeedShowcaseAsync(db, owner.LineageId, ShowcaseStatus.Draft);
        var project = await db.Projects.SingleAsync();
        project.Name = "Smart Garden Pro";
        project.Description = "Renamed in the Project Profile.";
        await db.SaveChangesAsync();

        var result = await Handler(db).GetMineAsync(owner.UserId, SystemRoles.Student);

        var shown = result.Value.Single().Showcase!;
        shown.Id.Should().Be(showcase.Id);
        shown.Profile.ProjectName.Should().Be("Smart Garden Pro");
        shown.Profile.Description.Should().Be("Renamed in the Project Profile.");
    }

    [Fact]
    public async Task GetMine_IgnoresProjectsSupersededByANewerProjectInTheSameLineage()
    {
        await using var db = Context();
        var older = await SeedProjectAsync(db);
        await Task.Delay(5);
        await SeedProjectAsync(db, lineageId: older.LineageId);

        var result = await Handler(db).GetMineAsync(older.UserId, SystemRoles.Student);

        result.Value.Should().BeEmpty();
    }

    [Theory]
    [InlineData(SystemRoles.Lecturer)]
    [InlineData(SystemRoles.Admin)]
    [InlineData(SystemRoles.Mentor)]
    public async Task NonStudentsCannotManageShowcases(string role)
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var handler = Handler(db);

        var mine = await handler.GetMineAsync(owner.UserId, role);
        var create = await handler.CreateAsync(ValidRequest(owner.LineageId), owner.UserId, role);

        mine.Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
        create.Error.Code.Should().Be(ErrorCodes.ShowcaseAccessDenied);
        db.ShowcaseProjects.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_SavesDraftWithShowcaseOnlyContentAndLinks()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);

        var result = await Handler(db).CreateAsync(
            ValidRequest(owner.LineageId, highlights: "  Works offline  "), owner.UserId, SystemRoles.Student);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(ShowcaseStatus.Draft));
        result.Value.Highlights.Should().Be("Works offline");
        result.Value.Profile.ProjectName.Should().Be("Smart Garden");
        result.Value.DevelopmentStage.Should().Be(nameof(ShowcaseDevelopmentStage.Mvp));
        result.Value.Links.Should().ContainSingle().Which.Url.Should().Be("https://garden.example.test");
        var stored = await db.ShowcaseProjects.SingleAsync();
        stored.ProjectLineageId.Should().Be(owner.LineageId);
        stored.CreatedBy.Should().Be(owner.UserId);
    }

    [Fact]
    public async Task Create_RejectsStudentsWhoDoNotOwnTheLineage_WithoutRevealingIt()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var outsider = Guid.NewGuid();

        var result = await Handler(db).CreateAsync(ValidRequest(owner.LineageId), outsider, SystemRoles.Student);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
        db.ShowcaseProjects.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_RejectsInactiveTeamMember()
    {
        await using var db = Context();
        var former = await SeedProjectAsync(db, countsTowardActiveTeam: false);

        var result = await Handler(db).CreateAsync(ValidRequest(former.LineageId), former.UserId, SystemRoles.Student);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
    }

    [Fact]
    public async Task Create_RejectsSecondShowcaseForTheSameLineage()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var handler = Handler(db);
        (await handler.CreateAsync(ValidRequest(owner.LineageId), owner.UserId, SystemRoles.Student)).IsSuccess.Should().BeTrue();

        var duplicate = await handler.CreateAsync(ValidRequest(owner.LineageId), owner.UserId, SystemRoles.Student);

        duplicate.Error.Code.Should().Be(ErrorCodes.ShowcaseAlreadyExists);
        db.ShowcaseProjects.Should().ContainSingle();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,AAAA")]
    [InlineData("ftp://files.example.test/a")]
    [InlineData("https://user:secret@example.test")]
    [InlineData("not a url")]
    public async Task Create_RejectsUnsafeLinks(string url)
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var request = ValidRequest(owner.LineageId, links: [new ShowcaseLinkRequest { Label = "Site", Url = url }]);

        var result = await Handler(db).CreateAsync(request, owner.UserId, SystemRoles.Student);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
        db.ShowcaseProjects.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://example.test/video.mp4")]
    [InlineData("http://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://vimeo.com/channels/staffpicks")]
    [InlineData("https://youtu.be.evil.example/dQw4w9WgXcQ")]
    public async Task Create_RejectsVideoLinksFromOtherProviders(string url)
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var request = ValidRequest(owner.LineageId, links: [new ShowcaseLinkRequest { LinkType = "Video", Label = "Demo", Url = url }]);

        var result = await Handler(db).CreateAsync(request, owner.UserId, SystemRoles.Student);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
        db.ShowcaseProjects.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://vimeo.com/123456789")]
    public async Task Create_AcceptsYouTubeAndVimeoVideoLinks_AndOtherTypesStayUnrestricted(string url)
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var request = ValidRequest(owner.LineageId, links:
        [
            new ShowcaseLinkRequest { LinkType = "Video", Label = "Demo", Url = url },
            new ShowcaseLinkRequest { LinkType = "Website", Label = "Site", Url = "https://garden.example.test/video.mp4" }
        ]);

        var result = await Handler(db).CreateAsync(request, owner.UserId, SystemRoles.Student);

        result.IsSuccess.Should().BeTrue();
        db.ShowcaseLinks.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_RejectsInvalidStageAndOverlongHighlights()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var handler = Handler(db);

        var stage = await handler.CreateAsync(ValidRequest(owner.LineageId, stage: "Unicorn"), owner.UserId, SystemRoles.Student);
        var highlights = await handler.CreateAsync(ValidRequest(owner.LineageId, highlights: new string('x', 2001)), owner.UserId, SystemRoles.Student);

        stage.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
        highlights.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
    }

    [Fact]
    public async Task Update_ChangesContentAndReplacesLinks()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var showcase = await SeedShowcaseAsync(db, owner.LineageId, ShowcaseStatus.Draft);
        var request = ValidRequest(owner.LineageId, highlights: "Updated highlights", links:
        [
            new ShowcaseLinkRequest { LinkType = "Demo", Label = "Demo", Url = "https://demo.example.test" }
        ]);

        var result = await Handler(db).UpdateAsync(showcase.Id, request, owner.UserId, SystemRoles.Student);

        result.IsSuccess.Should().BeTrue();
        result.Value.Highlights.Should().Be("Updated highlights");
        (await db.ShowcaseLinks.ToListAsync()).Should().ContainSingle().Which.Url.Should().Be("https://demo.example.test");
        db.ShowcaseReviewHistories.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_OfPublishedShowcaseKeepsItPublishedAndRecordsHistory()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var showcase = await SeedShowcaseAsync(db, owner.LineageId, ShowcaseStatus.Published);

        var result = await Handler(db).UpdateAsync(showcase.Id, ValidRequest(owner.LineageId), owner.UserId, SystemRoles.Student);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(ShowcaseStatus.Published));
        var history = await db.ShowcaseReviewHistories.SingleAsync();
        history.ActorId.Should().Be(owner.UserId);
        history.FromStatus.Should().Be(ShowcaseStatus.Published);
        history.ToStatus.Should().Be(ShowcaseStatus.Published);
    }

    [Fact]
    public async Task Update_IsLockedWhilePendingReview()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var showcase = await SeedShowcaseAsync(db, owner.LineageId, ShowcaseStatus.PendingReview);

        var result = await Handler(db).UpdateAsync(showcase.Id, ValidRequest(owner.LineageId), owner.UserId, SystemRoles.Student);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseInvalidState);
    }

    [Fact]
    public async Task Update_RejectsStaleRowVersion()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var showcase = await SeedShowcaseAsync(db, owner.LineageId, ShowcaseStatus.Draft);
        var request = ValidRequest(owner.LineageId, rowVersion: showcase.Version + 7);

        var result = await Handler(db).UpdateAsync(showcase.Id, request, owner.UserId, SystemRoles.Student);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseConcurrencyConflict);
    }

    [Fact]
    public async Task Update_ByAnotherTeamLooksLikeNotFound()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var other = await SeedProjectAsync(db);
        var showcase = await SeedShowcaseAsync(db, owner.LineageId, ShowcaseStatus.Draft);

        var result = await Handler(db).UpdateAsync(showcase.Id, ValidRequest(owner.LineageId), other.UserId, SystemRoles.Student);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
        (await db.ShowcaseProjects.SingleAsync()).Highlights.Should().Be("Existing highlights");
    }

    [Fact]
    public async Task UploadMedia_StoresValidImageAndAppendsInOrder()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var showcase = await SeedShowcaseAsync(db, owner.LineageId, ShowcaseStatus.Draft);
        var handler = Handler(db);

        var first = await handler.UploadMediaAsync(showcase.Id, new MemoryStream(PngBytes), "a.png", PngBytes.Length, owner.UserId, SystemRoles.Student);
        var second = await handler.UploadMediaAsync(showcase.Id, new MemoryStream(PngBytes), "b.PNG", PngBytes.Length, owner.UserId, SystemRoles.Student);

        first.Value.Url.Should().Be("https://cdn.example.test/showcase.png");
        first.Value.SortOrder.Should().Be(0);
        second.Value.SortOrder.Should().Be(1);
        // The client file name never reaches the storage provider.
        await _storage.Received(2).UploadShowcaseImageAsync(Arg.Any<Stream>(), "showcase.png", showcase.Id, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("evil.exe", new byte[] { 0x4D, 0x5A, 0x90 })]
    [InlineData("fake.png", new byte[] { 0x4D, 0x5A, 0x90, 0x00 })]
    [InlineData("empty.png", new byte[0])]
    public async Task UploadMedia_RejectsWrongTypeOrSignature(string fileName, byte[] bytes)
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var showcase = await SeedShowcaseAsync(db, owner.LineageId, ShowcaseStatus.Draft);

        var result = await Handler(db).UploadMediaAsync(
            showcase.Id, new MemoryStream(bytes), fileName, bytes.Length, owner.UserId, SystemRoles.Student);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
        await _storage.DidNotReceiveWithAnyArgs().UploadShowcaseImageAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task UploadMedia_RejectsOversizedImageAndFullGallery()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var showcase = await SeedShowcaseAsync(db, owner.LineageId, ShowcaseStatus.Draft);
        var handler = Handler(db);

        var tooLarge = await handler.UploadMediaAsync(
            showcase.Id, new MemoryStream(PngBytes), "big.png", ShowcaseManageHandler.MaximumImageBytes + 1, owner.UserId, SystemRoles.Student);
        for (var i = 0; i < ShowcaseManageHandler.MaximumMediaItems; i++)
            db.ShowcaseMedia.Add(new ShowcaseMedia { ShowcaseProjectId = showcase.Id, Url = $"https://cdn.example.test/{i}.png", SortOrder = i });
        await db.SaveChangesAsync();
        var full = await handler.UploadMediaAsync(
            showcase.Id, new MemoryStream(PngBytes), "ok.png", PngBytes.Length, owner.UserId, SystemRoles.Student);

        tooLarge.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
        full.Error.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
    }

    [Fact]
    public async Task UploadMedia_ByAnotherTeamIsNotFoundAndNeverReachesStorage()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var other = await SeedProjectAsync(db);
        var showcase = await SeedShowcaseAsync(db, owner.LineageId, ShowcaseStatus.Draft);

        var result = await Handler(db).UploadMediaAsync(
            showcase.Id, new MemoryStream(PngBytes), "a.png", PngBytes.Length, other.UserId, SystemRoles.Student);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
        await _storage.DidNotReceiveWithAnyArgs().UploadShowcaseImageAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task UploadLogo_SetsLogoUrl_AndDeleteMediaRemovesOnlyOwnImage()
    {
        await using var db = Context();
        var owner = await SeedProjectAsync(db);
        var showcase = await SeedShowcaseAsync(db, owner.LineageId, ShowcaseStatus.Draft);
        var handler = Handler(db);

        var logo = await handler.UploadLogoAsync(showcase.Id, new MemoryStream(PngBytes), "logo.png", PngBytes.Length, owner.UserId, SystemRoles.Student);
        var media = await handler.UploadMediaAsync(showcase.Id, new MemoryStream(PngBytes), "a.png", PngBytes.Length, owner.UserId, SystemRoles.Student);
        var missing = await handler.DeleteMediaAsync(showcase.Id, Guid.NewGuid(), owner.UserId, SystemRoles.Student);
        var deleted = await handler.DeleteMediaAsync(showcase.Id, media.Value.Id, owner.UserId, SystemRoles.Student);

        logo.Value.LogoUrl.Should().Be("https://cdn.example.test/showcase.png");
        missing.Error.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
        deleted.IsSuccess.Should().BeTrue();
        db.ShowcaseMedia.Should().BeEmpty();
    }

    private ShowcaseManageHandler Handler(AppDbContext db)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);
        return new ShowcaseManageHandler(db, _storage, clock, new SaveShowcaseRequestValidator());
    }

    private static AppDbContext Context() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SaveShowcaseRequest ValidRequest(
        Guid lineageId,
        string? highlights = "Works offline",
        string stage = "Mvp",
        IReadOnlyCollection<ShowcaseLinkRequest>? links = null,
        uint? rowVersion = null) => new()
    {
        ProjectLineageId = lineageId,
        Highlights = highlights,
        DevelopmentStage = stage,
        Links = links ?? [new ShowcaseLinkRequest { LinkType = "Website", Label = "Website", Url = "https://garden.example.test" }],
        RowVersion = rowVersion
    };

    private sealed record Owner(Guid UserId, Guid LineageId);

    /// <summary>Seeds a student on an active team whose project is the newest of its lineage unless a lineage is supplied.</summary>
    private static async Task<Owner> SeedProjectAsync(AppDbContext db, Guid? lineageId = null, bool countsTowardActiveTeam = true)
    {
        var userId = Guid.NewGuid();
        var @class = new Class { ClassCode = "SE1801", Slug = Guid.NewGuid().ToString("N") };
        var classId = @class.Id;
        var student = new Student { FullName = "Student", UserId = userId };
        var enrollment = new ClassStudent
        {
            Class = @class, ClassId = classId, Student = student, StudentId = student.Id, EnrollmentStatus = EnrollmentStatus.Active
        };
        var team = new Team { Class = @class, ClassId = classId, TeamCode = Guid.NewGuid().ToString("N")[..8], TeamName = "Team", Status = TeamStatus.Active };
        team.TeamMembers.Add(new TeamMember
        {
            Team = team, TeamId = team.Id, ClassId = classId, StudentId = student.Id, ClassStudent = enrollment,
            CountsTowardActiveTeam = countsTowardActiveTeam
        });
        var project = new Project
        {
            Team = team, TeamId = team.Id, Name = "Smart Garden", Problem = "Plants die",
            Description = "Waters your plants while you travel.",
            Solution = "A sensor and a pump", TargetUsers = "Busy students",
            ProjectLineageId = lineageId ?? Guid.NewGuid()
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return new Owner(userId, project.ProjectLineageId);
    }

    private static async Task<ShowcaseProject> SeedShowcaseAsync(AppDbContext db, Guid lineageId, ShowcaseStatus status)
    {
        var showcase = new ShowcaseProject { ProjectLineageId = lineageId, Highlights = "Existing highlights", Status = status };
        showcase.Links.Add(new ShowcaseLink { Label = "Old", Url = "https://old.example.test", SortOrder = 0 });
        db.ShowcaseProjects.Add(showcase);
        await db.SaveChangesAsync();
        return showcase;
    }
}
