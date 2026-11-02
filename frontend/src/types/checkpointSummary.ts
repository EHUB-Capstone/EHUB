// Mirrors EHub.Contracts.Workspaces.WorkspaceCheckpointSummaryResponse.
export type FileSummaryStatus = 'None' | 'Pending' | 'Processing' | 'Ready' | 'Failed' | 'Cancelled';

/** Related: a sentence names the requirement. Unclear: the AI could not back its claim. NotChecked: no file summarised yet. */
export type RequirementFinding = 'Related' | 'Unclear' | 'NotFound' | 'NotChecked';

export interface FileRequirementFinding {
  index: number;
  title: string;
  finding: Exclude<RequirementFinding, 'NotChecked'>;
  evidence: string | null;
}

export interface FileSummaryContent {
  overview: string;
  keyPoints: string[];
  /** The file's most informative sentences, quoted as written. */
  highlights: string[];
  requirements: FileRequirementFinding[];
  questions: string[];
}

export interface FileSummary {
  fileId: string;
  name: string;
  versionNumber: number;
  uploadedAt: string;
  status: FileSummaryStatus;
  /** For queued files: summaries ahead in the shared queue. */
  queueAhead: number | null;
  requestedAt: string | null;
  generatedAt: string | null;
  errorCode: string | null;
  inputTruncated: boolean;
  content: FileSummaryContent | null;
}

export interface CheckpointRequirementFinding {
  index: number;
  title: string;
  finding: RequirementFinding;
  evidence: string | null;
  /** The file the evidence was quoted from. */
  source: string | null;
}

export interface CheckpointSummaryLink {
  name: string;
  domain: string;
}

export interface CheckpointSummary {
  checkpointNumber: number;
  enabled: boolean;
  filePromptCharacters: number;
  /** Files shown on the checkpoint, newest version first. */
  files: FileSummary[];
  requirements: CheckpointRequirementFinding[];
  links: CheckpointSummaryLink[];
}
