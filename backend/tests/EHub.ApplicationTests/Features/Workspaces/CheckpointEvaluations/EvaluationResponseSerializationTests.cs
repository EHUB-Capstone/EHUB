using System.Text.Json;
using EHub.Contracts.Workspaces;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointEvaluations;

public sealed class EvaluationResponseSerializationTests
{
    [Fact]
    public void HiddenSensitiveScores_AreOmittedFromSerializedResponse()
    {
        var response = new WorkspaceCheckpointEvaluationSummaryResponse
        {
            Evaluations =
            [
                new WorkspaceCheckpointEvaluationResponse
                {
                    Status = "SUBMITTED",
                    OverallFeedback = "Feedback remains visible.",
                    CheckpointTotal = null,
                    MemberScores = null,
                    RubricScores =
                    [
                        new WorkspaceCheckpointCriterionScoreResponse
                        {
                            CriterionKey = "evidence",
                            CriterionName = "Evidence",
                            Score = null,
                            Comment = "Add more customer evidence.",
                        },
                    ],
                },
            ],
            Summary = new WorkspaceCheckpointEvaluationAggregateResponse
            {
                EvaluationCount = 1,
                SubmittedCount = 1,
                AverageScore = null,
            },
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Contain("Feedback remains visible.");
        json.Should().Contain("Add more customer evidence.");
        json.Should().NotContain("checkpointTotal");
        json.Should().NotContain("memberScores");
        json.Should().NotContain("averageScore");
        json.Should().NotContain("\"score\"");
    }
}
