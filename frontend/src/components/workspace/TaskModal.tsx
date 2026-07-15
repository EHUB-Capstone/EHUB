// @ts-nocheck
import { useState, useEffect } from 'react';
import { X } from 'lucide-react';
import toast from 'react-hot-toast';

const PRIORITY_OPTIONS = ['LOW', 'MEDIUM', 'HIGH', 'URGENT'];
const STATUS_OPTIONS = ['TODO', 'IN_PROGRESS', 'REVIEW', 'DONE'];

const STATUS_LABELS = {
  TODO: 'To Do',
  IN_PROGRESS: 'In Progress',
  REVIEW: 'Review',
  DONE: 'Done',
};

function toDateInputValue(dateStr) {
  if (!dateStr) return '';
  try {
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return '';
    return d.toISOString().split('T')[0];
  } catch {
    return '';
  }
}

const INITIAL_FORM = {
  title: '',
  description: '',
  milestoneId: '',
  assigneeStudentId: '',
  priority: 'MEDIUM',
  status: 'TODO',
  startDate: '',
  dueDate: '',
};

export default function TaskModal({ isOpen, onClose, onSubmit, initialData, milestones, members }) {
  const isEdit = !!initialData;

  const [form, setForm] = useState(INITIAL_FORM);
  const [errors, setErrors] = useState({});
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (isOpen) {
      if (initialData) {
        setForm({
          title: initialData.title || '',
          description: initialData.description || '',
          milestoneId: initialData.milestoneId?._id || initialData.milestoneId || '',
          assigneeStudentId:
            initialData.assigneeStudentId?._id || initialData.assigneeStudentId || '',
          priority: initialData.priority || 'MEDIUM',
          status: initialData.status || 'TODO',
          startDate: toDateInputValue(initialData.startDate),
          dueDate: toDateInputValue(initialData.dueDate),
        });
      } else {
        setForm(INITIAL_FORM);
      }
      setErrors({});
      setSubmitting(false);
    }
  }, [isOpen, initialData]);

  if (!isOpen) return null;

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: null }));
    }
  };

  const validate = () => {
    const newErrors = {};
    if (!form.title.trim()) {
      newErrors.title = 'Title is required';
    }
    if (form.startDate && form.dueDate && form.dueDate < form.startDate) {
      newErrors.dueDate = 'Due date must be on or after start date';
    }
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!validate()) return;

    setSubmitting(true);
    try {
      const payload = { ...form };
      if (!payload.milestoneId) delete payload.milestoneId;
      if (!payload.assigneeStudentId) delete payload.assigneeStudentId;
      if (!payload.startDate) delete payload.startDate;
      if (!payload.dueDate) delete payload.dueDate;

      await onSubmit?.(payload);
      onClose?.();
    } catch (err) {
      toast.error(err?.message || 'Failed to save task');
    } finally {
      setSubmitting(false);
    }
  };

  const safeMilestones = Array.isArray(milestones) ? milestones : [];
  const safeMembers = Array.isArray(members) ? members : [];

  const inputClass =
    'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400 transition';
  const labelClass = 'block text-sm font-medium text-gray-700 mb-1';
  const errorClass = 'text-xs text-red-500 mt-1';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Overlay */}
      <div
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        onClick={onClose}
      />

      {/* Modal */}
      <div className="relative bg-white rounded-xl shadow-2xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <h2 className="text-lg font-semibold text-gray-900">
            {isEdit ? 'Edit Task' : 'Create Task'}
          </h2>
          <button
            onClick={onClose}
            className="p-1 rounded-lg hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
          >
            <X size={20} />
          </button>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="px-6 py-4 space-y-4">
          {/* Title */}
          <div>
            <label htmlFor="task-title" className={labelClass}>
              Title <span className="text-red-500">*</span>
            </label>
            <input
              id="task-title"
              type="text"
              name="title"
              value={form.title}
              onChange={handleChange}
              placeholder="Enter task title"
              className={`${inputClass} ${errors.title ? 'border-red-400 focus:ring-red-400' : ''}`}
            />
            {errors.title && <p className={errorClass}>{errors.title}</p>}
          </div>

          {/* Description */}
          <div>
            <label htmlFor="task-desc" className={labelClass}>
              Description
            </label>
            <textarea
              id="task-desc"
              name="description"
              value={form.description}
              onChange={handleChange}
              rows={3}
              placeholder="Describe the task..."
              className={`${inputClass} resize-none`}
            />
          </div>

          {/* Milestone */}
          <div>
            <label htmlFor="task-milestone" className={labelClass}>
              Milestone
            </label>
            <select
              id="task-milestone"
              name="milestoneId"
              value={form.milestoneId}
              onChange={handleChange}
              className={inputClass}
            >
              <option value="">— No Milestone —</option>
              {safeMilestones.map((m) => (
                <option key={m._id} value={m._id}>
                  {m.title}
                </option>
              ))}
            </select>
          </div>

          {/* Assignee */}
          <div>
            <label htmlFor="task-assignee" className={labelClass}>
              Assignee
            </label>
            <select
              id="task-assignee"
              name="assigneeStudentId"
              value={form.assigneeStudentId}
              onChange={handleChange}
              className={inputClass}
            >
              <option value="">Unassigned</option>
              {safeMembers.map((m) => {
                const student = m?.studentId;
                if (!student?._id) return null;
                return (
                  <option key={student._id} value={student._id}>
                    {student.fullName || 'Unknown'} - {student.rollNumber || ''}
                  </option>
                );
              })}
            </select>
          </div>

          {/* Priority + Status row */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label htmlFor="task-priority" className={labelClass}>
                Priority
              </label>
              <select
                id="task-priority"
                name="priority"
                value={form.priority}
                onChange={handleChange}
                className={inputClass}
              >
                {PRIORITY_OPTIONS.map((p) => (
                  <option key={p} value={p}>
                    {p}
                  </option>
                ))}
              </select>
            </div>

            {isEdit && (
              <div>
                <label htmlFor="task-status" className={labelClass}>
                  Status
                </label>
                <select
                  id="task-status"
                  name="status"
                  value={form.status}
                  onChange={handleChange}
                  className={inputClass}
                >
                  {STATUS_OPTIONS.map((s) => (
                    <option key={s} value={s}>
                      {STATUS_LABELS[s]}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>

          {/* Dates row */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label htmlFor="task-start" className={labelClass}>
                Start Date
              </label>
              <input
                id="task-start"
                type="date"
                name="startDate"
                value={form.startDate}
                onChange={handleChange}
                className={inputClass}
              />
            </div>
            <div>
              <label htmlFor="task-due" className={labelClass}>
                Due Date
              </label>
              <input
                id="task-due"
                type="date"
                name="dueDate"
                value={form.dueDate}
                onChange={handleChange}
                className={`${inputClass} ${errors.dueDate ? 'border-red-400 focus:ring-red-400' : ''}`}
              />
              {errors.dueDate && <p className={errorClass}>{errors.dueDate}</p>}
            </div>
          </div>

          {/* Actions */}
          <div className="flex items-center justify-end gap-3 pt-2 border-t border-gray-100">
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              className="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 transition-colors disabled:opacity-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50 flex items-center gap-2"
            >
              {submitting && (
                <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              )}
              {isEdit ? 'Update Task' : 'Create Task'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
