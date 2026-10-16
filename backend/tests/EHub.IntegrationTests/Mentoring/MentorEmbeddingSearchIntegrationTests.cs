using System.Net;
using System.Text;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Domain.Entities;
using EHub.IntegrationTests.Common;
using EHub.Infrastructure.Mentoring;
using EHub.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Mentoring;

[Collection("Sequential")]
public sealed class MentorEmbeddingSearchIntegrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task RecommendationsRequireAuthentication()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/mentoring/teams/{Guid.NewGuid()}/recommendations");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    [Fact]
    public async Task StoresEmbeddingsAndRefreshesWhenMentorProfileChanges()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = $"vector-{Guid.NewGuid():N}@example.test";
        var user = new User { Email = email, NormalizedEmail = email.ToUpperInvariant(), FullName = "Mentor" };
        var mentor = new MentorProfile { User = user, Expertise = ["AI"], Experience = "Data science" };
        db.MentorProfiles.Add(mentor);
        await db.SaveChangesAsync();

        var fake = new FakeOllamaHandler();
        using var client = new HttpClient(fake) { BaseAddress = new Uri("http://localhost:11434/") };
        var service = new OllamaMentorEmbeddingSearch(db, client,
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>());

        var first = await service.SimilaritiesAsync("AI data platform", [mentor], default);
        var second = await service.SimilaritiesAsync("AI data platform", [mentor], default);
        first[mentor.Id].Should().BeGreaterThan(0.9);
        second[mentor.Id].Should().Be(first[mentor.Id]);
        fake.RequestCount.Should().Be(3); // Mentor + project, then cached mentor + project.
        (await db.MentorEmbeddings.AsNoTracking().CountAsync()).Should().BeGreaterThan(0);

        mentor.Experience = "Marketing strategy";
        await db.SaveChangesAsync();
        await service.SimilaritiesAsync("AI data platform", [mentor], default);
        fake.RequestCount.Should().Be(5); // Updated mentor + project.
    }

    [Fact]
    public async Task InvalidOllamaVectorFailsWithoutWritingCache()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fake = new FakeOllamaHandler { InvalidResponse = true };
        using var client = new HttpClient(fake) { BaseAddress = new Uri("http://localhost:11434/") };
        var service = new OllamaMentorEmbeddingSearch(db, client,
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>());
        var mentor = new MentorProfile { Expertise = ["AI"] };

        var act = () => service.SimilaritiesAsync("AI project", [mentor], default);
        await act.Should().ThrowAsync<MentorEmbeddingUnavailableException>();
        (await db.MentorEmbeddings.AsNoTracking().AnyAsync(x => x.MentorProfileId == mentor.Id)).Should().BeFalse();
    }

    private sealed class FakeOllamaHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public bool InvalidResponse { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (InvalidResponse)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"embeddings\":[[]]}", Encoding.UTF8, "application/json")
                };
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var inputs = document.RootElement.GetProperty("input").EnumerateArray()
                .Select(x => x.GetString() ?? string.Empty).ToArray();
            var vectors = inputs.Select(text =>
            {
                var values = new float[1024];
                values[text.Contains("AI", StringComparison.OrdinalIgnoreCase) ? 0 : 1] = 1;
                return values;
            }).ToArray();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { embeddings = vectors }),
                    Encoding.UTF8, "application/json")
            };
        }
    }
}
