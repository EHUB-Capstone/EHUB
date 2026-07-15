// @ts-nocheck
import { useEffect, useMemo, useRef, useState } from 'react';
import { Loader2, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { PRIORITY_CFG, WEEKS } from '../constants';

const blankForm = {
  title: '',
  description: '',
  priority: 'MEDIUM',
  weekNumber: 1,
  startDate: '',
  dueDate: '',
  assigneeStudentId: '',
  estimatedHours: '',
  tags: '',
  checklist: [],
};

const memberId = (member) => member._id || member.studentId?._id || member.userId?._id;
const memberName = (member) => member.fullName || member.studentId?.fullName || member.userId?.name || 'Member';
const todayString = () => new Date().toISOString().split('T')[0];

function toForm(task) {
  if (!task) return blankForm;
  return {
    title: task.title || '',
    description: task.description || '',
    priority: task.priority || 'MEDIUM',
    weekNumber: task.weekNumber || 1,
    startDate: task.startDate ? task.startDate.slice(0, 10) : '',
    dueDate: task.dueDate ? task.dueDate.slice(0, 10) : '',
    assigneeStudentId: task.assigneeStudentId?._id || task.assigneeStudentId || '',
    estimatedHours: task.estimatedHours || '',
    tags: (task.tags || []).join(', '),
    checklist: task.checklist || [],
  };
}

export default function TaskModal({ isOpen, onClose, onSave, task, teamMembers, loading }) {
  const [form, setForm] = useState(blankForm);
  const [newCheckItem, setNewCheckItem] = useState('');
  const titleRef = useRef(null);
  const isEdit = Boolean(task);
  const minDate = useMemo(() => todayString(), []);

  useEffect(() => {
    if (!isOpen) return;
    // Form state intentionally resets whenever a new add/edit dialog opens.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setForm(toForm(task));
    setNewCheckItem('');
    window.setTimeout(() => titleRef.current?.focus(), 0);
  }, [task, isOpen]);

  useEffect(() => {
    const handleKeyDown = (event) => {
      if (event.key === 'Escape' && isOpen && !loading) onClose();
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, loading, onClose]);

  if (!isOpen) return null;

  const setField = (key, value) => setForm((current) => ({ ...current, [key]: value }));

  const addCheckItem = () => {
    const text = newCheckItem.trim();
    if (!text) return;
    setField('checklist', [...form.checklist, { text, isCompleted: false }]);
    setNewCheckItem('');
  };

  const removeCheckItem = (index) => {
    setField('checklist', form.checklist.filter((_, itemIndex) => itemIndex !== index));
  };

  const toggleCheckItem = (index) => {
    setField('checklist', form.checklist.map((item, itemIndex) => (
      itemIndex === index ? { ...item, isCompleted: !item.isCompleted } : item
    )));
  };

  const handleSubmit = (event) => {
    event.preventDefault();

    if (!form.title.trim()) {
      toast.error('Title is required');
      return;
    }

    if (form.startDate && form.startDate < minDate) {
      toast.error('Start date cannot be in the past');
      return;
    }
    if (form.dueDate && form.dueDate < minDate) {
      toast.error('Due date cannot be in the past');
      return;
    }
    if (form.startDate && form.dueDate && form.dueDate < form.startDate) {
      toast.error('Due date must be on or after start date');
      return;
    }

    onSave({
      ...form,
      taskType: 'TEAM_TASK',
      title: form.title.trim(),
      description: form.description.trim(),
      tags: form.tags.split(',').map((tag) => tag.trim()).filter(Boolean),
      estimatedHours: Number(form.estimatedHours) || 0,
      assigneeStudentId: form.assigneeStudentId || undefined,
      startDate: form.startDate || undefined,
      dueDate: form.dueDate || undefined,
    });
  };

  const inputClass = 'w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-800 outline-none transition focus:border-blue-400 focus:ring-2 focus:ring-blue-500/20';
  const labelClass = 'mb-1 block text-xs font-semibold text-slate-600';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-labelledby="task-modal-title">
      <div className="absolute inset-0 bg-slate-950/35" onClick={loading ? undefined : onClose} />
      <div className="relative flex max-h-[90vh] w-full max-w-2xl flex-col rounded-xl border border-slate-200 bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-slate-100 px-5 py-4">
          <h2 id="task-modal-title" className="text-base font-semibold text-slate-950">
            {isEdit ? 'Edit Team Task' : 'New Team Task'}
          </h2>
          <button
            type="button"
            onClick={onClose}
            disabled={loading}
            className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700 disabled:opacity-50"
            aria-label="Close task modal"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="flex-1 space-y-4 overflow-y-auto px-5 py-4">
          <div>
            <label className={labelClass}>Title *</label>
            <input ref={titleRef} className={inputClass} value={form.title} onChange={(event) => setField('title', event.target.value)} required />
          </div>

          <div>
            <label className={labelClass}>Description</label>
            <textarea className={`${inputClass} resize-none`} rows={3} value={form.description} onChange={(event) => setField('description', event.target.value)} />
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div>
              <label className={labelClass}>Week *</label>
              <select className={inputClass} value={form.weekNumber} onChange={(event) => setField('weekNumber', Number(event.target.value))}>
                {WEEKS.map((week) => <option key={week} value={week}>Week {week}</option>)}
              </select>
            </div>
            <div>
              <label className={labelClass}>Priority</label>
              <select className={inputClass} value={form.priority} onChange={(event) => setField('priority', event.target.value)}>
                {Object.entries(PRIORITY_CFG).map(([priority, cfg]) => <option key={priority} value={priority}>{cfg.label}</option>)}
              </select>
            </div>
            <div>
              <label className={labelClass}>Est. Hours</label>
              <input type="number" min="0" step="0.5" className={inputClass} value={form.estimatedHours} onChange={(event) => setField('estimatedHours', event.target.value)} />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div>
              <label className={labelClass}>Assignee</label>
              <select className={inputClass} value={form.assigneeStudentId} onChange={(event) => setField('assigneeStudentId', event.target.value)}>
                <option value="">Unassigned</option>
                {teamMembers.map((member) => <option key={memberId(member)} value={memberId(member)}>{memberName(member)}</option>)}
              </select>
            </div>
            <div>
              <label className={labelClass}>Start Date</label>
              <input type="date" min={minDate} className={inputClass} value={form.startDate} onChange={(event) => setField('startDate', event.target.value)} />
            </div>
            <div>
              <label className={labelClass}>Due Date</label>
              <input type="date" min={form.startDate || minDate} className={inputClass} value={form.dueDate} onChange={(event) => setField('dueDate', event.target.value)} />
            </div>
          </div>

          <div>
            <label className={labelClass}>Tags</label>
            <input className={inputClass} placeholder="pitch, customer, prototype" value={form.tags} onChange={(event) => setField('tags', event.target.value)} />
          </div>

          <div>
            <label className={labelClass}>Checklist</label>
            <div className="mb-2 space-y-2">
              {form.checklist.map((item, index) => (
                <div key={`${item.text}-${index}`} className="flex items-center gap-2 rounded-lg border border-slate-100 bg-slate-50 px-2 py-1.5">
                  <input type="checkbox" checked={item.isCompleted} onChange={() => toggleCheckItem(index)} className="h-3.5 w-3.5 rounded border-slate-300 text-blue-600" />
                  <span className={`flex-1 text-sm ${item.isCompleted ? 'text-slate-400 line-through' : 'text-slate-700'}`}>{item.text}</span>
                  <button type="button" onClick={() => removeCheckItem(index)} className="rounded px-1 text-xs text-slate-400 hover:bg-red-50 hover:text-red-600" aria-label={`Remove checklist item ${item.text}`}>
                    Remove
                  </button>
                </div>
              ))}
            </div>
            <div className="flex gap-2">
              <input
                className={`${inputClass} flex-1`}
                placeholder="Add checklist item"
                value={newCheckItem}
                onChange={(event) => setNewCheckItem(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    event.preventDefault();
                    addCheckItem();
                  }
                }}
              />
              <button type="button" onClick={addCheckItem} className="rounded-lg bg-slate-100 px-3 text-xs font-semibold text-slate-600 transition-colors hover:bg-slate-200">
                Add
              </button>
            </div>
          </div>
        </form>

        <div className="flex items-center justify-end gap-3 border-t border-slate-100 px-5 py-4">
          <button type="button" onClick={onClose} disabled={loading} className="rounded-lg px-3 py-2 text-sm font-semibold text-slate-600 transition-colors hover:bg-slate-100 disabled:opacity-50">
            Cancel
          </button>
          <button type="button" onClick={handleSubmit} disabled={loading} className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-blue-700 disabled:opacity-60">
            {loading && <Loader2 className="h-4 w-4 animate-spin" />}
            {isEdit ? 'Save Changes' : 'Create Task'}
          </button>
        </div>
      </div>
    </div>
  );
}
