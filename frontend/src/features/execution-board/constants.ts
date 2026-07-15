// @ts-nocheck
export const WEEKS = Array.from({ length: 10 }, (_, index) => index + 1);

export const STATUSES = ['TODO', 'IN_PROGRESS', 'REVIEW', 'COMPLETED', 'OVERDUE'];

export const WORKFLOW_STATUSES = ['TODO', 'IN_PROGRESS', 'REVIEW', 'COMPLETED'];

export const EMPTY_GROUPED = {
  TODO: [],
  IN_PROGRESS: [],
  REVIEW: [],
  COMPLETED: [],
  OVERDUE: [],
};

export const STATUS_CFG = {
  TODO: {
    label: 'To Do',
    bg: 'bg-slate-100',
    text: 'text-slate-600',
    dot: 'bg-slate-400',
    border: 'border-slate-200',
    accent: 'bg-slate-400',
  },
  IN_PROGRESS: {
    label: 'In Progress',
    bg: 'bg-blue-50',
    text: 'text-blue-700',
    dot: 'bg-blue-500',
    border: 'border-blue-200',
    accent: 'bg-blue-500',
  },
  REVIEW: {
    label: 'Review',
    bg: 'bg-violet-50',
    text: 'text-violet-700',
    dot: 'bg-violet-500',
    border: 'border-violet-200',
    accent: 'bg-violet-500',
  },
  COMPLETED: {
    label: 'Completed',
    bg: 'bg-emerald-50',
    text: 'text-emerald-700',
    dot: 'bg-emerald-500',
    border: 'border-emerald-200',
    accent: 'bg-emerald-500',
  },
  OVERDUE: {
    label: 'Overdue',
    bg: 'bg-red-50',
    text: 'text-red-700',
    dot: 'bg-red-500',
    border: 'border-red-200',
    accent: 'bg-red-500',
  },
};

export const PRIORITY_CFG = {
  LOW: { label: 'Low', bg: 'bg-slate-100', text: 'text-slate-600' },
  MEDIUM: { label: 'Medium', bg: 'bg-blue-50', text: 'text-blue-700' },
  HIGH: { label: 'High', bg: 'bg-amber-50', text: 'text-amber-700' },
  CRITICAL: { label: 'Critical', bg: 'bg-red-50', text: 'text-red-700' },
};
