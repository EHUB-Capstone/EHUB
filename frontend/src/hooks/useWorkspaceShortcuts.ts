import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { shortcutApi } from '../api/shortcutApi';
import type { ProjectShortcut, SaveShortcutPayload } from '../types/workspaceTools';
import { parseApiError } from '../utils/apiError';
import { useAuth } from './useAuth';
import i18n from '../i18n/index.ts';

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
      toast.success(shortcut ? i18n.t('utils:useWorkspaceShortcuts.shortcutUpdated') : i18n.t('utils:useWorkspaceShortcuts.shortcutAdded'));
    },
    onError: error => toast.error(parseApiError(error, i18n.t('utils:useWorkspaceShortcuts.unableToSaveShortcut')).message),
  });
  const remove = useMutation({
    mutationFn: (shortcut: ProjectShortcut) => shortcutApi.remove(teamId, shortcut._id),
    onSuccess: (_response, shortcut) => {
      queryClient.setQueryData<ProjectShortcut[]>(queryKey, (current = []) => current.filter(item => item._id !== shortcut._id));
      toast.success(i18n.t('utils:useWorkspaceShortcuts.shortcutDeleted'));
    },
    onError: error => toast.error(parseApiError(error, i18n.t('utils:useWorkspaceShortcuts.unableToDeleteShortcut')).message),
  });
  return { ...query, save, remove };
}
