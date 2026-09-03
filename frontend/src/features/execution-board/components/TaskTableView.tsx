// @ts-nocheck
import { memo, useMemo } from 'react';
import { Edit, Trash2 } from 'lucide-react';
import { PRIORITY_CFG, STATUSES, STATUS_CFG } from '../constants';
import { getTaskStatus } from '../boardUtils';
import { taskProgress as getProgress } from '../../../utils/taskProgress';

function Badge({ children, className = '' }) {
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold ${className}`}>
      {children}
    </span>
  );
}

function formatDate(value) {
  if (!value) return '—';
  return new Date(value).toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  });
}

function getAssignee(task) {
  return task.assigneeStudentId?.fullName
    || task.assigneeStudentId?.rollNumber
    || 'Unassigned';
}

function getDependency(task) {
  return task.dependency || task.dependencies?.join?.(', ') || '—';
}

function getMeetingNote(task) {
  return task.meetingNote || task.meetingNotes || '—';
}

function TaskTableView({ tasks, permissions, onEditTask, onDeleteTask, onStatusChange }) {
  const rows = useMemo(() => tasks, [tasks]);

  if (rows.length === 0) {
    return (
      <div className="rounded-xl border border-dashed border-slate-200 bg-white px-4 py-12 text-center">
        <p className="text-sm font-semibold text-slate-700">No tasks match the current filters.</p>
        <p className="mt-1 text-sm text-slate-500">Adjust search or filters to see more tasks.</p>
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
      <div className="overflow-x-auto">
        <table className="min-w-[1180px] w-full border-collapse text-left text-sm">
          <thead className="sticky top-0 z-10 border-b border-slate-200 bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-500">
            <tr>
              <th className="w-16 px-3 py-3">No.</th>
              <th className="min-w-[220px] px-3 py-3">Task Name</th>
              <th className="min-w-[260px] px-3 py-3">Description</th>
              <th className="min-w-[150px] px-3 py-3">Assignee</th>
              <th className="min-w-[120px] px-3 py-3">Start Date</th>
              <th className="min-w-[120px] px-3 py-3">Deadline</th>
              <th className="min-w-[130px] px-3 py-3">Status</th>
              <th className="min-w-[110px] px-3 py-3">Priority</th>
              <th className="min-w-[150px] px-3 py-3">Progress</th>
              <th className="min-w-[140px] px-3 py-3">Dependency</th>
              <th className="min-w-[180px] px-3 py-3">Meeting Note</th>
              <th className="w-24 px-3 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {rows.map((task, index) => {
              const status = getTaskStatus(task);
              const statusCfg = STATUS_CFG[status] || STATUS_CFG.TODO;
              const priorityCfg = PRIORITY_CFG[task.priority] || PRIORITY_CFG.MEDIUM;
              const progress = getProgress(task);
              const canEdit = permissions.canEditTask(task);
              const canDelete = permissions.canDeleteTask(task);

              return (
                <tr key={task._id} className="group transition-colors hover:bg-slate-50">
                  <td className="px-3 py-3 font-mono text-xs text-slate-400">{index + 1}</td>
                  <td className="px-3 py-3">
                    <div className="line-clamp-2 font-semibold text-slate-900">{task.title || 'Untitled Task'}</div>
                    <div className="mt-1 text-xs text-slate-400">W{task.weekNumber || '—'}</div>
                    <div className="text-xs text-slate-500">{task.taskType === 'COURSE_TEMPLATE' ? 'Course Roadmap · Read only' : task.taskType === 'CLASS_TASK' ? 'Class Requirements · Read only' : 'Team Task'}</div>
                  </td>
                  <td className="px-3 py-3 text-slate-600">
                    <div className="line-clamp-2">{task.description || '—'}</div>
                  </td>
                  <td className="px-3 py-3 text-slate-600">{getAssignee(task)}</td>
                  <td className="px-3 py-3 text-slate-600">{formatDate(task.startDate)}</td>
                  <td className="px-3 py-3 text-slate-600">{formatDate(task.dueDate)}</td>
                  <td className="px-3 py-3">
                    {permissions.canUpdateTaskStatus(task) ? (
                      <label className="relative inline-flex items-center">
                        <span className="sr-only">Change status for {task.title}</span>
                        <select
                          value={status}
                          onChange={(event) => onStatusChange(task._id, event.target.value)}
                          className={`h-7 rounded-full border border-transparent px-2.5 pr-7 text-[11px] font-semibold outline-none transition-colors focus:border-blue-400 focus:ring-2 focus:ring-blue-500/20 ${statusCfg.bg} ${statusCfg.text}`}
                        >
                          {STATUSES.map((nextStatus) => {
                            const cfg = STATUS_CFG[nextStatus] || STATUS_CFG.TODO;
                            return <option key={nextStatus} value={nextStatus}>{cfg.label}</option>;
                          })}
                        </select>
                      </label>
                    ) : (
                      <Badge className={`${statusCfg.bg} ${statusCfg.text}`}>
                        <span className={`h-1.5 w-1.5 rounded-full ${statusCfg.dot}`} />
                        {statusCfg.label}
                      </Badge>
                    )}
                  </td>
                  <td className="px-3 py-3">
                    <Badge className={`${priorityCfg.bg} ${priorityCfg.text}`}>{priorityCfg.label}</Badge>
                  </td>
                  <td className="px-3 py-3">
                    <div className="flex items-center gap-2">
                      <div className="h-1.5 w-20 overflow-hidden rounded-full bg-slate-100">
                        <div className="h-full rounded-full bg-blue-500" style={{ width: `${progress}%` }} />
                      </div>
                      <span className="w-9 text-xs font-semibold text-slate-600">{progress}%</span>
                    </div>
                  </td>
                  <td className="px-3 py-3 text-slate-600">
                    <div className="line-clamp-2">{getDependency(task)}</div>
                  </td>
                  <td className="px-3 py-3 text-slate-600">
                    <div className="line-clamp-2">{getMeetingNote(task)}</div>
                  </td>
                  <td className="px-3 py-3">
                    <div className="flex justify-end gap-1 opacity-80 transition-opacity group-hover:opacity-100">
                      {canEdit && (
                        <button
                          type="button"
                          onClick={() => onEditTask(task)}
                          className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-white hover:text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500/30"
                          aria-label={`Edit ${task.title}`}
                        >
                          <Edit className="h-4 w-4" />
                        </button>
                      )}
                      {canDelete && (
                        <button
                          type="button"
                          onClick={() => onDeleteTask(task)}
                          className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-red-50 hover:text-red-600 focus:outline-none focus:ring-2 focus:ring-red-500/20"
                          aria-label={`Delete ${task.title}`}
                        >
                          <Trash2 className="h-4 w-4" />
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default memo(TaskTableView);
