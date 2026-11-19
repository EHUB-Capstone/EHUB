// "What changed since the previous version" for files a team uploaded again. Worked out from the text of the two versions
// (no AI model), read on request so nothing is downloaded until a reviewer asks. Shown inside the AI summary card, so it
// exists only while AI summaries are on.
import { useCallback, useEffect, useRef, useState } from 'react';
import axios from 'axios';
import { FileSearch, GitCompare, Loader2, RefreshCw } from 'lucide-react';
import Badge from '../../ui/Badge';
import Button from '../../ui/Button';
import Card from '../../ui/Card';
import { checkpointSummaryApi } from '../../../api/checkpointSummaryApi';
import { parseApiError } from '../../../utils/apiError';
import { pageLabel, versionChangeCounts, versionChangeHeadline } from '../../../utils/checkpointSummary';
import type { CheckpointVersionChanges, FileVersionChange, VersionChangeItem } from '../../../types/checkpointSummary';

interface Props {
  teamId: string;
  checkpointNumber: number;
  onOpenFile?: (file: { id: string; name: string }, page?: number) => void;
}

const KIND: Record<VersionChangeItem['kind'], { label: string; tone: 'info' | 'success' | 'neutral' }> = {
  Changed: { label: 'Changed', tone: 'info' },
  Added: { label: 'Added', tone: 'success' },
  Removed: { label: 'Removed', tone: 'neutral' },
};

function ChangeItem({ item, file, onOpenFile }: { item: VersionChangeItem; file: FileVersionChange; onOpenFile?: Props['onOpenFile'] }) {
  const kind = KIND[item.kind];
  // A removed sentence only exists in the previous version, which has its own file id we do not hold: no link for those.
  const canOpen = onOpenFile && item.kind !== 'Removed';
  return (
    <li className="space-y-1">
      <div className="flex flex-wrap items-center gap-2">
        <Badge variant={kind.tone}>{kind.label}</Badge>
        {item.figuresChanged && <Badge variant="warning">Figure changed</Badge>}
        {canOpen && (
          <button
            type="button"
            onClick={() => onOpenFile({ id: file.fileId, name: file.fileName }, item.page ?? undefined)}
            className="inline-flex items-center gap-1 rounded-md border border-slate-200 bg-white px-2 py-1 text-caption font-medium text-slate-600 transition hover:bg-slate-50 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
          >
            <FileSearch className="h-3 w-3" aria-hidden="true" />
            {item.page ? `Open ${pageLabel(file.fileName, item.page)}` : 'Open file'}
          </button>
        )}
      </div>
      {item.kind === 'Changed' && item.before && (
        <p className="border-l-2 border-slate-200 pl-3 text-body-sm text-slate-500 line-through decoration-slate-300">{item.before}</p>
      )}
      <p className={item.kind === 'Removed' ? 'border-l-2 border-slate-200 pl-3 text-body-sm text-slate-500' : 'border-l-2 border-primary-200 pl-3 text-body-sm text-slate-800'}>{item.text}</p>
    </li>
  );
}

function FileChange({ file, onOpenFile }: { file: FileVersionChange; onOpenFile?: Props['onOpenFile'] }) {
  const counts = versionChangeCounts(file);
  return (
    <li className="space-y-2">
      <div>
        <p className="text-body-sm font-semibold text-slate-900">{file.fileName}</p>
        <p className="text-caption text-slate-500">{versionChangeHeadline(file)}{counts && ` ${counts}.`}</p>
      </div>
      {file.items.length > 0 && (
        <ul className="space-y-3">
          {file.items.map((item, index) => <ChangeItem key={`${item.kind}-${index}`} item={item} file={file} onOpenFile={onOpenFile} />)}
        </ul>
      )}
    </li>
  );
}

export default function CheckpointVersionChangesBlock({ teamId, checkpointNumber, onOpenFile }: Props) {
  const [changes, setChanges] = useState<CheckpointVersionChanges | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const controllerRef = useRef<AbortController | null>(null);

  // A different team or checkpoint is a different comparison; an unfinished request must not answer for it.
  useEffect(() => {
    setChanges(null);
    setError('');
    return () => controllerRef.current?.abort();
  }, [teamId, checkpointNumber]);

  const load = useCallback(async () => {
    controllerRef.current?.abort();
    const controller = new AbortController();
    controllerRef.current = controller;
    setLoading(true);
    setError('');
    try {
      const response = await checkpointSummaryApi.changes(teamId, checkpointNumber, controller.signal);
      setChanges(response.data);
    } catch (caught) {
      if (axios.isCancel(caught)) return;
      setError(parseApiError(caught, 'Could not compare the versions. Please try again.').message);
    } finally {
      if (!controller.signal.aborted) setLoading(false);
    }
  }, [teamId, checkpointNumber]);

  return (
    <Card tone="inset" padding="p-4" hover={false} className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h4 className="flex items-center gap-1.5 text-overline font-bold uppercase text-slate-500">
          <GitCompare className="h-3.5 w-3.5" aria-hidden="true" />
          Changes since the previous version
        </h4>
        {!changes && (
          <Button variant="outline" size="sm" icon={loading ? Loader2 : GitCompare} disabled={loading} onClick={load}>
            {loading ? 'Comparing…' : 'Compare'}
          </Button>
        )}
      </div>
      <p className="text-caption text-slate-500">
        The team uploaded some files again. The two versions are compared sentence by sentence without the AI, so nothing is made up; only the most notable changes are shown.
      </p>
      {error && (
        <div className="flex flex-wrap items-center gap-3" role="alert">
          <p className="text-body-sm text-danger-dark">{error}</p>
          <Button variant="outline" size="sm" icon={RefreshCw} onClick={load}>Retry</Button>
        </div>
      )}
      {changes && (changes.files.length === 0
        ? <p className="text-body-sm text-slate-600">No file has been uploaded again.</p>
        : <ul className="space-y-5">{changes.files.map((file) => <FileChange key={file.fileId} file={file} onOpenFile={onOpenFile} />)}</ul>)}
    </Card>
  );
}
