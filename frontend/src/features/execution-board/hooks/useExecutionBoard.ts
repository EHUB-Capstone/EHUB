// @ts-nocheck
import { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { teamWorkspaceApi } from '../../../api/teamWorkspaceApi';
import { weeklyTaskSavePayload } from '../../../utils/weeklyTaskPayload';
import { teamApi } from '../../../api/teamApi';
import { classFeatureFlags } from '../../../config/classFeatureFlags';
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
    queryKey: ['execution-board', 'team-context', role, queryTeamId, user?.id || user?._id],
    enabled: Boolean(user),
    staleTime: 60_000,
    queryFn: async () => {
      if (queryTeamId) return queryTeamId;
      if (role === 'STUDENT' || role === 'USER') {
        const response = await teamWorkspaceApi.getCurrentWorkspace();
        return response.data?.selectedWorkspace?.teamId || null;
      }

      return queryTeamId || null;
    },
  });
}

export function useTeamMembers(teamId) {
  return useQuery({
    queryKey: ['execution-board', 'team-members', teamId],
    enabled: Boolean(teamId) && classFeatureFlags.teamManagement,
    staleTime: 5 * 60_000,
    queryFn: async ({ signal }) => {
      const response = await teamApi.getById(teamId, { signal });
      const team = extractTeam(response);
      return (team?.members || []).map((member) => ({ ...member, _id: member.studentId?._id || member.studentId || member._id }));
    },
  });
}

export function useTaskBoard({ teamId, filters }) {
  const params = useMemo(() => normalizeFilters(filters), [filters]);

  return useQuery({
    queryKey: ['execution-board', 'task-board', teamId, params],
    enabled: Boolean(teamId),
    staleTime: 30_000,
    queryFn: async ({ signal }) => {
      const response = await getTeamTaskBoard(teamId, params, { signal });
      return normalizeBoardResponse(response);
    },
  });
}

export function useTaskMutations({ boardKey, teamId, courseCode, classId, filters, onCloseModal }) {
  const queryClient = useQueryClient();
  const refresh = () => Promise.all([
    queryClient.invalidateQueries({ queryKey: ['execution-board', 'task-board'] }),
    queryClient.invalidateQueries({ queryKey: ['workspace', 'weekly-roadmap'] }),
  ]);

  const saveTask = useMutation({
    mutationFn: ({ task, payload }) => {
      const request = weeklyTaskSavePayload(task, { ...payload, teamId, classId, courseCode, taskType: 'TEAM_TASK', scope: 'TEAM' });
      return task ? updateWeeklyTask(task._id, request) : createWeeklyTask(request);
    },
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
    onSettled: refresh,
  });

  const changeStatus = useMutation({
    onSettled: refresh,
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
    onSettled: refresh,
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
