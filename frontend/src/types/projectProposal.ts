export type ProjectProposalStatus =
  | 'Draft'
  | 'Submitted'
  | 'NeedsRevision'
  | 'Approved'
  | 'Rejected'
  | 'Archived';

export type ProjectProposalVersionPurpose = 'DraftSave' | 'Submission';

export type ProjectProposalAnalysisStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed';
export type ProjectProposalOverlapRisk = 'InsufficientData' | 'Low' | 'Medium' | 'High';

export interface ProjectProposalContent {
  title: string;
  startupName: string;
  tagline: string;
  problem: string;
  solution: string;
  targetCustomers: string;
  valueProposition: string;
  marketSize: string;
  competitors: string;
  businessModel: string;
  revenueModel: string;
  marketingStrategy: string;
  technology: string;
  financialPlan: string;
  roadmap: string;
  teamIntroduction: string;
}

export interface ProjectProposalReview {
  id: string;
  proposalVersionId: string;
  fromStatus: ProjectProposalStatus;
  toStatus: ProjectProposalStatus;
  feedback: string;
  reviewedByUserId: string;
  occurredAtUtc: string;
}

export interface ProjectProposal extends ProjectProposalContent {
  id: string;
  projectId: string;
  teamId: string;
  classId: string;
  status: ProjectProposalStatus;
  currentSubmittedVersionId: string | null;
  currentAnalysisJobId: string | null;
  currentAnalysisStatus: ProjectProposalAnalysisStatus | null;
  submittedAtUtc: string | null;
  approvedAtUtc: string | null;
  rejectedAtUtc: string | null;
  rowVersion: string;
  reviews: ProjectProposalReview[];
}

export interface ProjectProposalDraft extends ProjectProposalContent {
  changeNote: string;
}

export interface CreateProjectProposalRequest extends ProjectProposalDraft {}

export interface UpdateProjectProposalRequest extends ProjectProposalDraft {
  rowVersion: string;
}

export interface SubmitProjectProposalRequest {
  rowVersion: string;
  changeNote: string;
}

export interface RestoreProjectProposalVersionRequest {
  rowVersion: string;
  changeNote: string;
}

export type ProjectProposalReviewDecision = 'Approved' | 'NeedsRevision' | 'Rejected';

export interface ReviewProjectProposalRequest {
  decision: ProjectProposalReviewDecision;
  feedback: string;
  rowVersion: string;
}

export interface ProjectProposalVersionSummary {
  id: string;
  versionNumber: number;
  purpose: ProjectProposalVersionPurpose;
  snapshotSchemaVersion: string;
  changeNote: string;
  changedByUserId: string;
  createdAtUtc: string;
}

export interface ProjectProposalVersion extends ProjectProposalVersionSummary {
  projectProposalId: string;
  snapshot: ProjectProposalContent;
}

export interface ProjectProposalAnalysisReport {
  summary: string;
  overlapRisk: ProjectProposalOverlapRisk;
  potentialDifferentiators: string[];
  limitations: string[];
  provider: string;
  model: string;
  promptVersion: string;
  outputSchemaVersion: string;
  embeddingProvider: string;
  embeddingModel: string;
  embeddingDimension: number;
  textSchemaVersion: string;
  retrievalVersion: string;
  matches: ProjectProposalAnalysisMatch[];
  generatedAtUtc: string;
}

export interface ProjectProposalAnalysisMatch {
  rank: number;
  proposalVersionId: string;
  projectProposalId: string;
  projectId: string;
  teamId: string;
  classId: string;
  classCode: string;
  semesterCode: string;
  title: string;
  startupName: string;
  semanticSimilarity: number;
  submittedAtUtc: string;
}

export interface ProjectProposalAnalysis {
  jobId: string;
  proposalVersionId: string;
  status: ProjectProposalAnalysisStatus;
  attemptCount: number;
  candidateScope: 'AllSystem';
  includeCrossSemester: boolean;
  languageMode: 'VietnameseAndEnglish';
  requestedAtUtc: string;
  processingStartedAtUtc: string | null;
  completedAtUtc: string | null;
  failedAtUtc: string | null;
  failureCode: string | null;
  canViewDetailedReport: boolean;
  report: ProjectProposalAnalysisReport | null;
}
