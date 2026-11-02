// AI summaries of a team's checkpoint files, shown to admins, lecturers and mentors only. Each file is summarised on its
// own, once per version; the reviewer chooses which files to summarise.
import { useCallback, useEffect, useId, useRef, useState } from 'react';
import axios from 'axios';
import toast from 'react-hot-toast';
import { AlertTriangle, ChevronDown, FileText, HelpCircle, Link2, ListChecks, Loader2, Quote, RefreshCw, Sparkles, X } from 'lucide-react';
import Badge from '../../ui/Badge';
import Button from '../../ui/Button';
import Card from '../../ui/Card';
import { checkpointSummaryApi } from '../../../api/checkpointSummaryApi';
import { parseApiError } from '../../../utils/apiError';
import {
  defaultFileSelection, fileStatusView, formatSummaryTime, hasWorkInProgress, isFileInProgress, isFileSelectable,
  keepSelectable, nextSummaryPollDelay, summaryErrorMessage, summaryFindingView,
} from '../../../utils/checkpointSummary';
import type { CheckpointSummary, FileSummary } from '../../../types/checkpointSummary';

interface CheckpointAiSummaryCardProps {
  teamId: string;
  checkpointNumber: number;
}

const SectionLabel = ({ icon: Icon, children }: { icon: typeof FileText; children: string }) => (
  <h4 className="flex items-center gap-1.5 text-overline font-bold uppercase text-slate-500">
    <Icon className="h-3.5 w-3.5" aria-hidden="true" />
    {children}
  </h4>
);

const Quoted = ({ text, source }: { text: string; source?: string | null }) => (
  <blockquote className="border-l-2 border-slate-300 pl-3 text-body-sm italic text-slate-600">
    “{text}”{source && <span className="ml-1 text-caption not-italic text-slate-500">— {source}</span>}
  </blockquote>
);

/** The summary of one file, shown when its row is opened. */
function FileSummaryDetails({ file, maxCharacters }: { file: FileSummary; maxCharacters: number }) {
  if (file.status === 'Failed') return <p className="text-body-sm text-danger-dark">{summaryErrorMessage(file.errorCode)}</p>;
  const content = file.content;
  if (!content) return null;
  return (
    <div className="space-y-3">
      <p className="whitespace-pre-line text-body text-slate-700">{content.overview}</p>
      {content.keyPoints.length > 0 && (
        <ul className="list-disc space-y-1 pl-5 text-body-sm text-slate-700">
          {content.keyPoints.map((point) => <li key={point}>{point}</li>)}
        </ul>
      )}
      {content.requirements.length > 0 && (
        <div className="space-y-2">
          <SectionLabel icon={ListChecks}>Requirements in this file</SectionLabel>
          <ul className="space-y-2">
            {content.requirements.map((requirement) => {
              const finding = summaryFindingView[requirement.finding];
              return (
                <li key={requirement.index} className="space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-body-sm font-medium text-slate-900">{requirement.index + 1}. {requirement.title}</span>
                    <Badge variant={finding.tone}>{finding.label}</Badge>
                  </div>
                  {requirement.evidence && <Quoted text={requirement.evidence} />}
                </li>
              );
            })}
          </ul>
        </div>
      )}
      {content.highlights.length > 0 && (
        <div className="space-y-2">
          <SectionLabel icon={Quote}>Key sentences</SectionLabel>
          {content.highlights.map((sentence) => <Quoted key={sentence} text={sentence} />)}
        </div>
      )}
      {content.questions.length > 0 && (
        <div className="space-y-2">
          <SectionLabel icon={HelpCircle}>Questions to ask the team</SectionLabel>
          <ul className="list-disc space-y-1 pl-5 text-body-sm text-slate-700">
            {content.questions.map((question) => <li key={question}>{question}</li>)}
          </ul>
        </div>
      )}
      <p className="text-caption text-slate-500">
        {file.inputTruncated && `Long file: the AI read its most informative sentences (about ${maxCharacters.toLocaleString()} characters). `}
        {file.generatedAt && `Summarised ${formatSummaryTime(file.generatedAt)}.`}
      </p>
    </div>
  );
}

function FileRow({ file, selected, onToggleSelected, maxCharacters, disabled }: {
  file: FileSummary;
  selected: boolean;
  onToggleSelected: () => void;
  maxCharacters: number;
  disabled: boolean;
}) {
  const [open, setOpen] = useState(false);
  const regionId = useId();
  const status = fileStatusView(file);
  const canOpen = file.status === 'Ready' || file.status === 'Failed';
  const selectable = isFileSelectable(file);
  return (
    <li className="rounded-lg border border-slate-200">
      <div className="flex items-center gap-2 px-3 py-2">
        <input
          type="checkbox"
          className="h-4 w-4 shrink-0 accent-primary disabled:opacity-40"
          checked={selected}
          disabled={!selectable || disabled}
          onChange={onToggleSelected}
          aria-label={selectable ? `Summarise ${file.name}` : `${file.name}: ${status.label}`}
        />
        <button
          type="button"
          onClick={() => setOpen((value) => !value)}
          disabled={!canOpen}
          aria-expanded={canOpen ? open : undefined}
          aria-controls={canOpen ? regionId : undefined}
          className="flex min-w-0 flex-1 flex-wrap items-center gap-2 rounded-md px-1 py-1 text-left text-body-sm transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 disabled:cursor-default disabled:hover:bg-transparent"
        >
          <span className="min-w-0 flex-1 truncate font-medium text-slate-900">{file.name}</span>
          <span className="text-caption text-slate-500">v{file.versionNumber}</span>
          {isFileInProgress(file.status) && <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" aria-hidden="true" />}
          <Badge variant={status.tone}>{status.label}</Badge>
          {canOpen && <ChevronDown className={`h-4 w-4 shrink-0 text-slate-500 transition-transform ${open ? 'rotate-180' : ''}`} aria-hidden="true" />}
        </button>
      </div>
      {canOpen && open && (
        <div id={regionId} className="border-t border-slate-200 px-4 py-3">
          <FileSummaryDetails file={file} maxCharacters={maxCharacters} />
        </div>
      )}
    </li>
  );
}

export default function CheckpointAiSummaryCard({ teamId, checkpointNumber }: CheckpointAiSummaryCardProps) {
  const regionId = useId();
  const [summary, setSummary] = useState<CheckpointSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [actionError, setActionError] = useState('');
  const [expanded, setExpanded] = useState(true);
  const [selected, setSelected] = useState<string[]>([]);
  const [requesting, setRequesting] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const selectionTouchedRef = useRef(false);
  const busyRef = useRef(false);
  const actionControllerRef = useRef<AbortController | null>(null);
  const retryControllerRef = useRef<AbortController | null>(null);

  const applySummary = useCallback((next: CheckpointSummary) => {
    setSummary(next);
    // Until the reviewer changes the choice, every file that still needs a summary is chosen.
    setSelected((current) => (selectionTouchedRef.current ? keepSelectable(current, next) : defaultFileSelection(next)));
  }, []);

  const load = useCallback(async (signal: AbortSignal) => {
    try {
      const response = await checkpointSummaryApi.get(teamId, checkpointNumber, signal);
      applySummary(response.data);
      setLoadError('');
    } catch (error) {
      if (axios.isCancel(error)) return;
      setLoadError(summaryErrorMessage(parseApiError(error, '').code));
    } finally {
      if (!signal.aborted) setLoading(false);
    }
  }, [teamId, checkpointNumber, applySummary]);

  // A different team or checkpoint starts from a clean state; leaving the screen cancels the request.
  useEffect(() => {
    const controller = new AbortController();
    selectionTouchedRef.current = false;
    setSummary(null);
    setSelected([]);
    setLoading(true);
    setLoadError('');
    setActionError('');
    load(controller.signal);
    return () => controller.abort();
  }, [load]);

  // Ask again while a file is queued or being summarised. Each answer schedules the next check.
  useEffect(() => {
    const delay = nextSummaryPollDelay(summary);
    if (delay === null) return undefined;
    const controller = new AbortController();
    const timer = window.setTimeout(() => load(controller.signal), delay);
    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [summary, load]);

  useEffect(() => () => {
    actionControllerRef.current?.abort();
    retryControllerRef.current?.abort();
  }, []);

  const retryLoad = () => {
    retryControllerRef.current?.abort();
    const controller = new AbortController();
    retryControllerRef.current = controller;
    setLoading(true);
    load(controller.signal);
  };

  // Request and cancel share one lock so a double click never sends two actions.
  const runAction = async (action: (signal: AbortSignal) => Promise<CheckpointSummary>, setBusy: (value: boolean) => void, success: string) => {
    if (busyRef.current) return;
    busyRef.current = true;
    setBusy(true);
    setActionError('');
    const controller = new AbortController();
    actionControllerRef.current = controller;
    try {
      const next = await action(controller.signal);
      selectionTouchedRef.current = false;
      applySummary(next);
      toast.success(success);
    } catch (error) {
      if (axios.isCancel(error)) return;
      const message = summaryErrorMessage(parseApiError(error, '').code);
      setActionError(message);
      toast.error(message);
      // The list may have changed (a file deleted or already summarised); show the current one.
      load(controller.signal);
    } finally {
      busyRef.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  };

  const requestSelected = () => {
    const count = selected.length;
    return runAction(
      async (signal) => (await checkpointSummaryApi.request(teamId, checkpointNumber, selected, signal)).data,
      setRequesting,
      `${count} file${count === 1 ? '' : 's'} queued. Each summary appears in its row when ready.`);
  };

  const cancelAll = () => runAction(
    async (signal) => (await checkpointSummaryApi.cancel(teamId, checkpointNumber, signal)).data,
    setCancelling,
    'AI summaries cancelled. Finished files keep their summary.');

  const toggleFile = (fileId: string) => {
    selectionTouchedRef.current = true;
    setSelected((current) => (current.includes(fileId) ? current.filter((id) => id !== fileId) : [...current, fileId]));
  };

  const files = summary?.files ?? [];
  const inProgress = hasWorkInProgress(summary);
  const anySummarised = files.some((file) => file.status === 'Ready');
  const busy = requesting || cancelling;

  return (
    <section className="rounded-2xl border border-slate-200/60 bg-white shadow-sm" aria-label="AI summary">
      <button
        type="button"
        onClick={() => setExpanded((value) => !value)}
        aria-expanded={expanded}
        aria-controls={regionId}
        className="flex w-full items-center gap-3 rounded-2xl px-5 py-4 text-left transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
      >
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary-50">
          <Sparkles className="h-[18px] w-[18px] text-primary-700" aria-hidden="true" />
        </span>
        <span className="min-w-0 flex-1">
          <span className="block text-subheading text-secondary">AI summary</span>
          <span className="block text-caption text-slate-500">Visible to mentors, lecturers and admins only</span>
        </span>
        {inProgress && <Badge variant="info">Working</Badge>}
        <ChevronDown className={`h-4 w-4 shrink-0 text-slate-500 transition-transform ${expanded ? 'rotate-180' : ''}`} aria-hidden="true" />
      </button>

      {expanded && (
        <div id={regionId} className="space-y-4 px-5 pb-5">
          {loading ? (
            <div className="flex items-center gap-2 py-4 text-body-sm text-slate-500" role="status">
              <Loader2 className="h-4 w-4 animate-spin text-primary" aria-hidden="true" /> Loading AI summary…
            </div>
          ) : loadError ? (
            <div className="flex flex-wrap items-center gap-3 py-2" role="alert">
              <p className="text-body-sm text-danger-dark">{loadError}</p>
              <Button variant="outline" size="sm" icon={RefreshCw} onClick={retryLoad}>Retry</Button>
            </div>
          ) : !summary?.enabled && !anySummarised ? (
            <p className="text-body-sm text-slate-500">{summaryErrorMessage('WORKSPACE_SUMMARY_DISABLED')}</p>
          ) : files.length === 0 ? (
            <p className="text-body-sm text-slate-500">The team has not uploaded any file for this checkpoint yet.</p>
          ) : (
            <>
              <div className="flex items-start gap-2 text-caption text-slate-500">
                <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                <p>AI-generated and may be inaccurate. Check the files before relying on it; it is not used for grading.</p>
              </div>

              <div className="space-y-2">
                <SectionLabel icon={FileText}>Files</SectionLabel>
                <p className="text-caption text-slate-500">
                  Choose the files to summarise; each file is summarised on its own, newest version first. Open a summarised file to read its summary.
                </p>
                <ul className="space-y-2">
                  {files.map((file) => (
                    <FileRow
                      key={file.fileId}
                      file={file}
                      selected={selected.includes(file.fileId)}
                      onToggleSelected={() => toggleFile(file.fileId)}
                      maxCharacters={summary.filePromptCharacters}
                      disabled={busy || !summary.enabled}
                    />
                  ))}
                </ul>
              </div>

              {actionError && <p className="text-body-sm text-danger-dark" role="alert">{actionError}</p>}
              {summary.enabled && (
                <div className="flex flex-wrap items-center gap-2">
                  {files.some(isFileSelectable) ? (
                    <Button variant="primary" size="sm" icon={Sparkles} isLoading={requesting}
                      disabled={selected.length === 0 || cancelling} onClick={requestSelected}>
                      {selected.length > 0 ? `Summarise ${selected.length} file${selected.length === 1 ? '' : 's'}` : 'Choose files to summarise'}
                    </Button>
                  ) : !inProgress && (
                    <span className="text-body-sm text-slate-600">Every file is summarised. New uploads appear here to summarise.</span>
                  )}
                  {inProgress && (
                    <Button variant="danger-outline" size="sm" icon={X} isLoading={cancelling} disabled={requesting} onClick={cancelAll}>
                      Cancel
                    </Button>
                  )}
                  {inProgress && <span className="text-caption text-slate-500">Files are summarised one at a time; this page updates automatically.</span>}
                </div>
              )}

              {summary.requirements.length > 0 && (
                <Card tone="inset" padding="p-4" hover={false} className="space-y-3">
                  <SectionLabel icon={ListChecks}>Checkpoint requirements</SectionLabel>
                  <p className="text-caption text-slate-500">Combined from the summarised files.</p>
                  <ul className="space-y-3">
                    {summary.requirements.map((requirement) => {
                      const finding = summaryFindingView[requirement.finding];
                      return (
                        <li key={requirement.index} className="space-y-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="text-body-sm font-semibold text-slate-900">{requirement.index + 1}. {requirement.title}</span>
                            <Badge variant={finding.tone}>{finding.label}</Badge>
                          </div>
                          {requirement.evidence
                            ? <Quoted text={requirement.evidence} source={requirement.source} />
                            : <p className="text-caption text-slate-500">{finding.hint}</p>}
                        </li>
                      );
                    })}
                  </ul>
                </Card>
              )}

              {summary.links.length > 0 && (
                <div className="space-y-2">
                  <SectionLabel icon={Link2}>Links</SectionLabel>
                  <p className="text-caption text-slate-500">The AI does not open links; only their names are listed.</p>
                  <ul className="space-y-1 text-body-sm text-slate-700">
                    {summary.links.map((link) => <li key={`${link.name}-${link.domain}`}>{link.name} <span className="text-slate-500">({link.domain})</span></li>)}
                  </ul>
                </div>
              )}
            </>
          )}
        </div>
      )}
    </section>
  );
}
