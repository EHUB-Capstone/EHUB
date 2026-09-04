using EHub.Application.Common.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Teams.ManageTeams;

// Called only after authorization, inside the caller's serializable transaction.
// ExecuteDelete bypasses the application's soft-delete interceptor intentionally.
internal static class TeamDataDeletion
{
    public static async Task<bool> HasExternalFilesAsync(IApplicationDbContext db, Guid teamId, CancellationToken ct)
    {
        var projects = db.Projects.IgnoreQueryFilters().Where(x => x.TeamId == teamId).Select(x => x.Id);
        var submissions = db.Submissions.IgnoreQueryFilters().Where(x => x.TeamId == teamId || projects.Contains(x.ProjectId)).Select(x => x.Id);
        return await db.SubmissionFiles.IgnoreQueryFilters().AnyAsync(x => submissions.Contains(x.SubmissionId) && (x.FileUrl != "" || x.CloudinaryPublicId != ""), ct)
            || await db.PitchDecks.IgnoreQueryFilters().AnyAsync(x => projects.Contains(x.ProjectId) && (x.FileUrl != "" || x.CloudinaryPublicId != ""), ct)
            || await db.ChatMessages.IgnoreQueryFilters().AnyAsync(x =>
                db.ChatGroups.IgnoreQueryFilters().Any(g => g.Id == x.ChatGroupId && g.TeamId == teamId)
                && x.AttachmentJson != null && x.AttachmentJson != "[]" && x.AttachmentJson != "null", ct)
            || await db.WeeklyTasks.IgnoreQueryFilters().AnyAsync(x => x.TeamId == teamId
                && x.AttachmentsJson != "[]" && x.AttachmentsJson != "null", ct);
    }

    public static async Task DeleteAsync(IApplicationDbContext db, Guid teamId, CancellationToken ct)
    {
        var projects = db.Projects.IgnoreQueryFilters().Where(x => x.TeamId == teamId).Select(x => x.Id);
        var submissions = db.Submissions.IgnoreQueryFilters().Where(x => x.TeamId == teamId || projects.Contains(x.ProjectId)).Select(x => x.Id);
        var evaluations = db.Evaluations.IgnoreQueryFilters().Where(x => projects.Contains(x.ProjectId) || (x.SubmissionId.HasValue && submissions.Contains(x.SubmissionId.Value))).Select(x => x.Id);
        var proposals = db.ProjectProposals.IgnoreQueryFilters().Where(x => x.TeamId == teamId || projects.Contains(x.ProjectId)).Select(x => x.Id);
        var teamProposals = db.TeamProposals.IgnoreQueryFilters().Where(x => x.ApprovedTeamId == teamId).Select(x => x.Id);
        var directions = db.ProjectDirections.IgnoreQueryFilters().Where(x => x.TeamId == teamId).Select(x => x.Id);
        var chats = db.ChatGroups.IgnoreQueryFilters().Where(x => x.TeamId == teamId).Select(x => x.Id);
        var mentors = db.MentorAssignments.IgnoreQueryFilters().Where(x => x.TeamId == teamId).Select(x => x.Id);
        var sessions = db.MentoringSessions.IgnoreQueryFilters().Where(x => mentors.Contains(x.MentorAssignmentId)).Select(x => x.Id);
        var datasets = db.AcademicDatasets.IgnoreQueryFilters().Where(x => x.ProjectId.HasValue && projects.Contains(x.ProjectId.Value)).Select(x => x.Id);

        await db.EvaluationHistories.IgnoreQueryFilters().Where(x => evaluations.Contains(x.EvaluationId)).ExecuteDeleteAsync(ct);
        await db.EvaluationDetails.IgnoreQueryFilters().Where(x => evaluations.Contains(x.EvaluationId)).ExecuteDeleteAsync(ct);
        await db.Evaluations.IgnoreQueryFilters().Where(x => evaluations.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.SubmissionFeedbacks.IgnoreQueryFilters().Where(x => submissions.Contains(x.SubmissionId)).ExecuteDeleteAsync(ct);
        await db.SubmissionFiles.IgnoreQueryFilters().Where(x => submissions.Contains(x.SubmissionId)).ExecuteDeleteAsync(ct);
        await db.Submissions.IgnoreQueryFilters().Where(x => submissions.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.PitchDecks.IgnoreQueryFilters().Where(x => projects.Contains(x.ProjectId)).ExecuteDeleteAsync(ct);
        await db.ProjectComments.IgnoreQueryFilters().Where(x => proposals.Contains(x.ProjectProposalId)).ExecuteDeleteAsync(ct);
        await db.ProjectProposalVersions.IgnoreQueryFilters().Where(x => proposals.Contains(x.ProjectProposalId)).ExecuteDeleteAsync(ct);
        await db.ProjectProposals.IgnoreQueryFilters().Where(x => proposals.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.TeamProposalHistory.IgnoreQueryFilters().Where(x => teamProposals.Contains(x.ProposalId)).ExecuteDeleteAsync(ct);
        await db.TeamProposalMembers.IgnoreQueryFilters().Where(x => teamProposals.Contains(x.ProposalId)).ExecuteDeleteAsync(ct);
        await db.TeamProposals.IgnoreQueryFilters().Where(x => teamProposals.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.ProjectDirectionReviews.IgnoreQueryFilters().Where(x => directions.Contains(x.ProjectDirectionId)).ExecuteDeleteAsync(ct);
        await db.ProjectDirections.IgnoreQueryFilters().Where(x => directions.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.ChatMessages.IgnoreQueryFilters().Where(x => chats.Contains(x.ChatGroupId)).ExecuteDeleteAsync(ct);
        await db.ChatGroupMembers.IgnoreQueryFilters().Where(x => chats.Contains(x.ChatGroupId)).ExecuteDeleteAsync(ct);
        await db.ChatGroups.IgnoreQueryFilters().Where(x => chats.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.MentoringAttendances.IgnoreQueryFilters().Where(x => sessions.Contains(x.MentoringSessionId)).ExecuteDeleteAsync(ct);
        await db.MentoringActionItems.IgnoreQueryFilters().Where(x => sessions.Contains(x.MentoringSessionId)).ExecuteDeleteAsync(ct);
        await db.MentoringSessions.IgnoreQueryFilters().Where(x => sessions.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.MentorAssignments.IgnoreQueryFilters().Where(x => mentors.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.SprintTasks.IgnoreQueryFilters().Where(x => x.TeamId == teamId).ExecuteDeleteAsync(ct);
        await db.WeeklyTasks.IgnoreQueryFilters().Where(x => x.TeamId == teamId).ExecuteDeleteAsync(ct);
        await db.Milestones.IgnoreQueryFilters().Where(x => x.TeamId == teamId).ExecuteDeleteAsync(ct);
        await db.Shortcuts.IgnoreQueryFilters().Where(x => x.TeamId == teamId || projects.Contains(x.ProjectId)).ExecuteDeleteAsync(ct);
        await db.ProjectAnalyses.IgnoreQueryFilters().Where(x => projects.Contains(x.ProjectId)).ExecuteDeleteAsync(ct);
        await db.ProjectTags.IgnoreQueryFilters().Where(x => projects.Contains(x.ProjectId)).ExecuteDeleteAsync(ct);
        await db.ProjectActivityLogs.IgnoreQueryFilters().Where(x => projects.Contains(x.ProjectId)).ExecuteDeleteAsync(ct);
        await db.StartupLineages.IgnoreQueryFilters().Where(x => projects.Contains(x.OriginalProjectId) || projects.Contains(x.CurrentProjectId)).ExecuteDeleteAsync(ct);
        await db.DataBankFieldHistories.IgnoreQueryFilters().Where(x => datasets.Contains(x.DatasetId)).ExecuteDeleteAsync(ct);
        await db.AcademicDatasets.IgnoreQueryFilters().Where(x => datasets.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.Projects.IgnoreQueryFilters().Where(x => projects.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await db.TeamMembers.IgnoreQueryFilters().Where(x => x.TeamId == teamId).ExecuteDeleteAsync(ct);
        await db.Teams.IgnoreQueryFilters().Where(x => x.Id == teamId).ExecuteDeleteAsync(ct);
    }
}
