export const PROJECT_ACHIEVEMENTS = ['Potential', 'Funded', 'Awarded'] as const;
export type ProjectAchievement = (typeof PROJECT_ACHIEVEMENTS)[number];

export const PROJECT_DATA_PAGE_SIZES = [10, 20, 50, 100] as const;
export type ProjectDataPageSize = (typeof PROJECT_DATA_PAGE_SIZES)[number];

export const PROJECT_DATA_SORT_FIELDS = ['projectName', 'semester', 'subject'] as const;
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
  groups: string[];
  projectName: string;
  description: string | null;
  startupIndustries: string[];
  lecturer: ProjectDataPerson | null;
  mentor: ProjectDataMentor | null;
  academicMentor: ProjectDataMentor | null;
  achievements: ProjectAchievement[];
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
  semesterId: string;
  subjectCode: string;
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

export interface ProjectDataSemesterOption {
  id: string;
  code: string;
  year: number;
  isActive: boolean;
}

export interface ProjectDataSubjectOption {
  code: string;
  name: string;
}

export interface ProjectDataMentorOption extends ProjectDataPerson {
  slot: ProjectDataMentorSlot;
}

export interface ProjectDataFilterOptions {
  semesters: ProjectDataSemesterOption[];
  subjects: ProjectDataSubjectOption[];
  groups: string[];
  startupIndustries: string[];
  lecturers: ProjectDataPerson[];
  mentors: ProjectDataMentorOption[];
  achievements: ProjectAchievement[];
}

export interface UpdateProjectAchievementsPayload {
  achievements: ProjectAchievement[];
  rowVersion: string;
}

export interface ProjectAchievementsResult {
  projectId: string;
  achievements: ProjectAchievement[];
  rowVersion: string;
}
