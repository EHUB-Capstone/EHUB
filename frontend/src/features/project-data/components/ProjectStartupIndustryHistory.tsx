import { useId, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ChevronDown, History } from 'lucide-react';
import { projectDataApi } from '../../../api/projectDataApi';
import LoadingSkeleton from '../../../components/ui/LoadingSkeleton';
import { useAuth } from '../../../hooks/useAuth';
import type {
  ProjectStartupIndustryHistory as ProjectStartupIndustryHistoryData,
  ProjectStartupIndustryHistoryItem,
} from '../../../types/projectData';
import { cn } from '../../../utils/cn';
import { formatHistoryDate, historyTruncationNotice, projectStartupIndustryHistoryKey } from '../../../utils/projectData';

interface ProjectStartupIndustryHistoryViewProps {
  isLoading: boolean;
  isError: boolean;
  history: ProjectStartupIndustryHistoryData | undefined;
  onRetry: () => void;
}

/** The list itself. It scrolls inside its own box, so many entries never make the dialog taller. */
export function ProjectStartupIndustryHistoryView({ isLoading, isError, history, onRetry }: ProjectStartupIndustryHistoryViewProps) {
  if (isLoading) return <LoadingSkeleton lines={3} />;

  if (isError || !history) {
    return (
      <p role="alert" className="flex flex-wrap items-center gap-2 text-sm text-danger">
        The industry history could not be loaded.
        <button type="button" onClick={onRetry} className="font-semibold underline underline-offset-2">Retry</button>
      </p>
    );
  }

  if (history.items.length === 0) return <p className="text-sm text-slate-400">No changes recorded yet.</p>;

  const notice = historyTruncationNotice(history);
  return (
    <>
      <ol
        tabIndex={0}
        aria-label="Startup industry history entries"
        className="max-h-64 space-y-2 overflow-y-auto pr-1 focus-visible:outline-2 focus-visible:outline-primary"
      >
        {history.items.map(entry => <HistoryEntry key={entry.id} entry={entry} />)}
      </ol>
      {notice && <p className="mt-2 text-xs text-slate-400">{notice}</p>}
    </>
  );
}

function HistoryEntry({ entry }: { entry: ProjectStartupIndustryHistoryItem }) {
  const hasStructure = entry.added.length > 0 || entry.removed.length > 0 || entry.kept.length > 0;
  return (
    <li className="rounded-xl border border-slate-100 bg-white px-3 py-2.5">
      <div className="flex items-baseline justify-between gap-3">
        <span className="min-w-0 truncate text-sm font-semibold text-slate-700">{entry.actorName ?? 'Unknown user'}</span>
        <time dateTime={entry.occurredAtUtc} className="shrink-0 text-xs text-slate-400">{formatHistoryDate(entry.occurredAtUtc)}</time>
      </div>

      {hasStructure ? (
        <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
          {entry.added.map(name => (
            <span key={`add-${name}`} className="rounded-full border border-success-light bg-success-50 px-2 py-0.5 text-caption font-semibold text-success-dark">
              <span className="sr-only">Added </span>+ {name}
            </span>
          ))}
          {entry.removed.map(name => (
            <span key={`remove-${name}`} className="rounded-full border border-danger-light bg-danger-50 px-2 py-0.5 text-caption font-semibold text-danger line-through">
              <span className="sr-only">Removed </span>− {name}
            </span>
          ))}
          {entry.kept.length > 0 && <span className="text-xs text-slate-400">Kept {entry.kept.join(', ')}</span>}
        </div>
      ) : (
        <p className="mt-1 break-words text-sm text-slate-600">{entry.summary}</p>
      )}
    </li>
  );
}

interface ProjectStartupIndustryHistoryProps {
  projectId: string;
  /** Changes whenever the industries are saved, so an open list refreshes by itself. */
  rowVersion: string;
}

/** Collapsed by default; the history is only requested once the section is opened. */
export default function ProjectStartupIndustryHistory({ projectId, rowVersion }: ProjectStartupIndustryHistoryProps) {
  const { user } = useAuth();
  const panelId = useId();
  const [open, setOpen] = useState(false);

  const history = useQuery({
    queryKey: projectStartupIndustryHistoryKey(user?.id, projectId, rowVersion),
    queryFn: async ({ signal }) => (await projectDataApi.getStartupIndustryHistory(projectId, signal)).data,
    enabled: open && Boolean(user?.id),
    staleTime: 0,
  });

  return (
    <div className="border-t border-slate-100 pt-3">
      <button
        type="button"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen(current => !current)}
        className="flex w-full items-center gap-2 rounded-lg py-1 text-left text-xs font-medium uppercase tracking-wider text-slate-400 hover:text-slate-600 focus-visible:outline-2 focus-visible:outline-primary"
      >
        <History className="h-3.5 w-3.5" aria-hidden="true" />
        Startup industry history
        <ChevronDown className={cn('ml-auto h-4 w-4 transition-transform', open && 'rotate-180')} aria-hidden="true" />
      </button>
      <div id={panelId} hidden={!open} className="mt-2">
        {open && (
          <ProjectStartupIndustryHistoryView
            isLoading={history.isPending && history.fetchStatus !== 'idle'}
            isError={history.isError}
            history={history.data}
            onRetry={() => { void history.refetch(); }}
          />
        )}
      </div>
    </div>
  );
}
