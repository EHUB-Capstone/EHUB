using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Subjects;

/// <summary>
/// A subject whose rubric was seeded before criteria had keys (every key empty) must still be configurable: saving the
/// official rubric used to fail with "An item with the same key has already been added".
/// </summary>
[Collection("Sequential")]
public sealed class SubjectRubricLegacyCriteriaIntegrationTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> SeedLegacySubjectAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var code = "LK" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var course = new Course { Code = code, Name = "Legacy rubric subject" };
        var checkpoint = new Checkpoint { Course = course, Name = "Checkpoint 1", CheckpointNumber = 1, CourseWeight = 100 };
        var rubric = new Rubric { Course = course, Checkpoint = checkpoint, Name = $"{code} Checkpoint 1", TotalWeight = 100 };
        context.Courses.Add(course);
        context.Checkpoints.Add(checkpoint);
        context.Rubrics.Add(rubric);
        // The five criteria the old seeder wrote, none of them with a key.
        context.RubricCriteria.AddRange(new[] { "Problem Clarity", "Solution Fit", "Market Potential", "Business Model", "Presentation" }
            .Select((name, index) => new RubricCriterion { Rubric = rubric, Name = name, Weight = 20, MaxScore = 10, DisplayOrder = index + 1 }));
        await context.SaveChangesAsync();
        return code;
    }

    private async Task<HttpResponseMessage> SaveAsync(string code, params (string Key, string Label, decimal Weight)[] criteria)
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = "admin@ehub.test", Password = "Admin@123456" });
        var token = (await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!.AccessToken;
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/subjects/{code}/checkpoints")
        {
            Content = JsonContent.Create(new SaveSubjectCheckpointsRequest
            {
                Checkpoints =
                [
                    new SubjectCheckpointRequest
                    {
                        Number = 1, Title = "Checkpoint 1", CourseWeight = 100, Requirements = [],
                        Rubrics = criteria.Select(item => new SubjectCriterionRequest { Key = item.Key, Label = item.Label, Weight = item.Weight }).ToArray()
                    }
                ]
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task Save_ReplacesCriteriaSeededWithoutKeysByTheOfficialRubric()
    {
        var code = await SeedLegacySubjectAsync();

        using var response = await SaveAsync(code,
            ("productRoadmapCoreFeatures", "Product Roadmap & Core Features", 30),
            ("productCompleteness", "Product Completeness (UI/UX & Stability) / MVP demo", 25),
            ("problemSolving", "Problem Solving & Practicality (Persona & CJM) + Marketing Plan", 30),
            ("demoSkillsQA", "Demo Skills & Q&A", 15));

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().RubricCriteria.AsNoTracking()
            .Where(item => item.Rubric.Course!.Code == code).OrderBy(item => item.DisplayOrder).ToListAsync();
        saved.Select(item => item.Key).Should().Equal("productRoadmapCoreFeatures", "productCompleteness", "problemSolving", "demoSkillsQA");
        saved.Select(item => item.Weight).Should().Equal(30m, 25m, 30m, 15m);
    }

    [Fact]
    public async Task Save_KeepsTheRowsOfLegacyCriteriaWhoseNamesAreResubmitted()
    {
        var code = await SeedLegacySubjectAsync();

        using var response = await SaveAsync(code,
            ("problemClarity", "Problem Clarity", 50), ("solutionFit", "Solution Fit", 50));

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().RubricCriteria.AsNoTracking()
            .Where(item => item.Rubric.Course!.Code == code).OrderBy(item => item.DisplayOrder).ToListAsync();
        saved.Select(item => item.Key).Should().Equal("problemClarity", "solutionFit");
    }
}
