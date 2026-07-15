// @ts-nocheck
// frontend/src/components/workspace/WeeklyRoadmapPlanner.jsx
import { useState, useEffect, useCallback } from 'react';
import toast from 'react-hot-toast';
import {
  BookOpen, ClipboardList, Users, Plus, Pencil, Trash2, CheckCircle2,
  ChevronDown, Clock, AlertTriangle, Tag, User, Loader2, MapPin,
  ChevronRight, Star, Shield,
} from 'lucide-react';
import {
  getWeeklyTasks,
  createWeeklyTask,
  updateWeeklyTask,
  deleteWeeklyTask,
  updateWeeklyTaskStatus,
} from '../../api/weeklyTaskApi';
import ConfirmDialog from '../ui/ConfirmDialog';

// ─── Constants ────────────────────────────────────────────────────────────────

const WEEKS = Array.from({ length: 10 }, (_, i) => i + 1);

const STATUSES = ['TODO', 'IN_PROGRESS', 'REVIEW', 'COMPLETED', 'OVERDUE'];

const STATUS_CFG = {
  TODO:        { label: 'To Do',      bg: 'bg-slate-100',   text: 'text-slate-600',  dot: 'bg-slate-400'  },
  IN_PROGRESS: { label: 'In Progress',bg: 'bg-blue-50',     text: 'text-blue-600',   dot: 'bg-blue-500'   },
  REVIEW:      { label: 'Review',     bg: 'bg-violet-50',   text: 'text-violet-600', dot: 'bg-violet-500' },
  COMPLETED:   { label: 'Completed',  bg: 'bg-emerald-50',  text: 'text-emerald-600',dot: 'bg-emerald-500'},
  OVERDUE:     { label: 'Overdue',    bg: 'bg-red-50',      text: 'text-red-600',    dot: 'bg-red-500'    },
};

const PRIORITY_CFG = {
  LOW:      { label: 'Low',      bg: 'bg-slate-100',  text: 'text-slate-500'  },
  MEDIUM:   { label: 'Medium',   bg: 'bg-blue-50',    text: 'text-blue-600'   },
  HIGH:     { label: 'High',     bg: 'bg-amber-50',   text: 'text-amber-600'  },
  CRITICAL: { label: 'Critical', bg: 'bg-red-50',     text: 'text-red-600'    },
};

const LEADER_ROLE_KEYS = ['CEO', 'LEADER', 'TEAM LEADER', 'TEAM_LEADER'];
const DUPLICATE_TASK_MESSAGE = 'Duplicate task: A task with this title already exists in this week.';

const normalizeTaskTitle = (title) =>
  title?.trim().toLowerCase().replace(/\s+/g, ' ') || '';

const getTaskSaveErrorMessage = (error) => {
  if (error?.status === 409 || error?.message === 'Duplicate task') {
    return DUPLICATE_TASK_MESSAGE;
  }
  return error?.message || 'Failed to save task';
};

// ─── Tiny helpers ─────────────────────────────────────────────────────────────

const Badge = ({ children, className = '' }) => (
  <span className={`inline-flex items-center gap-1 text-[11px] font-semibold px-2 py-0.5 rounded-full ${className}`}>
    {children}
  </span>
);

const StatusBadge = ({ status }) => {
  const cfg = STATUS_CFG[status] || STATUS_CFG.TODO;
  return (
    <Badge className={`${cfg.bg} ${cfg.text}`}>
      <span className={`w-1.5 h-1.5 rounded-full ${cfg.dot}`} />
      {cfg.label}
    </Badge>
  );
};

const PriorityBadge = ({ priority }) => {
  const cfg = PRIORITY_CFG[priority] || PRIORITY_CFG.MEDIUM;
  return <Badge className={`${cfg.bg} ${cfg.text}`}>{cfg.label}</Badge>;
};

const SectionEmpty = ({ label }) => (
  <div className="text-center py-10">
    <div className="w-10 h-10 rounded-xl bg-slate-100 flex items-center justify-center mx-auto mb-3">
      <ClipboardList className="w-5 h-5 text-slate-400" />
    </div>
    <p className="text-sm text-slate-400 font-medium">No {label} for this week</p>
  </div>
);

// ─── TaskCard ─────────────────────────────────────────────────────────────────

function TaskCard({ task, canEdit, canDelete, canUpdateSts, onEdit, onDelete, onStatusChange, onMarkComplete }) {
  const [statusOpen, setStatusOpen] = useState(false);

  const checklist = task.checklist || [];
  const checklistDone = checklist.filter(c => c.isCompleted).length;
  const assigneeName = task.assigneeStudentId?.fullName || task.assigneeStudentId?.rollNumber || null;
  const dueDateStr = task.dueDate ? new Date(task.dueDate).toLocaleDateString('en-GB', { day: '2-digit', month: 'short' }) : null;
  const isOverdue = task.status === 'OVERDUE';

  return (
    <div className={`bg-white rounded-xl border shadow-sm hover:shadow-md transition-shadow duration-200 ${isOverdue ? 'border-red-200' : 'border-slate-200/80'}`}>
      {/* Priority bar */}
      <div className={`h-0.5 rounded-t-xl ${
        task.priority === 'CRITICAL' ? 'bg-red-500' :
        task.priority === 'HIGH' ? 'bg-amber-400' :
        task.priority === 'MEDIUM' ? 'bg-blue-400' : 'bg-slate-200'
      }`} />

      <div className="p-4">
        {/* Top row */}
        <div className="flex items-start justify-between gap-2 mb-2">
          <div className="flex flex-wrap gap-1">
            <StatusBadge status={task.status} />
            <PriorityBadge priority={task.priority} />
            {task.isMandatory && (
              <Badge className="bg-rose-50 text-rose-600"><Star className="w-2.5 h-2.5" />Required</Badge>
            )}
          </div>
          {/* Actions */}
          <div className="flex items-center gap-1 shrink-0">
            {canEdit && (
              <button onClick={() => onEdit(task)} title="Edit"
                className="p-1.5 rounded-lg hover:bg-slate-100 text-slate-400 hover:text-slate-700 transition-colors">
                <Pencil className="w-3.5 h-3.5" />
              </button>
            )}
            {canDelete && (
              <button onClick={() => onDelete(task)} title="Delete"
                className="p-1.5 rounded-lg hover:bg-red-50 text-slate-400 hover:text-red-600 transition-colors">
                <Trash2 className="w-3.5 h-3.5" />
              </button>
            )}
          </div>
        </div>

        {/* Title */}
        <h4 className="text-sm font-semibold text-slate-800 leading-snug mb-1">{task.title}</h4>
        {task.description && (
          <p className="text-xs text-slate-500 leading-relaxed line-clamp-2 mb-2">{task.description}</p>
        )}

        {/* Checklist progress */}
        {checklist.length > 0 && (
          <div className="mb-3">
            <div className="flex items-center justify-between text-[10px] text-slate-400 mb-1">
              <span>Checklist</span>
              <span>{checklistDone}/{checklist.length}</span>
            </div>
            <div className="w-full h-1 bg-slate-100 rounded-full overflow-hidden">
              <div
                className="h-full bg-blue-500 rounded-full transition-all"
                style={{ width: `${checklist.length ? (checklistDone / checklist.length) * 100 : 0}%` }}
              />
            </div>
          </div>
        )}

        {/* Completion % for items without checklist */}
        {checklist.length === 0 && task.completionPercentage > 0 && (
          <div className="mb-3">
            <div className="flex items-center justify-between text-[10px] text-slate-400 mb-1">
              <span>Progress</span><span>{task.completionPercentage}%</span>
            </div>
            <div className="w-full h-1 bg-slate-100 rounded-full overflow-hidden">
              <div className="h-full bg-emerald-500 rounded-full" style={{ width: `${task.completionPercentage}%` }} />
            </div>
          </div>
        )}

        {/* Meta row */}
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-slate-400">
          {assigneeName && (
            <span className="flex items-center gap-1">
              <User className="w-3 h-3" />{assigneeName}
            </span>
          )}
          {dueDateStr && (
            <span className={`flex items-center gap-1 ${isOverdue ? 'text-red-500 font-semibold' : ''}`}>
              <Clock className="w-3 h-3" />{dueDateStr}
            </span>
          )}
          {task.estimatedHours > 0 && (
            <span className="flex items-center gap-1">
              <MapPin className="w-3 h-3" />{task.estimatedHours}h est.
            </span>
          )}
          {task.tags?.length > 0 && (
            <span className="flex items-center gap-1">
              <Tag className="w-3 h-3" />{task.tags.slice(0, 2).join(', ')}
            </span>
          )}
        </div>

        {/* Status actions */}
        {canUpdateSts && task.taskType === 'TEAM_TASK' && task.status !== 'COMPLETED' && (
          <div className="mt-3 flex items-center gap-2 pt-3 border-t border-slate-100">
            <button
              onClick={() => onMarkComplete(task._id)}
              className="flex items-center gap-1.5 text-[11px] font-semibold text-emerald-600 hover:text-emerald-700 hover:bg-emerald-50 px-2.5 py-1 rounded-lg transition-colors"
            >
              <CheckCircle2 className="w-3.5 h-3.5" />Mark Complete
            </button>

            <div className="relative ml-auto">
              <button
                onClick={() => setStatusOpen(p => !p)}
                className="flex items-center gap-1 text-[11px] font-medium text-slate-500 hover:text-slate-700 hover:bg-slate-50 px-2.5 py-1 rounded-lg border border-slate-200 transition-colors"
              >
                Change Status <ChevronDown className="w-3 h-3" />
              </button>
              {statusOpen && (
                <div className="absolute right-0 bottom-full mb-1 z-20 bg-white rounded-xl shadow-lg border border-slate-200/80 py-1 min-w-[140px]">
                  {STATUSES.filter(s => s !== task.status).map(s => {
                    const cfg = STATUS_CFG[s];
                    return (
                      <button
                        key={s}
                        onClick={() => { onStatusChange(task._id, s); setStatusOpen(false); }}
                        className="w-full flex items-center gap-2 px-3 py-1.5 text-xs text-slate-700 hover:bg-slate-50 transition-colors"
                      >
                        <span className={`w-1.5 h-1.5 rounded-full ${cfg.dot}`} />
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
  );
}

// ─── SectionPanel ─────────────────────────────────────────────────────────────

function SectionPanel({ icon: Icon, label, sublabel, accentClass, tasks, emptyLabel, renderCard, headerAction }) {
  return (
    <div className="flex flex-col min-w-0">
      {/* Section header */}
      <div className={`rounded-xl p-4 mb-3 ${accentClass}`}>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-white/30 flex items-center justify-center">
              <Icon className="w-4 h-4 text-white" />
            </div>
            <div>
              <h3 className="font-bold text-white text-sm">{label}</h3>
              <p className="text-white/70 text-[11px]">{sublabel}</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <span className="bg-white/20 text-white text-[11px] font-bold px-2 py-0.5 rounded-full">
              {tasks.length}
            </span>
            {headerAction}
          </div>
        </div>
      </div>

      {/* Task list */}
      <div className="space-y-3 flex-1">
        {tasks.length === 0
          ? <SectionEmpty label={emptyLabel} />
          : tasks.map(task => renderCard(task))
        }
      </div>
    </div>
  );
}

// ─── WeeklyTaskModal ──────────────────────────────────────────────────────────

function WeeklyTaskModal({ isOpen, onClose, onSave, task, fixedTaskType, selectedWeek, teamMembers, loading }) {
  const isEdit = !!task;

  const [form, setForm] = useState({
    title: '',
    description: '',
    taskType: fixedTaskType || 'TEAM_TASK',
    priority: 'MEDIUM',
    startDate: '',
    dueDate: '',
    assigneeStudentId: '',
    estimatedHours: '',
    isMandatory: false,
    tags: '',
    checklist: [],
  });
  const [newCheckItem, setNewCheckItem] = useState('');

  useEffect(() => {
    if (task) {
      setForm({
        title: task.title || '',
        description: task.description || '',
        taskType: task.taskType || fixedTaskType || 'TEAM_TASK',
        priority: task.priority || 'MEDIUM',
        startDate: task.startDate ? task.startDate.slice(0, 10) : '',
        dueDate: task.dueDate ? task.dueDate.slice(0, 10) : '',
        assigneeStudentId: task.assigneeStudentId?._id || task.assigneeStudentId || '',
        estimatedHours: task.estimatedHours || '',
        isMandatory: task.isMandatory || false,
        tags: (task.tags || []).join(', '),
        checklist: task.checklist || [],
      });
    } else {
      setForm({
        title: '', description: '', taskType: fixedTaskType || 'TEAM_TASK',
        priority: 'MEDIUM', startDate: '', dueDate: '', assigneeStudentId: '',
        estimatedHours: '', isMandatory: false, tags: '', checklist: [],
      });
    }
    setNewCheckItem('');
  }, [task, isOpen, fixedTaskType]);

  if (!isOpen) return null;

  const set = (key, val) => setForm(p => ({ ...p, [key]: val }));

  const addCheckItem = () => {
    const t = newCheckItem.trim();
    if (!t) return;
    set('checklist', [...form.checklist, { text: t, isCompleted: false }]);
    setNewCheckItem('');
  };

  const removeCheckItem = (idx) => {
    set('checklist', form.checklist.filter((_, i) => i !== idx));
  };

  const toggleCheckItem = (idx) => {
    set('checklist', form.checklist.map((c, i) => i === idx ? { ...c, isCompleted: !c.isCompleted } : c));
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!form.title.trim()) { toast.error('Title is required'); return; }

    const today = new Date().toISOString().split('T')[0];

    if (form.startDate && form.startDate < today) {
      toast.error('Start date cannot be in the past');
      return;
    }

    if (form.dueDate && form.dueDate < today) {
      toast.error('Due date cannot be in the past');
      return;
    }

    if (form.startDate && form.dueDate && form.dueDate < form.startDate) {
      toast.error('Due date must be on or after start date');
      return;
    }

    const payload = {
      ...form,
      weekNumber: selectedWeek,
      tags: form.tags.split(',').map(t => t.trim()).filter(Boolean),
      estimatedHours: Number(form.estimatedHours) || 0,
      assigneeStudentId: form.assigneeStudentId || undefined,
      startDate: form.startDate || undefined,
      dueDate: form.dueDate || undefined,
    };
    onSave(payload);
  };

  const inputCls = 'w-full text-sm border border-slate-200 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500/30 focus:border-blue-400 transition-colors bg-white placeholder:text-slate-300';
  const labelCls = 'block text-xs font-semibold text-slate-600 mb-1';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-slate-900/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100">
          <div>
            <h2 className="text-base font-bold text-slate-900">
              {isEdit ? 'Edit Task' : `New ${form.taskType === 'CLASS_TASK' ? 'Class Requirement' : 'Team Task'}`}
            </h2>
            <p className="text-xs text-slate-400 mt-0.5">Week {selectedWeek}</p>
          </div>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-700 p-1">✕</button>
        </div>

        {/* Body */}
        <form onSubmit={handleSubmit} className="overflow-y-auto flex-1 px-6 py-4 space-y-4">
          {/* Title */}
          <div>
            <label className={labelCls}>Title *</label>
            <input className={inputCls} placeholder="Task title…" value={form.title}
              onChange={e => set('title', e.target.value)} required />
          </div>

          {/* Description */}
          <div>
            <label className={labelCls}>Description</label>
            <textarea className={`${inputCls} resize-none`} rows={3} placeholder="Optional description…"
              value={form.description} onChange={e => set('description', e.target.value)} />
          </div>

          {/* Priority + Assignee row */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={labelCls}>Priority</label>
              <select className={inputCls} value={form.priority} onChange={e => set('priority', e.target.value)}>
                {['LOW', 'MEDIUM', 'HIGH', 'CRITICAL'].map(p => (
                  <option key={p} value={p}>{PRIORITY_CFG[p].label}</option>
                ))}
              </select>
            </div>
            {form.taskType === 'TEAM_TASK' && teamMembers?.length > 0 && (
              <div>
                <label className={labelCls}>Assignee</label>
                <select className={inputCls} value={form.assigneeStudentId} onChange={e => set('assigneeStudentId', e.target.value)}>
                  <option value="">Unassigned</option>
                  {teamMembers.map(m => {
                    const id = m._id || m.studentId?._id || m.userId?._id;
                    const name = m.fullName || m.studentId?.fullName || m.userId?.name || id;
                    return <option key={id} value={id}>{name}</option>;
                  })}
                </select>
              </div>
            )}
          </div>

          {/* Due date + Estimated hours */}
          <div className="grid grid-cols-3 gap-3">
            <div>
              <label className={labelCls}>Start Date</label>
              <input type="date" className={inputCls} min={new Date().toISOString().split('T')[0]} value={form.startDate} onChange={e => set('startDate', e.target.value)} />
            </div>
            <div>
              <label className={labelCls}>Due Date</label>
              <input type="date" className={inputCls} min={form.startDate || new Date().toISOString().split('T')[0]} value={form.dueDate} onChange={e => set('dueDate', e.target.value)} />
            </div>
            <div>
              <label className={labelCls}>Estimated Hours</label>
              <input type="number" min="0" step="0.5" className={inputCls} placeholder="0"
                value={form.estimatedHours} onChange={e => set('estimatedHours', e.target.value)} />
            </div>
          </div>

          {/* Tags */}
          <div>
            <label className={labelCls}>Tags <span className="font-normal text-slate-400">(comma separated)</span></label>
            <input className={inputCls} placeholder="design, backend, mvp…" value={form.tags}
              onChange={e => set('tags', e.target.value)} />
          </div>

          {/* isMandatory */}
          <div className="flex items-center gap-3">
            <input type="checkbox" id="isMandatory" checked={form.isMandatory}
              onChange={e => set('isMandatory', e.target.checked)}
              className="w-4 h-4 rounded border-slate-300 text-blue-600 focus:ring-blue-500" />
            <label htmlFor="isMandatory" className="text-sm font-medium text-slate-700">Mark as Required</label>
          </div>

          {/* Checklist */}
          <div>
            <label className={labelCls}>Checklist</label>
            <div className="space-y-2 mb-2">
              {form.checklist.map((item, idx) => (
                <div key={idx} className="flex items-center gap-2">
                  <input type="checkbox" checked={item.isCompleted} onChange={() => toggleCheckItem(idx)}
                    className="w-3.5 h-3.5 rounded border-slate-300 text-blue-600" />
                  <span className={`flex-1 text-sm ${item.isCompleted ? 'line-through text-slate-400' : 'text-slate-700'}`}>
                    {item.text}
                  </span>
                  <button type="button" onClick={() => removeCheckItem(idx)}
                    className="text-slate-300 hover:text-red-500 text-xs transition-colors">✕</button>
                </div>
              ))}
            </div>
            <div className="flex gap-2">
              <input className={`${inputCls} flex-1`} placeholder="Add checklist item…"
                value={newCheckItem} onChange={e => setNewCheckItem(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && (e.preventDefault(), addCheckItem())} />
              <button type="button" onClick={addCheckItem}
                className="px-3 py-2 text-xs font-semibold bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-lg transition-colors">
                Add
              </button>
            </div>
          </div>
        </form>

        {/* Footer */}
        <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-slate-100">
          <button type="button" onClick={onClose}
            className="px-4 py-2 text-sm font-semibold text-slate-600 hover:text-slate-800 hover:bg-slate-50 rounded-lg transition-colors">
            Cancel
          </button>
          <button onClick={handleSubmit} disabled={loading}
            className="flex items-center gap-2 px-5 py-2 text-sm font-semibold bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 transition-colors shadow-sm">
            {loading && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
            {isEdit ? 'Save Changes' : 'Create Task'}
          </button>
        </div>
      </div>
    </div>
  );
}

// ─── WeeklyRoadmapPlanner (main) ──────────────────────────────────────────────

export default function WeeklyRoadmapPlanner({
  courseCode,
  classId,
  teamId,
  currentUser,
  roleInTeam = '',
  teamMembers = [],
  isReadOnly = false,
}) {
  // ── Permissions ────────────────────────────────────────────
  const role = currentUser?.role?.toUpperCase() || '';

  const isAdmin    = role === 'ADMIN';
  const isLecturer = role === 'LECTURER';
  const isMentor   = role === 'MENTOR';
  const isTeamMemberContext = !!teamId && (role === 'STUDENT' || role === 'USER');

  const canCreateTeamTask  = !isReadOnly && (isAdmin || isLecturer || isMentor || isTeamMemberContext);
  const canCreateClassTask = !isReadOnly && (isAdmin || isLecturer);

  const canEditTask = (task) => {
    if (isReadOnly) return false;
    if (!task) return false;
    if (task.taskType === 'COURSE_TEMPLATE') return isAdmin;
    if (task.taskType === 'CLASS_TASK')      return isAdmin || isLecturer;
    if (task.taskType === 'TEAM_TASK')       return isAdmin || isLecturer || isMentor || isTeamMemberContext;
    return false;
  };
  const canDeleteTask = (task) => {
    if (isReadOnly) return false;
    if (!task) return false;
    
    const createdById = String(task.createdBy?._id || task.createdBy || '');
    const currentUserId = String(currentUser?._id || '');

    if (task.taskType === 'COURSE_TEMPLATE') return isAdmin;
    if (task.taskType === 'CLASS_TASK')      return isAdmin || isLecturer;
    if (task.taskType === 'TEAM_TASK') {
      if (isAdmin || isLecturer || isMentor) return true;
      if (!isTeamMemberContext) return false;
      return createdById === currentUserId;
    }
    return false;
  };
  const canUpdateStatus  = (task) => {
    if (isReadOnly) return false;
    if (!task) return false;
    if (task.taskType === 'COURSE_TEMPLATE') return isAdmin;
    if (task.taskType === 'CLASS_TASK')      return isAdmin || isLecturer;
    if (task.taskType === 'TEAM_TASK')       return isAdmin || isLecturer || isMentor || isTeamMemberContext;
    return false;
  };

  // ── State ──────────────────────────────────────────────────
  const [selectedWeek, setSelectedWeek] = useState(() => {
    const stored = localStorage.getItem('selectedRoadmapWeek');
    const n = Number(stored);
    return n >= 1 && n <= 10 ? n : 1;
  });
  const [statusFilter,   setStatusFilter]   = useState('ALL');
  const [assigneeFilter, setAssigneeFilter] = useState('ALL');

  const [courseTasks, setCourseTasks] = useState([]);
  const [classTasks,  setClassTasks]  = useState([]);
  const [teamTasks,   setTeamTasks]   = useState([]);

  const [loading,       setLoading]       = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [error,         setError]         = useState(null);

  // Modal
  const [modalOpen,    setModalOpen]    = useState(false);
  const [editingTask,  setEditingTask]  = useState(null);
  const [modalType,    setModalType]    = useState('TEAM_TASK');
  const [deleteTarget, setDeleteTarget] = useState(null);

  // ── Fetch ──────────────────────────────────────────────────
  const fetchTasks = useCallback(async () => {
    if (!courseCode) return;
    try {
      setError(null);
      const params = { courseCode, weekNumber: selectedWeek };
      if (classId) params.classId = classId;
      if (teamId)  params.teamId  = teamId;
      const res = await getWeeklyTasks(params);
      // Backend wraps: { success, data: { courseTasks, classTasks, teamTasks } }
      // Axios may or may not have already unwrapped one layer, so double-unwrap safely.
      const raw = res?.data ?? res;
      const d   = raw?.data ?? raw;
      setCourseTasks(d.courseTasks || []);
      setClassTasks(d.classTasks   || []);
      setTeamTasks(d.teamTasks     || []);
    } catch (err) {
      const msg = err?.message || 'Failed to load tasks';
      setError(msg);
      toast.error(msg);
    } finally {
      setLoading(false);
    }
  }, [courseCode, classId, teamId, selectedWeek]);

  useEffect(() => {
    setLoading(true);
    fetchTasks();
  }, [fetchTasks]);

  // ── Handlers ───────────────────────────────────────────────
  const handleWeekChange = (w) => {
    const n = Number(w);
    setSelectedWeek(n);
    localStorage.setItem('selectedRoadmapWeek', String(n));
  };

  const openCreate = (type) => { setEditingTask(null); setModalType(type); setModalOpen(true); };
  const openEdit   = (task)  => { setEditingTask(task); setModalType(task.taskType); setModalOpen(true); };
  const closeModal = ()      => { setModalOpen(false); setEditingTask(null); };

  const getTasksByType = useCallback((taskType) => {
    if (taskType === 'TEAM_TASK') return teamTasks;
    if (taskType === 'CLASS_TASK') return classTasks;
    return courseTasks;
  }, [classTasks, courseTasks, teamTasks]);

  const hasDuplicateTaskInSelectedWeek = useCallback((payload, excludeTaskId) => {
    const titleNormalized = normalizeTaskTitle(payload.title);
    if (!titleNormalized) return false;

    return getTasksByType(payload.taskType).some((task) => {
      if (excludeTaskId && String(task._id) === String(excludeTaskId)) return false;
      return Number(task.weekNumber) === Number(selectedWeek)
        && normalizeTaskTitle(task.title) === titleNormalized;
    });
  }, [getTasksByType, selectedWeek]);

  const handleSave = async (formData) => {
    const scope = formData.taskType === 'TEAM_TASK' ? 'TEAM'
                : formData.taskType === 'CLASS_TASK' ? 'CLASS' : 'COURSE';
    const payload = {
      ...formData,
      scope,
      courseCode,
      weekNumber: selectedWeek,
      classId: formData.taskType !== 'COURSE_TEMPLATE' ? classId : undefined,
      teamId:  formData.taskType === 'TEAM_TASK'       ? teamId  : undefined,
    };

    if (hasDuplicateTaskInSelectedWeek(payload, editingTask?._id)) {
      toast.error(DUPLICATE_TASK_MESSAGE);
      return;
    }

    try {
      setActionLoading(true);
      if (editingTask) {
        await updateWeeklyTask(editingTask._id, payload);
        toast.success('Task updated successfully');
      } else {
        await createWeeklyTask(payload);
        toast.success('Task created successfully');
      }
      closeModal();
      await fetchTasks();
    } catch (err) {
      toast.error(getTaskSaveErrorMessage(err));
    } finally {
      setActionLoading(false);
    }
  };

  const handleDelete = (task) => {
    setDeleteTarget(task);
  };

  const handleDeleteConfirm = async () => {
    if (!deleteTarget?._id) return;
    try {
      setActionLoading(true);
      await deleteWeeklyTask(deleteTarget._id);
      toast.success('Task deleted');
      setDeleteTarget(null);
      await fetchTasks();
    } catch (err) {
      toast.error(err?.message || 'Failed to delete task');
    } finally {
      setActionLoading(false);
    }
  };

  const handleStatusChange = async (taskId, status) => {
    try {
      setActionLoading(true);
      await updateWeeklyTaskStatus(taskId, { status });
      toast.success('Status updated');
      await fetchTasks();
    } catch (err) {
      toast.error(err?.message || 'Failed to update status');
    } finally {
      setActionLoading(false);
    }
  };

  const handleMarkComplete = (taskId) => handleStatusChange(taskId, 'COMPLETED');

  // ── Filtered team tasks ────────────────────────────────────
  const filteredTeamTasks = teamTasks.filter(t => {
    if (statusFilter !== 'ALL' && t.status !== statusFilter) return false;
    if (assigneeFilter !== 'ALL') {
      const aid = t.assigneeStudentId?._id || t.assigneeStudentId;
      if (aid !== assigneeFilter) return false;
    }
    return true;
  });

  // ── Render card helper ─────────────────────────────────────
  const renderCard = (task) => (
    <TaskCard
      key={task._id}
      task={task}
      canEdit={canEditTask(task)}
      canDelete={canDeleteTask(task)}
      canUpdateSts={canUpdateStatus(task)}
      onEdit={openEdit}
      onDelete={handleDelete}
      onStatusChange={handleStatusChange}
      onMarkComplete={handleMarkComplete}
    />
  );

  // ─── Loading ───────────────────────────────────────────────
  if (loading) {
    return (
      <div className="space-y-4">
        {/* Header skeleton */}
        <div className="bg-white rounded-2xl border border-slate-200/60 p-5 animate-pulse">
          <div className="h-6 w-48 bg-slate-100 rounded-lg mb-2" />
          <div className="h-4 w-72 bg-slate-100 rounded-lg" />
        </div>
        {/* Columns skeleton */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
          {[1,2,3].map(i => (
            <div key={i} className="bg-slate-100 rounded-xl h-64 animate-pulse" />
          ))}
        </div>
      </div>
    );
  }

  // ─── Error ─────────────────────────────────────────────────
  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-2xl p-8 text-center">
        <AlertTriangle className="w-8 h-8 text-red-400 mx-auto mb-3" />
        <p className="text-red-600 font-semibold text-sm">{error}</p>
        <button onClick={() => { setLoading(true); fetchTasks(); }}
          className="mt-3 text-sm text-red-600 hover:text-red-800 font-medium underline">
          Try again
        </button>
      </div>
    );
  }

  // ─── Main render ───────────────────────────────────────────
  return (
    <div className="space-y-5">
      {/* ── Header ─────────────────────────────────────────── */}
      <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-5">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          {/* Title */}
          <div>
            <h2 className="text-lg font-bold text-slate-900 flex items-center gap-2">
              <MapPin className="w-5 h-5 text-blue-500" />
              Weekly Roadmap
            </h2>
            <p className="text-sm text-slate-500 mt-0.5">
              Course roadmap, class requirements, and team execution tasks
            </p>
          </div>

          {/* Controls */}
          <div className="flex flex-wrap items-center gap-2">
            {/* Week selector */}
            <div className="flex items-center gap-1.5 bg-slate-50 border border-slate-200 rounded-xl px-3 py-2">
              <BookOpen className="w-3.5 h-3.5 text-slate-500" />
              <select
                value={selectedWeek}
                onChange={e => handleWeekChange(e.target.value)}
                className="text-sm font-semibold text-slate-700 bg-transparent focus:outline-none cursor-pointer"
              >
                {WEEKS.map(w => <option key={w} value={w}>Week {w}</option>)}
              </select>
            </div>

            {/* Status filter */}
            <select
              value={statusFilter}
              onChange={e => setStatusFilter(e.target.value)}
              className="text-sm border border-slate-200 rounded-xl px-3 py-2 bg-white text-slate-600 focus:outline-none focus:ring-2 focus:ring-blue-500/30 cursor-pointer"
            >
              <option value="ALL">All Status</option>
              {STATUSES.map(s => <option key={s} value={s}>{STATUS_CFG[s].label}</option>)}
            </select>

            {/* Assignee filter */}
            {teamMembers.length > 0 && (
              <select
                value={assigneeFilter}
                onChange={e => setAssigneeFilter(e.target.value)}
                className="text-sm border border-slate-200 rounded-xl px-3 py-2 bg-white text-slate-600 focus:outline-none focus:ring-2 focus:ring-blue-500/30 cursor-pointer"
              >
                <option value="ALL">All Members</option>
                {teamMembers.map(m => {
                  const id = m._id || m.studentId?._id || m.userId?._id;
                  const name = m.fullName || m.studentId?.fullName || m.userId?.name || 'Member';
                  return <option key={id} value={id}>{name}</option>;
                })}
              </select>
            )}

            {/* Action buttons */}
            {canCreateTeamTask && (
              <button
                disabled={actionLoading}
                onClick={() => openCreate('TEAM_TASK')}
                className="flex items-center gap-1.5 px-3.5 py-2 text-sm font-semibold bg-blue-600 text-white rounded-xl hover:bg-blue-700 disabled:opacity-50 transition-colors shadow-sm"
              >
                <Plus className="w-3.5 h-3.5" />Add Team Task
              </button>
            )}
            {canCreateClassTask && (
              <button
                disabled={actionLoading}
                onClick={() => openCreate('CLASS_TASK')}
                className="flex items-center gap-1.5 px-3.5 py-2 text-sm font-semibold bg-emerald-600 text-white rounded-xl hover:bg-emerald-700 disabled:opacity-50 transition-colors shadow-sm"
              >
                <Plus className="w-3.5 h-3.5" />Add Class Task
              </button>
            )}
          </div>
        </div>

        {/* Week pills */}
        <div className="mt-4 flex flex-wrap gap-1.5">
          {WEEKS.map(w => (
            <button
              key={w}
              onClick={() => handleWeekChange(w)}
              className={`text-xs font-semibold px-3 py-1 rounded-full transition-colors ${
                selectedWeek === w
                  ? 'bg-blue-600 text-white shadow-sm'
                  : 'bg-slate-100 text-slate-500 hover:bg-slate-200'
              }`}
            >
              W{w}
            </button>
          ))}
        </div>
      </div>

      {/* ── Three sections ─────────────────────────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5 items-start">

        {/* A. Course Roadmap */}
        <SectionPanel
          icon={BookOpen}
          label="Course Roadmap"
          sublabel="Syllabus requirements — read only"
          accentClass="bg-gradient-to-br from-violet-600 to-violet-700"
          tasks={courseTasks}
          emptyLabel="course tasks"
          renderCard={task => (
            <TaskCard
              key={task._id}
              task={task}
              canEdit={isAdmin}
              canDelete={isAdmin}
              canUpdateSts={isAdmin}
              onEdit={openEdit}
              onDelete={handleDelete}
              onStatusChange={handleStatusChange}
              onMarkComplete={handleMarkComplete}
            />
          )}
        />

        {/* B. Class Requirements */}
        <SectionPanel
          icon={ClipboardList}
          label="Class Requirements"
          sublabel="Instructor assignments for this class"
          accentClass="bg-gradient-to-br from-emerald-500 to-emerald-700"
          tasks={classTasks}
          emptyLabel="class tasks"
          renderCard={task => (
            <TaskCard
              key={task._id}
              task={task}
              canEdit={isAdmin || isLecturer}
              canDelete={isAdmin || isLecturer}
              canUpdateSts={isAdmin || isLecturer}
              onEdit={openEdit}
              onDelete={handleDelete}
              onStatusChange={handleStatusChange}
              onMarkComplete={handleMarkComplete}
            />
          )}
        />

        {/* C. Team Tasks */}
        <SectionPanel
          icon={Users}
          label="Team Tasks"
          sublabel="Your team's execution tasks"
          accentClass="bg-gradient-to-br from-blue-500 to-blue-700"
          tasks={filteredTeamTasks}
          emptyLabel="team tasks"
          renderCard={renderCard}
          headerAction={
            actionLoading && <Loader2 className="w-3.5 h-3.5 text-white/60 animate-spin" />
          }
        />
      </div>

      {/* ── Modal ──────────────────────────────────────────── */}
      <WeeklyTaskModal
        isOpen={modalOpen}
        onClose={closeModal}
        onSave={handleSave}
        task={editingTask}
        fixedTaskType={modalType}
        selectedWeek={selectedWeek}
        teamMembers={teamMembers}
        loading={actionLoading}
      />

      <ConfirmDialog
        isOpen={Boolean(deleteTarget)}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleDeleteConfirm}
        title="Delete Task"
        description={
          deleteTarget?.title
            ? `Are you sure you want to delete "${deleteTarget.title}"? This action cannot be undone.`
            : 'Are you sure you want to delete this task? This action cannot be undone.'
        }
        confirmText="Delete Task"
        isSubmitting={actionLoading}
      />
    </div>
  );
}
