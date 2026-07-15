// @ts-nocheck
import { memo, useEffect, useMemo, useRef, useState } from 'react';
import { motion, useReducedMotion } from 'framer-motion';
import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import {
  CheckCircle2,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Clock,
  Edit,
  GripVertical,
  Trash2,
  User,
} from 'lucide-react';
import { PRIORITY_CFG, STATUSES, STATUS_CFG, WORKFLOW_STATUSES } from '../constants';

const SWIPE_THRESHOLD = 72;
const SWIPE_LIMIT = 120;

const Badge = ({ children, className = '' }) => (
  <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold ${className}`}>
    {children}
  </span>
);

function TaskCard({
  task,
  canEdit,
  canDelete,
  canUpdateStatus,
  onEdit,
  onDelete,
  onStatusChange,
  onSwipeStatusChange,
  enableSwipe = false,
  isOverlay = false,
}) {
  const [statusOpen, setStatusOpen] = useState(false);
  const [swipeX, setSwipeX] = useState(0);
  const [isSwiping, setIsSwiping] = useState(false);
  const menuRef = useRef(null);
  const swipeStartRef = useRef(null);
  const swipeXRef = useRef(0);
  const suppressClickRef = useRef(false);
  const reduceMotion = useReducedMotion();
  const {
    attributes,
    listeners,
    setActivatorNodeRef,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({
    id: task._id,
    data: {
      type: 'task',
      task,
      status: task.computedStatus || task.status || 'TODO',
    },
    disabled: isOverlay || !canUpdateStatus,
  });

  const currentStatus = task.computedStatus || task.status || 'TODO';
  const statusCfg = STATUS_CFG[currentStatus] || STATUS_CFG.TODO;
  const priorityCfg = PRIORITY_CFG[task.priority] || PRIORITY_CFG.MEDIUM;
  const checklist = task.checklist || [];
  const checklistDone = checklist.filter((item) => item.isCompleted).length;
  const assigneeName = task.assigneeStudentId?.fullName || task.assigneeStudentId?.rollNumber || null;
  const isOverdue = currentStatus === 'OVERDUE';
  const workflowStatus = isOverdue && WORKFLOW_STATUSES.includes(task.status)
    ? task.status
    : currentStatus;
  const workflowIndex = WORKFLOW_STATUSES.indexOf(workflowStatus);
  const previousStatus = workflowIndex > 0 ? WORKFLOW_STATUSES[workflowIndex - 1] : null;
  const nextStatus = workflowIndex >= 0 && workflowIndex < WORKFLOW_STATUSES.length - 1
    ? WORKFLOW_STATUSES[workflowIndex + 1]
    : null;
  const previousCfg = previousStatus ? STATUS_CFG[previousStatus] : null;
  const nextCfg = nextStatus ? STATUS_CFG[nextStatus] : null;

  const dueDate = useMemo(() => {
    if (!task.dueDate) return null;
    return new Date(task.dueDate).toLocaleDateString('en-GB', { day: '2-digit', month: 'short' });
  }, [task.dueDate]);

  useEffect(() => {
    if (!statusOpen) return undefined;

    const handlePointerDown = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) setStatusOpen(false);
    };
    const handleKeyDown = (event) => {
      if (event.key === 'Escape') setStatusOpen(false);
    };

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [statusOpen]);

  const style = isOverlay
    ? undefined
    : {
      transform: CSS.Transform.toString(transform),
      transition,
    };

  const resetSwipe = () => {
    swipeStartRef.current = null;
    swipeXRef.current = 0;
    setSwipeX(0);
    setIsSwiping(false);
  };

  const handlePointerDown = (event) => {
    if (!enableSwipe || !canUpdateStatus || isOverlay || event.button !== 0) return;
    if (event.target.closest('button, input, textarea, select, a, [role="menuitem"]')) return;

    swipeStartRef.current = {
      pointerId: event.pointerId,
      x: event.clientX,
      y: event.clientY,
      horizontal: false,
      cancelled: false,
    };
    suppressClickRef.current = false;
  };

  const handlePointerMove = (event) => {
    const start = swipeStartRef.current;
    if (!start || start.pointerId !== event.pointerId || start.cancelled) return;

    const dx = event.clientX - start.x;
    const dy = event.clientY - start.y;

    if (!start.horizontal) {
      if (Math.abs(dy) > 10 && Math.abs(dy) > Math.abs(dx)) {
        start.cancelled = true;
        return;
      }
      if (Math.abs(dx) < 8 || Math.abs(dx) <= Math.abs(dy) * 1.2) return;
      start.horizontal = true;
      suppressClickRef.current = true;
      setIsSwiping(true);
      event.currentTarget.setPointerCapture?.(event.pointerId);
    }

    event.preventDefault();
    const hasTarget = dx > 0 ? Boolean(previousStatus) : Boolean(nextStatus);
    const resistance = hasTarget ? 1 : 0.18;
    const nextX = Math.max(-SWIPE_LIMIT, Math.min(SWIPE_LIMIT, dx * resistance));
    swipeXRef.current = nextX;
    setSwipeX(nextX);
  };

  const finishSwipe = (event) => {
    const start = swipeStartRef.current;
    if (!start || start.pointerId !== event.pointerId) return;

    const finalX = swipeXRef.current;
    const targetStatus = finalX <= -SWIPE_THRESHOLD
      ? nextStatus
      : finalX >= SWIPE_THRESHOLD
        ? previousStatus
        : null;

    if (targetStatus) {
      (onSwipeStatusChange || onStatusChange)(task._id, targetStatus);
    }
    resetSwipe();
  };

  return (
    <motion.article
      ref={isOverlay ? undefined : setNodeRef}
      layout={!reduceMotion && !isOverlay}
      layoutId={isOverlay ? undefined : `task-${task._id}`}
      initial={false}
      animate={{
        scale: isOverlay ? 1.03 : 1,
        opacity: isDragging ? 0.35 : 1,
      }}
      transition={reduceMotion ? { duration: 0 } : { type: 'spring', stiffness: 520, damping: 38, mass: 0.8 }}
      style={style}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={finishSwipe}
      onPointerCancel={resetSwipe}
      onClickCapture={(event) => {
        if (!suppressClickRef.current) return;
        event.preventDefault();
        event.stopPropagation();
        suppressClickRef.current = false;
      }}
      className={`group relative overflow-hidden rounded-lg border bg-slate-100 transition-colors hover:border-slate-300 ${
        isOverlay ? 'shadow-2xl ring-2 ring-blue-500/20' : ''
      } ${isOverdue ? 'border-red-200' : 'border-slate-200'}`}
    >
      {enableSwipe && canUpdateStatus && previousCfg && (
        <div className={`absolute inset-y-0 left-0 flex w-28 items-center gap-1.5 px-3 ${previousCfg.bg} ${previousCfg.text}`}>
          <ChevronRight className="h-5 w-5" />
          <span className="text-xs font-bold">{previousCfg.label}</span>
        </div>
      )}
      {enableSwipe && canUpdateStatus && nextCfg && (
        <div className={`absolute inset-y-0 right-0 flex w-28 items-center justify-end gap-1.5 px-3 ${nextCfg.bg} ${nextCfg.text}`}>
          <span className="text-xs font-bold">{nextCfg.label}</span>
          <ChevronLeft className="h-5 w-5" />
        </div>
      )}

      <div
        className="relative bg-white transition-transform duration-150 ease-out touch-pan-y"
        style={{ transform: `translateX(${swipeX}px)`, transitionDuration: isSwiping ? '0ms' : '150ms' }}
      >
        <div className={`h-0.5 rounded-t-lg ${priorityCfg.text.includes('red') ? 'bg-red-500' : task.priority === 'HIGH' ? 'bg-amber-400' : task.priority === 'MEDIUM' ? 'bg-blue-400' : 'bg-slate-300'}`} />
        <div className="p-3">
        <div className="mb-2 flex items-start justify-between gap-2">
          <div className="flex min-w-0 flex-wrap gap-1">
            <Badge className={`${statusCfg.bg} ${statusCfg.text}`}>
              <span className={`h-1.5 w-1.5 rounded-full ${statusCfg.dot}`} />
              {statusCfg.label}
            </Badge>
            <Badge className={`${priorityCfg.bg} ${priorityCfg.text}`}>{priorityCfg.label}</Badge>
          </div>

          <div className="flex shrink-0 items-center gap-0.5 opacity-70 transition-opacity group-hover:opacity-100">
            {!isOverlay && canUpdateStatus && (
              <button
                type="button"
                ref={setActivatorNodeRef}
                {...attributes}
                {...listeners}
                className="touch-none rounded-md p-1.5 text-slate-300 transition-colors hover:bg-white hover:text-slate-600 focus:outline-none focus:ring-2 focus:ring-blue-500/30"
                aria-label={`Drag ${task.title}`}
              >
                <GripVertical className="h-3.5 w-3.5" />
              </button>
            )}
            {canEdit && (
              <button
                type="button"
                onClick={() => onEdit(task)}
                className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-white hover:text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500/30"
                aria-label={`Edit ${task.title}`}
              >
                <Edit className="h-3.5 w-3.5" />
              </button>
            )}
            {canDelete && (
              <button
                type="button"
                onClick={() => onDelete(task)}
                className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-red-50 hover:text-red-600 focus:outline-none focus:ring-2 focus:ring-red-500/20"
                aria-label={`Delete ${task.title}`}
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            )}
          </div>
        </div>

        <h4 className="mb-1 line-clamp-2 text-sm font-semibold leading-snug text-slate-900">{task.title}</h4>
        {task.description && <p className="mb-2 line-clamp-2 text-xs leading-relaxed text-slate-500">{task.description}</p>}

        {checklist.length > 0 && (
          <div className="mb-3">
            <div className="mb-1 flex items-center justify-between text-[10px] font-medium text-slate-400">
              <span>Checklist</span>
              <span>{checklistDone}/{checklist.length}</span>
            </div>
            <div className="h-1 overflow-hidden rounded-full bg-slate-100">
              <div
                className="h-full rounded-full bg-blue-500 transition-[width] duration-300"
                style={{ width: `${(checklistDone / checklist.length) * 100}%` }}
              />
            </div>
          </div>
        )}

        {checklist.length === 0 && task.completionPercentage > 0 && (
          <div className="mb-3">
            <div className="mb-1 flex items-center justify-between text-[10px] font-medium text-slate-400">
              <span>Progress</span>
              <span>{task.completionPercentage}%</span>
            </div>
            <div className="h-1 overflow-hidden rounded-full bg-slate-100">
              <div className="h-full rounded-full bg-emerald-500" style={{ width: `${task.completionPercentage}%` }} />
            </div>
          </div>
        )}

        <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-slate-400">
          <span className="rounded bg-blue-50 px-1.5 py-0.5 font-semibold text-blue-700">W{task.weekNumber}</span>
          {assigneeName && <span className="flex items-center gap-1"><User className="h-3 w-3" />{assigneeName}</span>}
          {dueDate && <span className={`flex items-center gap-1 ${isOverdue ? 'font-semibold text-red-500' : ''}`}><Clock className="h-3 w-3" />{dueDate}</span>}
        </div>

        {canUpdateStatus && currentStatus !== 'COMPLETED' && (
          <div className="mt-3 flex items-center gap-2 border-t border-slate-100 pt-3">
            <button
              type="button"
              onClick={() => onStatusChange(task._id, 'COMPLETED')}
              className="inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-[11px] font-semibold text-emerald-700 transition-colors hover:bg-emerald-50"
            >
              <CheckCircle2 className="h-3.5 w-3.5" />
              Complete
            </button>

            <div ref={menuRef} className="relative ml-auto">
              <button
                type="button"
                onClick={() => setStatusOpen((value) => !value)}
                aria-haspopup="menu"
                aria-expanded={statusOpen}
                className="inline-flex items-center gap-1 rounded-md border border-slate-200 bg-white px-2 py-1 text-[11px] font-medium text-slate-500 transition-colors hover:text-slate-800"
              >
                Move <ChevronDown className="h-3 w-3" />
              </button>

              {statusOpen && (
                <div role="menu" className="absolute bottom-full right-0 z-20 mb-1 min-w-[150px] rounded-lg border border-slate-200 bg-white py-1 shadow-lg">
                  {STATUSES.filter((status) => status !== currentStatus).map((status) => {
                    const cfg = STATUS_CFG[status];
                    return (
                      <button
                        key={status}
                        type="button"
                        role="menuitem"
                        onClick={() => {
                          onStatusChange(task._id, status);
                          setStatusOpen(false);
                        }}
                        className="flex w-full items-center gap-2 px-3 py-1.5 text-left text-xs text-slate-700 transition-colors hover:bg-slate-50"
                      >
                        <span className={`h-1.5 w-1.5 rounded-full ${cfg.dot}`} />
                        {cfg.label}
                      </button>
                    );
                  })}
                </div>
              )}
            </div>
          </div>
        )}
        </div>
      </div>
    </motion.article>
  );
}

export default memo(TaskCard);
