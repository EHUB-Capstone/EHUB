// @ts-nocheck
import { EMPTY_GROUPED, STATUSES } from './constants';

export const normalizeBoardResponse = (response) => {
  const data = response?.data || response || {};
  return {
    tasks: data.tasks || [],
    grouped: data.grouped || groupTasks(data.tasks || []),
    summary: data.summary || summarizeTasks(data.tasks || []),
  };
};

export const getTaskStatus = (task) => task?.computedStatus || task?.status || 'TODO';

export const groupTasks = (tasks = []) => {
  const grouped = {
    TODO: [],
    IN_PROGRESS: [],
    REVIEW: [],
    COMPLETED: [],
    OVERDUE: [],
  };

  tasks.forEach((task) => {
    const status = getTaskStatus(task);
    if (grouped[status]) grouped[status].push(task);
    else grouped.TODO.push(task);
  });

  return grouped;
};

export const summarizeTasks = (tasks = []) => {
  const summary = {
    total: tasks.length,
    todo: 0,
    inProgress: 0,
    review: 0,
    completed: 0,
    overdue: 0,
    completionPercentage: 0,
  };

  let progress = 0;

  tasks.forEach((task) => {
    const status = getTaskStatus(task);
    if (status === 'TODO') summary.todo += 1;
    if (status === 'IN_PROGRESS') summary.inProgress += 1;
    if (status === 'REVIEW') summary.review += 1;
    if (status === 'COMPLETED') summary.completed += 1;
    if (status === 'OVERDUE') summary.overdue += 1;
    progress += Number(task.completionPercentage || 0);
  });

  if (tasks.length > 0) {
    summary.completionPercentage = Math.round(progress / tasks.length);
  }

  return summary;
};

export const buildBoard = (tasks = []) => ({
  tasks,
  grouped: groupTasks(tasks),
  summary: summarizeTasks(tasks),
});

export const normalizeFilters = (filters) => {
  const params = {};
  if (filters.week !== 'ALL') params.weekNumber = filters.week;
  if (filters.assignee !== 'ALL') params.assigneeStudentId = filters.assignee;
  if (filters.priority !== 'ALL') params.priority = filters.priority;
  if (filters.search.trim()) params.search = filters.search.trim();
  return params;
};

export const updateTaskInBoard = (board, taskId, updater) => {
  if (!board) return board;

  const tasks = board.tasks.map((task) => (
    task._id === taskId ? updater(task) : task
  ));

  return buildBoard(tasks);
};

const replaceGroupedTask = (grouped, status, taskId, nextTask) => {
  const list = grouped[status] || [];
  return list.map((task) => (task._id === taskId ? nextTask : task));
};

export const moveTaskStatusInBoard = (board, taskId, nextStatus) => {
  if (!board) return board;

  const currentTask = board.tasks.find((task) => task._id === taskId);
  if (!currentTask) return board;

  const previousStatus = getTaskStatus(currentTask);
  const nextTask = {
    ...currentTask,
    status: nextStatus,
    computedStatus: nextStatus,
    completionPercentage: nextStatus === 'COMPLETED' ? 100 : currentTask.completionPercentage,
    checklist: nextStatus === 'COMPLETED'
      ? (currentTask.checklist || []).map((item) => ({ ...item, isCompleted: true }))
      : currentTask.checklist,
  };

  const tasks = board.tasks.map((task) => (task._id === taskId ? nextTask : task));

  if (previousStatus === nextStatus) {
    return {
      tasks,
      grouped: {
        ...board.grouped,
        [nextStatus]: replaceGroupedTask(board.grouped, nextStatus, taskId, nextTask),
      },
      summary: summarizeTasks(tasks),
    };
  }

  const previousList = (board.grouped?.[previousStatus] || []).filter((task) => task._id !== taskId);
  const nextList = [nextTask, ...(board.grouped?.[nextStatus] || [])];

  return {
    tasks,
    grouped: {
      ...board.grouped,
      [previousStatus]: previousList,
      [nextStatus]: nextList,
    },
    summary: summarizeTasks(tasks),
  };
};

export const replaceTaskInBoard = (board, taskId, nextTask) => {
  if (!board) return board;
  return buildBoard(board.tasks.map((task) => (task._id === taskId ? nextTask : task)));
};

export const patchTaskInBoard = (board, taskId, nextTask) => {
  if (!board) return board;
  const previousTask = board.tasks.find((task) => task._id === taskId);
  if (!previousTask) return board;

  const previousStatus = getTaskStatus(previousTask);
  const nextStatus = getTaskStatus(nextTask);
  const tasks = board.tasks.map((task) => (task._id === taskId ? nextTask : task));

  if (previousStatus !== nextStatus) {
    return {
      tasks,
      grouped: {
        ...board.grouped,
        [previousStatus]: (board.grouped?.[previousStatus] || []).filter((task) => task._id !== taskId),
        [nextStatus]: [nextTask, ...(board.grouped?.[nextStatus] || [])],
      },
      summary: summarizeTasks(tasks),
    };
  }

  return {
    tasks,
    grouped: {
      ...board.grouped,
      [nextStatus]: replaceGroupedTask(board.grouped, nextStatus, taskId, nextTask),
    },
    summary: summarizeTasks(tasks),
  };
};

export const removeTaskFromBoard = (board, taskId) => {
  if (!board) return board;
  return buildBoard(board.tasks.filter((task) => task._id !== taskId));
};

export const prependTaskToBoard = (board, task) => {
  const current = board || { tasks: [], grouped: EMPTY_GROUPED, summary: null };
  return buildBoard([task, ...current.tasks]);
};

export const taskMatchesFilters = (task, filters) => {
  if (!task) return false;
  if (filters.week !== 'ALL' && Number(task.weekNumber) !== Number(filters.week)) return false;
  if (filters.assignee !== 'ALL') {
    const assigneeId = task.assigneeStudentId?._id || task.assigneeStudentId || '';
    if (String(assigneeId) !== String(filters.assignee)) return false;
  }
  if (filters.priority !== 'ALL' && task.priority !== filters.priority) return false;
  if (filters.search.trim()) {
    const needle = filters.search.trim().toLowerCase();
    const haystack = `${task.title || ''} ${task.description || ''}`.toLowerCase();
    if (!haystack.includes(needle)) return false;
  }
  return true;
};

export const upsertTaskForFilters = (board, task, filters) => {
  if (!board) return taskMatchesFilters(task, filters) ? buildBoard([task]) : board;

  const exists = board.tasks.some((current) => current._id === task._id);
  if (!taskMatchesFilters(task, filters)) {
    return removeTaskFromBoard(board, task._id);
  }

  if (exists) return replaceTaskInBoard(board, task._id, task);
  return prependTaskToBoard(board, task);
};

export const createOptimisticTask = ({ payload, teamId, user, teamMembers }) => {
  const assignee = teamMembers.find((member) => {
    const id = member._id || member.studentId?._id || member.userId?._id;
    return id === payload.assigneeStudentId;
  });

  return {
    _id: `temp-${Date.now()}`,
    ...payload,
    teamId,
    status: 'TODO',
    computedStatus: 'TODO',
    completionPercentage: payload.checklist?.length ? 0 : 0,
    createdBy: user ? { _id: user._id, name: user.name, email: user.email } : null,
    assigneeStudentId: assignee || null,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  };
};

export const boardHasVisibleTasks = (board) =>
  STATUSES.some((status) => (board?.grouped?.[status] || []).length > 0);
