import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { shortcutApi } from '../api/shortcutApi';
import type { ProjectShortcut, SaveShortcutPayload } from '../types/workspaceTools';
import { parseApiError } from '../utils/apiError';
import { useAuth } from './useAuth';

const shortcutKey = (teamId: string) => ['workspace', teamId, 'shortcuts'] as const;

export function useWorkspaceShortcuts(teamId: string) {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const queryKey = [...shortcutKey(teamId), user?.id] as const;
  const query = useQuery({ queryKey, enabled: Boolean(teamId && user), staleTime: 0, queryFn: async () => (await shortcutApi.getAll(teamId)).data });
  const save = useMutation({
    mutationFn: ({ shortcut, payload }: { shortcut: ProjectShortcut | null; payload: SaveShortcutPayload }) => shortcut ? shortcutApi.update(teamId, shortcut._id, payload) : shortcutApi.create(teamId, payload),
    onSuccess: ({ data }, { shortcut }) => {
      queryClient.setQueryData<ProjectShortcut[]>(queryKey, (current = []) => shortcut ? current.map(item => item._id === shortcut._id ? data : item) : [data, ...current]);
      toast.success(shortcut ? 'Shortcut updated.' : 'Shortcut added.');
    },
    onError: error => toast.error(parseApiError(error, 'Unable to save shortcut.').message),
  });
  const remove = useMutation({
    mutationFn: (shortcut: ProjectShortcut) => shortcutApi.remove(teamId, shortcut._id),
    onSuccess: (_response, shortcut) => {
      queryClient.setQueryData<ProjectShortcut[]>(queryKey, (current = []) => current.filter(item => item._id !== shortcut._id));
      toast.success('Shortcut deleted.');
    },
    onError: error => toast.error(parseApiError(error, 'Unable to delete shortcut.').message),
  });
  return { ...query, save, remove };
}
