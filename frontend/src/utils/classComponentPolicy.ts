import type { ClassScheduleSlot, ClassStatus } from '../types/classes';
import i18n from '../i18n/index.ts';

export type EditableSchedule = {
  dayOfWeek: number | '';
  slotNumber: number | '';
  room: string;
};

// Five complete rows in the desktop three-column class grid.
export const CLASS_LIST_PAGE_SIZE = 15;

export const isClassReadOnly = (status: ClassStatus | string | undefined): boolean =>
  status === 'Completed' || status === 'Archived';

export const isArchivedClass = (status: ClassStatus | string | undefined): boolean => status === 'Archived';

export const getClassLifecyclePresentation = (status: ClassStatus | string | undefined) => (
  isArchivedClass(status)
    ? { action: 'restore' as const, label: i18n.t('utils:classComponentPolicy.restoreClass'), confirmLabel: 'Restore class' }
    : { action: 'archive' as const, label: i18n.t('utils:classComponentPolicy.archiveClass'), confirmLabel: 'Archive class' }
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
    return i18n.t('utils:classComponentPolicy.unsupportedFileTypePleaseChooseAn');
  if (file.size > 10 * 1024 * 1024)
    return i18n.t('utils:classComponentPolicy.theFileIsLargerThan10');
  return '';
};
