// AI summaries of a team's checkpoint files, shown to admins, lecturers and mentors only. Each file is summarised on its
// own, once per version; the reviewer chooses which files to summarise. The pieces of the card live in ./ai-summary; this file
// holds the state (loading, polling, the actions) and the order in which the pieces are laid out.
import { useCallback, useEffect, useId, useRef, useState } from 'react';
import axios from 'axios';
import toast from 'react-hot-toast';
import { AlertTriangle, ChevronDown, FileText, Link2, ListChecks, Loader2, RefreshCw, Sparkles, X } from 'lucide-react';
import Badge from '../../ui/Badge';
import Button from '../../ui/Button';
import Card from '../../ui/Card';
import LoadingSkeleton from '../../ui/LoadingSkeleton';
import { checkpointSummaryApi } from '../../../api/checkpointSummaryApi';
import CheckpointProgressBlock from './CheckpointProgressBlock';
import CheckpointVersionChangesBlock from './CheckpointVersionChanges';
import { FigureConflicts } from './ai-summary/FigureNotes';
import { useFigureHistory } from './ai-summary/useFigureHistory';
import { FileRow } from './ai-summary/FileSummaryRow';
import { RequirementEvidence } from './ai-summary/RequirementEvidence';
import { ReviewBrief } from './ai-summary/ReviewBrief';
import { SummaryActionsContext } from './ai-summary/summaryContext';
import { SectionLabel } from './ai-summary/SummaryParts';
import { parseApiError } from '../../../utils/apiError';
import {
  comparedCheckpointsText, figureHistoryHasNews, hasReplacedFiles, hasWorkInProgress, isFileSelectable, isSummaryFeatureVisible,
  keepSelectable, nextSummaryPollDelay, selectableFileIds, summaryErrorMessage, summaryEtaText, summaryStatusBadges, summaryStatusCounts,
  overviewHeadline, withFeedback,
} from '../../../utils/checkpointSummary';
import { summaryText } from '../../../utils/summaryText';
import type { CheckpointSummary, SummaryFeedback } from '../../../types/checkpointSummary';

interface CheckpointAiSummaryCardProps {
  teamId: string;
  checkpointNumber: number;
  /** Opens a submitted file, at a page or slide when one is given (the page of a quoted sentence). */
  onOpenFile?: (file: { id: string; name: string }, page?: number) => void;
  /** Puts a passage into the comment box of the checkpoint. */
  onQuote?: (text: string) => void;
}

export default function CheckpointAiSummaryCard({ teamId, checkpointNumber, onOpenFile, onQuote }: CheckpointAiSummaryCardProps) {
  const regionId = useId();
  const text = summaryText.card;
  const [summary, setSummary] = useState<CheckpointSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [actionError, setActionError] = useState('');
  // Closed until the reviewer opens it: the checkpoint page stays short, and the header already tells the state.
  const [expanded, setExpanded] = useState(false);
  // The overview of the whole checkpoint (requirements, comparisons, links) is closed until asked for: the card is about the files.
  const [overviewOpen, setOverviewOpen] = useState(false);
  const overviewId = useId();
  const [selected, setSelected] = useState<string[]>([]);
  const [requesting, setRequesting] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const busyRef = useRef(false);
  // Rating a summary has its own lock: it must not wait for, or block, a request to summarise files.
  const actionControllerRef = useRef<AbortController | null>(null);
  const retryControllerRef = useRef<AbortController | null>(null);
  const [ratingFileId, setRatingFileId] = useState<string | null>(null);
  const ratingRef = useRef(false);
  const ratingControllerRef = useRef<AbortController | null>(null);
  // Figures against earlier checkpoints are read only once the card is open.
  const history = useFigureHistory(teamId, checkpointNumber, expanded && summary?.enabled === true);

  const applySummary = useCallback((next: CheckpointSummary) => {
    setSummary(next);
    // Nothing is chosen on the reviewer's behalf; a choice only survives while the file can still be chosen.
    setSelected((current) => keepSelectable(current, next));
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
    ratingControllerRef.current?.abort();
  }, []);

  // The verdict is shown only after the server has kept it, so the buttons never claim something that was not saved.
  const rate = useCallback(async (fileId: string, feedback: SummaryFeedback | null) => {
    if (ratingRef.current) return;
    ratingRef.current = true;
    setRatingFileId(fileId);
    const controller = new AbortController();
    ratingControllerRef.current = controller;
    const generationId = summary?.files.find((file) => file.fileId === fileId)?.generationId ?? null;
    try {
      if (feedback) await checkpointSummaryApi.setFeedback(teamId, checkpointNumber, fileId,
        { ...feedback, generationId }, controller.signal);
      else await checkpointSummaryApi.clearFeedback(teamId, checkpointNumber, fileId, generationId, controller.signal);
      setSummary((current) => (current ? withFeedback(current, fileId, feedback, generationId) : current));
    } catch (error) {
      if (axios.isCancel(error)) return;
      const code = parseApiError(error, '').code;
      toast.error(summaryErrorMessage(code));
      if (code === 'WORKSPACE_CONCURRENCY_CONFLICT') await load(controller.signal);
    } finally {
      ratingRef.current = false;
      if (!controller.signal.aborted) setRatingFileId(null);
    }
  }, [teamId, checkpointNumber, summary, load]);

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
      text.queued(count));
  };

  const retryFile = (fileId: string) => runAction(
    async (signal) => (await checkpointSummaryApi.request(teamId, checkpointNumber, [fileId], signal)).data,
    setRequesting,
    text.requeued);

  const cancelAll = () => runAction(
    async (signal) => (await checkpointSummaryApi.cancel(teamId, checkpointNumber, signal)).data,
    setCancelling,
    text.cancelled);

  const toggleFile = (fileId: string) => {
    setSelected((current) => (current.includes(fileId) ? current.filter((id) => id !== fileId) : [...current, fileId]));
  };

  const files = summary?.files ?? [];
  const selectableIds = selectableFileIds(summary);
  const allSelected = selectableIds.length > 0 && selectableIds.every((id) => selected.includes(id));
  const inProgress = hasWorkInProgress(summary);
  const statusBadges = summaryStatusBadges(summary, Boolean(loadError));
  const counts = summaryStatusCounts(files);
  const busy = requesting || cancelling;
  const eta = summaryEtaText(summary);
  const actions = { onOpenFile, onQuote, onRate: rate, ratingFileId };
  // A checkpoint with a single file shows its requirements in the file's own summary, open; the combined list of the overview would repeat them.
  const singleFile = files.length === 1;
  const conflictsAcrossFiles = summary?.conflicts ?? [];
  const conflictsBeforeThisCheckpoint = history?.conflicts ?? [];
  const uncheckedEarlierFiles = history && figureHistoryHasNews(history) ? history.uncheckedFiles : [];
  const needsAttention = conflictsAcrossFiles.length > 0 || conflictsBeforeThisCheckpoint.length > 0 || uncheckedEarlierFiles.length > 0;
  const replacedFiles = summary?.enabled === true && hasReplacedFiles(files.map((file) => ({ name: file.name })));
  const comparedWithoutDifferences = Boolean(history && history.comparedCheckpoints.length > 0 && history.conflicts.length === 0);
  const overviewRequirements = singleFile ? [] : (summary?.requirements ?? []);
  const overviewParts = summary ? overviewHeadline({ ...summary, requirements: overviewRequirements }, replacedFiles) : [];
  // The brief (coverage, what to check, copy/draft) and the progress across checkpoints sit in the overview too; the progress
  // block draws nothing for checkpoint 1 or when there is nothing to compare, so the overview is offered from checkpoint 2.
  const hasReadySummary = files.some((file) => file.status === 'Ready' && file.content !== null);
  const hasOverview = Boolean(summary && (overviewRequirements.length > 0 || summary.links.length > 0 || replacedFiles || comparedWithoutDifferences
    || hasReadySummary || checkpointNumber > 1));

  // The server decides per environment whether AI summaries exist. Until it has answered, and whenever the feature is
  // switched off, nothing is drawn, so the screen looks as if the feature was never there. A failed load still shows
  // the card so the reviewer can retry.
  if (!isSummaryFeatureVisible(summary) && !loadError) return null;

  return (
    <SummaryActionsContext.Provider value={actions}>
    <section className="rounded-2xl border border-slate-200/60 bg-white shadow-sm" aria-label={text.title}>
      <button
        type="button"
        onClick={() => setExpanded((value) => !value)}
        aria-expanded={expanded}
        aria-controls={regionId}
        className="flex w-full flex-wrap items-center gap-x-3 gap-y-2 rounded-2xl px-5 py-4 text-left transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
      >
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary-50">
          <Sparkles className="h-5 w-5 text-primary-700" aria-hidden="true" />
        </span>
        <span className="min-w-0 flex-1">
          <span className="block text-subheading text-secondary">{text.title}</span>
        </span>
        {/* On a narrow screen the status drops under the title instead of squeezing it. */}
        <span className="order-last flex basis-full flex-wrap items-center gap-1.5 pl-12 sm:order-none sm:basis-auto sm:justify-end sm:pl-0">
          {inProgress && <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" aria-hidden="true" />}
          {statusBadges.map((badge) => <Badge key={badge.key} variant={badge.tone}>{badge.label}</Badge>)}
          {eta && <span className="text-caption text-slate-500">{eta}</span>}
          <span className="sr-only" role="status" aria-live="polite">
            {loadError ? text.loadFailedSr : text.progressSr(counts.ready, counts.total, counts.summarising + counts.queued)}
          </span>
        </span>
        <ChevronDown className={`h-4 w-4 shrink-0 text-slate-500 transition-transform ${expanded ? 'rotate-180' : ''}`} aria-hidden="true" />
      </button>

      {expanded && (
        <div id={regionId} className="space-y-4 px-5 pb-5">
          {loading ? (
            <div role="status" className="py-2">
              <span className="sr-only">{text.loading}</span>
              <LoadingSkeleton lines={3} />
            </div>
          ) : loadError ? (
            <div className="flex flex-wrap items-center gap-3 py-2" role="alert">
              <p className="text-body-sm text-danger-dark">{loadError}</p>
              <Button variant="outline" size="sm" icon={RefreshCw} onClick={retryLoad}>{summaryText.common.retry}</Button>
            </div>
          ) : files.length === 0 ? (
            <p className="text-body-sm text-slate-500">{text.noFiles}</p>
          ) : (
            <>
              <div className="space-y-2">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <SectionLabel icon={FileText}>{text.filesLabel}</SectionLabel>
                  {selectableIds.length > 1 && summary.enabled && (
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => setSelected(allSelected ? [] : selectableIds)}
                      className="rounded-md px-1.5 py-0.5 text-caption font-semibold text-primary-700 transition hover:bg-primary-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 disabled:opacity-50"
                    >
                      {allSelected ? text.clearSelection : text.selectAll(selectableIds.length)}
                    </button>
                  )}
                </div>
                <ul className="space-y-2">
                  {files.map((file) => (
                    <FileRow
                      key={file.fileId}
                      file={file}
                      selected={selected.includes(file.fileId)}
                      onToggleSelected={() => toggleFile(file.fileId)}
                      onRetry={() => retryFile(file.fileId)}
                      maxCharacters={summary.filePromptCharacters}
                      disabled={busy || !summary.enabled}
                      autoOpen={singleFile}
                      requirementsOpen={singleFile}
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
                      {selected.length > 0 ? text.summarise(selected.length) : text.chooseFiles}
                    </Button>
                  ) : !inProgress && (
                    <span className="text-body-sm text-slate-600">{text.allSummarised}</span>
                  )}
                  {inProgress && (
                    <Button variant="danger-outline" size="sm" icon={X} isLoading={cancelling} disabled={requesting} onClick={cancelAll}>
                      {text.cancel}
                    </Button>
                  )}
                  {inProgress && <span className="text-caption text-slate-500">{eta ? `${eta}. ` : ''}{text.queueNote}</span>}
                </div>
              )}

              {needsAttention && (
                <Card tone="inset" padding="p-4" hover={false} className="space-y-4">
                  <SectionLabel icon={AlertTriangle}>{text.needsAttention}</SectionLabel>
                  <FigureConflicts conflicts={conflictsAcrossFiles} acrossFiles />
                  <FigureConflicts conflicts={conflictsBeforeThisCheckpoint} earlier />
                  {uncheckedEarlierFiles.length > 0 && <p className="text-caption text-slate-500">{text.uncheckedFiles(uncheckedEarlierFiles)}</p>}
                </Card>
              )}

              {hasOverview && (
                <div className="rounded-xl border border-slate-200">
                  <button
                    type="button"
                    onClick={() => setOverviewOpen((value) => !value)}
                    aria-expanded={overviewOpen}
                    aria-controls={overviewId}
                    className="flex w-full items-center gap-3 rounded-xl px-4 py-3 text-left transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
                  >
                    <ListChecks className="h-4 w-4 shrink-0 text-slate-500" aria-hidden="true" />
                    <span className="min-w-0 flex-1">
                      <span className="block text-body-sm font-semibold text-slate-900">{text.overviewTitle}</span>
                      {overviewParts.length > 0 && <span className="block text-caption text-slate-500">{overviewParts.join(' · ')}</span>}
                    </span>
                    <ChevronDown className={`h-4 w-4 shrink-0 text-slate-500 transition-transform ${overviewOpen ? 'rotate-180' : ''}`} aria-hidden="true" />
                  </button>
                  {overviewOpen && (
                    <div id={overviewId} className="space-y-4 border-t border-slate-200 px-4 py-4">
                      <ReviewBrief summary={summary} onQuote={onQuote} />

                      {overviewRequirements.length > 0 && (
                        <div className="space-y-3">
                          <SectionLabel icon={ListChecks}>{text.requirementsTitle}</SectionLabel>
                          <p className="text-caption text-slate-500">{text.requirementsNote}</p>
                          <RequirementEvidence items={overviewRequirements} scope="files" />
                        </div>
                      )}

                      {replacedFiles && (
                        <CheckpointVersionChangesBlock teamId={teamId} checkpointNumber={checkpointNumber} onOpenFile={onOpenFile} />
                      )}

                      {history && comparedWithoutDifferences && (
                        <p className="text-caption text-slate-500">{text.comparedNote(comparedCheckpointsText(history.comparedCheckpoints))}</p>
                      )}

                      <CheckpointProgressBlock teamId={teamId} checkpointNumber={checkpointNumber} />

                      {summary.links.length > 0 && (
                        <div className="space-y-2">
                          <SectionLabel icon={Link2}>{text.linksTitle}</SectionLabel>
                          <p className="text-caption text-slate-500">{text.linksNote}</p>
                          <ul className="space-y-1 text-body-sm text-slate-700">
                            {summary.links.map((link) => <li key={`${link.name}-${link.domain}`}>{link.name} <span className="text-slate-500">({link.domain})</span></li>)}
                          </ul>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              )}
            </>
          )}
        </div>
      )}
    </section>
    </SummaryActionsContext.Provider>
  );
}
