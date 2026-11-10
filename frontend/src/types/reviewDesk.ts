// Mirrors EHub.Contracts.Workspaces.WorkspaceReviewDeskResponse.

/** NewVersion: a version this reviewer has not opened. Overdue: nothing submitted after the deadline. DueSoon: deadline within 48 hours. */
export type InboxReason = 'NewVersion' | 'Overdue' | 'DueSoon';

export interface ReviewDeskInboxItem {
  teamId: string;
  teamCode: string;
  teamName: string;
  classId: string;
  classCode: string;
  checkpointNumber: number;
  reason: InboxReason;
  versionNumber: number | null;
  submittedAtUtc: string | null;
  dueAtUtc: string | null;
}

export interface ReviewDesk {
  inbox: ReviewDeskInboxItem[];
}
