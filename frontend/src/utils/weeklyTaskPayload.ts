import type { SaveWeeklyTaskPayload, WeeklyTask } from '../types/workspaceTools';

// Editing task details must not reset fields managed outside the details form.
export function weeklyTaskSavePayload(task: WeeklyTask | null, payload: SaveWeeklyTaskPayload): SaveWeeklyTaskPayload {
  return {
    ...payload,
    status: task?.status ?? payload.status ?? 'TODO',
    attachments: payload.attachments ?? task?.attachments ?? [],
    visibleToStudents: payload.visibleToStudents ?? task?.visibleToStudents ?? true,
  };
}
