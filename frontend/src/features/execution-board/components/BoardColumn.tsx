// @ts-nocheck
import { Fragment, memo } from 'react';
import { useDroppable } from '@dnd-kit/core';
import { SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable';
import { STATUS_CFG } from '../constants';
import TaskCard from './TaskCard';

function BoardColumn({
  status,
  tasks,
  permissions,
  onEditTask,
  onDeleteTask,
  onStatusChange,
  onSwipeStatusChange,
  enableSwipe = false,
  activeOverStatus,
  showDropPlaceholder = false,
  dropPlaceholderIndex = 0,
  dropPlaceholderHeight,
}) {
  const cfg = STATUS_CFG[status];
  const { setNodeRef, isOver } = useDroppable({
    id: `column-${status}`,
    data: { type: 'column', status },
  });
  const highlighted = isOver || activeOverStatus === status;
  const safePlaceholderIndex = Math.max(0, Math.min(dropPlaceholderIndex, tasks.length));
  const dropPlaceholder = (
    <div
      aria-hidden="true"
      className="rounded-lg border-2 border-dashed border-primary-300 bg-primary-50/70 shadow-inner transition-[height] duration-150"
      style={{ height: Math.max(72, dropPlaceholderHeight || 112) }}
    />
  );

  return (
    <section
      ref={setNodeRef}
      className={`flex min-h-[280px] flex-col rounded-xl border bg-slate-50/70 transition-[background-color,border-color,box-shadow] duration-150 ${
        highlighted ? `${cfg.border} bg-white ring-2 ring-primary/20 shadow-sm` : 'border-slate-200'
      }`}
    >
      <header className="sticky top-0 z-10 flex items-center justify-between rounded-t-xl border-b border-slate-200 bg-white px-3 py-3">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-slate-800">
          <span className={`h-2 w-2 rounded-full ${cfg.dot}`} />
          {cfg.label}
        </h2>
        <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${cfg.bg} ${cfg.text}`}>
          {tasks.length}
        </span>
      </header>

      <div className="flex-1 space-y-3 p-3">
        <SortableContext items={tasks.map((task) => task._id)} strategy={verticalListSortingStrategy}>
          {tasks.length === 0 && !showDropPlaceholder ? (
            <div
              className={`rounded-lg border border-dashed px-3 py-8 text-center text-xs font-medium transition-colors ${
                highlighted ? 'border-primary-200 bg-primary-50 text-primary' : 'border-slate-200 bg-white/70 text-slate-400'
              }`}
            >
              Drop task here
            </div>
          ) : (
            <>
              {tasks.map((task, index) => (
                <Fragment key={task._id}>
                  {showDropPlaceholder && index === safePlaceholderIndex && dropPlaceholder}
                  <TaskCard
                    task={task}
                    canEdit={permissions.canEditTask(task)}
                    canDelete={permissions.canDeleteTask(task)}
                    canUpdateStatus={permissions.canUpdateTaskStatus(task)}
                    onEdit={onEditTask}
                    onDelete={onDeleteTask}
                    onStatusChange={onStatusChange}
                    onSwipeStatusChange={onSwipeStatusChange}
                    enableSwipe={enableSwipe}
                  />
                </Fragment>
              ))}
              {showDropPlaceholder && safePlaceholderIndex === tasks.length && dropPlaceholder}
            </>
          )}
        </SortableContext>
      </div>
    </section>
  );
}

export default memo(BoardColumn);
