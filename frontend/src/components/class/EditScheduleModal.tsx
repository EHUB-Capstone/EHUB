import { useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { X, Loader2, Plus, Trash2 } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { DAY_OF_WEEK_OPTIONS, SLOT_OPTIONS } from '../../constants/classSchedule';
import { parseApiError } from '../../utils/apiError';
import type { ClassScheduleSlot } from '../../types/classes';
import { buildScheduleUpdatePayload, type EditableSchedule } from '../../utils/classComponentPolicy';

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
      toast.error('A class can contain at most 12 weekly sessions.');
      return;
    }
    setSchedules(current => [...current, emptySchedule()]);
  };

  const removeSchedule = (index: number) => {
    setSchedules(current => current.filter((_, itemIndex) => itemIndex !== index));
  };

  const handleSubmit = async () => {
    if (schedules.some(schedule => schedule.dayOfWeek === '' || schedule.slotNumber === '')) {
      toast.error('Please select both day and slot for every session.');
      return;
    }

    const scheduleKeys = schedules.map(schedule => `${schedule.dayOfWeek}-${schedule.slotNumber}`);
    if (new Set(scheduleKeys).size !== scheduleKeys.length) {
      toast.error('The same day and slot cannot be added twice.');
      return;
    }

    if (!rowVersion) {
      toast.error('Class data is stale. Reload the page and try again.');
      return;
    }

    setSubmitting(true);
    try {
      await classApi.updateSchedule(classId, buildScheduleUpdatePayload(schedules, rowVersion));
      toast.success('Schedule updated successfully');
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
            <h2 className="text-lg font-bold text-slate-900">Edit weekly schedule</h2>
            <p className="mt-0.5 text-xs text-slate-400">Add up to 12 sessions. Updating schedule never changes the assigned lecturer.</p>
          </div>
          <button type="button" onClick={onClose} className="rounded-lg p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-600">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-5">
          {schedules.length === 0 && (
            <div className="rounded-xl border border-dashed border-amber-200 bg-amber-50 p-4 text-sm text-amber-700">Saving an empty schedule moves the class back to Draft.</div>
          )}
          {schedules.map((schedule, index) => (
            <div key={index} className="grid grid-cols-1 gap-3 rounded-xl border border-slate-200 bg-slate-50/50 p-3 sm:grid-cols-[1fr_1fr_1fr_auto] sm:items-end">
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-600">Day</label>
                <select value={schedule.dayOfWeek} onChange={event => updateSchedule(index, 'dayOfWeek', event.target.value)} className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20">
                  <option value="">Select day</option>
                  {DAY_OF_WEEK_OPTIONS.map(day => <option key={day.value} value={day.value}>{day.label}</option>)}
                </select>
              </div>
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-600">Slot</label>
                <select value={schedule.slotNumber} onChange={event => updateSchedule(index, 'slotNumber', event.target.value)} className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20">
                  <option value="">Select slot</option>
                  {SLOT_OPTIONS.map(slot => <option key={slot.val} value={slot.val}>{slot.label}</option>)}
                </select>
              </div>
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-600">Room</label>
                <input maxLength={50} value={schedule.room} onChange={event => updateSchedule(index, 'room', event.target.value)} placeholder="e.g. P.301" className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" />
              </div>
              <button type="button" onClick={() => removeSchedule(index)} aria-label={`Remove session ${index + 1}`} className="inline-flex h-9 w-9 items-center justify-center rounded-lg border border-red-200 bg-white text-red-500 hover:bg-red-50">
                <Trash2 className="h-4 w-4" />
              </button>
            </div>
          ))}

          <button type="button" onClick={addSchedule} className="inline-flex items-center gap-1.5 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2 text-xs font-semibold text-primary hover:bg-primary-100">
            <Plus className="h-3.5 w-3.5" /> Add session
          </button>
        </div>

        <div className="flex gap-2 border-t border-slate-100 p-5">
          <button type="button" onClick={onClose} className="flex-1 rounded-lg border border-slate-200 px-4 py-2 text-sm text-slate-600 hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void handleSubmit()} disabled={submitting} className="flex flex-1 items-center justify-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50">
            {submitting ? <><Loader2 className="h-4 w-4 animate-spin" /> Saving...</> : 'Save schedule'}
          </button>
        </div>
      </div>
    </div>
  );
}
