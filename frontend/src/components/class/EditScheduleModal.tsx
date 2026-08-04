// @ts-nocheck
import { useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { X, Loader2 } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { DAY_OF_WEEK_OPTIONS, SLOT_OPTIONS } from '../../constants/classSchedule';

const normalizeSchedule = (currentSchedule) => {
  const value = Array.isArray(currentSchedule) ? currentSchedule[0] : currentSchedule;
  if (!value) return null;

  const rawDay = value.dayOfWeek ?? value.DayOfWeek;
  const option = DAY_OF_WEEK_OPTIONS.find(day =>
    day.value === Number(rawDay) || day.key === String(rawDay).toUpperCase());

  return {
    dayOfWeek: option?.value ?? '',
    slotNumber: value.slotNumber ?? value.SlotNumber ?? value.slot ?? '',
    room: value.room ?? value.Room ?? '',
  };
};

export default function EditScheduleModal({ classId, currentSchedule, rowVersion, onClose, onUpdated }) {
  const initialSchedule = useMemo(() => normalizeSchedule(currentSchedule), [currentSchedule]);
  const [form, setForm] = useState(initialSchedule || {
    dayOfWeek: '',
    slotNumber: '',
    room: '',
  });
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async () => {
    if (!form.dayOfWeek || !form.slotNumber) {
      toast.error('Please select both day and slot');
      return;
    }

    if (!rowVersion) {
      toast.error('Class data is stale. Reload the page and try again.');
      return;
    }

    setSubmitting(true);
    try {
      await classApi.updateSchedule(classId, {
        schedules: [{
          dayOfWeek: Number(form.dayOfWeek),
          slotNumber: Number(form.slotNumber),
          room: form.room.trim() || null,
        }],
        rowVersion,
      });
      toast.success('Schedule updated successfully');
      await onUpdated();
    } catch (error) {
      toast.error(error?.response?.data?.message || error?.message || 'Failed to update schedule');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-float w-full max-w-md animate-scale-in">
        <div className="flex items-center justify-between p-6 border-b border-slate-100">
          <div>
            <h2 className="text-xl font-bold text-slate-900">Edit Schedule</h2>
            <p className="text-sm text-slate-400 mt-0.5">Update the class time slot without changing its lecturer</p>
          </div>
          <button onClick={onClose} className="p-2 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-all">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-6 space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Day of Week</label>
              <select
                value={form.dayOfWeek}
                onChange={(event) => setForm(current => ({ ...current, dayOfWeek: event.target.value }))}
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
              >
                <option value="">— Select Day —</option>
                {DAY_OF_WEEK_OPTIONS.map(day => <option key={day.value} value={day.value}>{day.label}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Slot</label>
              <select
                value={form.slotNumber}
                onChange={(event) => setForm(current => ({ ...current, slotNumber: event.target.value }))}
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
              >
                <option value="">— Select Slot —</option>
                {SLOT_OPTIONS.map(slot => <option key={slot.val} value={slot.val}>{slot.label}</option>)}
              </select>
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Room</label>
            <input
              type="text"
              maxLength={50}
              value={form.room}
              onChange={(event) => setForm(current => ({ ...current, room: event.target.value }))}
              placeholder="e.g. B101"
              className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>
        </div>

        <div className="flex gap-3 p-6 pt-0">
          <button onClick={onClose} className="flex-1 px-4 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-600 hover:bg-slate-50 transition-all">
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={submitting}
            className="flex-1 px-4 py-2.5 bg-primary text-white rounded-xl text-sm font-medium hover:bg-primary-700 disabled:opacity-50 transition-all flex items-center justify-center gap-2"
          >
            {submitting ? <><Loader2 className="w-4 h-4 animate-spin" /> Saving...</> : 'Save Schedule'}
          </button>
        </div>
      </div>
    </div>
  );
}
