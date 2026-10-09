import type { ClassCheckpointSchedule } from '../types/lecturerCheckpoints';

export const formatLecturerCheckpointName = (checkpointNumber: number): string =>
  `Checkpoint ${checkpointNumber}`;

export interface CheckpointScheduleGroup {
  checkpointId: string;
  checkpointNumber: number;
  checkpointTitle: string;
  schedules: ClassCheckpointSchedule[];
  uniform: boolean;
  allClosed: boolean;
  status: ClassCheckpointSchedule['status'] | 'Mixed';
  startDateUtc: string | null;
  endDateUtc: string | null;
}

export function groupCheckpointSchedules(schedules: ClassCheckpointSchedule[]): CheckpointScheduleGroup[] {
  const byNumber = new Map<number, ClassCheckpointSchedule[]>();
  for (const schedule of schedules) {
    const group = byNumber.get(schedule.checkpointNumber) || [];
    group.push(schedule);
    byNumber.set(schedule.checkpointNumber, group);
  }

  return Array.from(byNumber.values()).map((items): CheckpointScheduleGroup => {
    const first = items[0];
    const titles = new Set(items.map((item) => item.checkpointTitle));
    const uniform = items.every((item) =>
      (item.startDateUtc || null) === (first.startDateUtc || null) &&
      (item.endDateUtc || null) === (first.endDateUtc || null));
    return {
      checkpointId: first.checkpointId,
      checkpointNumber: first.checkpointNumber,
      checkpointTitle: titles.size === 1 ? first.checkpointTitle : 'Multiple subject definitions',
      schedules: items,
      uniform,
      allClosed: items.every((item) => item.status === 'Closed'),
      status: uniform ? first.status : 'Mixed',
      startDateUtc: uniform ? first.startDateUtc || null : null,
      endDateUtc: uniform ? first.endDateUtc || null : null,
    };
  }).sort((left, right) => left.checkpointNumber - right.checkpointNumber ||
    left.checkpointTitle.localeCompare(right.checkpointTitle));
}
