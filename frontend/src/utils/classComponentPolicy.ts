import type { ClassScheduleSlot, ClassStatus } from '../types/classes';

export type EditableSchedule = {
  dayOfWeek: number | '';
  slotNumber: number | '';
  room: string;
};

export const isClassReadOnly = (status: ClassStatus | string | undefined): boolean => status === 'Archived';

export const getClassLifecyclePresentation = (status: ClassStatus | string | undefined) => (
  isClassReadOnly(status)
    ? { action: 'restore' as const, label: 'Restore Class', confirmLabel: 'Restore class' }
    : { action: 'archive' as const, label: 'Archive Class', confirmLabel: 'Archive class' }
);

export const buildScheduleUpdatePayload = (schedules: EditableSchedule[], rowVersion: string) => ({
  schedules: schedules.map(schedule => ({
    dayOfWeek: Number(schedule.dayOfWeek),
    slotNumber: Number(schedule.slotNumber),
    room: schedule.room.trim() || null,
  } satisfies ClassScheduleSlot)),
  rowVersion,
});

export const validateImportFileSelection = (file: { name: string; size: number }): string => {
  const extension = file.name.split('.').pop()?.toLowerCase();
  if (extension !== 'xlsx' && extension !== 'xls')
    return 'Unsupported file type. Please choose an .xlsx or .xls file.';
  if (file.size > 10 * 1024 * 1024)
    return 'The file is larger than 10 MB. Please upload a smaller student list.';
  return '';
};
