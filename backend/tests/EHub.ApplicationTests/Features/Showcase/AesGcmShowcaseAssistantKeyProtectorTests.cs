using System.Security.Cryptography;
using System.Text;
using EHub.Infrastructure.Showcase.Assistant;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class AesGcmShowcaseAssistantKeyProtectorTests
{
    private const string ApiKey = "AIzaSyFakeKeyForProtectorTests0123456789";
    private static readonly Guid Owner = Guid.NewGuid();

    private static string NewMasterKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(ShowcaseAssistantKeyEncryptionOptions.KeySizeBytes));

    private static ShowcaseAssistantKeyEncryptionOptions Settings(string activeId, params (string Id, string Key)[] keys) =>
        new() { ActiveKeyId = activeId, Keys = keys.ToDictionary(item => item.Id, item => item.Key) };

    private static AesGcmShowcaseAssistantKeyProtector Protector(ShowcaseAssistantKeyEncryptionOptions settings) => new(Options.Create(settings));

    private static AesGcmShowcaseAssistantKeyProtector Protector(string activeId, params (string Id, string Key)[] keys) =>
        Protector(Settings(activeId, keys));

    [Fact]
    public void WithoutAnEncryptionKey_NothingCanBeStored()
    {
        var protector = Protector(new ShowcaseAssistantKeyEncryptionOptions());

        protector.IsAvailable.Should().BeFalse();
        var protect = () => protector.Protect(Owner, ApiKey);
        protect.Should().Throw<InvalidOperationException>();
        protector.TryUnprotect(Owner, new byte[64], out _).Should().BeFalse();
        protector.CanOpen(Owner, new byte[64]).Should().BeFalse();
    }

    [Fact]
    public void AKeyRoundTripsForItsOwner_AndTheStoredBytesNeverContainTheKey()
    {
        var protector = Protector("1", ("1", NewMasterKey()));

        var blob = protector.Protect(Owner, ApiKey);

        protector.IsAvailable.Should().BeTrue();
        protector.TryUnprotect(Owner, blob, out var opened).Should().BeTrue();
        opened.Should().Be(ApiKey);
        blob.Length.Should().Be(2 + 12 + Encoding.UTF8.GetByteCount(ApiKey) + 16);
        var asText = Encoding.Latin1.GetString(blob);
        asText.Should().NotContain(ApiKey).And.NotContain("AIza").And.NotContain(ApiKey[^8..]);
    }

    [Fact]
    public void ProtectingTheSameKeyTwiceGivesDifferentBytes()
    {
        var protector = Protector("1", ("1", NewMasterKey()));

        protector.Protect(Owner, ApiKey).Should().NotEqual(protector.Protect(Owner, ApiKey), "every blob gets its own random nonce");
    }

    [Fact]
    public void AKeyCannotBeOpenedForAnotherUser_EvenWithTheSameEncryptionKey()
    {
        var protector = Protector("1", ("1", NewMasterKey()));
        var blob = protector.Protect(Owner, ApiKey);

        protector.TryUnprotect(Guid.NewGuid(), blob, out var opened).Should().BeFalse("the owner is authenticated with the data");
        opened.Should().BeEmpty();
    }

    [Fact]
    public void AnotherServersEncryptionKeyCannotOpenIt()
    {
        var blob = Protector("1", ("1", NewMasterKey())).Protect(Owner, ApiKey);

        Protector("1", ("1", NewMasterKey())).TryUnprotect(Owner, blob, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, "format byte")]
    [InlineData(1, "encryption key id")]
    [InlineData(5, "nonce")]
    [InlineData(20, "cipher text")]
    public void ChangingAnyPartOfTheStoredBytesMakesThemUnusable(int index, string part)
    {
        var protector = Protector("1", ("1", NewMasterKey()));
        var blob = protector.Protect(Owner, ApiKey);
        blob[index] ^= 0x01;

        protector.TryUnprotect(Owner, blob, out var opened).Should().BeFalse(part);
        opened.Should().BeEmpty();
    }

    [Fact]
    public void ATamperedTag_ATruncatedBlobAndGarbageAreRejectedWithoutThrowing()
    {
        var protector = Protector("1", ("1", NewMasterKey()));
        var blob = protector.Protect(Owner, ApiKey);
        var badTag = (byte[])blob.Clone();
        badTag[^1] ^= 0xFF;

        protector.TryUnprotect(Owner, badTag, out _).Should().BeFalse();
        protector.TryUnprotect(Owner, blob[..^1], out _).Should().BeFalse();
        protector.TryUnprotect(Owner, [.. blob, 0], out _).Should().BeFalse();
        protector.TryUnprotect(Owner, [], out _).Should().BeFalse();
        protector.TryUnprotect(Owner, new byte[5_000], out _).Should().BeFalse("far larger than any key this class produces");
        protector.CanOpen(Owner, badTag).Should().BeFalse();
        protector.CanOpen(Owner, blob[..^1]).Should().BeFalse();
        protector.CanOpen(Owner, []).Should().BeFalse();
        protector.CanOpen(Owner, new byte[5_000]).Should().BeFalse();
    }

    [Fact]
    public void CanOpenAgreesWithTryUnprotectForTheOwnerAnotherUserAndAnotherEncryptionKey()
    {
        var master = NewMasterKey();
        var protector = Protector("1", ("1", master));
        var blob = protector.Protect(Owner, ApiKey);

        protector.CanOpen(Owner, blob).Should().BeTrue();
        protector.CanOpen(Guid.NewGuid(), blob).Should().BeFalse("it was made for someone else");
        Protector("1", ("1", master)).CanOpen(Owner, blob).Should().BeTrue("the same encryption key opens it on another instance");
        Protector("1", ("1", NewMasterKey())).CanOpen(Owner, blob).Should()
            .BeFalse("the id matches but the key itself is different, which a look at the header could not tell");
    }

    [Fact]
    public void TheActiveEncryptionKeyCanBeReplacedWithoutLosingWhatWasStoredBefore()
    {
        var first = NewMasterKey();
        var second = NewMasterKey();
        var oldBlob = Protector("1", ("1", first)).Protect(Owner, ApiKey);
        var rotated = Protector("2", ("1", first), ("2", second));

        rotated.TryUnprotect(Owner, oldBlob, out var opened).Should().BeTrue();
        opened.Should().Be(ApiKey);
        var newBlob = rotated.Protect(Owner, ApiKey);
        newBlob[1].Should().Be(2, "new keys use the active encryption key");
        Protector("1", ("1", first)).TryUnprotect(Owner, newBlob, out _).Should().BeFalse("an old server cannot open what the new key protected");
    }

    [Fact]
    public void AnEncryptionKeyThatWasRetiredNoLongerOpensItsBlobs_AndTheyAreReportedAsUnusable()
    {
        var first = NewMasterKey();
        var oldBlob = Protector("1", ("1", first)).Protect(Owner, ApiKey);
        var retired = Protector("2", ("2", NewMasterKey()));

        retired.CanOpen(Owner, oldBlob).Should().BeFalse();
        retired.TryUnprotect(Owner, oldBlob, out _).Should().BeFalse();
    }

    [Fact]
    public void SettingsAreOffWhenEmptyAndOtherwiseMustBeCompleteAndCorrect()
    {
        var valid = NewMasterKey();

        new ShowcaseAssistantKeyEncryptionOptions().IsValid.Should().BeTrue("empty means the feature is off, not broken");
        Settings("", ("1", ""), ("2", " ")).IsValid.Should().BeTrue("blank slots are ignored");
        Settings("1", ("1", valid)).IsValid.Should().BeTrue();
        Settings("2", ("1", valid), ("2", NewMasterKey()), ("3", "")).IsValid.Should().BeTrue();

        Settings("1").IsValid.Should().BeFalse("an active id without a key");
        Settings("", ("1", valid)).IsValid.Should().BeFalse("keys without an active id");
        Settings("2", ("1", valid)).IsValid.Should().BeFalse("the active id is not among the keys");
        Settings("1", ("1", "not base64!!")).IsValid.Should().BeFalse();
        Settings("1", ("1", Convert.ToBase64String(new byte[16]))).IsValid.Should().BeFalse("128-bit key");
        Settings("1", ("1", Convert.ToBase64String(RandomNumberGenerator.GetBytes(33)))).IsValid.Should().BeFalse("longer than 256 bits");
        Settings("1", ("1", Convert.ToBase64String(new byte[32]))).IsValid.Should().BeFalse("an all-zero placeholder");
        Settings("0", ("0", valid)).IsValid.Should().BeFalse("ids run from 1");
        Settings("256", ("256", valid)).IsValid.Should().BeFalse("ids run to 255");
        Settings("one", ("one", valid)).IsValid.Should().BeFalse();
        Settings("1", ("1", valid), ("01", NewMasterKey())).IsValid.Should().BeFalse("two entries for the same id");
    }

    [Fact]
    public void ABrokenConfigurationNeverProducesAUsableProtector()
    {
        Protector(Settings("1", ("1", "oops"))).IsAvailable.Should().BeFalse();
        Protector(Settings("2", ("1", NewMasterKey()))).IsAvailable.Should().BeFalse();
    }
}
