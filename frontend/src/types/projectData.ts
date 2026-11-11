export const PROJECT_ACHIEVEMENTS = ['Potential', 'Internally Funded', 'Externally Funded', 'Awarded', 'Pitching', 'Demo'] as const;
export type ProjectAchievement = (typeof PROJECT_ACHIEVEMENTS)[number];

/** The competitions projects usually enter. "Others" is a free-text name. Keep in step with ProjectCompetitionNames (API). */
export const PROJECT_COMPETITIONS = [
  'SV.STARTUP',
  'Sinh viên Thế Hệ Mới VTV',
  'VietFuture Awards',
  'FTalent',
  'DBC',
  'FShark',
  'Startup Wheel',
  'DAVAS',
  'SURF',
  'Others',
] as const;
export const COMPETITION_OTHERS = 'Others';

/** Furthest stage reached, in order. Sent to and received from the API as these names. */
export const PROJECT_COMPETITION_STAGES = ['Registered', 'Qualifier', 'Final'] as const;
export type ProjectCompetitionStage = (typeof PROJECT_COMPETITION_STAGES)[number];

export const COMPETITION_STAGE_LABELS: Record<ProjectCompetitionStage, string> = {
  Registered: 'Registered',
  Qualifier: 'Qualifier round',
  Final: 'Final round',
};

/** The year a competition took place (not the academic year of the project). */
export const COMPETITION_YEAR_MIN = 2000;
export const COMPETITION_YEAR_MAX = 2100;
export const COMPETITION_OTHER_NAME_MAX_LENGTH = 100;
export const COMPETITION_MAX_ENTRIES = 30;

export const PROJECT_DATA_SEMESTER_TERMS = ['SP', 'SU', 'FA'] as const;
export type ProjectDataSemesterTerm = (typeof PROJECT_DATA_SEMESTER_TERMS)[number];

export const PROJECT_DATA_TABS = ['overview', 'analytics'] as const;
export type ProjectDataTab = (typeof PROJECT_DATA_TABS)[number];
export const DEFAULT_PROJECT_DATA_TAB: ProjectDataTab = 'overview';

export const PROJECT_DATA_PAGE_SIZES = [10, 20, 50, 100] as const;
export type ProjectDataPageSize = (typeof PROJECT_DATA_PAGE_SIZES)[number];

export const PROJECT_DATA_SORT_FIELDS = ['classCode', 'semester', 'group', 'projectName'] as const;
export type ProjectDataSortField = (typeof PROJECT_DATA_SORT_FIELDS)[number];

export type ProjectDataMentorSlot = 'Enterprise' | 'Academic';

export interface ProjectDataPerson {
  userId: string;
  fullName: string;
}

export interface ProjectDataMentor extends ProjectDataPerson {
  assignmentId: string;
  mentorProfileId: string;
  slot: ProjectDataMentorSlot;
  assignedAtUtc: string;
  endedAtUtc: string | null;
  /** True when the assignment was closed by class completion instead of being currently active. */
  isHistorical: boolean;
}

/** Current counters of the lineage's showcase across semesters and publication states. Reference only. */
export interface ProjectDataShowcaseMetrics {
  viewCount: number;
  upvoteCount: number;
  commentCount: number;
}

export interface ProjectDataItem {
  projectId: string;
  teamId: string;
  classId: string;
  semesterId: string;
  semesterCode: string;
  subjectId: string;
  subjectCode: string;
  classCode: string;
  groups: string[];
  projectName: string;
  description: string | null;
  startupIndustries: string[];
  lecturer: ProjectDataPerson | null;
  mentor: ProjectDataMentor | null;
  academicMentor: ProjectDataMentor | null;
  achievements: ProjectAchievement[];
  competitions: ProjectCompetition[];
  /** Why the project carries its labels; only present while at least one label is set. */
  achievementNote: string | null;
  achievementsUpdatedAtUtc: string | null;
  achievementsUpdatedBy: ProjectDataPerson | null;
  rowVersion: string;
  /** Null when the lineage has no non-deleted showcase. */
  showcaseMetrics: ProjectDataShowcaseMetrics | null;
}

export interface ProjectCompetition {
  competition: string;
  /** The free-text name; only set when `competition` is "Others". */
  otherName: string | null;
  /** What to show: the free text for "Others", otherwise the competition name. */
  name: string;
  year: number;
  stage: ProjectCompetitionStage;
}

export interface ProjectDataPage {
  items: ProjectDataItem[];
  pageIndex: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface ProjectDataQuery {
  search: string;
  subjectCode: string;
  semester: ProjectDataSemesterTerm | '';
  year: string;
  // The filters below hold any number of values: a project matches when it matches ANY of them; empty means "All".
  group: string[];
  startupIndustry: string[];
  /** Major codes or UNSPECIFIED; matches projects with at least one active member of any of those majors. */
  major: string[];
  lecturerId: string[];
  mentorId: string[];
  achievement: ProjectAchievement[];
  /** Competition filters describe one entry: a project matches when one of its entries satisfies all of them. */
  competition: string[];
  competitionStage: ProjectCompetitionStage[];
  competitionYear: string[];
  pageIndex: number;
  pageSize: ProjectDataPageSize;
  sortBy: ProjectDataSortField;
  isDescending: boolean;
}

export interface ProjectDataSummary {
  totalGroups: number;
  potentialGroups: number;
  internallyFundedGroups: number;
  externallyFundedGroups: number;
  awardedGroups: number;
  pitchingGroups: number;
  demoGroups: number;
}

export interface ProjectDataDistributionBucket {
  name: string;
  count: number;
}

export interface ProjectDataDistributionSemester {
  code: string;
  year: number;
  term: ProjectDataSemesterTerm;
  count: number;
}

/** Groups counted per dimension. Semester buckets add up to `totalGroups`; the others can overlap. */
export interface ProjectDataDistribution {
  totalGroups: number;
  semesters: ProjectDataDistributionSemester[];
  majors: ProjectDataDistributionBucket[];
  /** Startup industry, which is also the project topic. */
  startupIndustries: ProjectDataDistributionBucket[];
  unspecifiedStartupIndustryCount: number;
  outcomes: ProjectDataDistributionBucket[];
  noOutcomeCount: number;
}

export interface ProjectDataTrendRow {
  semesterCode: string;
  year: number;
  term: ProjectDataSemesterTerm;
  subjectCode: string | null;
  classId: string | null;
  classCode: string | null;
  totalGroups: number;
  /** Groups whose course score is official: every weighted component has a published evaluation. */
  gradedGroups: number;
  /** Official course score out of 10; null while no group in the row is graded. */
  averageScore: number | null;
  potentialGroups: number;
  internallyFundedGroups: number;
  externallyFundedGroups: number;
  awardedGroups: number;
  pitchingGroups: number;
  demoGroups: number;
  /** Change since the previous semester of the same subject or class code; null for the earliest one. */
  averageScoreChange: number | null;
  potentialChange: number | null;
  internallyFundedChange: number | null;
  externallyFundedChange: number | null;
  awardedChange: number | null;
  pitchingChange: number | null;
  demoChange: number | null;
}

export interface ProjectDataTrends {
  semesters: ProjectDataTrendRow[];
  subjects: ProjectDataTrendRow[];
  classes: ProjectDataTrendRow[];
}

export interface ProjectDataSubjectOption {
  code: string;
  name: string;
}

export interface ProjectDataMentorOption extends ProjectDataPerson {
  slot: ProjectDataMentorSlot;
}

export interface ProjectDataFilterOptions {
  subjects: ProjectDataSubjectOption[];
  years: number[];
  groups: string[];
  startupIndustries: string[];
  majors: string[];
  lecturers: ProjectDataPerson[];
  mentors: ProjectDataMentorOption[];
  achievements: ProjectAchievement[];
  competitions: string[];
  competitionStages: ProjectCompetitionStage[];
  competitionYears: number[];
}

export interface UpdateProjectAchievementsPayload {
  achievements: ProjectAchievement[];
  note: string | null;
  rowVersion: string;
}

export interface ProjectCompetitionEntryPayload {
  competition: string;
  otherName: string | null;
  year: number;
  stage: ProjectCompetitionStage;
}

export interface UpdateProjectCompetitionsPayload {
  entries: ProjectCompetitionEntryPayload[];
  rowVersion: string;
}

export interface ProjectCompetitionsResult {
  projectId: string;
  competitions: ProjectCompetition[];
  rowVersion: string;
}

export interface UpdateProjectStartupIndustriesPayload {
  startupIndustryIds: string[];
  rowVersion: string;
}

export interface ProjectStartupIndustriesResult {
  projectId: string;
  startupIndustries: string[];
  rowVersion: string;
}

export interface ProjectStartupIndustryHistoryItem {
  id: string;
  summary: string;
  actorName: string | null;
  occurredAtUtc: string;
  /** What the change did. All empty means unreadable: show `summary` instead. */
  added: string[];
  removed: string[];
  kept: string[];
}

export interface ProjectStartupIndustryHistory {
  /** Every recorded change; `items` holds only the latest ones, newest first. */
  totalCount: number;
  items: ProjectStartupIndustryHistoryItem[];
}

export interface ProjectAchievementHistoryItem {
  id: string;
  /** PROJECT_ACHIEVEMENTS_CHANGED or ACHIEVEMENTS_CARRIED_OVER. */
  action: string;
  summary: string;
  actorName: string | null;
  occurredAtUtc: string;
  /** What the change did. All empty with `noteChanged` false means unreadable: show `summary` instead. */
  added: ProjectAchievement[];
  removed: ProjectAchievement[];
  kept: ProjectAchievement[];
  /** True when this entry set or cleared the note. */
  noteChanged: boolean;
  /** The note set by this entry; null when it was cleared. */
  note: string | null;
}

export interface ProjectAchievementHistory {
  /** Every recorded change; `items` holds only the latest ones, newest first. */
  totalCount: number;
  items: ProjectAchievementHistoryItem[];
}

/** How far a graded part of the course score has come; only a Published part shows a score or feedback. */
export const PROJECT_EVIDENCE_STATUSES = ['Published', 'PendingPublication', 'NotGraded'] as const;
export type ProjectEvidenceStatus = (typeof PROJECT_EVIDENCE_STATUSES)[number];

export interface ProjectEvidenceComponent {
  name: string;
  kind: 'Checkpoint' | 'Assessment';
  /** The checkpoint order (1, 2, ...); null for an assessment. */
  checkpointNumber: number | null;
  /** Share of the course score, 0-100. */
  weightPercent: number;
  status: ProjectEvidenceStatus;
  /** The 0-10 score of the published version. */
  score: number | null;
  feedback: string | null;
  publishedAtUtc: string | null;
}

export interface ProjectEvidenceEvaluation {
  /** The official 0-10 course score (same as Analytics and the export); null until every part is published. */
  courseScore: number | null;
  components: ProjectEvidenceComponent[];
}

export interface ProjectEvidenceMentor {
  assignmentId: string;
  mentorProfileId: string;
  fullName: string;
  organization: string | null;
  /** Enterprise or Academic: the mentor slot of the team. */
  slot: 'Enterprise' | 'Academic';
  assignedAtUtc: string;
  endedAtUtc: string | null;
  /** True for the mentor the project list shows; false for one who left or was replaced earlier. */
  isCurrent: boolean;
  expertise: string[];
  completedSessions: number;
}

export interface ProjectEvidenceMentoring {
  mentors: ProjectEvidenceMentor[];
  totalCompletedSessions: number;
  lastSessionAtUtc: string | null;
}

/** The academic evidence of one project: how it was graded and who mentored it. */
export interface ProjectEvidence {
  projectId: string;
  evaluation: ProjectEvidenceEvaluation;
  mentoring: ProjectEvidenceMentoring;
}

export interface ProjectAchievementsResult {
  projectId: string;
  achievements: ProjectAchievement[];
  rowVersion: string;
  note: string | null;
  updatedAtUtc: string | null;
  updatedBy: ProjectDataPerson | null;
}
