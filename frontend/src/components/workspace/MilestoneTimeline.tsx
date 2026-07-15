// @ts-nocheck
import React from 'react';

const STATUS_CONFIG = {
  PLANNED:     { label: 'Planned',     bg: 'bg-slate-100',  text: 'text-slate-700',  dot: 'bg-slate-400' },
  TODO:        { label: 'To Do',       bg: 'bg-slate-100',  text: 'text-slate-700',  dot: 'bg-slate-400' },
  IN_PROGRESS: { label: 'In Progress', bg: 'bg-blue-100',   text: 'text-blue-700',   dot: 'bg-blue-500' },
  COMPLETED:   { label: 'Completed',   bg: 'bg-green-100',  text: 'text-green-700',  dot: 'bg-green-500' },
  DONE:        { label: 'Done',        bg: 'bg-green-100',  text: 'text-green-700',  dot: 'bg-green-500' },
  OVERDUE:     { label: 'Overdue',     bg: 'bg-red-100',    text: 'text-red-700',    dot: 'bg-red-500' },
};

const DEFAULT_STATUS = { label: 'Unknown', bg: 'bg-gray-100', text: 'text-gray-600', dot: 'bg-gray-400' };

function formatDate(dateStr) {
  if (!dateStr) return null;
  try {
    return new Date(dateStr).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  } catch {
    return dateStr;
  }
}

function getDateDisplay(startDate, dueDate) {
  const start = formatDate(startDate);
  const due = formatDate(dueDate);
  if (start && due) return `${start} – ${due}`;
  if (due) return `Due: ${due}`;
  if (start) return `Starts: ${start}`;
  return null;
}

export default function MilestoneTimeline({ milestones = [], onEdit, onDelete, isEditable = false }) {
  if (!milestones || milestones.length === 0) {
    return (
      <div className="text-center p-8 bg-gray-50 rounded-lg border border-dashed border-gray-300">
        <svg className="mx-auto h-10 w-10 text-gray-400 mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
        </svg>
        <p className="text-gray-500 text-sm">No milestones yet. Create your first milestone to track progress.</p>
      </div>
    );
  }

  return (
    <div className="relative">
      {milestones.map((milestone, index) => {
        const status = STATUS_CONFIG[milestone?.status] || DEFAULT_STATUS;
        const progress = Math.min(100, Math.max(0, milestone?.progress ?? 0));
        const completedTasks = milestone?.completedTasks ?? 0;
        const totalTasks = milestone?.totalTasks ?? 0;
        const isLast = index === milestones.length - 1;
        const dateDisplay = getDateDisplay(milestone?.startDate, milestone?.dueDate);

        return (
          <div key={milestone?._id || index} className="relative flex gap-4">
            {/* Timeline column: dot + line */}
            <div className="flex flex-col items-center">
              <div className={`w-3.5 h-3.5 rounded-full ${status.dot} ring-4 ring-white flex-shrink-0 mt-1.5`} />
              {!isLast && (
                <div className="w-0.5 bg-gray-200 flex-1 min-h-[2rem]" />
              )}
            </div>

            {/* Content card */}
            <div className={`flex-1 bg-white rounded-lg border border-gray-200 shadow-sm p-4 ${!isLast ? 'mb-4' : ''}`}>
              <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-2">
                {/* Left side info */}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <h3 className="text-base font-bold text-gray-900 truncate">
                      {milestone?.title || 'Untitled Milestone'}
                    </h3>
                    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold ${status.bg} ${status.text}`}>
                      {status.label}
                    </span>
                  </div>

                  {dateDisplay && (
                    <div className="flex items-center mt-1.5 text-sm text-gray-500">
                      <svg className="w-4 h-4 mr-1.5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                      </svg>
                      {dateDisplay}
                    </div>
                  )}

                  {milestone?.description && (
                    <p className="mt-1.5 text-sm text-gray-600 line-clamp-2">{milestone.description}</p>
                  )}

                  {/* Progress bar */}
                  <div className="mt-3">
                    <div className="flex items-center justify-between mb-1">
                      <span className="text-xs font-medium text-gray-600">Progress</span>
                      <span className="text-xs font-semibold text-gray-700">{progress}%</span>
                    </div>
                    <div className="w-full bg-gray-100 rounded-full h-2 overflow-hidden">
                      <div
                        className={`h-2 rounded-full transition-all duration-300 ${
                          progress >= 100 ? 'bg-green-500' :
                          progress >= 60  ? 'bg-blue-500'  :
                          progress >= 30  ? 'bg-amber-500'  : 'bg-gray-400'
                        }`}
                        style={{ width: `${progress}%` }}
                      />
                    </div>
                  </div>

                  {/* Task count */}
                  {totalTasks > 0 && (
                    <p className="mt-1.5 text-xs text-gray-500">
                      {completedTasks}/{totalTasks} tasks done
                    </p>
                  )}
                </div>

                {/* Edit / Delete buttons */}
                {isEditable && (
                  <div className="flex items-center gap-2 sm:flex-shrink-0">
                    {onEdit && (
                      <button
                        onClick={() => onEdit(milestone)}
                        className="text-blue-600 hover:text-blue-800 text-sm font-medium transition-colors"
                      >
                        Edit
                      </button>
                    )}
                    {onDelete && (
                      <button
                        onClick={() => onDelete(milestone?._id)}
                        className="text-red-600 hover:text-red-800 text-sm font-medium transition-colors"
                      >
                        Delete
                      </button>
                    )}
                  </div>
                )}
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}
