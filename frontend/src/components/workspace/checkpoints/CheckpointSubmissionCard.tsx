import { useRef, useState } from 'react';
import type { KeyboardEvent } from 'react';
import {
  CalendarClock, CheckCircle2, CircleDashed, Download, ExternalLink, FileText, Link2,
  Loader2, Pencil, RotateCcw, ShieldCheck, Trash2, Eye, XCircle,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import Badge from '../../ui/Badge';
import Button from '../../ui/Button';
import TruncatedText from '../../ui/TruncatedText';
import FileUploadZone from './FileUploadZone';
import CheckpointLinkForm from './CheckpointLinkForm';
import { warmUpPdfPreview } from '../../../utils/pdfPreviewRuntime';
import {
  CHECKPOINT_UPLOAD_MAX_FILES,
  CHECKPOINT_UPLOAD_MAX_FILE_SIZE_MB,
  formatFileSize,
  isCheckpointFilePreviewable,
} from '../../../utils/checkpointUpload';
import type {
  WorkspaceCheckpointConfig,
  WorkspaceCheckpointFile,
  WorkspaceCheckpointLink,
} from '../../../types/workspaceCheckpoints';

export const MAX_CHECKPOINT_LINKS = 10;

type SubmissionTab = 'documents' | 'links';

interface CheckpointSubmissionCardProps {
  checkpoint: WorkspaceCheckpointConfig;
  status?: string | null;
  submittedAt?: string | null;
  files: WorkspaceCheckpointFile[];
  links: WorkspaceCheckpointLink[];
  loading: boolean;
  isStudent: boolean;
  isEditable: boolean;
  currentUserId?: string;
  teamId: string;
  downloadingId: string | null;
  editingLink: WorkspaceCheckpointLink | null;
  onRefresh: () => void | Promise<void>;
  onPreview: (file: WorkspaceCheckpointFile) => void;
  onDownload: (file: WorkspaceCheckpointFile) => void;
  onRequestDeleteFile: (file: WorkspaceCheckpointFile) => void;
  onRequestDeleteLink: (link: WorkspaceCheckpointLink) => void;
  onEditLink: (link: WorkspaceCheckpointLink | null) => void;
  onUnsentFilesChange?: (hasUnsent: boolean) => void;
}

const STATUS_META: Record<string, { label: string; tone: string; icon: LucideIcon }> = {
  NotSubmitted: { label: 'Not submitted', tone: 'neutral', icon: CircleDashed },
  Draft: { label: 'Not submitted', tone: 'neutral', icon: CircleDashed },
  Submitted: { label: 'Submitted', tone: 'brand', icon: CheckCircle2 },
  Returned: { label: 'Returned', tone: 'warning', icon: RotateCcw },
  Approved: { label: 'Approved', tone: 'success', icon: ShieldCheck },
  Rejected: { label: 'Rejected', tone: 'danger', icon: XCircle },
};

const FILE_TYPE_LABEL: Record<string, string> = { pdf: 'PDF', docx: 'DOCX', pptx: 'PPTX' };

const formatDateTime = (value?: string | null) => (value ? new Date(value).toLocaleString() : '');

const displayLinkHost = (url: string) => {
  try { return new URL(url).hostname; } catch { return url; }
};

export default function CheckpointSubmissionCard({
  checkpoint,
  status,
  submittedAt,
  files,
  links,
  loading,
  isStudent,
  isEditable,
  currentUserId,
  teamId,
  downloadingId,
  editingLink,
  onRefresh,
  onPreview,
  onDownload,
  onRequestDeleteFile,
  onRequestDeleteLink,
  onEditLink,
  onUnsentFilesChange,
}: CheckpointSubmissionCardProps) {
  const [tab, setTab] = useState<SubmissionTab | null>(null);
  // Decided once, when the first load finishes, so deleting the last file does not move the user to another tab.
  const [initialTab, setInitialTab] = useState<SubmissionTab | null>(null);
  if (initialTab === null && !loading) {
    setInitialTab(files.length === 0 && links.length > 0 ? 'links' : 'documents');
  }
  const tabRefs = useRef<Record<SubmissionTab, HTMLButtonElement | null>>({ documents: null, links: null });

  const canSubmit = isStudent && isEditable && checkpoint.canUpload;
  const showScheduleNotice = isStudent && isEditable && !checkpoint.canUpload;
  // Until the user picks a tab, open on links only if they were the only thing submitted when the panel loaded.
  const activeTab: SubmissionTab = tab ?? initialTab ?? 'documents';
  const statusMeta = STATUS_META[status ?? 'NotSubmitted'] ?? STATUS_META.NotSubmitted;
  const StatusIcon = statusMeta.icon;
  const dueLabel = formatDateTime(checkpoint.endDateUtc);

  const selectTab = (next: SubmissionTab) => {
    setTab(next);
    tabRefs.current[next]?.focus();
  };

  const onTabKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
      event.preventDefault();
      selectTab(activeTab === 'documents' ? 'links' : 'documents');
    }
  };

  const tabClass = (value: SubmissionTab) => `flex-1 sm:flex-none inline-flex items-center justify-center gap-2 border-b-2 px-4 py-2.5 text-sm font-semibold transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/40 ${activeTab === value ? 'border-primary text-primary-700' : 'border-transparent text-slate-500 hover:text-slate-800'}`;

  return (
    <section className="rounded-2xl border border-slate-200/60 bg-white shadow-sm" aria-labelledby="checkpoint-submission-title">
      <div className="space-y-3 border-b border-slate-100 p-5">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
          <h2 id="checkpoint-submission-title" className="text-heading font-bold text-slate-800">Your submission</h2>
          <Badge variant={statusMeta.tone} size="md">
            <StatusIcon className="h-3.5 w-3.5" aria-hidden="true" />
            {loading ? 'Loading…' : statusMeta.label}
          </Badge>
        </div>
        <dl className="flex flex-wrap gap-x-6 gap-y-1 text-xs text-slate-500">
          {submittedAt && (
            <div className="flex gap-1.5"><dt className="font-semibold text-slate-600">Last submitted</dt><dd>{formatDateTime(submittedAt)}</dd></div>
          )}
          {dueLabel && (
            <div className="flex gap-1.5"><dt className="font-semibold text-slate-600">Due</dt><dd>{dueLabel}</dd></div>
          )}
        </dl>
        {showScheduleNotice && (
          <div className={`flex items-start gap-3 rounded-xl border p-3 ${checkpoint.scheduleStatus === 'Closed' ? 'border-red-200 bg-red-50' : 'border-blue-200 bg-blue-50'}`}>
            <CalendarClock className={`mt-0.5 h-5 w-5 shrink-0 ${checkpoint.scheduleStatus === 'Closed' ? 'text-red-600' : 'text-blue-600'}`} aria-hidden="true" />
            <div>
              <p className="text-sm font-bold text-slate-900">
                {checkpoint.scheduleStatus === 'Upcoming' ? 'Checkpoint not open yet' : checkpoint.scheduleStatus === 'Closed' ? 'Checkpoint closed' : 'Checkpoint not scheduled'}
              </p>
              <p className="mt-0.5 text-xs text-slate-600">
                {checkpoint.scheduleStatus === 'Upcoming'
                  ? `Opens ${formatDateTime(checkpoint.startDateUtc)}.`
                  : checkpoint.scheduleStatus === 'Closed'
                    ? `The submission deadline was ${dueLabel}.`
                    : 'Your lecturer has not configured an upload window for this checkpoint.'}
              </p>
            </div>
          </div>
        )}
      </div>

      <div role="tablist" aria-label="Submission method" className="flex border-b border-slate-100 px-2 sm:px-3">
        <button
          ref={(node) => { tabRefs.current.documents = node; }}
          type="button"
          role="tab"
          id="submission-tab-documents"
          aria-selected={activeTab === 'documents'}
          aria-controls="submission-panel-documents"
          tabIndex={activeTab === 'documents' ? 0 : -1}
          onClick={() => setTab('documents')}
          onKeyDown={onTabKeyDown}
          className={tabClass('documents')}
        >
          <FileText className="h-4 w-4" aria-hidden="true" /> Documents ({loading ? '…' : files.length})
        </button>
        <button
          ref={(node) => { tabRefs.current.links = node; }}
          type="button"
          role="tab"
          id="submission-tab-links"
          aria-selected={activeTab === 'links'}
          aria-controls="submission-panel-links"
          tabIndex={activeTab === 'links' ? 0 : -1}
          onClick={() => setTab('links')}
          onKeyDown={onTabKeyDown}
          className={tabClass('links')}
        >
          <Link2 className="h-4 w-4" aria-hidden="true" /> Links ({loading ? '…' : links.length})
        </button>
      </div>

      {activeTab === 'documents' && (
        <div role="tabpanel" id="submission-panel-documents" aria-labelledby="submission-tab-documents" className="space-y-4 p-5">
          {canSubmit && (
            <FileUploadZone teamId={teamId} checkpointNumber={checkpoint.number} existingFileCount={files.length} onUploaded={onRefresh} onUnsentChange={onUnsentFilesChange} variant="large" />
          )}
          {loading ? (
            <div className="flex items-center justify-center gap-2 rounded-xl border border-dashed border-slate-200 py-10 text-sm text-slate-500">
              <Loader2 className="h-5 w-5 animate-spin text-primary" aria-hidden="true" /> Loading submissions…
            </div>
          ) : files.length === 0 ? (
            <div className="rounded-xl border border-dashed border-slate-200 bg-slate-50/50 px-6 py-8 text-center">
              <FileText className="mx-auto h-7 w-7 text-slate-300" aria-hidden="true" />
              <p className="mt-2 text-sm font-semibold text-slate-700">No documents yet</p>
              <p className="mt-1 text-xs text-slate-500">
                {canSubmit
                  ? `Upload up to ${CHECKPOINT_UPLOAD_MAX_FILES} PDF, DOCX or PPTX files (${CHECKPOINT_UPLOAD_MAX_FILE_SIZE_MB} MB each) above.`
                  : isStudent && isEditable
                    ? checkpoint.availabilityReason || 'This checkpoint is not open for submission.'
                    : 'This team has not uploaded files for this checkpoint.'}
              </p>
            </div>
          ) : (
            <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200/80" aria-label="Submitted files">
              {files.map((file) => {
                const canDelete = canSubmit && file.uploadedBy?._id === currentUserId;
                return (
                  <li key={file._id} className="flex flex-col gap-3 p-3 sm:flex-row sm:items-center">
                    <div className="flex min-w-0 flex-1 items-center gap-3">
                      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-overline font-extrabold text-slate-600">
                        {FILE_TYPE_LABEL[file.fileType?.toLowerCase()] ?? (file.fileType?.toUpperCase() || 'FILE')}
                      </span>
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <TruncatedText as="span" text={file.originalName} lines={1} className="max-w-full text-sm font-bold text-slate-800" />
                          {file.versionNumber > 0 && <Badge variant="brand" size="xs">Version {file.versionNumber}</Badge>}
                        </div>
                        <p className="mt-0.5 text-xs text-slate-500">
                          {formatFileSize(file.fileSize)} · {file.uploadedBy?.name || 'Unknown'} · {formatDateTime(file.uploadedAt)}
                        </p>
                      </div>
                    </div>
                    <div className="flex shrink-0 gap-2">
                      {isCheckpointFilePreviewable(file.originalName) && (
                        <Button
                          variant="outline"
                          size="md"
                          icon={Eye}
                          onClick={() => onPreview(file)}
                          onMouseEnter={warmUpPdfPreview}
                          onFocus={warmUpPdfPreview}
                          aria-label={`Preview ${file.originalName}`}
                          className="w-9 px-0 sm:w-auto sm:px-3"
                        >
                          <span className="hidden sm:inline">Preview</span>
                        </Button>
                      )}
                      <Button
                        variant="outline"
                        size="md"
                        icon={downloadingId === file._id ? undefined : Download}
                        isLoading={downloadingId === file._id}
                        onClick={() => onDownload(file)}
                        aria-label={`Download ${file.originalName}`}
                        className="w-9 px-0 sm:w-auto sm:px-3"
                      >
                        <span className="hidden sm:inline">Download</span>
                      </Button>
                      {canDelete && (
                        <Button
                          variant="danger-outline"
                          size="md"
                          icon={Trash2}
                          onClick={() => onRequestDeleteFile(file)}
                          aria-label={`Delete ${file.originalName}`}
                          className="w-9 px-0"
                        />
                      )}
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      )}

      {activeTab === 'links' && (
        <div role="tabpanel" id="submission-panel-links" aria-labelledby="submission-tab-links" className="space-y-4 p-5">
          {canSubmit && (links.length < MAX_CHECKPOINT_LINKS ? (
            !editingLink && <CheckpointLinkForm teamId={teamId} checkpointNumber={checkpoint.number} onSaved={onRefresh} />
          ) : (
            <p className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm font-semibold text-amber-800">
              This checkpoint already has the maximum of {MAX_CHECKPOINT_LINKS} active links. Delete one of your links before adding another.
            </p>
          ))}
          {loading ? (
            <div className="flex justify-center rounded-xl border border-dashed border-slate-200 py-10">
              <Loader2 className="h-6 w-6 animate-spin text-primary" aria-label="Loading links" />
            </div>
          ) : links.length === 0 ? (
            <div className="rounded-xl border border-dashed border-slate-200 bg-slate-50/50 px-6 py-8 text-center">
              <Link2 className="mx-auto h-7 w-7 text-slate-300" aria-hidden="true" />
              <p className="mt-2 text-sm font-semibold text-slate-700">No links yet</p>
              <p className="mt-1 text-xs text-slate-500">
                {canSubmit ? 'Add a public HTTPS resource such as a prototype, repository or demo video above.' : 'Links submitted by the team will appear here.'}
              </p>
            </div>
          ) : (
            <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200/80" aria-label="Submitted links">
              {links.map((link) => {
                const canManage = canSubmit && link.submittedBy?._id === currentUserId;
                if (canManage && editingLink?._id === link._id) {
                  return (
                    <li key={link._id} className="bg-primary-50/30 p-4">
                      <p className="mb-3 flex items-center gap-2 text-sm font-bold text-slate-800">
                        <Pencil className="h-4 w-4 text-primary" aria-hidden="true" /> Edit link
                      </p>
                      <CheckpointLinkForm
                        teamId={teamId}
                        checkpointNumber={checkpoint.number}
                        editingLink={link}
                        onCancelEdit={() => onEditLink(null)}
                        onSaved={onRefresh}
                      />
                    </li>
                  );
                }
                return (
                  <li key={link._id} className="flex flex-col gap-3 p-3 sm:flex-row sm:items-center">
                    <div className="flex min-w-0 flex-1 items-center gap-3">
                      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-slate-600">
                        <Link2 className="h-4 w-4" aria-hidden="true" />
                      </span>
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="max-w-full truncate text-sm font-bold text-slate-800" title={link.name}>{link.name}</span>
                          <Badge variant="brand" size="xs">Version {link.versionNumber}</Badge>
                        </div>
                        <p className="truncate text-xs text-slate-500" title={link.url}>{displayLinkHost(link.url)}</p>
                        <p className="mt-0.5 text-xs text-slate-500">{link.submittedBy?.name || 'Unknown'} · {formatDateTime(link.submittedAt)}</p>
                      </div>
                    </div>
                    <div className="flex shrink-0 gap-2">
                      <a
                        href={link.url}
                        target="_blank"
                        rel="noopener noreferrer"
                        aria-label={`Open ${link.name} in a new tab`}
                        className="inline-flex h-9 w-9 items-center justify-center gap-2 rounded-lg border border-slate-300 bg-white text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 sm:w-auto sm:px-3"
                      >
                        <ExternalLink className="h-4 w-4" aria-hidden="true" />
                        <span className="hidden sm:inline">Open</span>
                      </a>
                      {canManage && (
                        <>
                          <Button
                            variant="outline"
                            size="md"
                            icon={Pencil}
                            onClick={() => onEditLink(link)}
                            aria-label={`Edit ${link.name}`}
                            className="w-9 px-0 sm:w-auto sm:px-3"
                          >
                            <span className="hidden sm:inline">Edit</span>
                          </Button>
                          <Button
                            variant="danger-outline"
                            size="md"
                            icon={Trash2}
                            onClick={() => onRequestDeleteLink(link)}
                            aria-label={`Delete ${link.name}`}
                            className="w-9 px-0"
                          />
                        </>
                      )}
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      )}
    </section>
  );
}
