export const PROJECT_ACHIEVEMENTS = ['Potential', 'Internally Funded', 'Externally Funded', 'Awarded'] as const;
export type ProjectAchievement = (typeof PROJECT_ACHIEVEMENTS)[number];

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
  /** Why the project carries its labels; only present while at least one label is set. */
  achievementNote: string | null;
  achievementsUpdatedAtUtc: string | null;
  achievementsUpdatedBy: ProjectDataPerson | null;
  rowVersion: string;
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
  group: string;
  startupIndustry: string;
  /** A major code or UNSPECIFIED; matches projects with at least one active member of that major. */
  major: string;
  lecturerId: string;
  mentorId: string;
  achievement: ProjectAchievement | '';
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
  /** Change since the previous semester of the same subject or class code; null for the earliest one. */
  averageScoreChange: number | null;
  potentialChange: number | null;
  internallyFundedChange: number | null;
  externallyFundedChange: number | null;
  awardedChange: number | null;
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
}

export interface UpdateProjectAchievementsPayload {
  achievements: ProjectAchievement[];
  note: string | null;
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

export interface ProjectAchievementsResult {
  projectId: string;
  achievements: ProjectAchievement[];
  rowVersion: string;
  note: string | null;
  updatedAtUtc: string | null;
  updatedBy: ProjectDataPerson | null;
}
