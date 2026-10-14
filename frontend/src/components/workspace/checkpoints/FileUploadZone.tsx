// src/components/workspace/checkpoints/FileUploadZone.tsx
import { useCallback, useEffect, useRef, useState } from 'react';
import type { DragEvent } from 'react';
import { AlertCircle, CheckCircle2, Info, Loader2, RotateCcw, Send, UploadCloud, X } from 'lucide-react';
import { badgeTones } from '../../ui/Badge';
import Button from '../../ui/Button';
import ConfirmDialog from '../../ui/ConfirmDialog';
import toast from 'react-hot-toast';
import { checkpointApi } from '../../../api/checkpointApi';
import type {
  CheckpointUploadItem,
  CheckpointUploadSession,
} from '../../../types/workspaceCheckpoints';
import {
  CHECKPOINT_UPLOAD_MAX_FILES,
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
  /** Files already submitted to this checkpoint; they count toward the per-checkpoint limit. */
  existingFileCount?: number;
  onUploaded?: () => void | Promise<void>;
  /** Reports whether files are selected or uploading but not yet submitted, so the host can confirm before leaving. */
  onUnsentChange?: (hasUnsent: boolean) => void;
  variant?: 'default' | 'large';
}

const ACTIVE_STATUSES = ['queued', 'uploading', 'completing'] as const;
const isActive = (item: CheckpointUploadItem) =>
  (ACTIVE_STATUSES as readonly string[]).includes(item.status);
const isCancelled = (error: unknown) => (error as { code?: string })?.code === 'ERR_CANCELED';

const STATUS_LABEL: Record<CheckpointUploadItem['status'], string> = {
  staged: 'Not submitted',
  queued: 'Waiting',
  uploading: 'Uploading',
  completing: 'Finishing',
  completed: 'Submitted',
  failed: 'Failed',
};

export default function FileUploadZone({
  teamId,
  checkpointNumber,
  existingFileCount = 0,
  onUploaded,
  onUnsentChange,
  variant = 'default',
}: FileUploadZoneProps) {
  const isLarge = variant === 'large';
  const [dragging, setDragging] = useState(false);
  const [items, setItems] = useState<CheckpointUploadItem[]>([]);
  const [confirmOpen, setConfirmOpen] = useState(false);
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
  const onUnsentChangeRef = useRef(onUnsentChange);
  useEffect(() => {
    onUnsentChangeRef.current = onUnsentChange;
  }, [onUnsentChange]);

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

  const stagedItems = items.filter(item => item.status === 'staged');
  const hasUnsentWork = items.some(isActive) || stagedItems.length > 0;
  useEffect(() => {
    if (!hasUnsentWork) return undefined;
    const warnBeforeLeaving = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', warnBeforeLeaving);
    return () => window.removeEventListener('beforeunload', warnBeforeLeaving);
  }, [hasUnsentWork]);

  useEffect(() => {
    onUnsentChangeRef.current?.(hasUnsentWork);
  }, [hasUnsentWork]);
  useEffect(() => () => onUnsentChangeRef.current?.(false), []);

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
    // Completed items are already part of existingFileCount once the list refreshes.
    const reserved = itemsRef.current.filter(item => item.status !== 'completed').length;
    let slotsLeft = Math.max(0, CHECKPOINT_UPLOAD_MAX_FILES - existingFileCount - reserved);
    let skippedForLimit = 0;
    for (const file of files) {
      const validationMessage = validateCheckpointUploadFile(file);
      if (validationMessage) {
        toast.error(validationMessage);
        continue;
      }
      if (slotsLeft <= 0) {
        skippedForLimit += 1;
        continue;
      }
      slotsLeft -= 1;
      accepted.push({
        id: `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`,
        file,
        status: 'staged',
        percent: 0,
        putDone: false,
      });
    }

    if (skippedForLimit > 0) {
      toast.error(`A checkpoint can contain at most ${CHECKPOINT_UPLOAD_MAX_FILES} files. ${skippedForLimit} file${skippedForLimit !== 1 ? 's were' : ' was'} not added.`);
    }
    if (fileRef.current) fileRef.current.value = '';
    if (!accepted.length) return;
    commit([...itemsRef.current, ...accepted]);
  };

  // Nothing is uploaded or submitted until the student confirms.
  const submitStaged = () => {
    commit(itemsRef.current.map(item => (item.status === 'staged' ? { ...item, status: 'queued' as const } : item)));
    setConfirmOpen(false);
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

  const clearFinished = () => commit(itemsRef.current.filter(item => isActive(item) || item.status === 'staged'));
  const clearStaged = () => commit(itemsRef.current.filter(item => item.status !== 'staged'));

  const onDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragging(false);
    addFiles(event.dataTransfer.files);
  };

  const finishedCount = items.filter(item => item.status === 'completed').length;
  const usedSlots = existingFileCount + items.filter(item => item.status !== 'completed').length;
  const limitReached = usedSlots >= CHECKPOINT_UPLOAD_MAX_FILES;

  return (
    <div className="space-y-3">
      {limitReached ? (
        <p role="status" className="flex items-start gap-2 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
          <Info className="mt-0.5 h-4 w-4 shrink-0 text-slate-500" aria-hidden="true" />
          <span>You've reached the {CHECKPOINT_UPLOAD_MAX_FILES}-file limit for this checkpoint. Delete a file to add another.</span>
        </p>
      ) : (
        <div
          role="button"
          tabIndex={0}
          aria-label={`Upload files. PDF, DOCX or PPTX, up to ${CHECKPOINT_UPLOAD_MAX_FILE_SIZE_MB} MB each, ${CHECKPOINT_UPLOAD_MAX_FILES - usedSlots} more allowed.`}
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
            ${isLarge ? 'rounded-2xl min-h-[160px] p-6 gap-2' : 'rounded-xl min-h-[128px] p-5'}
            ${dragging
              ? 'border-primary bg-primary-50'
              : 'border-slate-300 bg-white hover:border-primary hover:bg-primary-50/40'}
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
          <div className="rounded-2xl bg-primary-50 flex items-center justify-center w-12 h-12">
            <UploadCloud className={`transition-colors ${isLarge ? 'w-7 h-7' : 'w-8 h-8'} text-primary-700`} />
          </div>
          <p className={`font-semibold text-slate-700 ${isLarge ? 'text-sm' : 'text-xs'}`}>
            {dragging ? 'Drop files here' : 'Drag & drop files here, or click to browse'}
          </p>
          <p className={`text-slate-500 ${isLarge ? 'text-xs' : 'text-overline'}`}>
            PDF · DOCX · PPTX · Max {CHECKPOINT_UPLOAD_MAX_FILE_SIZE_MB} MB per file · Up to {CHECKPOINT_UPLOAD_MAX_FILES} files per checkpoint ({CHECKPOINT_UPLOAD_MAX_FILES - usedSlots} more allowed)
          </p>
        </div>
      )}

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
                className="text-caption font-semibold text-slate-500 hover:text-slate-700"
              >
                Clear finished
              </button>
            </li>
          )}
        </ul>
      )}

      {stagedItems.length > 0 && (
        <div className="flex flex-col gap-3 rounded-xl border border-warning-light bg-warning-50 p-3 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-sm text-slate-800">
            <span className="font-semibold">{stagedItems.length} file{stagedItems.length !== 1 ? 's' : ''} selected — not submitted yet.</span>{' '}
            Review the list, then submit to send {stagedItems.length !== 1 ? 'them' : 'it'} to your lecturer.
          </p>
          <div className="flex shrink-0 gap-2">
            <Button variant="ghost" onClick={clearStaged}>Remove all</Button>
            <Button variant="primary" icon={Send} onClick={() => setConfirmOpen(true)}>
              Submit {stagedItems.length} file{stagedItems.length !== 1 ? 's' : ''}
            </Button>
          </div>
        </div>
      )}

      <ConfirmDialog
        isOpen={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        onConfirm={submitStaged}
        title={`Submit ${stagedItems.length} file${stagedItems.length !== 1 ? 's' : ''}?`}
        description="The selected files will be uploaded and submitted to this checkpoint. Your lecturer and team will be able to see them."
        confirmText="Submit"
        confirmVariant="primary"
      />
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
    staged: badgeTones.warning,
    queued: badgeTones.neutral,
    uploading: badgeTones.brand,
    completing: badgeTones.brand,
    completed: badgeTones.success,
    failed: badgeTones.danger,
  }[item.status];
  const barColor = item.status === 'failed' ? 'bg-danger' : item.status === 'completed' ? 'bg-success' : 'bg-primary';

  return (
    <li className="rounded-xl border border-slate-200 bg-white p-3">
      <div className="flex items-center gap-2">
        {item.status === 'completed' && <CheckCircle2 className="h-4 w-4 shrink-0 text-success-dark" aria-hidden="true" />}
        {item.status === 'failed' && <AlertCircle className="h-4 w-4 shrink-0 text-danger-dark" aria-hidden="true" />}
        {(item.status === 'uploading' || item.status === 'completing') && (
          <Loader2 className="h-4 w-4 shrink-0 animate-spin text-primary-700" aria-hidden="true" />
        )}
        <p className="min-w-0 flex-1 truncate text-xs font-semibold text-slate-700" title={item.file.name}>
          {item.file.name}
        </p>
        <span className={`shrink-0 rounded-md border px-2 py-0.5 text-overline font-bold ${badge}`}>
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
            aria-label={active ? `Cancel uploading ${item.file.name}` : item.status === 'staged' ? `Remove ${item.file.name}` : `Dismiss ${item.file.name}`}
            className="shrink-0 rounded-md p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
          >
            <X className="h-4 w-4" />
          </button>
        )}
      </div>

      {item.status !== 'queued' && item.status !== 'staged' && (
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

      <p className={`mt-1 text-overline ${item.status === 'failed' ? 'text-danger-dark' : 'text-slate-500'}`}>
        {item.status === 'failed' && item.error}
        {item.status === 'staged' && `${formatFileSize(item.file.size)} · will be submitted when you confirm`}
        {item.status === 'queued' && `${formatFileSize(item.file.size)} · waiting for earlier uploads`}
        {item.status === 'uploading' && `${item.percent}% · ${formatFileSize(sentBytes)} of ${formatFileSize(item.file.size)}`}
        {item.status === 'completing' && 'Saving and validating file… not submitted until this finishes'}
        {item.status === 'completed' && `Submitted · ${formatFileSize(item.file.size)}${item.versionNumber ? ` · Version ${item.versionNumber}` : ''}`}
      </p>
    </li>
  );
}
