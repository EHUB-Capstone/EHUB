using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Showcase.Assistant;
using EHub.Contracts.Showcase;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EHub.ApplicationTests.Features.Showcase;

/// <summary>Which Gemini key the chat uses: one sent with the request, otherwise the one the user stored on the server.</summary>
public sealed class ShowcaseAssistantStoredKeyChatTests
{
    private const string HeaderKey = "AIzaSyHeaderKeyForChatTests0123456789";
    private const string StoredKeyValue = "AIzaSyStoredKeyForChatTests012345678";
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class CapturingModel : IShowcaseAssistantModel
    {
        public List<string> Keys { get; } = [];

        public Task<ShowcaseAssistantModelReply> AskAsync(ShowcaseAssistantModelInput input, string apiKey, CancellationToken cancellationToken)
        {
            Keys.Add(apiKey);
            return Task.FromResult(new ShowcaseAssistantModelReply("ok", [], []));
        }
    }

    private sealed class RejectingModel : IShowcaseAssistantModel
    {
        public Task<ShowcaseAssistantModelReply> AskAsync(ShowcaseAssistantModelInput input, string apiKey, CancellationToken cancellationToken) =>
            throw new ShowcaseAssistantKeyRejectedException("Google không chấp nhận API key này.");
    }

    private sealed class FakeStoredKey(string? key) : IStoredShowcaseAssistantKey
    {
        public List<Guid> Asked { get; } = [];

        public Task<string?> RevealAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            Asked.Add(userId);
            return Task.FromResult(key);
        }
    }

    private sealed record FixedClock(DateTime UtcNow) : IDateTimeProvider;

    private static ShowcaseAssistantHandler Handler(AppDbContext db, IShowcaseAssistantModel model, IStoredShowcaseAssistantKey? stored) =>
        new(db, model, new ShowcaseAssistantChatRequestValidator(), new FixedClock(new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc)), stored);

    private static ShowcaseAssistantChatRequest Ask() => new() { Message = "Có dự án nào về giáo dục không?" };

    private static async Task<AppDbContext> DatabaseWithAProjectAsync()
    {
        var db = ShowcaseTestData.NewContext();
        var seeded = await ShowcaseTestData.SeedAsync(db, ShowcaseStatus.Published, publishedAt: DateTime.UtcNow);
        var project = await db.Projects.SingleAsync(item => item.ProjectLineageId == seeded.LineageId);
        project.Name = "EduBot";
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task WithoutAKeyInTheRequest_TheStoredKeyIsUsed()
    {
        await using var db = await DatabaseWithAProjectAsync();
        var model = new CapturingModel();
        var stored = new FakeStoredKey(StoredKeyValue);

        var result = await Handler(db, model, stored).AskAsync(Ask(), null, UserId);

        result.IsSuccess.Should().BeTrue();
        model.Keys.Should().Equal(StoredKeyValue);
        stored.Asked.Should().Equal(UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankHeaderCountsAsNoKey(string header)
    {
        await using var db = await DatabaseWithAProjectAsync();
        var model = new CapturingModel();

        await Handler(db, model, new FakeStoredKey(StoredKeyValue)).AskAsync(Ask(), header, UserId);

        model.Keys.Should().Equal(StoredKeyValue);
    }

    [Fact]
    public async Task AKeySentWithTheRequestWinsAndTheStoredOneIsNotEvenOpened()
    {
        await using var db = await DatabaseWithAProjectAsync();
        var model = new CapturingModel();
        var stored = new FakeStoredKey(StoredKeyValue);

        var result = await Handler(db, model, stored).AskAsync(Ask(), $"  {HeaderKey}\n", UserId);

        result.IsSuccess.Should().BeTrue();
        model.Keys.Should().Equal(HeaderKey);
        stored.Asked.Should().BeEmpty();
    }

    [Fact]
    public async Task WithNeitherKeyTheUserIsAskedForOne_AndNothingIsSentToTheModel()
    {
        await using var db = await DatabaseWithAProjectAsync();
        var model = new CapturingModel();

        var result = await Handler(db, model, new FakeStoredKey(null)).AskAsync(Ask(), null, UserId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.ShowcaseAssistantKeyRequired);
        model.Keys.Should().BeEmpty();
        (await Handler(db, model, null).AskAsync(Ask(), null, UserId)).Error.Code.Should().Be(ErrorCodes.ShowcaseAssistantKeyRequired);
    }

    [Fact]
    public async Task AStoredKeyThatIsNotShapedLikeAKeyIsNeverSentToTheProvider()
    {
        await using var db = await DatabaseWithAProjectAsync();
        var model = new CapturingModel();

        var result = await Handler(db, model, new FakeStoredKey("not a key")).AskAsync(Ask(), null, UserId);

        result.Error.Code.Should().Be(ErrorCodes.ShowcaseAssistantKeyInvalid);
        model.Keys.Should().BeEmpty();
    }

    [Fact]
    public async Task AStoredKeyTheProviderRefusesIsReportedAsAKeyProblem()
    {
        await using var db = await DatabaseWithAProjectAsync();

        var result = await Handler(db, new RejectingModel(), new FakeStoredKey(StoredKeyValue)).AskAsync(Ask(), null, UserId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.ShowcaseAssistantKeyInvalid);
        result.Error.Message.Should().NotContain(StoredKeyValue);
    }
}
