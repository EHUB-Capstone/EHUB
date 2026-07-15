// @ts-nocheck
import { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { dashboardApi } from '../../../api/dashboardApi';
import { teamApi } from '../../../api/teamApi';
import {
  createWeeklyTask,
  deleteWeeklyTask,
  getTeamTaskBoard,
  updateWeeklyTask,
  updateWeeklyTaskStatus,
} from '../../../api/weeklyTaskApi';
import {
  moveTaskStatusInBoard,
  normalizeBoardResponse,
  normalizeFilters,
  patchTaskInBoard,
  removeTaskFromBoard,
  replaceTaskInBoard,
  upsertTaskForFilters,
} from '../boardUtils';

const extractTeam = (response) => response?.data?.team || response?.team || response?.data || null;
const duplicateTaskMessage = 'Duplicate task: A task with this title already exists in this week.';
const getTaskErrorMessage = (error, fallback) => {
  if (error?.status === 409 || error?.message === 'Duplicate task') return duplicateTaskMessage;
  return error?.message || fallback;
};

export function useTeamContext({ user, queryTeamId }) {
  const role = user?.role?.toUpperCase() || '';

  return useQuery({
    queryKey: ['execution-board', 'team-context', role, queryTeamId, user?._id],
    enabled: Boolean(user),
    staleTime: 60_000,
    queryFn: async ({ signal }) => {
      if (role === 'STUDENT' || role === 'USER') {
        const response = await dashboardApi.getStudent(undefined, { signal });
        const data = response.data || response;
        return data.team?._id || null;
      }

      return queryTeamId || null;
    },
  });
}

export function useTeamMembers(teamId) {
  return useQuery({
    queryKey: ['execution-board', 'team-members', teamId],
    enabled: Boolean(teamId),
    staleTime: 5 * 60_000,
    queryFn: async ({ signal }) => {
      const response = await teamApi.getById(teamId, { signal });
      const team = extractTeam(response);
      return team?.members || [];
    },
  });
}

export function useTaskBoard({ teamId, filters }) {
  const params = useMemo(() => normalizeFilters(filters), [filters]);

  return useQuery({
    queryKey: ['execution-board', 'task-board', teamId, params],
    enabled: Boolean(teamId),
    staleTime: 30_000,
    placeholderData: (previous) => previous,
    queryFn: async ({ signal }) => {
      const response = await getTeamTaskBoard(teamId, params, { signal });
      return normalizeBoardResponse(response);
    },
  });
}

export function useTaskMutations({ boardKey, teamId, filters, onCloseModal }) {
  const queryClient = useQueryClient();

  const saveTask = useMutation({
    mutationFn: ({ task, payload }) => (
      task ? updateWeeklyTask(task._id, payload) : createWeeklyTask({ ...payload, teamId })
    ),
    onMutate: async ({ task, payload }) => {
      await queryClient.cancelQueries({ queryKey: boardKey });
      const previous = queryClient.getQueryData(boardKey);

      if (task) {
        queryClient.setQueryData(boardKey, (board) =>
          upsertTaskForFilters(board, { ...task, ...payload }, filters)
        );
      }

      return { previous };
    },
    onSuccess: (response, variables) => {
      const savedTask = response?.data?.task || response?.task || response?.data;
      if (savedTask) {
        const idToReplace = variables.task?._id;
        queryClient.setQueryData(boardKey, (board) => (
          idToReplace
            ? upsertTaskForFilters(replaceTaskInBoard(board, idToReplace, savedTask), savedTask, filters)
            : upsertTaskForFilters(board, savedTask, filters)
        ));
      }
      onCloseModal();
      toast.success(variables.task ? 'Task updated' : 'Task created');
    },
    onError: (error, _variables, context) => {
      if (context?.previous) queryClient.setQueryData(boardKey, context.previous);
      toast.error(getTaskErrorMessage(error, 'Failed to save task'));
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: boardKey, exact: true, refetchType: 'inactive' });
    },
  });

  const changeStatus = useMutation({
    mutationFn: ({ taskId, status }) => updateWeeklyTaskStatus(taskId, { status }),
    onMutate: async ({ taskId, status }) => {
      await queryClient.cancelQueries({ queryKey: boardKey });
      const previous = queryClient.getQueryData(boardKey);
      queryClient.setQueryData(boardKey, (board) => moveTaskStatusInBoard(board, taskId, status));
      return { previous };
    },
    onSuccess: (response, variables) => {
      const savedTask = response?.data?.task || response?.task || response?.data;
      if (savedTask) {
        queryClient.setQueryData(boardKey, (board) => patchTaskInBoard(board, variables.taskId, savedTask));
      }
      toast.success('Status updated');
    },
    onError: (error, _variables, context) => {
      if (context?.previous) queryClient.setQueryData(boardKey, context.previous);
      toast.error(error.message || 'Failed to update status');
    },
  });

  const removeTask = useMutation({
    mutationFn: (task) => deleteWeeklyTask(task._id),
    onMutate: async (task) => {
      await queryClient.cancelQueries({ queryKey: boardKey });
      const previous = queryClient.getQueryData(boardKey);
      queryClient.setQueryData(boardKey, (board) => removeTaskFromBoard(board, task._id));
      return { previous };
    },
    onSuccess: () => {
      toast.success('Task deleted');
    },
    onError: (error, _task, context) => {
      if (context?.previous) queryClient.setQueryData(boardKey, context.previous);
      toast.error(error.message || 'Failed to delete task');
    },
  });

  return {
    saveTask,
    changeStatus,
    removeTask,
    isMutating: saveTask.isPending || changeStatus.isPending || removeTask.isPending,
  };
}
