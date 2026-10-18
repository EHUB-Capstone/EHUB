import { useId, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Pencil, RefreshCw } from 'lucide-react';
import { startupIndustryApi } from '../../../api/startupIndustryApi';
import StartupIndustryPicker from '../../../components/workspace/StartupIndustryPicker';
import Button from '../../../components/ui/Button';
import type { ProjectDataItem } from '../../../types/projectData';
import type { StartupIndustryDto } from '../../../types/startupIndustries';
import type { ApiEnvelope } from '../../../types/workspaceTools';
import { parseApiError } from '../../../utils/apiError';
import { PROJECT_DATA_EMPTY_VALUE } from '../../../utils/projectData';
import { haveSameIndustrySelection, resolveIndustryIds, validateIndustrySelection } from '../../../utils/projectWorkspace';
import type { ProjectDataSaveError } from './ProjectDataDetailModal';

const MAX_INDUSTRIES = 3;

interface ProjectStartupIndustryEditorProps {
  item: Pick<ProjectDataItem, 'startupIndustries'>;
  canManage: boolean;
  editing: boolean;
  /** The ids picked so far; null until the user changes something, so the saved industries show first. */
  draftIds: string[] | null;
  /** Why editing is not offered right now; null when it is. */
  lockedReason: string | null;
  isSaving: boolean;
  isReloading: boolean;
  error: ProjectDataSaveError | null;
  onStartEdit: () => void;
  onDraftChange: (ids: string[]) => void;
  onCancel: () => void;
  onSave: (ids: string[]) => void;
  onReload: () => void;
}

/** The industries a project belongs to. Admins and lecturers can pick them again; saving is recorded in the history. */
export default function ProjectStartupIndustryEditor({
  item,
  canManage,
  editing,
  draftIds,
  lockedReason,
  isSaving,
  isReloading,
  error,
  onStartEdit,
  onDraftChange,
  onCancel,
  onSave,
  onReload,
}: ProjectStartupIndustryEditorProps) {
  const labelId = useId();

  // Only needed once editing starts, then kept for a minute so reopening is instant.
  const options = useQuery({
    queryKey: ['startup-industries', 'active-options'],
    queryFn: async ({ signal }) => {
      const response = await startupIndustryApi.getActiveOptions(signal) as unknown as ApiEnvelope<{ industries: StartupIndustryDto[] }>;
      return response.data.industries ?? [];
    },
    enabled: editing,
    staleTime: 60_000,
  });
  const industries = useMemo(() => options.data ?? [], [options.data]);

  const savedIds = useMemo(() => resolveIndustryIds(item.startupIndustries, industries), [item.startupIndustries, industries]);
  // Names saved on the project that no longer match an active industry; saving drops them.
  const retired = useMemo(
    () => item.startupIndustries.filter(name => !industries.some(industry => haveSameIndustrySelection([name], [industry.name]))),
    [item.startupIndustries, industries],
  );
  const selectedIds = draftIds ?? savedIds;
  const validation = validateIndustrySelection(selectedIds);
  const idsChanged = draftIds !== null && !haveSameIndustrySelection(selectedIds, savedIds);
  const canSave = !validation && !options.isPending && !options.isError && (idsChanged || (draftIds !== null && retired.length > 0));

  if (!editing) {
    return (
      <div className="min-w-0">
        <div className="flex items-center justify-between gap-2">
          <dt className="text-xs font-medium uppercase tracking-wider text-slate-400">Startup Industry</dt>
          {canManage && (
            <div className="flex items-center gap-2">
              {lockedReason && <span className="text-caption text-slate-400">{lockedReason}</span>}
              <Button variant="ghost" size="xs" icon={Pencil} disabled={lockedReason !== null} onClick={onStartEdit}>
                Edit
              </Button>
            </div>
          )}
        </div>
        <dd className="mt-1 break-words text-sm text-slate-700">
          {item.startupIndustries.length === 0
            ? <span className="text-slate-400">{PROJECT_DATA_EMPTY_VALUE}</span>
            : (
              <span className="flex flex-wrap gap-1.5">
                {item.startupIndustries.map(value => <span key={value} className="rounded-md bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{value}</span>)}
              </span>
            )}
        </dd>
      </div>
    );
  }

  return (
    <div className="min-w-0">
      <dt id={labelId} className="text-xs font-medium uppercase tracking-wider text-slate-400">Startup Industry</dt>
      <dd className="mt-1.5">
        <StartupIndustryPicker
          industries={industries}
          selectedIds={selectedIds}
          onChange={onDraftChange}
          labelId={labelId}
          loading={options.isPending}
          error={options.isError ? parseApiError(options.error, 'Failed to load startup industries.').message : ''}
          onRetry={() => { void options.refetch(); }}
          max={MAX_INDUSTRIES}
          disabled={isSaving}
          invalid={Boolean(validation) && draftIds !== null}
          errorMessage={draftIds !== null ? validation : ''}
        />
        {retired.length > 0 && !options.isPending && (
          <p className="mt-1 text-xs text-slate-500">
            Not in the active list anymore: {retired.join(', ')}. Saving replaces them with your selection.
          </p>
        )}

        {error && (
          <div role="alert" className="mt-2 rounded-xl border border-danger-light bg-danger-50 p-3 text-sm text-danger">
            <p>{error.message}</p>
            {error.isConflict && (
              <Button variant="outline" size="sm" icon={RefreshCw} isLoading={isReloading} onClick={onReload} className="mt-2">
                Reload latest data
              </Button>
            )}
          </div>
        )}

        <div className="mt-2 flex items-center gap-2">
          <Button variant="outline" size="sm" isLoading={isSaving} disabled={!canSave} onClick={() => onSave(selectedIds)}>
            Save industries
          </Button>
          <Button variant="ghost" size="sm" disabled={isSaving} onClick={onCancel}>Cancel</Button>
        </div>
      </dd>
    </div>
  );
}
