import { useId } from 'react';
import { RefreshCw } from 'lucide-react';
import StartupIndustryPicker from '../../../components/workspace/StartupIndustryPicker';
import Button from '../../../components/ui/Button';
import type { ProjectDataItem } from '../../../types/projectData';
import type { StartupIndustryDto } from '../../../types/startupIndustries';
import { PROJECT_DATA_EMPTY_VALUE } from '../../../utils/projectData';
import type { ProjectDataSaveError } from './ProjectDataDetailModal';

const MAX_INDUSTRIES = 3;

interface ProjectStartupIndustryEditorProps {
  item: Pick<ProjectDataItem, 'startupIndustries'>;
  canManage: boolean;
  industries: StartupIndustryDto[];
  industriesLoading: boolean;
  industriesError: string;
  onRetryIndustries: () => void;
  selectedIds: string[];
  onChange: (ids: string[]) => void;
  /** Saved industry names that are no longer active; saving replaces them with the selection. */
  retired: string[];
  /** Shown under the picker once the user has changed the selection; empty while it is valid. */
  validationMessage: string;
  isSaving: boolean;
  isReloading: boolean;
  error: ProjectDataSaveError | null;
  onReload: () => void;
}

/**
 * The industries a project belongs to. Admins and lecturers pick them here and save together with the achievements
 * from the dialog's own button; everyone else sees them read-only.
 */
export default function ProjectStartupIndustryEditor({
  item,
  canManage,
  industries,
  industriesLoading,
  industriesError,
  onRetryIndustries,
  selectedIds,
  onChange,
  retired,
  validationMessage,
  isSaving,
  isReloading,
  error,
  onReload,
}: ProjectStartupIndustryEditorProps) {
  const labelId = useId();

  if (!canManage) {
    return (
      <div className="min-w-0">
        <dt className="text-xs font-medium uppercase tracking-wider text-slate-400">Startup Industry</dt>
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
          onChange={onChange}
          labelId={labelId}
          loading={industriesLoading}
          error={industriesError}
          onRetry={onRetryIndustries}
          max={MAX_INDUSTRIES}
          disabled={isSaving}
          invalid={Boolean(validationMessage)}
          errorMessage={validationMessage}
        />
        {retired.length > 0 && !industriesLoading && (
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
      </dd>
    </div>
  );
}
