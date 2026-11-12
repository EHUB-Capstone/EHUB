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
  /** The page (PDF) or slide (PPTX) of the evidence, when it can be told. */
  evidencePage?: number | null;
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
  /** The page or slide of each key sentence (0 when unknown), in the same order. */
  highlightPages?: number[];
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
  /** An original source excerpt is shown when the generated overview could not be verified. */
  overviewFromSource?: boolean;
  overviewEvidence?: SummaryClaimEvidence[];
  keyPointEvidence?: SummaryClaimEvidence[];
  keyPoints: string[];
  /** The file's most informative sentences, quoted as written. */
  highlights: string[];
  /** The page or slide of each key sentence (0 when unknown), in the same order. */
  highlightPages?: number[];
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

export interface SummaryClaimEvidence {
  text: string;
  quote: string;
  page: number | null;
}

export type SummaryFeedbackRating = 'Helpful' | 'NotHelpful';
export type SummaryFeedbackReason = 'Inaccurate' | 'MissingKeyPoint' | 'WrongEvidence' | 'Other';

/** The signed-in reviewer's own verdict on a file summary; other reviewers' verdicts are never sent. */
export interface SummaryFeedback {
  rating: SummaryFeedbackRating;
  /** Only with NotHelpful, one of a fixed list. */
  reason: SummaryFeedbackReason | null;
}

export interface SummaryFeedbackRequest extends SummaryFeedback {
  generationId?: string | null;
}

export interface FileSummary {
  fileId: string;
  /** Optional for compatibility while older backends are being replaced. */
  needsRefresh?: boolean;
  generationId?: string | null;
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
  /** The signed-in reviewer's verdict on this summary; null when they have not rated it. */
  myFeedback?: SummaryFeedback | null;
}

export interface CheckpointRequirementFinding {
  index: number;
  kind: ChecklistKind;
  title: string;
  finding: RequirementFinding;
  evidence: string | null;
  /** The page (PDF) or slide (PPTX) of the evidence, when it can be told. */
  evidencePage?: number | null;
  /** The file the evidence was quoted from. */
  source: string | null;
  /** The id of that file, so it can be opened at the evidence page. */
  sourceFileId?: string | null;
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

/** Mirrors WorkspaceCheckpointVersionChangesResponse: what changed in files the team uploaded again. Worked out from the text, not by the AI. */
export type VersionChangeStatus = 'Compared' | 'Unchanged' | 'NewFile' | 'Unreadable';
export type VersionChangeKind = 'Added' | 'Removed' | 'Changed';

export interface VersionChangeItem {
  kind: VersionChangeKind;
  /** The sentence as it reads now (Added, Changed) or as it read before (Removed). */
  text: string;
  /** The earlier wording (Changed). */
  before?: string | null;
  page?: number | null;
  figuresChanged: boolean;
}

export interface FileVersionChange {
  /** The newest version of the file. */
  fileId: string;
  fileName: string;
  fromVersion: number;
  toVersion: number;
  status: VersionChangeStatus;
  addedCount: number;
  removedCount: number;
  changedCount: number;
  unchangedPercent: number;
  items: VersionChangeItem[];
}

export interface CheckpointVersionChanges {
  checkpointNumber: number;
  files: FileVersionChange[];
}

/** Mirrors WorkspaceCheckpointFigureHistoryResponse: figures this checkpoint states differently from the team's earlier checkpoints. */
export interface CheckpointFigureHistory {
  checkpointNumber: number;
  /** Earlier checkpoints that had files to compare with; empty when there is nothing earlier. */
  comparedCheckpoints: number[];
  conflicts: FigureConflict[];
  /** Files of earlier checkpoints with no figures yet, named so a missing warning is not read as "all consistent". */
  uncheckedFiles: string[];
}

export type SummaryCircuitState = 'Closed' | 'Open' | 'HalfOpen';

export interface SummaryFailureCount {
  errorCode: string;
  count: number;
}

export interface SummaryFeedbackCount {
  promptVersion: string;
  helpful: number;
  notHelpful: number;
}

/**
 * Mirrors WorkspaceCheckpointSummaryDiagnosticsResponse, for administrators only: counts and error codes, never a file name or any
 * text from a submission.
 */
export interface SummaryDiagnostics {
  enabled: boolean;
  model: string;
  promptVersion: string;
  autoSummarize: boolean;
  /** The model server answered a request when this was read. */
  modelReachable: boolean;
  /** The configured model has been pulled into the model server. */
  modelAvailable: boolean;
  /** Kept in memory by one API instance. */
  circuit: SummaryCircuitState;
  consecutiveModelFailures: number;
  circuitOpenUntilUtc: string | null;
  lastModelFailureUtc: string | null;
  pendingRequested: number;
  pendingAutomatic: number;
  processing: number;
  /** Files being processed whose lease has run out; the worker takes them again. */
  expiredLeases: number;
  ready: number;
  failed: number;
  cancelled: number;
  oldestPendingSeconds: number | null;
  lastGeneratedAtUtc: string | null;
  failuresByCode: SummaryFailureCount[];
  feedbackByPromptVersion: SummaryFeedbackCount[];
  maxRequestedQueue: number;
  maxAutomaticQueue: number;
}
