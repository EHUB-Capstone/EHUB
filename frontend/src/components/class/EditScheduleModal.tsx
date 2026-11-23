import { useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { X, Loader2, Plus, Trash2 } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { DAY_OF_WEEK_OPTIONS, SLOT_OPTIONS } from '../../constants/classSchedule';
import { parseApiError } from '../../utils/apiError';
import type { ClassScheduleSlot } from '../../types/classes';
import { buildScheduleUpdatePayload, type EditableSchedule } from '../../utils/classComponentPolicy';
import { useTranslation } from 'react-i18next';

interface EditScheduleModalProps {
  classId: string;
  currentSchedule?: ClassScheduleSlot[] | ClassScheduleSlot | null;
  rowVersion?: string;
  onClose: () => void;
  onUpdated: () => Promise<void> | void;
}

const emptySchedule = (): EditableSchedule => ({ dayOfWeek: '', slotNumber: '', room: '' });

const normalizeSchedules = (currentSchedule?: ClassScheduleSlot[] | ClassScheduleSlot | null): EditableSchedule[] => {
  const values = Array.isArray(currentSchedule)
    ? currentSchedule
    : currentSchedule ? [currentSchedule] : [];

  const normalized = values.map((value) => {
    const rawDay = value.dayOfWeek ?? (value as unknown as { DayOfWeek?: number }).DayOfWeek;
    const option = DAY_OF_WEEK_OPTIONS.find(day => day.value === Number(rawDay));
    return {
      dayOfWeek: option?.value ?? '',
      slotNumber: value.slotNumber ?? (value as unknown as { SlotNumber?: number }).SlotNumber ?? '',
      room: value.room ?? (value as unknown as { Room?: string }).Room ?? '',
    } satisfies EditableSchedule;
  });

  return normalized.length > 0 ? normalized : [emptySchedule()];
};

export default function EditScheduleModal({
  classId,
  currentSchedule,
  rowVersion,
  onClose,
  onUpdated,
}: EditScheduleModalProps) {
  const { t } = useTranslation('class');
  const initialSchedules = useMemo(() => normalizeSchedules(currentSchedule), [currentSchedule]);
  const [schedules, setSchedules] = useState<EditableSchedule[]>(initialSchedules);
  const [submitting, setSubmitting] = useState(false);

  const updateSchedule = (index: number, field: keyof EditableSchedule, value: string) => {
    setSchedules(current => current.map((schedule, itemIndex) => itemIndex === index
      ? {
          ...schedule,
          [field]: field === 'room' ? value : (value === '' ? '' : Number(value)),
        }
      : schedule));
  };

  const addSchedule = () => {
    if (schedules.length >= 12) {
      toast.error(t('editScheduleModal.aClassCanContainAtMost'));
      return;
    }
    setSchedules(current => [...current, emptySchedule()]);
  };

  const removeSchedule = (index: number) => {
    setSchedules(current => current.filter((_, itemIndex) => itemIndex !== index));
  };

  const handleSubmit = async () => {
    if (schedules.some(schedule => schedule.dayOfWeek === '' || schedule.slotNumber === '')) {
      toast.error(t('editScheduleModal.pleaseSelectBothDayAndSlot'));
      return;
    }

    const scheduleKeys = schedules.map(schedule => `${schedule.dayOfWeek}-${schedule.slotNumber}`);
    if (new Set(scheduleKeys).size !== scheduleKeys.length) {
      toast.error(t('editScheduleModal.theSameDayAndSlotCannot'));
      return;
    }

    if (!rowVersion) {
      toast.error(t('assignLectureModal.classDataIsStaleReloadThe'));
      return;
    }

    setSubmitting(true);
    try {
      await classApi.updateSchedule(classId, buildScheduleUpdatePayload(schedules, rowVersion));
      toast.success(t('editScheduleModal.scheduleUpdatedSuccessfully'));
      await onUpdated();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to update schedule').message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative flex max-h-[92vh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl bg-white shadow-float animate-scale-in">
        <div className="flex items-center justify-between border-b border-slate-100 p-5">
          <div>
            <h2 className="text-lg font-bold text-slate-900">{t('editScheduleModal.editWeeklySchedule')}</h2>
            <p className="mt-0.5 text-xs text-slate-400">{t('editScheduleModal.addUpTo12SessionsUpdating')}</p>
          </div>
          <button type="button" onClick={onClose} className="rounded-lg p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-600">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-5">
          {schedules.length === 0 && (
            <div className="rounded-xl border border-dashed border-amber-200 bg-amber-50 p-4 text-sm text-amber-700">{t('editScheduleModal.savingAnEmptyScheduleMovesThe')}</div>
          )}
          {schedules.map((schedule, index) => (
            <div key={index} className="grid grid-cols-1 gap-3 rounded-xl border border-slate-200 bg-slate-50/50 p-3 sm:grid-cols-[1fr_1fr_1fr_auto] sm:items-end">
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-600">{t('editScheduleModal.day')}</label>
                <select value={schedule.dayOfWeek} onChange={event => updateSchedule(index, 'dayOfWeek', event.target.value)} className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20">
                  <option value="">{t('editScheduleModal.selectDay')}</option>
                  {DAY_OF_WEEK_OPTIONS.map(day => <option key={day.value} value={day.value}>{day.label}</option>)}
                </select>
              </div>
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-600">{t('editScheduleModal.slot')}</label>
                <select value={schedule.slotNumber} onChange={event => updateSchedule(index, 'slotNumber', event.target.value)} className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20">
                  <option value="">{t('editScheduleModal.selectSlot')}</option>
                  {SLOT_OPTIONS.map(slot => <option key={slot.val} value={slot.val}>{slot.label}</option>)}
                </select>
              </div>
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-600">{t('editScheduleModal.room')}</label>
                <input maxLength={50} value={schedule.room} onChange={event => updateSchedule(index, 'room', event.target.value)} placeholder={t('editScheduleModal.eGP301')} className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" />
              </div>
              <button type="button" onClick={() => removeSchedule(index)} aria-label={t('editScheduleModal.removeSession', { v1: index + 1 })} className="inline-flex h-9 w-9 items-center justify-center rounded-lg border border-red-200 bg-white text-red-500 hover:bg-red-50">
                <Trash2 className="h-4 w-4" />
              </button>
            </div>
          ))}

          <button type="button" onClick={addSchedule} className="inline-flex items-center gap-1.5 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2 text-xs font-semibold text-primary-700 hover:bg-primary-100">
            <Plus className="h-3.5 w-3.5" /> {t('editScheduleModal.addSession')}
          </button>
        </div>

        <div className="flex gap-2 border-t border-slate-100 p-5">
          <button type="button" onClick={onClose} className="flex-1 rounded-lg border border-slate-200 px-4 py-2 text-sm text-slate-600 hover:bg-slate-50">{t('common:actions.cancel')}</button>
          <button type="button" onClick={() => void handleSubmit()} disabled={submitting} className="flex flex-1 items-center justify-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50">
            {submitting ? <><Loader2 className="h-4 w-4 animate-spin" /> {t('assignLectureModal.saving')}</> : t('editScheduleModal.saveSchedule')}
          </button>
        </div>
      </div>
    </div>
  );
}
