export interface WorkspaceCheckpointConfig {
  number: number;
  title: string;
  shortDescription?: string | null;
  courseWeight: number;
  startDateUtc?: string | null;
  endDateUtc?: string | null;
  scheduleStatus: 'NotScheduled' | 'Upcoming' | 'Open' | 'Closed';
  canUpload: boolean;
  requirements: string[];
  rubrics?: unknown[];
  openDate?: string | null;
  dueDate?: string | null;
  availabilityStatus?: string | null;
  availabilityReason?: string | null;
}

export interface WorkspaceCheckpointFile {
  _id: string;
  versionNumber: number;
  originalName: string;
  fileType: string;
  fileSize: number;
  uploadedAt: string;
  uploadedBy?: WorkspaceCheckpointUser | null;
}

export interface WorkspaceCheckpointUser {
  _id: string;
  name: string;
  role?: string;
  avatarUrl?: string | null;
}

export interface WorkspaceCheckpointLink {
  _id: string;
  versionNumber: number;
  name: string;
  url: string;
  submittedAt: string;
  submittedBy: WorkspaceCheckpointUser;
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
  links: WorkspaceCheckpointLink[];
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
  linkCount: number;
  latest: WorkspaceCheckpointFile | null;
  latestVersion: number | null;
  reqFilled: number;
  reqTotal: number;
  status?: string;
  submittedAt?: string | null;
}
