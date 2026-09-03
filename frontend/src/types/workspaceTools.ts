export interface ApiEnvelope<T> {
  success: boolean;
  message: string;
  code?: string | null;
  data: T;
  errors?: Array<{ field: string; message: string }> | null;
}

export type WeeklyTaskKind = 'COURSE_TEMPLATE' | 'CLASS_TASK' | 'TEAM_TASK';
export type WeeklyTaskStatus = 'TODO' | 'IN_PROGRESS' | 'REVIEW' | 'COMPLETED' | 'CANCELLED' | 'OVERDUE';
export type WeeklyTaskPriority = 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL';

export interface WeeklyTaskChecklistItem {
  text: string;
  isCompleted: boolean;
}

export interface WeeklyTaskAttachment {
  name: string;
  url: string;
}

export interface WeeklyTaskAssignee {
  _id: string;
  fullName: string;
  rollNumber: string;
}

export interface WeeklyTask {
  _id: string;
  title: string;
  description: string;
  taskType: WeeklyTaskKind;
  scope: 'COURSE' | 'CLASS' | 'TEAM';
  weekNumber: number;
  courseCode: string;
  classId?: string | null;
  teamId?: string | null;
  assigneeStudentId?: WeeklyTaskAssignee | string | null;
  status: WeeklyTaskStatus;
  priority: WeeklyTaskPriority;
  startDate?: string | null;
  dueDate?: string | null;
  attachments: WeeklyTaskAttachment[];
  checklist: WeeklyTaskChecklistItem[];
  tags: string[];
  isTemplate: boolean;
  isMandatory: boolean;
  visibleToStudents: boolean;
  completionPercentage: number;
  estimatedHours?: number | null;
  createdBy: { _id: string; name: string; avatar?: string | null };
  createdAt: string;
  updatedAt?: string | null;
}

export interface WeeklyTaskBoard {
  courseTasks: WeeklyTask[];
  classTasks: WeeklyTask[];
  teamTasks: WeeklyTask[];
}

export interface WeeklyTaskQuery {
  courseCode?: string;
  weekNumber?: number;
  classId?: string;
  teamId?: string;
  status?: WeeklyTaskStatus;
  assigneeStudentId?: string;
  priority?: WeeklyTaskPriority;
  search?: string;
}

export type SaveWeeklyTaskPayload = Partial<WeeklyTask> & Pick<WeeklyTask, 'title' | 'taskType' | 'weekNumber' | 'courseCode'>;

export interface ProjectShortcut {
  _id: string;
  teamId: string;
  projectId: string;
  name: string;
  url: string;
  description?: string | null;
  shortcutType: 'DOCUMENT' | 'DESIGN' | 'REPOSITORY' | 'DEMO' | 'VIDEO' | 'RESEARCH' | 'OTHER';
  createdBy: { _id: string; name: string; avatar?: string | null };
  createdAt: string;
  updatedAt?: string | null;
}

export interface SaveShortcutPayload {
  name: string;
  url: string;
  description?: string;
  shortcutType?: ProjectShortcut['shortcutType'];
}
