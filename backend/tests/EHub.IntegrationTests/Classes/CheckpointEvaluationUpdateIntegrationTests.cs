using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Classes;

public sealed partial class TeamWorkflowIntegrationTests
{
    [Fact]
    public async Task CheckpointEvaluationUpdate_WhenEveryCriterionAlreadyHasAScore_SavesTheDraft()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        var evaluation = await SeedDraftEvaluationAsync(context, seed, scoredCriteria: 2);

        using var response = await PutEvaluationAsync(scope.ServiceProvider, context, evaluation.Id, seed.LecturerId,
            new SaveWorkspaceCheckpointEvaluationRequest
            {
                Status = "DRAFT",
                RubricScores =
                [
                    new WorkspaceCheckpointCriterionScoreInput { CriterionKey = "first", Score = 9 },
                    new WorkspaceCheckpointCriterionScoreInput { CriterionKey = "second", Score = 7 }
                ]
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CheckpointEvaluationUpdate_WhenACriterionHasNoScoreYet_SavesTheDraftAndAddsTheScore()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        // The rubric has two criteria but the stored evaluation only scored the first one,
        // as happens when a criterion is added to a rubric after a draft was saved.
        var evaluation = await SeedDraftEvaluationAsync(context, seed, scoredCriteria: 1);

        using var response = await PutEvaluationAsync(scope.ServiceProvider, context, evaluation.Id, seed.LecturerId,
            new SaveWorkspaceCheckpointEvaluationRequest
            {
                Status = "DRAFT",
                RubricScores =
                [
                    new WorkspaceCheckpointCriterionScoreInput { CriterionKey = "first", Score = 8 },
                    new WorkspaceCheckpointCriterionScoreInput { CriterionKey = "second", Score = 7 }
                ]
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var verify = _factory.Services.CreateScope();
        var verifyContext = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var details = await verifyContext.EvaluationDetails.AsNoTracking()
            .Where(item => item.EvaluationId == evaluation.Id)
            .Select(item => item.Score)
            .ToArrayAsync();
        details.Should().BeEquivalentTo(new[] { 8m, 7m });
    }

    private static async Task<Evaluation> SeedDraftEvaluationAsync(AppDbContext context, WorkflowSeed seed, int scoredCriteria)
    {
        var courseId = await context.Classes.Where(item => item.Id == seed.ClassId).Select(item => item.CourseId).SingleAsync();
        var checkpoint = new Checkpoint { CourseId = courseId, Name = "Update checkpoint", CheckpointNumber = 1,
            CourseWeight = 100, Status = CheckpointStatus.Open, CreatedById = seed.AdminId };
        var first = new RubricCriterion { Key = "first", Name = "First criterion", Weight = 50, MaxScore = 10, DisplayOrder = 1 };
        var second = new RubricCriterion { Key = "second", Name = "Second criterion", Weight = 50, MaxScore = 10, DisplayOrder = 2 };
        var rubric = new Rubric { CourseId = courseId, Checkpoint = checkpoint, Name = "Update rubric", TotalWeight = 100,
            CourseWeight = 100, Status = RubricStatus.Active, CreatedById = seed.AdminId, Criteria = [first, second] };
        var project = new Project { TeamId = seed.TeamId!.Value, Name = "Update project", CreatedById = seed.AdminId };
        var evaluation = new Evaluation { Project = project, Rubric = rubric, EvaluatorId = seed.LecturerId,
            TotalScore = 4, MaxTotalScore = 10, Status = EvaluationStatus.Draft };
        evaluation.Details.Add(new EvaluationDetail { RubricCriterion = first, Score = 8 });
        if (scoredCriteria > 1) evaluation.Details.Add(new EvaluationDetail { RubricCriterion = second, Score = 6 });
        context.Evaluations.Add(evaluation);
        await context.SaveChangesAsync();
        return evaluation;
    }

    private async Task<HttpResponseMessage> PutEvaluationAsync(IServiceProvider services, AppDbContext context,
        Guid evaluationId, Guid userId, SaveWorkspaceCheckpointEvaluationRequest body)
    {
        using var client = _factory.CreateClient();
        var user = await context.Users.AsNoTracking().SingleAsync(item => item.Id == userId);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/workspace/checkpoints/evaluations/{evaluationId}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            services.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [SystemRoles.Lecturer]).Token);
        return await client.SendAsync(request);
    }
}
