// Mirrors EHub.Contracts.Workspaces.WorkspaceCheckpointSummaryResponse.
export type FileSummaryStatus = 'None' | 'Pending' | 'Processing' | 'Ready' | 'Failed' | 'Cancelled';

/**
 * Related: a sentence of the file names the item. NotFound: files were read but none does. NotChecked: no file read yet.
 * Live: judged live (presentation, Q&A, teamwork), so no file can show it. Product: judged on the product or demo.
 * Overall: judged on how clear or complete the whole file is, so no passage stands for it.
 */
export type RequirementFinding = 'Related' | 'NotFound' | 'NotChecked' | 'Live' | 'Product' | 'Overall';

/** Document: a file can show it. Product: judged on the product or demo. Live: presentation, Q&A, teamwork. Overall: the whole file. */
export type CriterionAssessment = 'Document' | 'Product' | 'Live' | 'Overall';

/** One thing the criterion's Excellent level expects (roadmap, persona...) and whether the file mentions it. */
export interface CriterionCue {
  label: string;
  found: boolean;
  evidence: string | null;
}

/** Requirement: written by the lecturer for the checkpoint. Criterion: a rubric criterion the checkpoint is graded on. */
export type ChecklistKind = 'Requirement' | 'Criterion';

export interface FileRequirementFinding {
  index: number;
  kind: ChecklistKind;
  title: string;
  finding: Exclude<RequirementFinding, 'NotChecked'>;
  evidence: string | null;
  /** Share of the checkpoint grade, in percent (criteria only). */
  weight?: number | null;
  assessment?: CriterionAssessment;
  cues?: CriterionCue[];
}

/** One measure (for example the conversion rate) given different values in different places of a file. */
export interface FigureConflict {
  subject: string;
  /** `source` is the file that states the value, when the figures of different files are compared. */
  values: { value: string; quote: string; source?: string | null }[];
}

/** A line of the file that reports where a piece of work stands, quoted as written. */
export interface StatusItem {
  status: 'Done' | 'InProgress' | 'Pending';
  text: string;
  /** The numbered request this status answers, when the file numbers both. */
  request?: string | null;
}

/** Worked out from the file text without the AI model, so it is available seconds after a file is queued. */
export interface FilePreview {
  highlights: string[];
  requirements: FileRequirementFinding[];
  /** Figures the file states twice with different values, found without the AI model. */
  conflicts: FigureConflict[];
  /** Lines reporting done / in progress / pending work, found without the AI model. */
  statusItems: StatusItem[];
  /** Sentences addressed to the AI (orders, requests for a grade) that were left out before summarising. */
  ignoredInstructions?: number;
  /** Mostly images or diagrams: only titles and a few lines of text could be read. */
  littleText: boolean;
}

export interface FileSummaryContent {
  overview: string;
  keyPoints: string[];
  /** The file's most informative sentences, quoted as written. */
  highlights: string[];
  requirements: FileRequirementFinding[];
  questions: string[];
  /** Figures the file states twice with different values, found without the AI model. */
  conflicts: FigureConflict[];
  /** Lines reporting done / in progress / pending work, found without the AI model. */
  statusItems: StatusItem[];
  /** Sentences addressed to the AI (orders, requests for a grade) that were left out before summarising. */
  ignoredInstructions?: number;
  /** Mostly images or diagrams: only titles and a few lines of text could be read. */
  littleText: boolean;
}

export interface FileSummary {
  fileId: string;
  name: string;
  versionNumber: number;
  uploadedAt: string;
  status: FileSummaryStatus;
  /** For queued files: summaries ahead in the shared queue. */
  queueAhead: number | null;
  /** Queued automatically after upload (low priority) rather than requested by a reviewer. */
  isAutomatic: boolean;
  /** Quick view shown while the AI is still working; null once the summary is ready or when nothing could be read. */
  preview: FilePreview | null;
  requestedAt: string | null;
  generatedAt: string | null;
  errorCode: string | null;
  inputTruncated: boolean;
  content: FileSummaryContent | null;
}

export interface CheckpointRequirementFinding {
  index: number;
  kind: ChecklistKind;
  title: string;
  finding: RequirementFinding;
  evidence: string | null;
  /** The file the evidence was quoted from. */
  source: string | null;
  weight?: number | null;
  assessment?: CriterionAssessment;
  cues?: CriterionCue[];
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
  /** Figures that two files state differently (a sample of 51 and of 52), found without the AI model. */
  conflicts?: FigureConflict[];
  links: CheckpointSummaryLink[];
}
