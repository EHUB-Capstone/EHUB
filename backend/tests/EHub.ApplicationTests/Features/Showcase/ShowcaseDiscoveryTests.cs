using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Showcase.Discovery;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class ShowcaseDiscoveryTests
{
    private static GetShowcasesQueryHandler Handler(AppDbContext db, DateTime? now = null) =>
        new(db, new InMemorySearch(), new GetShowcasesRequestValidator(), new FixedClock(now ?? DateTime.UtcNow));

    private sealed record FixedClock(DateTime UtcNow) : IDateTimeProvider;

    [Fact]
    public async Task FeaturedFilter_ComposesWithStageSearchAndPaging_AndNeverIncludesHiddenPages()
    {
        await using var db = ShowcaseTestData.NewContext();
        var first = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var second = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var regular = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var hidden = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Hidden);
        var now = DateTime.UtcNow;
        foreach (var id in new[] { first.ShowcaseId, second.ShowcaseId, hidden.ShowcaseId })
        {
            var page = (await db.ShowcaseProjects.FindAsync(id))!;
            page.IsFeatured = true;
            page.FeaturedAt = now;
        }
        (await db.ShowcaseProjects.FindAsync(first.ShowcaseId))!.DevelopmentStage = ShowcaseDevelopmentStage.Mvp;
        await db.SaveChangesAsync();
        var featured = (await Handler(db).HandleAsync(new() { Featured = true, PageSize = 1, Page = 2 })).Value;
        featured.TotalItems.Should().Be(2);
        featured.Items.Should().ContainSingle().Which.IsFeatured.Should().BeTrue();
        featured.Items.Single().FeaturedAt.Should().Be(now);
        var mvp = (await Handler(db).HandleAsync(new() { Featured = true, Search = "GARDEN", Stage = "Mvp" })).Value;
        mvp.Items.Should().ContainSingle().Which.Id.Should().Be(first.ShowcaseId);
        var normal = (await Handler(db).HandleAsync(new() { Featured = false })).Value;
        normal.Items.Should().ContainSingle().Which.Id.Should().Be(regular.ShowcaseId);
        (await Handler(db).HandleAsync(new())).Value.TotalItems.Should().Be(3);
    }

    [Fact]
    public async Task FilterOptions_ReturnAllActiveCatalogIndustriesAndSemesters_WithoutAnyShowcase()
    {
        await using var db = ShowcaseTestData.NewContext();
        var names = new[] { "BIT_SE", "chatgpt", "ecommom", "infomation", "logictics" };
        db.StartupIndustries.AddRange(names.Select(name => new StartupIndustry { Name = name, NormalizedName = name.ToUpperInvariant() }));
        db.StartupIndustries.Add(new StartupIndustry { Name = "Inactive", NormalizedName = "INACTIVE", Status = StartupIndustryStatus.Inactive });
        db.StartupIndustries.Add(new StartupIndustry { Name = "Deleted", NormalizedName = "DELETED", IsDeleted = true });
        db.Semesters.AddRange(new Semester { Code = "SP26", Year = 2026, Term = SemesterTerm.Spring },
            new Semester { Code = "SU26", Year = 2026, Term = SemesterTerm.Summer },
            new Semester { Code = "FA26", Year = 2026, Term = SemesterTerm.Fall, Status = SemesterStatus.Archived },
            new Semester { Code = "SP27", Year = 2027, Term = SemesterTerm.Spring },
            new Semester { Code = "DELETED", Year = 2028, IsDeleted = true });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var result = await Handler(db).GetFilterOptionsAsync();
        result.Value.Industries.Should().BeEquivalentTo(names);
        result.Value.Semesters.Select(semester => semester.Code).Should().Equal("SP27", "FA26", "SU26", "SP26");
        db.ChangeTracker.Entries().Should().BeEmpty();
        foreach (var name in names)
            (await Handler(db).HandleAsync(new() { Industry = name })).Value.TotalItems.Should().Be(0);
        (await Handler(db).HandleAsync(new() { SemesterId = result.Value.Semesters.First().Id })).Value.TotalItems.Should().Be(0);
    }

    // InMemory does not implement PostgreSQL ILIKE; its SQL translation is tested separately below.
    private sealed class InMemorySearch : IShowcaseProjectSearch
    {
        public IQueryable<Project> Apply(IQueryable<Project> projects, string search) => projects.Where(project =>
            project.Name.ToUpper().Contains(search.ToUpper()) ||
            project.Description != null && project.Description.ToUpper().Contains(search.ToUpper()));
    }

    [Fact]
    public async Task List_ReturnsOnlyPublishedAndLiveRows_WithoutReviewFieldsOrTracking()
    {
        await using var db = ShowcaseTestData.NewContext();
        foreach (var status in Enum.GetValues<ShowcaseStatus>()) await ShowcaseTestData.SeedAsync(db, status);
        var deleted = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        db.ShowcaseProjects.Remove((await db.ShowcaseProjects.FindAsync(deleted.ShowcaseId))!);
        db.ShowcaseProjects.Add(new ShowcaseProject { Status = ShowcaseStatus.Published }); // orphan lineage
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await Handler(db).HandleAsync(new());

        result.Value.TotalItems.Should().Be(1);
        result.Value.Items.Should().ContainSingle().Which.ProjectName.Should().Be("Smart Garden");
        db.ChangeTracker.Entries().Should().BeEmpty();
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        json.Should().NotContain("ReviewNote").And.NotContain("History");
    }

    [Fact]
    public async Task Card_ExposesOnlyTheFirstSupportedVideoLinkAsACanonicalUrl()
    {
        await using var db = ShowcaseTestData.NewContext();
        var supported = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var legacy = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var none = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        db.ShowcaseLinks.AddRange(
            new ShowcaseLink { ShowcaseProjectId = supported.ShowcaseId, LinkType = ShowcaseLinkType.Website, Label = "Site", Url = "https://site.example.test", SortOrder = 0 },
            new ShowcaseLink { ShowcaseProjectId = supported.ShowcaseId, LinkType = ShowcaseLinkType.Video, Label = "Demo", Url = "https://youtu.be/dQw4w9WgXcQ?t=5", SortOrder = 1 },
            new ShowcaseLink { ShowcaseProjectId = legacy.ShowcaseId, LinkType = ShowcaseLinkType.Video, Label = "Old", Url = "https://example.test/old.mp4", SortOrder = 0 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var items = (await Handler(db).HandleAsync(new())).Value.Items.ToDictionary(item => item.Id);

        items[supported.ShowcaseId].VideoUrl.Should().Be("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        items[legacy.ShowcaseId].VideoUrl.Should().BeNull();
        items[none.ShowcaseId].VideoUrl.Should().BeNull();
    }

    [Theory]
    [InlineData(" gArDeN ", 1)]
    [InlineData("AUTOMATICALLY", 1)]
    [InlineData("absent", 0)]
    public async Task Search_MatchesNameOrDescriptionCaseInsensitively(string search, int count)
    {
        await using var db = ShowcaseTestData.NewContext();
        await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var result = await Handler(db).HandleAsync(new() { Search = search });
        result.Value.TotalItems.Should().Be(count);
    }

    [Fact]
    public async Task Filters_UseStageIndustryAndLatestSemester_IndividuallyAndCombined()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var project = await db.Projects.Include(p => p.Team).ThenInclude(t => t.Class)
            .SingleAsync(p => p.ProjectLineageId == seed.LineageId);
        var semesterId = Guid.NewGuid();
        project.Team.Class.SemesterId = semesterId;
        project.ProjectTags.Add(new ProjectTag { TagType = ProjectTagType.StartupField, TagName = "HealthTech", NormalizedTagName = "HEALTHTECH" });
        project.ProjectTags.Add(new ProjectTag { TagType = ProjectTagType.Keyword, TagName = "Internal", NormalizedTagName = "INTERNAL" });
        db.ProjectTags.AddRange(project.ProjectTags);
        (await db.ShowcaseProjects.FindAsync(seed.ShowcaseId))!.DevelopmentStage = ShowcaseDevelopmentStage.Mvp;
        await db.SaveChangesAsync();

        foreach (var request in new GetShowcasesRequest[] {
            new() { Stage = "mvp" }, new() { Industry = " healthtech " }, new() { SemesterId = semesterId },
            new() { Stage = "Mvp", Industry = "HealthTech", SemesterId = semesterId } })
        {
            var result = await Handler(db).HandleAsync(request);
            result.Value.Items.Should().ContainSingle().Which.Id.Should().Be(seed.ShowcaseId);
            result.Value.Items.Single().Industries.Should().Equal("HealthTech");
        }
        (await Handler(db).HandleAsync(new() { Industry = "Internal" })).Value.TotalItems.Should().Be(0);
        (await Handler(db).HandleAsync(new() { Stage = "Beta" })).Value.TotalItems.Should().Be(0);
        (await Handler(db).HandleAsync(new() { SemesterId = Guid.NewGuid() })).Value.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task LatestProfile_WinsBeforeSearchAndIndustryFiltering_AndAppearsOnce()
    {
        await using var db = ShowcaseTestData.NewContext();
        var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published);
        var old = await db.Projects.Include(p => p.Team).ThenInclude(t => t.Class).SingleAsync(p => p.ProjectLineageId == seed.LineageId);
        var oldSemester = Guid.NewGuid();
        var newSemester = Guid.NewGuid();
        old.Team.Class.SemesterId = oldSemester;
        old.ProjectTags.Add(new ProjectTag { TagType = ProjectTagType.StartupField, TagName = "Old", NormalizedTagName = "OLD" });
        db.ProjectTags.AddRange(old.ProjectTags);
        var latest = new Project { ProjectLineageId = seed.LineageId, Name = "New profile", Description = null,
            Team = new Team { TeamName = "New team", Class = new Class { SemesterId = newSemester } }, PreviousProjectId = old.Id };
        db.Projects.Add(latest);
        await db.SaveChangesAsync();
        // Exercise the deterministic Id tie-breaker as well as timestamp ordering.
        old.CreatedAt = latest.CreatedAt.AddDays(-1);
        await db.SaveChangesAsync();
        (await Handler(db).HandleAsync(new() { Search = "Garden" })).Value.TotalItems.Should().Be(0);
        (await Handler(db).HandleAsync(new() { Industry = "Old" })).Value.TotalItems.Should().Be(0);
        (await Handler(db).GetFilterOptionsAsync()).Value.Industries.Should().BeEmpty();
        (await Handler(db).HandleAsync(new() { SemesterId = oldSemester })).Value.TotalItems.Should().Be(0);
        (await Handler(db).HandleAsync(new() { SemesterId = newSemester })).Value.TotalItems.Should().Be(1);
        var result = await Handler(db).HandleAsync(new());
        result.Value.Items.Should().ContainSingle().Which.ProjectName.Should().Be("New profile");
        result.Value.Items.Single().Description.Should().BeEmpty();
        old.CreatedAt = latest.CreatedAt;
        await db.SaveChangesAsync();
        (await Handler(db).HandleAsync(new())).Value.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task SortAndPagination_AreStableAndKeepFilteredTotals()
    {
        await using var db = ShowcaseTestData.NewContext();
        var older = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published, publishedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var newer = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published, publishedAt: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        (await db.ShowcaseProjects.FindAsync(older.ShowcaseId))!.LikeCount = 10;
        await db.SaveChangesAsync();
        var newest = (await Handler(db).HandleAsync(new() { PageSize = 1 })).Value;
        newest.Items.Single().Id.Should().Be(newer.ShowcaseId);
        newest.TotalItems.Should().Be(2);
        newest.TotalPages.Should().Be(2);
        (await Handler(db).HandleAsync(new() { Sort = "popular", PageSize = 1 })).Value.Items.Single().Id.Should().Be(older.ShowcaseId);
        (await Handler(db).HandleAsync(new() { Page = 2, PageSize = 1 })).Value.Items.Single().Id.Should().Be(older.ShowcaseId);
        (await Handler(db).HandleAsync(new() { Page = 3, PageSize = 1 })).Value.Items.Should().BeEmpty();
        var rows = await db.ShowcaseProjects.ToListAsync();
        foreach (var row in rows) { row.PublishedAt = rows[0].PublishedAt; row.LikeCount = 0; }
        await db.SaveChangesAsync();
        var expected = rows.OrderBy(row => row.Id).Select(row => row.Id);
        (await Handler(db).HandleAsync(new())).Value.Items.Select(row => row.Id).Should().Equal(expected);
    }

    [Fact]
    public async Task Trending_UsesRecentVotesAndFreshness_AndBreaksTiesBeforePaging()
    {
        await using var db = ShowcaseTestData.NewContext();
        var now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        var recent = await AddPageAsync(now.AddDays(-7), 5);
        var freshA = await AddPageAsync(now.AddHours(-1), 6);
        var freshB = await AddPageAsync(now.AddHours(-1), 6);
        var oneDay = await AddPageAsync(now.AddDays(-1), 6);
        var fiveDays = await AddPageAsync(now.AddDays(-5), 10);
        var unknownDate = await AddPageAsync(null, 10);
        await AddPageAsync(now, 100, ShowcaseStatus.Hidden);

        AddVote(recent, now.AddDays(-1));
        AddVote(recent, now.AddHours(-2));
        AddVote(fiveDays, now.AddDays(-1).AddSeconds(-1));
        AddVote(fiveDays, now.AddSeconds(1));
        await db.SaveChangesAsync();

        var handler = Handler(db, now);
        var firstPage = (await handler.HandleAsync(new() { Sort = " Trending ", PageSize = 3 })).Value;
        var secondPage = (await handler.HandleAsync(new() { Sort = "trending", Page = 2, PageSize = 3 })).Value;
        var equalTime = new[] { freshA, freshB }.Order().ToArray();

        firstPage.TotalItems.Should().Be(6);
        // The recent project scores 11: below the newer ties, but above the 10-point group.
        firstPage.Items.Select(item => item.Id).Should().Equal(equalTime[0], equalTime[1], recent);
        secondPage.Items.Select(item => item.Id).Should().Equal(oneDay, fiveDays, unknownDate);

        async Task<Guid> AddPageAsync(DateTime? publishedAt, int likes, ShowcaseStatus status = ShowcaseStatus.Published)
        {
            var seed = await ShowcaseTestData.SeedAsync(db, status, publishedAt: publishedAt);
            var showcase = (await db.ShowcaseProjects.FindAsync(seed.ShowcaseId))!;
            showcase.LikeCount = likes;
            return showcase.Id;
        }

        void AddVote(Guid showcaseId, DateTime createdAt) =>
            db.ShowcaseUpvotes.Add(new ShowcaseUpvote { ShowcaseProjectId = showcaseId, UserId = Guid.NewGuid(), CreatedAt = createdAt });
    }

    [Fact]
    public async Task Trending_UsesFractionalSquareRootBonusForViewsFromTheCurrentUtcDay()
    {
        await using var db = ShowcaseTestData.NewContext();
        var now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(now);
        var viewed = await AddPageAsync(8, 5);
        var priorViews = await AddPageAsync(7, 6);
        var leading = await AddPageAsync(6, 7);
        AddViews(viewed, today, 9);
        AddViews(priorViews, today.AddDays(-1), 4);
        AddViews(priorViews, today.AddDays(1), 4);
        await db.SaveChangesAsync();

        var handler = Handler(db, now);
        var firstPage = (await handler.HandleAsync(new() { Sort = "trending", PageSize = 2 })).Value;
        var secondPage = (await handler.HandleAsync(new() { Sort = "trending", PageSize = 2, Page = 2 })).Value;

        // The three scores are 7, 6.5 and 6. The fractional bonus must survive paging and sorting.
        firstPage.Items.Select(item => item.Id).Should().Equal(leading, viewed);
        secondPage.Items.Select(item => item.Id).Should().Equal(priorViews);

        async Task<Guid> AddPageAsync(int daysOld, int likes)
        {
            var seed = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published, publishedAt: now.AddDays(-daysOld));
            (await db.ShowcaseProjects.FindAsync(seed.ShowcaseId))!.LikeCount = likes;
            return seed.ShowcaseId;
        }

        void AddViews(Guid showcaseId, DateOnly date, int count)
        {
            db.ShowcaseViewsDaily.AddRange(Enumerable.Range(0, count).Select(_ => new ShowcaseViewDaily
            {
                ShowcaseProjectId = showcaseId,
                UserId = Guid.NewGuid(),
                ViewDateUtc = date
            }));
        }
    }

    [Theory]
    [InlineData(0, 20, "newest", "Idea")]
    [InlineData(1, 51, "newest", "Idea")]
    [InlineData(1, 0, "newest", "Idea")]
    [InlineData(1, 20, "unknown", "Idea")]
    [InlineData(1, 20, "popular", "99")]
    public async Task InvalidRequest_IsRejected(int page, int size, string sort, string stage)
    {
        await using var db = ShowcaseTestData.NewContext();
        var result = await Handler(db).HandleAsync(new() { Page = page, PageSize = size, Sort = sort, Stage = stage });
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Validation_CoversLengthGuidOverflowAndMaximumPageSize()
    {
        await using var db = ShowcaseTestData.NewContext();
        foreach (var request in new GetShowcasesRequest[] {
            new() { Search = new string('x', 101) }, new() { Industry = new string('x', 101) },
            new() { SemesterId = Guid.Empty }, new() { Page = int.MaxValue } })
            (await Handler(db).HandleAsync(request)).IsFailure.Should().BeTrue();
        (await Handler(db).HandleAsync(new() { PageSize = 50 })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void PostgreSqlSearch_TranslatesToILikeWithEscapedLiteralInput()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=showcase_sql_translation", options => options.UseVector()).Options);
        var sql = new ShowcaseProjectSearch().Apply(db.Projects.AsNoTracking(), "50%_\\").ToQueryString();
        sql.Should().Contain("ILIKE").And.Contain("ESCAPE").And.Contain("name").And.Contain("description");
        sql.Should().Contain(@"\%").And.Contain(@"\_");
    }
}
