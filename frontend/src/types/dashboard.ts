export interface AcademicOverviewScope {
  semesterId: string;
  semesterCode: string;
  semesterName: string;
  courseId: string | null;
  subjectCode: string | null;
  subjectName: string | null;
  classId: string | null;
  classCode: string | null;
}

export interface AcademicOverviewFilters {
  semesterId?: string;
  courseId?: string;
  classId?: string;
}

export interface AcademicOverviewSemesterOption {
  id: string;
  code: string;
  name: string;
  year: number;
  isActive: boolean;
}

export interface AcademicOverviewSubjectOption {
  id: string;
  code: string;
  name: string;
}

export interface AcademicOverviewClassOption {
  id: string;
  code: string;
  courseId: string;
}

export interface AcademicOverviewFilterOptions {
  semesters: AcademicOverviewSemesterOption[];
  subjects: AcademicOverviewSubjectOption[];
  classes: AcademicOverviewClassOption[];
}

export interface AcademicOverviewMetrics {
  totalClasses: number;
  totalTeams: number;
  totalProjects: number;
  totalSubmissions: number;
  totalEvaluations: number;
  totalPotentialProjects: number;
}

export interface AcademicOverviewResponse {
  scope: AcademicOverviewScope;
  filterOptions: AcademicOverviewFilterOptions;
  metrics: AcademicOverviewMetrics;
  attention: AcademicOverviewAttention;
  activityTrend: AcademicOverviewActivity[];
  checkpointProgress: AcademicOverviewCheckpoint[];
  classes: AcademicOverviewClass[];
  topTeams: AcademicOverviewTopTeam[];
  lastUpdatedAtUtc: string;
  hasAssignedClasses: boolean;
  hasMatchingClasses: boolean;
  hasClasses: boolean;
}

export interface AcademicOverviewAttention {
  missedDeadlines: number;
  pendingEvaluations: number;
}

export interface AcademicOverviewActivity {
  weekStartUtc: string;
  submissions: number;
  evaluations: number;
}

export interface AcademicOverviewCheckpoint {
  checkpointId: string;
  courseCode: string;
  checkpointNumber: number;
  title: string;
  expectedTeams: number;
  submittedTeams: number;
  evaluatedProjects: number;
  missedDeadlineTeams: number;
}

export interface AcademicOverviewClass {
  classId: string;
  classCode: string;
  teams: number;
  projects: number;
  submissions: number;
  evaluations: number;
  potentialProjects: number;
}

export interface AcademicOverviewTopTeam {
  teamId: string;
  teamName: string;
  classCode: string;
  projectName: string;
  courseTotal: number;
  completedComponents: number;
  totalComponents: number;
}
