export interface WorkspaceCheckpointConfig {
  number: number;
  title: string;
  shortDescription?: string | null;
  requirements: string[];
  rubrics?: unknown[];
  openDate?: string | null;
  dueDate?: string | null;
  availabilityStatus?: string | null;
  availabilityReason?: string | null;
}

export interface WorkspaceCheckpointFile {
  _id: string;
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
  submittedAt?: string | null;
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
  status?: string;
  submittedAt?: string | null;
}
