// src/components/workspace/checkpoints/FileUploadZone.tsx
import { useCallback, useEffect, useRef, useState } from 'react';
import type { DragEvent } from 'react';
import { AlertCircle, CheckCircle2, Loader2, RotateCcw, UploadCloud, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { checkpointApi } from '../../../api/checkpointApi';
import type {
  CheckpointUploadItem,
  CheckpointUploadSession,
} from '../../../types/workspaceCheckpoints';
import {
  CHECKPOINT_UPLOAD_MAX_FILE_SIZE_MB,
  checkpointUploadFailureMessage,
  formatFileSize,
  getUploadProgressPercent,
  needsNewUploadSession,
  validateCheckpointUploadFile,
} from '../../../utils/checkpointUpload';

interface FileUploadZoneProps {
  teamId: string;
  checkpointNumber: number;
  onUploaded?: () => void | Promise<void>;
  variant?: 'default' | 'large';
}

const ACTIVE_STATUSES = ['queued', 'uploading', 'completing'] as const;
const isActive = (item: CheckpointUploadItem) =>
  (ACTIVE_STATUSES as readonly string[]).includes(item.status);
const isCancelled = (error: unknown) => (error as { code?: string })?.code === 'ERR_CANCELED';

const STATUS_LABEL: Record<CheckpointUploadItem['status'], string> = {
  queued: 'Waiting',
  uploading: 'Uploading',
  completing: 'Finishing',
  completed: 'Completed',
  failed: 'Failed',
};

export default function FileUploadZone({
  teamId,
  checkpointNumber,
  onUploaded,
  variant = 'default',
}: FileUploadZoneProps) {
  const isLarge = variant === 'large';
  const [dragging, setDragging] = useState(false);
  const [items, setItems] = useState<CheckpointUploadItem[]>([]);
  const fileRef = useRef<HTMLInputElement>(null);
  // The queue loop reads the latest items synchronously, so state is mirrored in a ref.
  const itemsRef = useRef<CheckpointUploadItem[]>([]);
  const controllersRef = useRef(new Map<string, AbortController>());
  const runningRef = useRef(false);
  const mountedRef = useRef(true);
  const completedSinceRefreshRef = useRef(0);
  const onUploadedRef = useRef(onUploaded);
  useEffect(() => {
    onUploadedRef.current = onUploaded;
  }, [onUploaded]);

  const commit = useCallback((next: CheckpointUploadItem[]) => {
    itemsRef.current = next;
    if (mountedRef.current) setItems(next);
  }, []);

  const patch = useCallback((id: string, changes: Partial<CheckpointUploadItem>) => {
    commit(itemsRef.current.map(item => (item.id === id ? { ...item, ...changes } : item)));
  }, [commit]);

  useEffect(() => {
    mountedRef.current = true;
    const controllers = controllersRef.current;
    return () => {
      mountedRef.current = false;
      controllers.forEach(controller => controller.abort());
      controllers.clear();
    };
  }, []);

  const hasActiveUploads = items.some(isActive);
  useEffect(() => {
    if (!hasActiveUploads) return undefined;
    const warnBeforeLeaving = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', warnBeforeLeaving);
    return () => window.removeEventListener('beforeunload', warnBeforeLeaving);
  }, [hasActiveUploads]);

  const processItem = useCallback(async (item: CheckpointUploadItem) => {
    const controller = new AbortController();
    controllersRef.current.set(item.id, controller);
    let stage: 'request' | 'storage' = 'request';
    try {
      let session: CheckpointUploadSession | undefined = item.session;
      if (!item.putDone) {
        patch(item.id, { status: 'uploading', percent: 0, error: undefined });
        if (!session) {
          const created = await checkpointApi.initiateUpload(
            teamId,
            checkpointNumber,
            { fileName: item.file.name, contentType: item.file.type, size: item.file.size },
            { signal: controller.signal },
          );
          session = created.data as CheckpointUploadSession;
          patch(item.id, { session });
        }

        stage = 'storage';
        await checkpointApi.putToPresignedUrl(session, item.file, {
          signal: controller.signal,
          onUploadProgress: ({ loaded, total }: { loaded: number; total?: number }) =>
            patch(item.id, { percent: getUploadProgressPercent(loaded, total, item.file.size) }),
        });
        patch(item.id, { putDone: true, percent: 100 });
      }

      stage = 'request';
      patch(item.id, { status: 'completing', error: undefined });
      const completed = await checkpointApi.completeUpload(
        teamId,
        checkpointNumber,
        (session as CheckpointUploadSession).uploadId,
        { signal: controller.signal },
      );
      const version = completed?.data?.versionNumber as number | undefined;
      patch(item.id, { status: 'completed', percent: 100, versionNumber: version });
      completedSinceRefreshRef.current += 1;
      toast.success(`"${item.file.name}" uploaded${version ? ` as Version ${version}` : ''}!`);
    } catch (error) {
      if (isCancelled(error)) {
        commit(itemsRef.current.filter(candidate => candidate.id !== item.id));
      } else {
        patch(item.id, {
          status: 'failed',
          error: checkpointUploadFailureMessage(error, item.file.name, stage),
        });
      }
    } finally {
      controllersRef.current.delete(item.id);
    }
  }, [checkpointNumber, commit, patch, teamId]);

  const runQueue = useCallback(async () => {
    if (runningRef.current) return;
    runningRef.current = true;
    try {
      while (mountedRef.current) {
        const next = itemsRef.current.find(item => item.status === 'queued');
        if (!next) break;
        await processItem(next);
      }
    } finally {
      runningRef.current = false;
    }

    if (completedSinceRefreshRef.current > 0) {
      completedSinceRefreshRef.current = 0;
      await onUploadedRef.current?.();
    }
  }, [processItem]);

  const addFiles = (rawFiles: FileList | File[] | null) => {
    const files = Array.from(rawFiles ?? []);
    const accepted: CheckpointUploadItem[] = [];
    for (const file of files) {
      const validationMessage = validateCheckpointUploadFile(file);
      if (validationMessage) {
        toast.error(validationMessage);
        continue;
      }
      accepted.push({
        id: `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`,
        file,
        status: 'queued',
        percent: 0,
        putDone: false,
      });
    }

    if (fileRef.current) fileRef.current.value = '';
    if (!accepted.length) return;
    commit([...itemsRef.current, ...accepted]);
    void runQueue();
  };

  const retry = (id: string) => {
    const item = itemsRef.current.find(candidate => candidate.id === id);
    if (!item || item.status !== 'failed') return;
    // Keep the session when only "complete" is missing; otherwise ask for a fresh upload link.
    const reset = needsNewUploadSession(item, Date.now());
    patch(id, {
      status: 'queued',
      error: undefined,
      ...(reset ? { session: undefined, putDone: false, percent: 0 } : {}),
    });
    void runQueue();
  };

  const cancelOrDismiss = (id: string) => {
    const controller = controllersRef.current.get(id);
    if (controller) {
      controller.abort();
      return;
    }
    commit(itemsRef.current.filter(item => item.id !== id));
  };

  const clearFinished = () => commit(itemsRef.current.filter(isActive));

  const onDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragging(false);
    addFiles(event.dataTransfer.files);
  };

  const finishedCount = items.filter(item => item.status === 'completed').length;

  return (
    <div className="space-y-3">
      <div
        role="button"
        tabIndex={0}
        aria-label={`Upload files. PDF, DOCX or PPTX, up to ${CHECKPOINT_UPLOAD_MAX_FILE_SIZE_MB} MB each.`}
        onClick={() => fileRef.current?.click()}
        onKeyDown={(event) => {
          if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            fileRef.current?.click();
          }
        }}
        onDragOver={(event) => { event.preventDefault(); setDragging(true); }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        className={`
          relative border-2 border-dashed flex flex-col items-center justify-center gap-2 cursor-pointer select-none
          transition-all duration-200 text-center
          ${isLarge ? 'rounded-2xl min-h-[200px] p-8 gap-3' : 'rounded-xl min-h-[128px] p-5'}
          ${dragging
            ? 'border-orange-500 bg-orange-50 shadow-inner'
            : 'border-slate-200 bg-white hover:border-orange-400/70 hover:bg-orange-50/30 hover:shadow-sm'}
        `}
      >
        <input
          ref={fileRef}
          type="file"
          multiple
          accept=".pdf,.docx,.pptx"
          className="hidden"
          onChange={(event) => addFiles(event.target.files)}
        />
        <div className={`rounded-2xl bg-orange-50 flex items-center justify-center ${isLarge ? 'w-16 h-16' : 'w-12 h-12'}`}>
          <UploadCloud className={`transition-colors ${isLarge ? 'w-9 h-9' : 'w-8 h-8'} ${dragging ? 'text-orange-500' : 'text-orange-400'}`} />
        </div>
        <p className={`font-semibold text-slate-700 ${isLarge ? 'text-sm' : 'text-xs'}`}>
          {dragging ? 'Drop files here' : 'Drag & drop or click to upload'}
        </p>
        <p className={`text-slate-400 ${isLarge ? 'text-xs' : 'text-[10px]'}`}>
          PDF · DOCX · PPTX · Max {CHECKPOINT_UPLOAD_MAX_FILE_SIZE_MB} MB per file
        </p>
      </div>

      {items.length > 0 && (
        <ul className="space-y-2" aria-live="polite" aria-label="Upload progress">
          {items.map(item => (
            <UploadRow
              key={item.id}
              item={item}
              onRetry={() => retry(item.id)}
              onCancel={() => cancelOrDismiss(item.id)}
            />
          ))}
          {finishedCount > 0 && (
            <li className="text-right">
              <button
                type="button"
                onClick={clearFinished}
                className="text-[11px] font-semibold text-slate-500 hover:text-slate-700"
              >
                Clear finished
              </button>
            </li>
          )}
        </ul>
      )}
    </div>
  );
}

function UploadRow({
  item,
  onRetry,
  onCancel,
}: {
  item: CheckpointUploadItem;
  onRetry: () => void;
  onCancel: () => void;
}) {
  const active = isActive(item);
  const sentBytes = Math.round((item.file.size * item.percent) / 100);
  const badge = {
    queued: 'bg-slate-100 text-slate-600',
    uploading: 'bg-orange-50 text-orange-600',
    completing: 'bg-orange-50 text-orange-600',
    completed: 'bg-emerald-50 text-emerald-600',
    failed: 'bg-red-50 text-red-600',
  }[item.status];
  const barColor = item.status === 'failed' ? 'bg-red-400' : item.status === 'completed' ? 'bg-emerald-500' : 'bg-orange-500';

  return (
    <li className="rounded-xl border border-slate-200 bg-white p-3">
      <div className="flex items-center gap-2">
        {item.status === 'completed' && <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-500" aria-hidden="true" />}
        {item.status === 'failed' && <AlertCircle className="h-4 w-4 shrink-0 text-red-500" aria-hidden="true" />}
        {(item.status === 'uploading' || item.status === 'completing') && (
          <Loader2 className="h-4 w-4 shrink-0 animate-spin text-orange-500" aria-hidden="true" />
        )}
        <p className="min-w-0 flex-1 truncate text-xs font-semibold text-slate-700" title={item.file.name}>
          {item.file.name}
        </p>
        <span className={`shrink-0 rounded-full px-2 py-0.5 text-[10px] font-bold ${badge}`}>
          {STATUS_LABEL[item.status]}
        </span>
        {item.status === 'failed' && (
          <button
            type="button"
            onClick={onRetry}
            aria-label={`Retry uploading ${item.file.name}`}
            className="shrink-0 rounded-md p-1 text-slate-500 hover:bg-slate-100 hover:text-slate-700"
          >
            <RotateCcw className="h-4 w-4" />
          </button>
        )}
        {item.status !== 'completing' && (
          <button
            type="button"
            onClick={onCancel}
            aria-label={active ? `Cancel uploading ${item.file.name}` : `Dismiss ${item.file.name}`}
            className="shrink-0 rounded-md p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
          >
            <X className="h-4 w-4" />
          </button>
        )}
      </div>

      {item.status !== 'queued' && (
        <div
          className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-slate-200"
          role="progressbar"
          aria-label={`Upload progress for ${item.file.name}`}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={item.percent}
        >
          <div
            className={`h-full rounded-full transition-[width] duration-200 ${barColor}`}
            style={{ width: `${item.percent}%` }}
          />
        </div>
      )}

      <p className={`mt-1 text-[10px] ${item.status === 'failed' ? 'text-red-600' : 'text-slate-400'}`}>
        {item.status === 'failed' && item.error}
        {item.status === 'queued' && `${formatFileSize(item.file.size)} · waiting for earlier uploads`}
        {item.status === 'uploading' && `${item.percent}% · ${formatFileSize(sentBytes)} of ${formatFileSize(item.file.size)}`}
        {item.status === 'completing' && 'Saving and validating file…'}
        {item.status === 'completed' && `${formatFileSize(item.file.size)}${item.versionNumber ? ` · Version ${item.versionNumber}` : ''}`}
      </p>
    </li>
  );
}
