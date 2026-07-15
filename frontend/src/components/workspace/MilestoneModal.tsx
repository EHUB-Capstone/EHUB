// @ts-nocheck
import { useState, useEffect } from 'react';
import { X } from 'lucide-react';
import toast from 'react-hot-toast';

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
  startDate: '',
  dueDate: '',
};

export default function MilestoneModal({ isOpen, onClose, onSubmit, initialData }) {
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
    if (!form.dueDate) {
      newErrors.dueDate = 'Due date is required';
    } else {
      const today = new Date().toISOString().split('T')[0];
      if (new Date(form.dueDate) < new Date(today)) {
        newErrors.dueDate = 'Due date cannot be in the past';
        toast.error('Due date cannot be in the past');
      }
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
      if (!payload.startDate) delete payload.startDate;

      await onSubmit?.(payload);
      onClose?.();
    } catch (err) {
      toast.error(err?.message || 'Failed to save milestone');
    } finally {
      setSubmitting(false);
    }
  };

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
      <div className="relative bg-white rounded-xl shadow-2xl w-full max-w-md mx-4 max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <h2 className="text-lg font-semibold text-gray-900">
            {isEdit ? 'Edit Milestone' : 'Create Milestone'}
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
            <label htmlFor="ms-title" className={labelClass}>
              Title <span className="text-red-500">*</span>
            </label>
            <input
              id="ms-title"
              type="text"
              name="title"
              value={form.title}
              onChange={handleChange}
              placeholder="Enter milestone title"
              className={`${inputClass} ${errors.title ? 'border-red-400 focus:ring-red-400' : ''}`}
            />
            {errors.title && <p className={errorClass}>{errors.title}</p>}
          </div>

          {/* Description */}
          <div>
            <label htmlFor="ms-desc" className={labelClass}>
              Description
            </label>
            <textarea
              id="ms-desc"
              name="description"
              value={form.description}
              onChange={handleChange}
              rows={3}
              placeholder="Describe the milestone..."
              className={`${inputClass} resize-none`}
            />
          </div>

          {/* Dates row */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label htmlFor="ms-start" className={labelClass}>
                Start Date
              </label>
              <input
                id="ms-start"
                type="date"
                name="startDate"
                value={form.startDate}
                onChange={handleChange}
                className={inputClass}
              />
            </div>
            <div>
              <label htmlFor="ms-due" className={labelClass}>
                Due Date <span className="text-red-500">*</span>
              </label>
              <input
                id="ms-due"
                type="date"
                min={new Date().toISOString().split('T')[0]}
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
              {isEdit ? 'Update Milestone' : 'Create Milestone'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
