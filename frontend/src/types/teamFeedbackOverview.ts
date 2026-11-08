export interface TeamFeedbackAuthor {
  id: string | null;
  name: string;
  role: string;
}

export interface TeamFeedbackReply {
  id: string;
  content: string;
  createdAt: string;
  author: TeamFeedbackAuthor;
}

export interface TeamFeedbackThread {
  id: string;
  content: string;
  isTechnicalReview: boolean;
  resolved: boolean;
  createdAt: string;
  submissionVersion: number;
  author: TeamFeedbackAuthor;
  replies: TeamFeedbackReply[];
}

export interface TeamFeedbackCriterionComment {
  criterionName: string;
  comment: string;
}

/** Written evaluation feedback only. The API never returns scores for this view. */
export interface TeamFeedbackEvaluation {
  id: string;
  status: string;
  evaluator: TeamFeedbackAuthor;
  overallFeedback: string | null;
  strengths: string | null;
  weaknesses: string | null;
  suggestions: string | null;
  submittedAt: string | null;
  publishedAt: string | null;
  criterionComments: TeamFeedbackCriterionComment[];
}

export interface TeamFeedbackCheckpoint {
  checkpointId: string;
  checkpointNumber: number;
  title: string;
  threads: TeamFeedbackThread[];
  evaluations: TeamFeedbackEvaluation[];
}

export interface TeamFeedbackOverview {
  teamId: string;
  teamName: string;
  checkpoints: TeamFeedbackCheckpoint[];
}

export type TeamFeedbackSource = 'all' | 'comments' | 'evaluations';

export interface TeamFeedbackFilters {
  checkpointId: string;
  source: TeamFeedbackSource;
  query: string;
}
