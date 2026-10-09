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
  /** True for large R2 files: download through `download-url` instead of the proxy endpoint. */
  canDirectDownload?: boolean;
  uploadedAt: string;
  uploadedBy?: WorkspaceCheckpointUser | null;
}

/** Response of POST .../uploads: where and how the browser sends the file directly to storage. */
export interface CheckpointUploadSession {
  uploadId: string;
  uploadUrl: string;
  method: 'PUT';
  headers: Record<string, string>;
  urlExpiresAt: string;
  sessionExpiresAt: string;
  maxFileSize: number;
}

export type CheckpointUploadStatus = 'queued' | 'uploading' | 'completing' | 'completed' | 'failed';

export interface CheckpointUploadItem {
  id: string;
  file: File;
  status: CheckpointUploadStatus;
  percent: number;
  session?: CheckpointUploadSession;
  /** The bytes reached storage; a retry only needs to call complete again. */
  putDone: boolean;
  error?: string;
  versionNumber?: number;
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
