using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Showcase.Assistant;
using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EHub.IntegrationTests.Showcase;

/// <summary>
/// The Gemini key a user chose to keep on the server: encrypted, owned by that user alone, never sent back, and used by the
/// chat only when the request carries no key of its own.
/// </summary>
[Collection("Sequential")]
public sealed class ShowcaseAssistantKeyIntegrationTests(CustomWebApplicationFactory factory)
{
    private const string KeyUrl = "/api/showcase/assistant/key";
    private const string ChatUrl = "/api/showcase/assistant/chat";
    private const string FirstKey = "AIzaSyIntegrationStoredKeyAAAAAAAA1111";
    private const string SecondKey = "AIzaSyIntegrationStoredKeyBBBBBBBB2222";
    private const string HeaderKey = "AIzaSyIntegrationHeaderKeyCCCCCCCC3333";

    private sealed class CapturingModel : IShowcaseAssistantModel
    {
        private readonly List<string> keys = [];
        public bool Refuse { get; init; }
        public IReadOnlyList<string> Keys { get { lock (keys) return [.. keys]; } }

        public Task<ShowcaseAssistantModelReply> AskAsync(ShowcaseAssistantModelInput input, string apiKey, CancellationToken cancellationToken)
        {
            lock (keys) keys.Add(apiKey);
            if (Refuse) throw new ShowcaseAssistantKeyRejectedException("Google không chấp nhận API key này.");
            return Task.FromResult(new ShowcaseAssistantModelReply("ok", [], []));
        }
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task KeyEndpoints_RequireAuthentication(string method)
    {
        using var app = App(NewEncryptionKey());
        using var client = app.CreateClient();

        var response = await Send(client, new HttpMethod(method), KeyUrl, null,
            method == "PUT" ? new SaveShowcaseAssistantKeyRequest { ApiKey = FirstKey } : null);

        response.Status.Should().Be(HttpStatusCode.Unauthorized);
        response.Text.Should().NotContain(FirstKey);
    }

    [Fact]
    public async Task WithoutAnEncryptionKey_TheServerSaysStorageIsOff_SavingIsRefusedAsUnavailable_AndNothingIsStored()
    {
        using var app = App(null);
        using var client = app.CreateClient();
        var user = await Identity(SystemRoles.Student);

        var status = await Send(client, HttpMethod.Get, KeyUrl, user.Token);
        var save = await Send(client, HttpMethod.Put, KeyUrl, user.Token, Save(FirstKey));
        var remove = await Send(client, HttpMethod.Delete, KeyUrl, user.Token);

        status.Status.Should().Be(HttpStatusCode.OK);
        Data<ShowcaseAssistantKeyStatusDto>(status).StorageAvailable.Should().BeFalse();
        Data<ShowcaseAssistantKeyStatusDto>(status).HasKey.Should().BeFalse();
        save.Status.Should().Be(HttpStatusCode.ServiceUnavailable, "a missing server setting is not the caller's mistake");
        Parse<object>(save).Code.Should().Be(ErrorCodes.ShowcaseAssistantKeyStorageUnavailable);
        save.Text.Should().NotContain(FirstKey);
        remove.Status.Should().Be(HttpStatusCode.OK);
        (await StoredRow(user.Id)).Should().BeNull("a key is never stored without being encrypted");
    }

    [Theory]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Lecturer)]
    [InlineData(SystemRoles.Mentor)]
    [InlineData(SystemRoles.Admin)]
    public async Task EverySignedInRole_CanStoreCheckAndRemoveItsOwnKey_AndTheKeyIsNeverSentBack(string role)
    {
        using var app = App(NewEncryptionKey());
        using var client = app.CreateClient();
        var user = await Identity(role);

        var before = await Send(client, HttpMethod.Get, KeyUrl, user.Token);
        var saved = await Send(client, HttpMethod.Put, KeyUrl, user.Token, Save($"  {FirstKey}\r\n"));
        var after = await Send(client, HttpMethod.Get, KeyUrl, user.Token);

        Data<ShowcaseAssistantKeyStatusDto>(before).Should().Match<ShowcaseAssistantKeyStatusDto>(item => item.StorageAvailable && !item.HasKey);
        saved.Status.Should().Be(HttpStatusCode.OK);
        foreach (var response in new[] { saved, after })
        {
            var status = Data<ShowcaseAssistantKeyStatusDto>(response);
            status.StorageAvailable.Should().BeTrue();
            status.HasKey.Should().BeTrue();
            status.Last4.Should().Be("1111");
            status.UpdatedAt.Should().NotBeNull();
            response.Text.Should().NotContain(FirstKey).And.NotContain("AIza", "the key is never sent back, only its last four characters");
        }
        var row = (await StoredRow(user.Id))!;
        row.Last4.Should().Be("1111");
        Encoding.Latin1.GetString(row.Ciphertext).Should().NotContain("AIza").And.NotContain(FirstKey[^12..]);

        var removed = await Send(client, HttpMethod.Delete, KeyUrl, user.Token);

        removed.Status.Should().Be(HttpStatusCode.OK);
        Data<ShowcaseAssistantKeyStatusDto>(removed).HasKey.Should().BeFalse();
        (await StoredRow(user.Id)).Should().BeNull();
        Data<ShowcaseAssistantKeyStatusDto>(await Send(client, HttpMethod.Get, KeyUrl, user.Token)).HasKey.Should().BeFalse();
    }

    [Fact]
    public async Task SavingAgainReplacesTheKeyOfThatUser()
    {
        using var app = App(NewEncryptionKey());
        using var client = app.CreateClient();
        var user = await Identity(SystemRoles.Student);

        await Send(client, HttpMethod.Put, KeyUrl, user.Token, Save(FirstKey));
        var first = (await StoredRow(user.Id))!;
        var replaced = await Send(client, HttpMethod.Put, KeyUrl, user.Token, Save(SecondKey));

        Data<ShowcaseAssistantKeyStatusDto>(replaced).Last4.Should().Be("2222");
        var second = (await StoredRow(user.Id))!;
        second.Last4.Should().Be("2222");
        second.Ciphertext.Should().NotEqual(first.Ciphertext);
        second.CreatedAtUtc.Should().Be(first.CreatedAtUtc);
        second.UpdatedAtUtc.Should().BeOnOrAfter(first.UpdatedAtUtc);
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().ShowcaseAssistantUserKeys.CountAsync(row => row.UserId == user.Id))
            .Should().Be(1);
    }

    [Theory]
    [InlineData("GEMINI_API_KEY=AIzaSyIntegrationBadShapeKey0123456789")]
    [InlineData("https://aistudio.google.com/app/apikey?key=AIzaSyIntegrationBadShape0123456789")]
    [InlineData("AIzaSy IntegrationBadShape 0123456789")]
    [InlineData("short")]
    [InlineData("   ")]
    [InlineData("")]
    public async Task AKeyOfTheWrongShape_IsRefused_NeverEchoedBack_AndNothingIsStored(string key)
    {
        using var app = App(NewEncryptionKey());
        using var client = app.CreateClient();
        var user = await Identity(SystemRoles.Student);

        var response = await Send(client, HttpMethod.Put, KeyUrl, user.Token, Save(key));

        response.Status.Should().Be(HttpStatusCode.BadRequest);
        if (key.Trim().Length > 5) response.Text.Should().NotContain(key.Trim()).And.NotContain("AIzaSy");
        (await StoredRow(user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task ARequestWithNoKeyAtAll_IsRefusedAndNothingIsStored()
    {
        using var app = App(NewEncryptionKey());
        using var client = app.CreateClient();
        var user = await Identity(SystemRoles.Student);

        var response = await Send(client, HttpMethod.Put, KeyUrl, user.Token, new { });

        response.Status.Should().Be(HttpStatusCode.BadRequest);
        (await StoredRow(user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task UsersNeverSeeOrChangeEachOthersKey_AndEachChatUsesTheCallersOwnKey()
    {
        var model = new CapturingModel();
        using var app = App(NewEncryptionKey(), model);
        using var client = app.CreateClient();
        var alice = await Identity(SystemRoles.Student);
        var bob = await Identity(SystemRoles.Student);
        await using var showcase = await PublishedShowcase();
        await Send(client, HttpMethod.Put, KeyUrl, alice.Token, Save(FirstKey));

        var bobSees = await Send(client, HttpMethod.Get, KeyUrl, bob.Token);
        var bobRemoves = await Send(client, HttpMethod.Delete, KeyUrl, bob.Token);
        var bobChatsWithoutHisOwn = await Send(client, HttpMethod.Post, ChatUrl, bob.Token, Question());

        Data<ShowcaseAssistantKeyStatusDto>(bobSees).HasKey.Should().BeFalse("Alice's key is not Bob's");
        Data<ShowcaseAssistantKeyStatusDto>(bobSees).Last4.Should().BeNull();
        bobRemoves.Status.Should().Be(HttpStatusCode.OK);
        (await StoredRow(alice.Id)).Should().NotBeNull("Bob removing his own (empty) key touches nothing of Alice's");
        bobChatsWithoutHisOwn.Status.Should().Be(HttpStatusCode.BadRequest);
        Parse<object>(bobChatsWithoutHisOwn).Code.Should().Be(ErrorCodes.ShowcaseAssistantKeyRequired);
        model.Keys.Should().BeEmpty("Bob's question never reached the provider with Alice's key");

        await Send(client, HttpMethod.Put, KeyUrl, bob.Token, Save(SecondKey));
        (await Send(client, HttpMethod.Post, ChatUrl, alice.Token, Question())).Status.Should().Be(HttpStatusCode.OK);
        (await Send(client, HttpMethod.Post, ChatUrl, bob.Token, Question())).Status.Should().Be(HttpStatusCode.OK);

        model.Keys.Should().Equal(FirstKey, SecondKey);
        Data<ShowcaseAssistantKeyStatusDto>(await Send(client, HttpMethod.Get, KeyUrl, alice.Token)).Last4.Should().Be("1111");
    }

    [Fact]
    public async Task Chat_UsesTheStoredKeyWhenNoneIsSent_ACallKeyWins_AndAfterRemovalAKeyIsRequiredAgain()
    {
        var model = new CapturingModel();
        using var app = App(NewEncryptionKey(), model);
        using var client = app.CreateClient();
        var user = await Identity(SystemRoles.Student);
        await using var showcase = await PublishedShowcase();
        await Send(client, HttpMethod.Put, KeyUrl, user.Token, Save(FirstKey));

        var withStored = await Send(client, HttpMethod.Post, ChatUrl, user.Token, Question());
        var withHeader = await Send(client, HttpMethod.Post, ChatUrl, user.Token, Question(), HeaderKey);
        await Send(client, HttpMethod.Delete, KeyUrl, user.Token);
        var afterRemoval = await Send(client, HttpMethod.Post, ChatUrl, user.Token, Question());

        withStored.Status.Should().Be(HttpStatusCode.OK);
        withHeader.Status.Should().Be(HttpStatusCode.OK);
        model.Keys.Should().Equal(FirstKey, HeaderKey);
        afterRemoval.Status.Should().Be(HttpStatusCode.BadRequest, "400, never 401: the web app signs a user out on 401");
        Parse<object>(afterRemoval).Code.Should().Be(ErrorCodes.ShowcaseAssistantKeyRequired);
        foreach (var response in new[] { withStored, withHeader, afterRemoval })
            response.Text.Should().NotContain(FirstKey).And.NotContain(HeaderKey);
    }

    [Fact]
    public async Task AStoredKeyTheProviderRefuses_IsReportedAsAKeyProblem_NotAServerFault_AndStaysUntilTheUserChangesIt()
    {
        var model = new CapturingModel { Refuse = true };
        using var app = App(NewEncryptionKey(), model);
        using var client = app.CreateClient();
        var user = await Identity(SystemRoles.Student);
        await using var showcase = await PublishedShowcase();
        await Send(client, HttpMethod.Put, KeyUrl, user.Token, Save(FirstKey));

        var chat = await Send(client, HttpMethod.Post, ChatUrl, user.Token, Question());
        var status = await Send(client, HttpMethod.Get, KeyUrl, user.Token);

        chat.Status.Should().Be(HttpStatusCode.BadRequest);
        Parse<object>(chat).Code.Should().Be(ErrorCodes.ShowcaseAssistantKeyInvalid);
        chat.Text.Should().NotContain(FirstKey);
        Data<ShowcaseAssistantKeyStatusDto>(status).HasKey.Should().BeTrue("a refusal may be a passing quota problem, so the key is not deleted");
    }

    [Fact]
    public async Task AStoredKeySurvivesARestartWithTheSameEncryptionKey()
    {
        var encryptionKey = NewEncryptionKey();
        var user = await Identity(SystemRoles.Student);
        using (var first = App(encryptionKey))
        using (var client = first.CreateClient())
            (await Send(client, HttpMethod.Put, KeyUrl, user.Token, Save(FirstKey))).Status.Should().Be(HttpStatusCode.OK);

        var model = new CapturingModel();
        using var restarted = App(encryptionKey, model);
        using var restartedClient = restarted.CreateClient();
        await using var showcase = await PublishedShowcase();

        Data<ShowcaseAssistantKeyStatusDto>(await Send(restartedClient, HttpMethod.Get, KeyUrl, user.Token)).HasKey.Should().BeTrue();
        (await Send(restartedClient, HttpMethod.Post, ChatUrl, user.Token, Question())).Status.Should().Be(HttpStatusCode.OK);
        model.Keys.Should().Equal(FirstKey);
    }

    [Fact]
    public async Task WhenTheServersEncryptionKeyIsReplaced_TheUserIsAskedAgain_AndCanStoreANewKey()
    {
        var user = await Identity(SystemRoles.Student);
        using (var first = App(NewEncryptionKey()))
        using (var client = first.CreateClient())
            await Send(client, HttpMethod.Put, KeyUrl, user.Token, Save(FirstKey));

        var model = new CapturingModel();
        using var replaced = App(NewEncryptionKey(), model);
        using var replacedClient = replaced.CreateClient();
        await using var showcase = await PublishedShowcase();

        var status = await Send(replacedClient, HttpMethod.Get, KeyUrl, user.Token);
        var chat = await Send(replacedClient, HttpMethod.Post, ChatUrl, user.Token, Question());

        Data<ShowcaseAssistantKeyStatusDto>(status).HasKey.Should().BeFalse("the old key can no longer be opened, so it is not reported as saved");
        Data<ShowcaseAssistantKeyStatusDto>(status).StorageAvailable.Should().BeTrue();
        chat.Status.Should().Be(HttpStatusCode.BadRequest);
        Parse<object>(chat).Code.Should().Be(ErrorCodes.ShowcaseAssistantKeyRequired);
        model.Keys.Should().BeEmpty();

        (await Send(replacedClient, HttpMethod.Put, KeyUrl, user.Token, Save(SecondKey))).Status.Should().Be(HttpStatusCode.OK);
        (await Send(replacedClient, HttpMethod.Post, ChatUrl, user.Token, Question())).Status.Should().Be(HttpStatusCode.OK);
        model.Keys.Should().Equal(SecondKey);
    }

    [Fact]
    public async Task ANewActiveEncryptionKey_StillOpensWhatTheRetainedOldOneProtected()
    {
        var oldKey = NewEncryptionKey();
        var user = await Identity(SystemRoles.Student);
        using (var first = App(oldKey))
        using (var client = first.CreateClient())
            await Send(client, HttpMethod.Put, KeyUrl, user.Token, Save(FirstKey));

        var model = new CapturingModel();
        using var rotated = App("2", new Dictionary<string, string> { ["1"] = oldKey, ["2"] = NewEncryptionKey() }, model);
        using var rotatedClient = rotated.CreateClient();
        await using var showcase = await PublishedShowcase();

        Data<ShowcaseAssistantKeyStatusDto>(await Send(rotatedClient, HttpMethod.Get, KeyUrl, user.Token)).HasKey.Should().BeTrue();
        (await Send(rotatedClient, HttpMethod.Post, ChatUrl, user.Token, Question())).Status.Should().Be(HttpStatusCode.OK);
        model.Keys.Should().Equal(FirstKey);
    }

    [Fact]
    public async Task ADatabaseRowCopiedToAnotherUser_DoesNotOpenForThem()
    {
        var model = new CapturingModel();
        using var app = App(NewEncryptionKey(), model);
        using var client = app.CreateClient();
        var alice = await Identity(SystemRoles.Student);
        var mallory = await Identity(SystemRoles.Student);
        await using var showcase = await PublishedShowcase();
        await Send(client, HttpMethod.Put, KeyUrl, alice.Token, Save(FirstKey));
        var copied = (await StoredRow(alice.Id))!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ShowcaseAssistantUserKeys.Add(new ShowcaseAssistantUserKey
            {
                UserId = mallory.Id, Ciphertext = copied.Ciphertext, Last4 = copied.Last4, CreatedAtUtc = copied.CreatedAtUtc, UpdatedAtUtc = copied.UpdatedAtUtc
            });
            await db.SaveChangesAsync();
        }

        var status = await Send(client, HttpMethod.Get, KeyUrl, mallory.Token);
        var chat = await Send(client, HttpMethod.Post, ChatUrl, mallory.Token, Question());

        Data<ShowcaseAssistantKeyStatusDto>(status).HasKey.Should().BeFalse();
        chat.Status.Should().Be(HttpStatusCode.BadRequest);
        model.Keys.Should().BeEmpty("a copied row must never make the provider see Alice's key on Mallory's request");
    }

    [Fact]
    public async Task DeletingAnAccount_RemovesItsStoredKey_AndLeavesEveryoneElsesAlone()
    {
        using var app = App(NewEncryptionKey());
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var leaving = await Identity(SystemRoles.Student);
        var staying = await Identity(SystemRoles.Student);
        await Send(client, HttpMethod.Put, KeyUrl, leaving.Token, Save(FirstKey));
        await Send(client, HttpMethod.Put, KeyUrl, staying.Token, Save(SecondKey));

        var deleted = await Send(client, HttpMethod.Delete, $"/api/users/{leaving.Id}", admin.Token);

        deleted.Status.Should().Be(HttpStatusCode.OK, deleted.Text);
        (await StoredRow(leaving.Id)).Should().BeNull("an account that no longer exists keeps no key, not even an encrypted one");
        (await StoredRow(staying.Id))!.Last4.Should().Be("2222");
    }

    [Fact]
    public async Task RemovingAUserAlsoRemovesTheirStoredKey()
    {
        using var app = App(NewEncryptionKey());
        using var client = app.CreateClient();
        var user = await Identity(SystemRoles.Student);
        await Send(client, HttpMethod.Put, KeyUrl, user.Token, Save(FirstKey));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // A real DELETE, not the soft delete the application performs, so the database's own cascade is what is being checked.
            await db.UserRoles.IgnoreQueryFilters().Where(item => item.UserId == user.Id).ExecuteDeleteAsync();
            await db.Users.IgnoreQueryFilters().Where(item => item.Id == user.Id).ExecuteDeleteAsync();
        }

        (await StoredRow(user.Id)).Should().BeNull("the key row cascades with its owner");
    }

    private static string NewEncryptionKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static SaveShowcaseAssistantKeyRequest Save(string key) => new() { ApiKey = key };

    private static ShowcaseAssistantChatRequest Question() => new() { Message = "Có dự án nào về giáo dục không?" };

    private WebApplicationFactory<Program> App(string? encryptionKey, CapturingModel? model = null) =>
        App(encryptionKey is null ? "" : "1",
            encryptionKey is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["1"] = encryptionKey }, model);

    /// <summary>A server whose key storage is configured exactly as stated, whatever the machine running the tests has set.</summary>
    private WebApplicationFactory<Program> App(string activeKeyId, Dictionary<string, string> encryptionKeys, CapturingModel? model = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Showcase:Assistant:KeyEncryption:ActiveKeyId"] = activeKeyId,
                ["Showcase:Assistant:KeyEncryption:Keys:1"] = "",
                ["Showcase:Assistant:KeyEncryption:Keys:2"] = ""
            };
            foreach (var (id, key) in encryptionKeys) settings[$"Showcase:Assistant:KeyEncryption:Keys:{id}"] = key;
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
            if (model is not null)
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IShowcaseAssistantModel>();
                    services.AddSingleton<IShowcaseAssistantModel>(model);
                });
        });

    private async Task<(Guid Id, string Token)> Identity(string roleName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.Roles.SingleAsync(row => row.Name == roleName);
        var email = $"showcase-key-{Guid.NewGuid():N}@example.test";
        var user = new User { FullName = "Synthetic key owner", Email = email, NormalizedEmail = email.ToUpperInvariant() };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [roleName]).Token);
    }

    /// <summary>What the database holds for this user, read directly (the API never returns it).</summary>
    private async Task<ShowcaseAssistantUserKey?> StoredRow(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ShowcaseAssistantUserKeys.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId);
    }

    /// <summary>One published showcase, so the chat gets as far as asking the model. It is removed again when disposed.</summary>
    private async Task<PublishedShowcaseFixture> PublishedShowcase()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var token = Guid.NewGuid().ToString("N");
        var lecturer = new User { FullName = "Key chat lecturer", Email = $"key-chat-{token}@example.test", NormalizedEmail = $"KEY-CHAT-{token}@EXAMPLE.TEST" };
        db.Users.Add(lecturer);
        var courseId = await db.Courses.Select(course => course.Id).FirstAsync();
        var semesterId = await db.Semesters.Select(semester => semester.Id).FirstAsync();
        var index = (await db.Classes.IgnoreQueryFilters().Where(item => item.CourseId == courseId && item.SemesterId == semesterId)
            .Select(item => (int?)item.ClassIndex).MaxAsync() ?? 0) + 1;
        var @class = new Class { ClassCode = $"KeyChat-{token}", Slug = $"key-chat-{token}", ClassIndex = index,
            CourseId = courseId, SemesterId = semesterId, PrimaryLecturerId = lecturer.Id };
        @class.ClassLecturers.Add(new ClassLecturer { LecturerId = lecturer.Id, IsPrimary = true });
        var team = new Team { TeamCode = token[..8], TeamName = "Key chat team", Class = @class };
        var project = new Project { Team = team, Name = $"Key chat {token}", Description = "Showcase fixture for the stored-key chat tests" };
        var showcase = new ShowcaseProject { ProjectLineageId = project.ProjectLineageId, Status = ShowcaseStatus.Published, PublishedAt = DateTime.UtcNow };
        db.Projects.Add(project);
        db.ShowcaseProjects.Add(showcase);
        await db.SaveChangesAsync();
        return new PublishedShowcaseFixture(factory, showcase.Id, project.Id, team.Id, @class.Id, lecturer.Id);
    }

    private sealed class PublishedShowcaseFixture(
        CustomWebApplicationFactory factory, Guid showcaseId, Guid projectId, Guid teamId, Guid classId, Guid lecturerId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ShowcaseProjects.Remove(await db.ShowcaseProjects.SingleAsync(item => item.Id == showcaseId));
            db.Projects.Remove(await db.Projects.SingleAsync(item => item.Id == projectId));
            db.Teams.Remove(await db.Teams.SingleAsync(item => item.Id == teamId));
            db.Classes.Remove(await db.Classes.SingleAsync(item => item.Id == classId));
            db.Users.Remove(await db.Users.SingleAsync(item => item.Id == lecturerId));
            await db.SaveChangesAsync();
        }
    }

    private readonly record struct Reply(HttpStatusCode Status, string Text);

    private static async Task<Reply> Send(HttpClient client, HttpMethod method, string url, string? token, object? payload = null, string? headerKey = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (headerKey is not null) request.Headers.Add(ShowcaseAssistantApiKey.HeaderName, headerKey);
        if (payload is not null) request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request);
        return new Reply(response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static ApiResponse<T> Parse<T>(Reply reply) =>
        JsonSerializer.Deserialize<ApiResponse<T>>(reply.Text, JsonSerializerOptions.Web)!;

    private static T Data<T>(Reply reply) => Parse<T>(reply).Data!;
}
