import { useEffect } from 'react';
import type { ReactNode } from 'react';
import { CheckCircle2, Circle, Clock, Pencil, Tag, User } from 'lucide-react';
import Modal from '../ui/Modal';
import Button from '../ui/Button';
import { PRIORITY_CFG, STATUS_CFG } from '../../features/execution-board/constants';
import type { WeeklyTask } from '../../types/workspaceTools';

type DetailTask = WeeklyTask & { computedStatus?: string };

interface TaskDetailModalProps {
  task: DetailTask | null;
  onClose: () => void;
  onEdit?: (task: DetailTask) => void;
}

const TASK_TYPE_LABEL: Record<string, string> = {
  COURSE_TEMPLATE: 'Course Roadmap',
  CLASS_TASK: 'Class Requirement',
  TEAM_TASK: 'Team Task',
};

const formatDate = (value?: string | null) =>
  value ? new Date(value).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '—';

const assigneeLabel = (task: DetailTask) => {
  const assignee = task.assigneeStudentId;
  if (!assignee || typeof assignee === 'string') return 'Unassigned';
  return assignee.fullName || assignee.rollNumber || 'Unassigned';
};

const Field = ({ label, children }: { label: string; children: ReactNode }) => (
  <div>
    <dt className="text-[11px] font-semibold uppercase tracking-wide text-slate-400">{label}</dt>
    <dd className="mt-0.5 text-sm text-slate-700">{children}</dd>
  </div>
);

export default function TaskDetailModal({ task, onClose, onEdit }: TaskDetailModalProps) {
  const isOpen = Boolean(task);

  useEffect(() => {
    if (!isOpen) return undefined;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!task) return null;

  const status = task.computedStatus || task.status || 'TODO';
  const statusCfg = STATUS_CFG[status] || STATUS_CFG.TODO;
  const priorityCfg = PRIORITY_CFG[task.priority] || PRIORITY_CFG.MEDIUM;
  const checklist = task.checklist || [];
  const tags = task.tags || [];

  return (
    <Modal isOpen onClose={onClose} title="Task details" size="lg">
      <div className="space-y-5">
        <div>
          <div className="mb-2 flex flex-wrap items-center gap-1.5">
            <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold ${statusCfg.bg} ${statusCfg.text}`}>
              <span className={`h-1.5 w-1.5 rounded-full ${statusCfg.dot}`} />
              {statusCfg.label}
            </span>
            <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-semibold ${priorityCfg.bg} ${priorityCfg.text}`}>
              {priorityCfg.label}
            </span>
            <span className="text-xs text-slate-400">{TASK_TYPE_LABEL[task.taskType] || 'Task'} · Week {task.weekNumber}</span>
          </div>
          <h3 className="whitespace-pre-wrap break-words text-base font-semibold leading-snug text-slate-900 [overflow-wrap:anywhere]">{task.title}</h3>
        </div>

        <div>
          <h4 className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-slate-400">Description</h4>
          {task.description ? (
            <p className="whitespace-pre-wrap break-words text-sm leading-relaxed text-slate-700 [overflow-wrap:anywhere]">{task.description}</p>
          ) : (
            <p className="text-sm text-slate-400">No description.</p>
          )}
        </div>

        <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
          <Field label="Assignee"><span className="inline-flex items-center gap-1"><User className="h-3.5 w-3.5 text-slate-400" />{assigneeLabel(task)}</span></Field>
          <Field label="Start date"><span className="inline-flex items-center gap-1"><Clock className="h-3.5 w-3.5 text-slate-400" />{formatDate(task.startDate)}</span></Field>
          <Field label="Due date"><span className="inline-flex items-center gap-1"><Clock className="h-3.5 w-3.5 text-slate-400" />{formatDate(task.dueDate)}</span></Field>
          <Field label="Est. hours">{task.estimatedHours ? `${task.estimatedHours}h` : '—'}</Field>
        </dl>

        {tags.length > 0 && (
          <div className="flex flex-wrap items-center gap-1.5">
            <Tag className="h-3.5 w-3.5 text-slate-400" aria-hidden="true" />
            {tags.map((tag) => (
              <span key={tag} className="rounded bg-slate-100 px-1.5 py-0.5 text-xs font-medium text-slate-600">{tag}</span>
            ))}
          </div>
        )}

        {checklist.length > 0 && (
          <div>
            <h4 className="mb-1.5 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
              Checklist ({checklist.filter((item) => item.isCompleted).length}/{checklist.length})
            </h4>
            <ul className="space-y-1">
              {checklist.map((item, index) => (
                <li key={`${item.text}-${index}`} className="flex items-start gap-2 text-sm">
                  {item.isCompleted
                    ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-500" aria-label="Completed" />
                    : <Circle className="mt-0.5 h-4 w-4 shrink-0 text-slate-300" aria-label="Not completed" />}
                  <span className={`break-words [overflow-wrap:anywhere] ${item.isCompleted ? 'text-slate-400 line-through' : 'text-slate-700'}`}>{item.text}</span>
                </li>
              ))}
            </ul>
          </div>
        )}

        <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
          <Button variant="outline" onClick={onClose}>Close</Button>
          {onEdit && (
            <Button variant="gradient" icon={Pencil} onClick={() => onEdit(task)}>Edit</Button>
          )}
        </div>
      </div>
    </Modal>
  );
}
