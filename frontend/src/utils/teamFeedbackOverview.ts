import { matchesSearchQuery } from './searchText.ts';
import type {
  TeamFeedbackCheckpoint,
  TeamFeedbackEvaluation,
  TeamFeedbackFilters,
  TeamFeedbackThread,
} from '../types/teamFeedbackOverview.ts';

export const defaultTeamFeedbackFilters: TeamFeedbackFilters = {
  checkpointId: 'all',
  source: 'all',
  query: '',
};

const threadMatches = (thread: TeamFeedbackThread, query: string) => matchesSearchQuery(query, [
  thread.content,
  thread.author.name,
  ...thread.replies.flatMap((reply) => [reply.content, reply.author.name]),
]);

const evaluationMatches = (evaluation: TeamFeedbackEvaluation, query: string) => matchesSearchQuery(query, [
  evaluation.evaluator.name,
  evaluation.overallFeedback,
  evaluation.strengths,
  evaluation.weaknesses,
  evaluation.suggestions,
  ...evaluation.criterionComments.flatMap((item) => [item.criterionName, item.comment]),
]);

/** Applies the checkpoint, source and text filters. Checkpoints left without feedback are kept only when no filter is active. */
export function filterFeedbackCheckpoints(
  checkpoints: TeamFeedbackCheckpoint[],
  filters: TeamFeedbackFilters,
): TeamFeedbackCheckpoint[] {
  const query = filters.query.trim();
  const filtering = filters.checkpointId !== 'all' || filters.source !== 'all' || query.length > 0;

  return checkpoints
    .filter((checkpoint) => filters.checkpointId === 'all' || checkpoint.checkpointId === filters.checkpointId)
    .map((checkpoint) => ({
      ...checkpoint,
      threads: filters.source === 'evaluations'
        ? []
        : checkpoint.threads.filter((thread) => threadMatches(thread, query)),
      evaluations: filters.source === 'comments'
        ? []
        : checkpoint.evaluations.filter((evaluation) => evaluationMatches(evaluation, query)),
    }))
    .filter((checkpoint) => !filtering
      || filters.checkpointId !== 'all'
      || checkpoint.threads.length > 0
      || checkpoint.evaluations.length > 0);
}

export function countFeedbackItems(checkpoints: TeamFeedbackCheckpoint[]): number {
  return checkpoints.reduce((total, checkpoint) => total + checkpoint.threads.length + checkpoint.evaluations.length, 0);
}
