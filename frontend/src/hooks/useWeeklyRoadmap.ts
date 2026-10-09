import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createWeeklyTask, deleteWeeklyTask, getWeeklyTasks, updateWeeklyTask, updateWeeklyTaskStatus } from '../api/weeklyTaskApi';
import type { SaveWeeklyTaskPayload, WeeklyTask, WeeklyTaskQuery, WeeklyTaskStatus } from '../types/workspaceTools';
import { useAuth } from './useAuth';
import { weeklyTaskSavePayload } from '../utils/weeklyTaskPayload';

export function useWeeklyRoadmap(params: WeeklyTaskQuery) {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const queryKey = ['workspace', 'weekly-roadmap', user?.id, params] as const;
  const query = useQuery({
    queryKey,
    enabled: Boolean(params.courseCode && user),
    staleTime: 0,
    queryFn: async () => (await getWeeklyTasks(params)).data,
  });
  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['workspace', 'weekly-roadmap'] }),
      queryClient.invalidateQueries({ queryKey: ['execution-board'] }),
    ]);
  };
  const save = useMutation({
    mutationFn: ({ task, payload }: { task: WeeklyTask | null; payload: SaveWeeklyTaskPayload }) => {
      const request = weeklyTaskSavePayload(task, payload);
      return task ? updateWeeklyTask(task._id, request) : createWeeklyTask(request);
    },
    onSuccess: refresh,
  });
  const remove = useMutation({ mutationFn: (taskId: string) => deleteWeeklyTask(taskId), onSuccess: refresh });
  const changeStatus = useMutation({ mutationFn: ({ taskId, status }: { taskId: string; status: WeeklyTaskStatus }) => updateWeeklyTaskStatus(taskId, { status }), onSuccess: refresh });
  return { ...query, save, remove, changeStatus, isMutating: save.isPending || remove.isPending || changeStatus.isPending };
}
