// @ts-nocheck
import { useState } from 'react';
import { Pencil, Calendar, User, Flag, Milestone } from 'lucide-react';

const PRIORITY_STYLES = {
  LOW: 'bg-gray-100 text-gray-600',
  MEDIUM: 'bg-blue-100 text-blue-700',
  HIGH: 'bg-orange-100 text-orange-700',
  URGENT: 'bg-red-100 text-red-700',
};

const STATUS_OPTIONS = ['TODO', 'IN_PROGRESS', 'REVIEW', 'DONE'];

const STATUS_LABELS = {
  TODO: 'To Do',
  IN_PROGRESS: 'In Progress',
  REVIEW: 'Review',
  DONE: 'Done',
};

function formatDate(dateStr) {
  if (!dateStr) return null;
  try {
    const date = new Date(dateStr);
    if (isNaN(date.getTime())) return null;
    return date.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
    });
  } catch {
    return null;
  }
}

function isOverdue(task) {
  if (!task?.dueDate || task.status === 'DONE') return false;
  const due = new Date(task.dueDate);
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return due < today;
}

export default function TaskCard({ task, onEdit, onStatusChange, isEditable, members }) {
  if (!task) return null;

  const priorityClass = PRIORITY_STYLES[task.priority] || PRIORITY_STYLES.LOW;
  const assigneeName = task.assigneeStudentId?.fullName || 'Unassigned';
  const milestoneName = task.milestoneId?.title || null;
  const overdue = isOverdue(task);
  const formattedDue = formatDate(task.dueDate);

  const handleStatusChange = (e) => {
    if (onStatusChange) {
      onStatusChange(task._id, e.target.value);
    }
  };

  return (
    <div className="bg-white border border-gray-200 rounded-lg p-3 hover:shadow-md transition-shadow duration-200 space-y-2">
      {/* Header: title + edit */}
      <div className="flex items-start justify-between gap-2">
        <h4 className="text-sm font-bold text-gray-900 leading-snug line-clamp-2 flex-1">
          {task.title}
        </h4>
        {isEditable && (
          <button
            onClick={() => onEdit?.(task)}
            className="shrink-0 p-1 rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
            title="Edit task"
          >
            <Pencil size={14} />
          </button>
        )}
      </div>

      {/* Priority + Overdue badges */}
      <div className="flex items-center gap-1.5 flex-wrap">
        <span
          className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide ${priorityClass}`}
        >
          {task.priority || 'LOW'}
        </span>
        {overdue && (
          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide bg-red-500 text-white">
            Overdue
          </span>
        )}
      </div>

      {/* Assignee */}
      <div className="flex items-center gap-1.5 text-xs text-gray-500">
        <User size={12} className="shrink-0" />
        <span className="truncate">{assigneeName}</span>
      </div>

      {/* Due date */}
      {formattedDue && (
        <div className={`flex items-center gap-1.5 text-xs ${overdue ? 'text-red-500 font-medium' : 'text-gray-500'}`}>
          <Calendar size={12} className="shrink-0" />
          <span>{formattedDue}</span>
        </div>
      )}

      {/* Milestone */}
      {milestoneName && (
        <div className="flex items-center gap-1.5 text-xs text-purple-600">
          <Flag size={12} className="shrink-0" />
          <span className="truncate">{milestoneName}</span>
        </div>
      )}

      {/* Status dropdown */}
      {isEditable && (
        <div className="pt-1 border-t border-gray-100">
          <select
            value={task.status || 'TODO'}
            onChange={handleStatusChange}
            className="w-full text-xs border border-gray-200 rounded-md px-2 py-1.5 bg-gray-50 text-gray-700 focus:outline-none focus:ring-1 focus:ring-blue-400 focus:border-blue-400 cursor-pointer"
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
  );
}
