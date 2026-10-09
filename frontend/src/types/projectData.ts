export const PROJECT_ACHIEVEMENTS = ['Potential', 'Funded', 'Awarded'] as const;
export type ProjectAchievement = (typeof PROJECT_ACHIEVEMENTS)[number];

export const PROJECT_DATA_SEMESTER_TERMS = ['SP', 'SU', 'FA'] as const;
export type ProjectDataSemesterTerm = (typeof PROJECT_DATA_SEMESTER_TERMS)[number];

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
  fundedGroups: number;
  awardedGroups: number;
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
  lecturers: ProjectDataPerson[];
  mentors: ProjectDataMentorOption[];
  achievements: ProjectAchievement[];
}

export interface UpdateProjectAchievementsPayload {
  achievements: ProjectAchievement[];
  note: string | null;
  rowVersion: string;
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
