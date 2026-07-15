// @ts-nocheck
import React from 'react';
import TaskCard from './TaskCard';

const COLUMNS = [
  { key: 'TODO',        label: 'To Do',       border: 'border-t-gray-400',  badge: 'bg-gray-100 text-gray-700' },
  { key: 'IN_PROGRESS', label: 'In Progress', border: 'border-t-blue-500',  badge: 'bg-blue-100 text-blue-700' },
  { key: 'REVIEW',      label: 'Review',      border: 'border-t-amber-400', badge: 'bg-amber-100 text-amber-700' },
  { key: 'DONE',        label: 'Done',        border: 'border-t-green-500', badge: 'bg-green-100 text-green-700' },
];

export default function KanbanBoard({
  tasks = [],
  onStatusChange,
  onEditTask,
  isEditable = false,
  members = [],
}) {
  const safeTasks = Array.isArray(tasks) ? tasks : [];

  // Show empty state if no tasks at all
  if (safeTasks.length === 0) {
    return (
      <div className="text-center p-8 bg-gray-50 rounded-lg border border-dashed border-gray-300">
        <svg className="mx-auto h-10 w-10 text-gray-400 mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7" />
        </svg>
        <p className="text-gray-500 text-sm">No tasks yet. Create your first task to get started.</p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
      {COLUMNS.map((col) => {
        const columnTasks = safeTasks.filter((t) => t?.status === col.key);

        return (
          <div
            key={col.key}
            className={`bg-gray-50 rounded-lg border border-gray-200 border-t-4 ${col.border} flex flex-col min-h-[200px]`}
          >
            {/* Column header */}
            <div className="flex items-center justify-between px-3 py-2.5 border-b border-gray-200">
              <h4 className="text-sm font-semibold text-gray-700">{col.label}</h4>
              <span className={`inline-flex items-center justify-center min-w-[1.5rem] h-6 px-1.5 rounded-full text-xs font-bold ${col.badge}`}>
                {columnTasks.length}
              </span>
            </div>

            {/* Cards */}
            <div className="flex-1 p-2 space-y-2 overflow-y-auto max-h-[500px]">
              {columnTasks.length === 0 ? (
                <div className="flex items-center justify-center h-20 text-xs text-gray-400 italic">
                  No tasks
                </div>
              ) : (
                columnTasks.map((task) => (
                  <TaskCard
                    key={task._id || task.id}
                    task={task}
                    onStatusChange={onStatusChange}
                    onEdit={onEditTask}
                    isEditable={isEditable}
                    members={members}
                  />
                ))
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
