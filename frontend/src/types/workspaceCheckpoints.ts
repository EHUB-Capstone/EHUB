export interface WorkspaceCheckpointConfig {
  number: number;
  title: string;
  shortDescription?: string | null;
  startDateUtc?: string | null;
  endDateUtc?: string | null;
  scheduleStatus: 'NotScheduled' | 'Upcoming' | 'Open' | 'Closed';
  canUpload: boolean;
  requirements: string[];
  rubrics?: unknown[];
}

export interface WorkspaceCheckpointFile {
  _id: string;
  versionNumber: number;
  originalName: string;
  fileType: string;
  fileSize: number;
  uploadedAt: string;
}

export interface WorkspaceCheckpointRequirementContent {
  index: number;
  content: string;
}

export interface WorkspaceCheckpointSubmission {
  checkpointNumber: number;
  status: string;
  files: WorkspaceCheckpointFile[];
  requirementContents: WorkspaceCheckpointRequirementContent[];
}

export interface WorkspaceCheckpointOverviewResponse {
  subjectCode: string;
  checkpoints: WorkspaceCheckpointConfig[];
  submissions: WorkspaceCheckpointSubmission[];
  feedbacks: unknown[];
}

export interface WorkspaceCheckpointStats {
  count: number;
  latest: WorkspaceCheckpointFile | null;
  reqFilled: number;
  reqTotal: number;
}
