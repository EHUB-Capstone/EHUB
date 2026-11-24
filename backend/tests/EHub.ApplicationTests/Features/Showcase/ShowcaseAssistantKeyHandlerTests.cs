using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Showcase.Assistant;
using EHub.Contracts.Showcase;
using EHub.Infrastructure.Persistence;
using EHub.Infrastructure.Showcase.Assistant;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class ShowcaseAssistantKeyHandlerTests
{
    private const string ApiKey = "AIzaSyHandlerTestKey0123456789abcd";
    private const string OtherKey = "AIzaSyAnotherTestKey0123456789wxyz";
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    private sealed class MutableClock(DateTime now) : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = now;
    }

    private static string NewMasterKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static AesGcmShowcaseAssistantKeyProtector Protector(string? masterKey = null) =>
        new(Options.Create(new ShowcaseAssistantKeyEncryptionOptions
        {
            ActiveKeyId = "1", Keys = new Dictionary<string, string> { ["1"] = masterKey ?? NewMasterKey() }
        }));

    private static AesGcmShowcaseAssistantKeyProtector NoProtector() => new(Options.Create(new ShowcaseAssistantKeyEncryptionOptions()));

    private static ShowcaseAssistantKeyHandler Handler(AppDbContext db, IShowcaseAssistantKeyProtector protector, MutableClock? clock = null) =>
        new(db, protector, new SaveShowcaseAssistantKeyRequestValidator(), clock ?? new MutableClock(new DateTime(2026, 11, 24, 9, 0, 0, DateTimeKind.Utc)),
            NullLogger<ShowcaseAssistantKeyHandler>.Instance);

    private static SaveShowcaseAssistantKeyRequest Save(string key) => new() { ApiKey = key };

    [Fact]
    public async Task ASavedKeyIsReportedBackOnlyAsPresentAndItsLastFourCharacters()
    {
        await using var db = ShowcaseTestData.NewContext();
        var handler = Handler(db, Protector());

        var saved = await handler.SaveAsync(Save(ApiKey), Alice);
        var status = await handler.GetStatusAsync(Alice);

        saved.IsSuccess.Should().BeTrue();
        status.IsSuccess.Should().BeTrue();
        status.Value!.StorageAvailable.Should().BeTrue();
        status.Value.HasKey.Should().BeTrue();
        status.Value.Last4.Should().Be("abcd");
        status.Value.UpdatedAt.Should().Be(new DateTime(2026, 11, 24, 9, 0, 0, DateTimeKind.Utc));
        JsonSerializer.Serialize(saved.Value).Should().NotContain(ApiKey).And.NotContain("AIza");
        JsonSerializer.Serialize(status.Value).Should().NotContain(ApiKey).And.NotContain("AIza");
    }

    [Fact]
    public async Task TheDatabaseHoldsOnlyEncryptedBytesAndFourCharacters()
    {
        await using var db = ShowcaseTestData.NewContext();
        await Handler(db, Protector()).SaveAsync(Save(ApiKey), Alice);

        var row = await db.ShowcaseAssistantUserKeys.AsNoTracking().SingleAsync();

        row.UserId.Should().Be(Alice);
        row.Last4.Should().HaveLength(4).And.Be(ApiKey[^4..]);
        Encoding.Latin1.GetString(row.Ciphertext).Should().NotContain(ApiKey).And.NotContain("AIza");
    }

    [Fact]
    public async Task SavingAgainReplacesTheKey_KeepsOneRowAndTheOriginalCreationTime()
    {
        await using var db = ShowcaseTestData.NewContext();
        var clock = new MutableClock(new DateTime(2026, 11, 24, 9, 0, 0, DateTimeKind.Utc));
        var protector = Protector();
        var handler = Handler(db, protector, clock);
        await handler.SaveAsync(Save(ApiKey), Alice);
        var first = await db.ShowcaseAssistantUserKeys.AsNoTracking().SingleAsync();

        clock.UtcNow = clock.UtcNow.AddHours(2);
        var replaced = await handler.SaveAsync(Save(OtherKey), Alice);

        replaced.Value!.Last4.Should().Be("wxyz");
        var rows = await db.ShowcaseAssistantUserKeys.AsNoTracking().ToListAsync();
        rows.Should().ContainSingle();
        rows[0].CreatedAtUtc.Should().Be(first.CreatedAtUtc);
        rows[0].UpdatedAtUtc.Should().Be(clock.UtcNow);
        rows[0].Ciphertext.Should().NotEqual(first.Ciphertext);
        (await handler.RevealAsync(Alice)).Should().Be(OtherKey);
    }

    [Fact]
    public async Task EveryUserHasOnlyTheirOwnKey()
    {
        await using var db = ShowcaseTestData.NewContext();
        var handler = Handler(db, Protector());
        await handler.SaveAsync(Save(ApiKey), Alice);

        (await handler.GetStatusAsync(Bob)).Value!.HasKey.Should().BeFalse();
        (await handler.GetStatusAsync(Bob)).Value!.Last4.Should().BeNull();
        (await handler.RevealAsync(Bob)).Should().BeNull();
        (await handler.DeleteAsync(Bob)).IsSuccess.Should().BeTrue();
        (await handler.RevealAsync(Alice)).Should().Be(ApiKey, "deleting Bob's (empty) key touched nothing of Alice's");
        await handler.SaveAsync(Save(OtherKey), Bob);
        (await handler.RevealAsync(Alice)).Should().Be(ApiKey);
        (await handler.RevealAsync(Bob)).Should().Be(OtherKey);
    }

    [Fact]
    public async Task DeletingRemovesTheKeyAndDeletingAgainIsHarmless()
    {
        await using var db = ShowcaseTestData.NewContext();
        var handler = Handler(db, Protector());
        await handler.SaveAsync(Save(ApiKey), Alice);

        var removed = await handler.DeleteAsync(Alice);
        var again = await handler.DeleteAsync(Alice);

        removed.IsSuccess.Should().BeTrue();
        removed.Value!.HasKey.Should().BeFalse();
        again.IsSuccess.Should().BeTrue();
        (await db.ShowcaseAssistantUserKeys.AnyAsync()).Should().BeFalse();
        (await handler.RevealAsync(Alice)).Should().BeNull();
    }

    [Fact]
    public async Task WithoutAnEncryptionKeyNothingIsStored_AndTheStatusSaysSo()
    {
        await using var db = ShowcaseTestData.NewContext();
        var handler = Handler(db, NoProtector());

        var saved = await handler.SaveAsync(Save(ApiKey), Alice);
        var status = await handler.GetStatusAsync(Alice);

        saved.IsSuccess.Should().BeFalse();
        saved.Error.Code.Should().Be(ErrorCodes.ShowcaseAssistantKeyStorageUnavailable);
        saved.Error.Message.Should().NotContain(ApiKey);
        status.Value!.StorageAvailable.Should().BeFalse();
        status.Value.HasKey.Should().BeFalse();
        (await db.ShowcaseAssistantUserKeys.AnyAsync()).Should().BeFalse("a key is never stored unencrypted");
        (await handler.RevealAsync(Alice)).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    [InlineData("GEMINI_API_KEY=AIzaSyHandlerTestKey0123456789abcd")]
    [InlineData("https://aistudio.google.com/app/apikey?key=AIzaSyHandlerTestKey0123456789")]
    [InlineData("AIzaSy HandlerTestKey 0123456789abcd")]
    public async Task ADifferentShapeIsRefusedWithAFixedMessageAndNothingIsStored(string key)
    {
        await using var db = ShowcaseTestData.NewContext();

        var result = await Handler(db, Protector()).SaveAsync(Save(key), Alice);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.ShowcaseAssistantKeyInvalid);
        result.Error.Message.Should().Be(SaveShowcaseAssistantKeyRequestValidator.Message);
        (await db.ShowcaseAssistantUserKeys.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task APastedKeyWithSpacesAroundItIsStoredTrimmed()
    {
        await using var db = ShowcaseTestData.NewContext();
        var handler = Handler(db, Protector());

        await handler.SaveAsync(Save($"  {ApiKey}\r\n"), Alice);

        (await handler.RevealAsync(Alice)).Should().Be(ApiKey);
    }

    [Fact]
    public async Task AKeyProtectedByARetiredEncryptionKeyIsReportedAsNotThere_AndNeverThrows()
    {
        await using var db = ShowcaseTestData.NewContext();
        await Handler(db, Protector()).SaveAsync(Save(ApiKey), Alice);
        var afterRotation = Handler(db, Protector());

        (await afterRotation.GetStatusAsync(Alice)).Value!.HasKey.Should().BeFalse("it can no longer be opened, so the user is asked again");
        (await afterRotation.RevealAsync(Alice)).Should().BeNull();
        var replaced = await afterRotation.SaveAsync(Save(OtherKey), Alice);
        replaced.IsSuccess.Should().BeTrue("the user can simply store a new key");
        (await afterRotation.RevealAsync(Alice)).Should().Be(OtherKey);
    }

    [Fact]
    public async Task AStoredRowThatWasTamperedWithIsNeverRevealed()
    {
        await using var db = ShowcaseTestData.NewContext();
        var protector = Protector();
        var handler = Handler(db, protector);
        await handler.SaveAsync(Save(ApiKey), Alice);
        var row = await db.ShowcaseAssistantUserKeys.SingleAsync();
        row.Ciphertext = [.. row.Ciphertext];
        row.Ciphertext[^1] ^= 0xFF;
        await db.SaveChangesAsync();

        (await handler.RevealAsync(Alice)).Should().BeNull();
        (await handler.GetStatusAsync(Alice)).Value!.HasKey.Should().BeFalse();
    }

    [Fact]
    public async Task ARowCopiedToAnotherUserDoesNotOpenForThem()
    {
        await using var db = ShowcaseTestData.NewContext();
        var handler = Handler(db, Protector());
        await handler.SaveAsync(Save(ApiKey), Alice);
        var aliceRow = await db.ShowcaseAssistantUserKeys.AsNoTracking().SingleAsync();
        db.ShowcaseAssistantUserKeys.Add(new() { UserId = Bob, Ciphertext = aliceRow.Ciphertext, Last4 = aliceRow.Last4, CreatedAtUtc = aliceRow.CreatedAtUtc, UpdatedAtUtc = aliceRow.UpdatedAtUtc });
        await db.SaveChangesAsync();

        (await handler.RevealAsync(Bob)).Should().BeNull("the blob is bound to the user it was made for");
        (await handler.GetStatusAsync(Bob)).Value!.HasKey.Should().BeFalse("what cannot be used is not reported as saved");
        (await handler.RevealAsync(Alice)).Should().Be(ApiKey);
        (await handler.GetStatusAsync(Alice)).Value!.HasKey.Should().BeTrue();
    }

    [Fact]
    public async Task NoOneIsSignedInMeansNothingHappens()
    {
        await using var db = ShowcaseTestData.NewContext();
        var handler = Handler(db, Protector());

        (await handler.GetStatusAsync(Guid.Empty)).Error.Code.Should().Be(ErrorCodes.CommonUnauthorizedError);
        (await handler.SaveAsync(Save(ApiKey), Guid.Empty)).Error.Code.Should().Be(ErrorCodes.CommonUnauthorizedError);
        (await handler.DeleteAsync(Guid.Empty)).Error.Code.Should().Be(ErrorCodes.CommonUnauthorizedError);
        (await handler.RevealAsync(Guid.Empty)).Should().BeNull();
        (await db.ShowcaseAssistantUserKeys.AnyAsync()).Should().BeFalse();
    }
}
