// @ts-nocheck
import { memo } from 'react';
import { AnimatePresence, motion, useReducedMotion } from 'framer-motion';
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
}) {
  const cfg = STATUS_CFG[status];
  const reduceMotion = useReducedMotion();
  const { setNodeRef, isOver } = useDroppable({
    id: `column-${status}`,
    data: { type: 'column', status },
  });
  const highlighted = isOver || activeOverStatus === status;

  return (
    <motion.section
      ref={setNodeRef}
      layout={!reduceMotion}
      transition={reduceMotion ? { duration: 0 } : { type: 'spring', stiffness: 420, damping: 36 }}
      className={`flex min-h-[280px] flex-col rounded-xl border bg-slate-50/70 transition-colors ${
        highlighted ? `${cfg.border} bg-white ring-2 ring-blue-500/15` : 'border-slate-200'
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
          {tasks.length === 0 ? (
            <motion.div
              layout={!reduceMotion}
              className={`rounded-lg border border-dashed px-3 py-8 text-center text-xs font-medium transition-colors ${
                highlighted ? 'border-blue-200 bg-blue-50 text-blue-500' : 'border-slate-200 bg-white/70 text-slate-400'
              }`}
            >
              Drop task here
            </motion.div>
          ) : (
            <AnimatePresence initial={false}>
              {tasks.map((task) => (
                <TaskCard
                  key={task._id}
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
              ))}
            </AnimatePresence>
          )}
        </SortableContext>
      </div>
    </motion.section>
  );
}

export default memo(BoardColumn);
