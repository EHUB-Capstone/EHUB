using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Workspaces.CheckpointEvaluations;

internal static class EvaluationOutbox
{
    public const string PublishedEventType = "Evaluation.Published.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void EnqueuePublished(
        IApplicationDbContext context,
        Evaluation evaluation,
        Team team,
        int checkpointNumber,
        string checkpointTitle,
        DateTime occurredAtUtc)
    {
        var eventId = Guid.NewGuid();
        var studentUserIds = team.TeamMembers
            .Where(member => member.CountsTowardActiveTeam &&
                             member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active &&
                             member.ClassStudent.Student.UserId.HasValue)
            .Select(member => member.ClassStudent.Student.UserId!.Value)
            .Distinct()
            .ToArray();

        context.OutboxMessages.Add(new OutboxMessage
        {
            EventId = eventId,
            Type = PublishedEventType,
            AggregateType = "Evaluation",
            AggregateId = evaluation.Id,
            OccurredAtUtc = occurredAtUtc,
            AvailableAtUtc = occurredAtUtc,
            PayloadJson = JsonSerializer.Serialize(new
            {
                EventId = eventId,
                EventType = PublishedEventType,
                AggregateType = "Evaluation",
                AggregateId = evaluation.Id,
                OccurredAtUtc = occurredAtUtc,
                Data = new
                {
                    teamId = team.Id,
                    checkpointNumber,
                    checkpointTitle,
                    studentUserIds
                }
            }, JsonOptions)
        });
    }
}
