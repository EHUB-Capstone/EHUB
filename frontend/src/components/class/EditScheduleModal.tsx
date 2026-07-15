// @ts-nocheck
import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { X, Loader2 } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { userApi } from '../../api/userApi';
import { SLOT_OPTIONS, TEACHING_DAYS } from '../../constants/classSchedule';

export default function EditScheduleModal({ classId, currentLecture, currentSchedule, onClose, onAssigned }) {
  const [form, setForm] = useState({
    lectureId: currentLecture?._id || '',
    dayOfWeek: currentSchedule?.dayOfWeek || '',
    slot:      currentSchedule?.slot ? String(currentSchedule.slot) : '',
    room:      currentSchedule?.room || 'TBD',
  });
  const [submitting, setSubmitting] = useState(false);
  const [lecturers, setLecturers]   = useState(currentLecture ? [currentLecture] : []);
  const [loadingUsers, setLoadingUsers] = useState(true);

  useEffect(() => {
    const fetchUsers = async () => {
      try {
        const res = await userApi.getAll({ role: 'LECTURER', limit: 200 });
        const list = res?.data?.users || res?.users || [];
        setLecturers(list);
      } catch {
        toast.error('Failed to load lecturers');
      } finally {
        setLoadingUsers(false);
      }
    };
    fetchUsers();
  }, []);

  const handleChange = (k, v) => setForm(f => ({ ...f, [k]: v }));

  const handleSubmit = async () => {
    if (!form.dayOfWeek || !form.slot) {
      toast.error('Please select both day and slot');
      return;
    }
    
    setSubmitting(true);
    try {
      await classApi.updateTeachingAssignment(classId, {
        lectureId: form.lectureId || undefined,
        schedule: {
          dayOfWeek: form.dayOfWeek,
          slot: parseInt(form.slot, 10),
          room: form.room || 'TBD'
        }
      });
      toast.success('Schedule updated successfully');
      onAssigned();
    } catch (e) {
      toast.error(e?.response?.data?.message || e?.message || 'Failed to update schedule');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-float w-full max-w-md animate-scale-in">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-slate-100">
          <div>
            <h2 className="text-xl font-bold text-slate-900">Edit Schedule</h2>
            <p className="text-sm text-slate-400 mt-0.5">Assign lecturer and time slot</p>
          </div>
          <button onClick={onClose} className="p-2 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-all">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="p-6 space-y-4">
          {loadingUsers ? (
            <div className="flex justify-center py-4"><Loader2 className="w-5 h-5 animate-spin text-primary" /></div>
          ) : (
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Lecturer</label>
              <select
                value={form.lectureId}
                onChange={(e) => handleChange('lectureId', e.target.value)}
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
              >
                <option value="">— Unassigned —</option>
                {lecturers.map(l => <option key={l._id} value={l._id}>{l.name} ({l.email})</option>)}
              </select>
            </div>
          )}

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Day of Week</label>
              <select
                value={form.dayOfWeek}
                onChange={(e) => handleChange('dayOfWeek', e.target.value)}
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
              >
                <option value="">— Select Day —</option>
                {TEACHING_DAYS.map(d => <option key={d} value={d}>{d}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Slot</label>
              <select
                value={form.slot}
                onChange={(e) => handleChange('slot', e.target.value)}
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
              >
                <option value="">— Select Slot —</option>
                {SLOT_OPTIONS.map(s => <option key={s.val} value={s.val}>{s.label}</option>)}
              </select>
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Room</label>
            <input
              type="text"
              value={form.room}
              onChange={(e) => handleChange('room', e.target.value)}
              placeholder="e.g. B101"
              className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>
        </div>

        {/* Footer */}
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
