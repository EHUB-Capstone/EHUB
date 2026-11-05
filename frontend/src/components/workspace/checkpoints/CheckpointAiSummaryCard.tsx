// AI summaries of a team's checkpoint files, shown to admins, lecturers and mentors only. Each file is summarised on its
// own, once per version; the reviewer chooses which files to summarise.
import { useCallback, useEffect, useId, useRef, useState } from 'react';
import axios from 'axios';
import toast from 'react-hot-toast';
import { AlertTriangle, ArrowLeftRight, Check, CheckCircle2, ChevronDown, FileText, HelpCircle, Link2, ListChecks, Loader2, Minus, Quote, RefreshCw, Sparkles, X } from 'lucide-react';
import Badge from '../../ui/Badge';
import Button from '../../ui/Button';
import Card from '../../ui/Card';
import { checkpointSummaryApi } from '../../../api/checkpointSummaryApi';
import { parseApiError } from '../../../utils/apiError';
import {
  canOpenFile, fileStatusView, ignoredInstructionsMessage, formatSummaryTime, hasWorkInProgress, isFileInProgress, isFileSelectable,
  isNothingFound, isSearchable, isSummaryFeatureVisible, keepSelectable, selectableFileIds, sortByWeight, statusItemView,
  summaryStatusBadges, summaryStatusCounts, nextSummaryPollDelay, summaryErrorMessage, summaryFindingView,
} from '../../../utils/checkpointSummary';
import type { CheckpointSummary, CriterionCue, FigureConflict, FileRequirementFinding, FileSummary, RequirementFinding, StatusItem } from '../../../types/checkpointSummary';

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

const KIND_LABEL: Record<string, string> = { Requirement: 'Requirement', Criterion: 'Rubric criterion' };

/** What the Excellent level expects, and which of it the file mentions: presence of evidence, not a grade. */
function Cues({ cues }: { cues: CriterionCue[] }) {
  if (cues.length === 0) return null;
  return (
    <ul className="flex flex-wrap gap-1.5" aria-label="Expected content">
      {cues.map((cue) => (
        <li key={cue.label} title={cue.evidence ?? (cue.found ? 'Mentioned in the file, with no passage to quote' : 'Not mentioned in the file')}
          className={`flex items-center gap-1 rounded-md border px-1.5 py-0.5 text-caption ${cue.found
            ? 'border-success-light bg-success-50 text-success-dark' : 'border-slate-200 bg-slate-50 text-slate-500'}`}>
          {cue.found ? <Check className="h-3 w-3" aria-hidden="true" /> : <Minus className="h-3 w-3" aria-hidden="true" />}
          <span className="sr-only">{cue.found ? 'Mentioned: ' : 'Not mentioned: '}</span>
          {cue.label}
        </li>
      ))}
    </ul>
  );
}

/** Criteria with their weight, heaviest first. */
function FindingList({ findings }: { findings: FileRequirementFinding[] }) {
  return (
    <ul className="space-y-2">
      {sortByWeight(findings).map((requirement) => {
        const finding = summaryFindingView[requirement.finding];
        return (
          <li key={requirement.index} className="space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-body-sm font-medium text-slate-900">{requirement.index + 1}. {requirement.title}</span>
              {requirement.weight != null && <Badge variant="neutral">{requirement.weight}%</Badge>}
              <Badge variant={finding.tone}>{finding.label}</Badge>
            </div>
            {requirement.evidence
              ? <Quoted text={requirement.evidence} />
              : requirement.finding === 'Related' && <p className="text-caption text-slate-500">{finding.hint}</p>}
            <Cues cues={requirement.cues ?? []} />
          </li>
        );
      })}
    </ul>
  );
}

/** Live and product criteria are listed apart, so "nothing found" refers only to what a document can show. */
function OtherCriteria({ findings }: { findings: { index: number; title: string; weight?: number | null; finding: RequirementFinding }[] }) {
  const others = sortByWeight(findings.filter((item) => !isSearchable(item)));
  if (others.length === 0) return null;
  return (
    <ul className="space-y-1">
      {others.map((item) => {
        const view = summaryFindingView[item.finding];
        return (
          <li key={item.index} className="flex flex-wrap items-center gap-2 text-body-sm text-slate-700" title={view.hint}>
            <span>{item.index + 1}. {item.title}</span>
            {item.weight != null && <Badge variant="neutral">{item.weight}%</Badge>}
            <Badge variant={view.tone}>{view.label}</Badge>
          </li>
        );
      })}
    </ul>
  );
}

/** One line instead of a row of "Not found" badges, e.g. a software review checked against a business rubric. */
const NothingFound = ({ titles, subject }: { titles: string[]; subject: string }) => (
  <p className="text-body-sm text-slate-600">
    {subject} none of the {titles.length} requirements and rubric criteria ({titles.join(', ')}).
  </p>
);

/** The file tried to instruct the AI; the sentences were dropped before summarising, but a reviewer should know. */
const IgnoredInstructionsNote = ({ count }: { count?: number }) => {
  const message = ignoredInstructionsMessage(count);
  if (!message) return null;
  return (
    <p className="flex items-start gap-2 rounded-lg border border-warning-light bg-warning-50 px-3 py-2 text-caption text-warning-dark" role="note">
      <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
      {message}
    </p>
  );
};

const LittleTextNote = () => (
  <p className="flex items-start gap-2 rounded-lg border border-warning-light bg-warning-50 px-3 py-2 text-caption text-warning-dark">
    <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
    This file is mostly images or diagrams. Only its titles and a few lines of text could be read, so the summary is limited
    to them; open the file to see the diagrams.
  </p>
);

/** Figures the file gives twice with different values: worth asking the team which one is right. */
function FigureConflicts({ conflicts, acrossFiles = false }: { conflicts: FigureConflict[]; acrossFiles?: boolean }) {
  if (conflicts.length === 0) return null;
  return (
    <div className="space-y-2">
      <SectionLabel icon={ArrowLeftRight}>{acrossFiles ? 'Figures that differ between files' : 'Figures that disagree'}</SectionLabel>
      <p className="text-caption text-slate-500">
        {acrossFiles
          ? 'Two files give different values for what looks like the same figure. They may describe different things, so ask the team before treating either as wrong.'
          : 'The file states these figures with different values. Ask the team which one is right.'}
      </p>
      <ul className="space-y-2">
        {conflicts.map((conflict) => (
          <li key={conflict.subject} className="space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-body-sm font-medium text-slate-900">{conflict.subject}</span>
              {conflict.values.map((item) => <Badge key={`${item.value}-${item.source ?? ''}`} variant="warning">{item.value}</Badge>)}
            </div>
            {conflict.values.map((item) => <Quoted key={`${item.source ?? ''}-${item.quote}`} text={item.quote} source={item.source} />)}
          </li>
        ))}
      </ul>
    </div>
  );
}

/** Lines of the file that say where work stands (DONE, IN PROGRESS, PENDING…), quoted as written. */
function StatusLines({ items }: { items: StatusItem[] }) {
  if (items.length === 0) return null;
  return (
    <div className="space-y-2">
      <SectionLabel icon={CheckCircle2}>Status reported in the file</SectionLabel>
      <ul className="space-y-1.5">
        {items.map((item) => {
          const view = statusItemView[item.status];
          return (
            <li key={`${item.status}-${item.request ?? ''}-${item.text}`} className="flex items-start gap-2 text-body-sm text-slate-700">
              <Badge variant={view.tone}>{view.label}</Badge>
              <span className="min-w-0 flex-1">
                {item.request && <span className="block font-medium text-slate-900">{item.request}</span>}
                <span className={item.request ? 'block text-slate-600' : undefined}>{item.text}</span>
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

function QuickView({ highlights, requirements, conflicts, statusItems }: {
  highlights: string[];
  requirements: FileRequirementFinding[];
  conflicts: FigureConflict[];
  statusItems: StatusItem[];
}) {
  return (
    <>
      <StatusLines items={statusItems} />
      <FigureConflicts conflicts={conflicts} />
      {requirements.length > 0 && (
        <div className="space-y-2">
          <SectionLabel icon={ListChecks}>Requirements &amp; rubric criteria in this file</SectionLabel>
          {isNothingFound(requirements)
            ? <NothingFound titles={requirements.filter(isSearchable).map((item) => item.title)} subject="This file mentions" />
            : <FindingList findings={requirements.filter(isSearchable)} />}
          <OtherCriteria findings={requirements} />
        </div>
      )}
      {highlights.length > 0 && (
        <div className="space-y-2">
          <SectionLabel icon={Quote}>Key sentences</SectionLabel>
          {highlights.map((sentence) => <Quoted key={sentence} text={sentence} />)}
        </div>
      )}
    </>
  );
}

/** The summary of one file, shown when its row is opened. While the AI is still writing, the quick view is shown. */
function FileSummaryDetails({ file, maxCharacters }: { file: FileSummary; maxCharacters: number }) {
  if (file.status === 'Failed') return <p className="text-body-sm text-danger-dark">{summaryErrorMessage(file.errorCode)}</p>;
  const content = file.content;
  if (!content) {
    if (!file.preview) return null;
    return (
      <div className="space-y-3">
        <p className="flex items-center gap-2 text-caption text-slate-500" role="status">
          <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" aria-hidden="true" />
          Quick view from the file text. The AI overview is still being written.
        </p>
        <IgnoredInstructionsNote count={file.preview.ignoredInstructions} />
        {file.preview.littleText && <LittleTextNote />}
        <QuickView highlights={file.preview.highlights} requirements={file.preview.requirements} conflicts={file.preview.conflicts ?? []} statusItems={file.preview.statusItems ?? []} />
      </div>
    );
  }
  return (
    <div className="space-y-3">
      <IgnoredInstructionsNote count={content.ignoredInstructions} />
      {content.littleText && <LittleTextNote />}
      <p className="whitespace-pre-line text-body text-slate-700">{content.overview}</p>
      {content.keyPoints.length > 0 && (
        <ul className="list-disc space-y-1 pl-5 text-body-sm text-slate-700">
          {content.keyPoints.map((point) => <li key={point}>{point}</li>)}
        </ul>
      )}
      <QuickView highlights={content.highlights} requirements={content.requirements} conflicts={content.conflicts ?? []} statusItems={content.statusItems ?? []} />
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

function FileRow({ file, selected, onToggleSelected, onRetry, maxCharacters, disabled }: {
  file: FileSummary;
  selected: boolean;
  onToggleSelected: () => void;
  onRetry: () => void;
  maxCharacters: number;
  disabled: boolean;
}) {
  const [open, setOpen] = useState(false);
  const regionId = useId();
  const status = fileStatusView(file);
  const canOpen = canOpenFile(file);
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
        {file.status === 'Failed' && (
          <Button variant="outline" size="sm" icon={RefreshCw} disabled={disabled} onClick={onRetry} aria-label={`Retry summary of ${file.name}`}>
            Retry
          </Button>
        )}
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
  // Closed until the reviewer opens it: the checkpoint page stays short, and the header already tells the state.
  const [expanded, setExpanded] = useState(false);
  const [selected, setSelected] = useState<string[]>([]);
  const [requesting, setRequesting] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const busyRef = useRef(false);
  const actionControllerRef = useRef<AbortController | null>(null);
  const retryControllerRef = useRef<AbortController | null>(null);

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

  const retryFile = (fileId: string) => runAction(
    async (signal) => (await checkpointSummaryApi.request(teamId, checkpointNumber, [fileId], signal)).data,
    setRequesting,
    'File queued again. Its summary appears in its row when ready.');

  const cancelAll = () => runAction(
    async (signal) => (await checkpointSummaryApi.cancel(teamId, checkpointNumber, signal)).data,
    setCancelling,
    'AI summaries cancelled. Finished files keep their summary.');

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

  // The server decides per environment whether AI summaries exist. Until it has answered, and whenever the feature is
  // switched off, nothing is drawn, so the screen looks as if the feature was never there. A failed load still shows
  // the card so the reviewer can retry.
  if (!isSummaryFeatureVisible(summary) && !loadError) return null;

  return (
    <section className="rounded-2xl border border-slate-200/60 bg-white shadow-sm" aria-label="AI summary">
      <button
        type="button"
        onClick={() => setExpanded((value) => !value)}
        aria-expanded={expanded}
        aria-controls={regionId}
        className="flex w-full flex-wrap items-center gap-x-3 gap-y-2 rounded-2xl px-5 py-4 text-left transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
      >
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary-50">
          <Sparkles className="h-[18px] w-[18px] text-primary-700" aria-hidden="true" />
        </span>
        <span className="min-w-0 flex-1">
          <span className="block text-subheading text-secondary">AI summary</span>
          <span className="block text-caption text-slate-500">Visible to mentors, lecturers and admins only</span>
        </span>
        {/* On a narrow screen the status drops under the title instead of squeezing it. */}
        <span className="order-last flex basis-full flex-wrap items-center gap-1.5 pl-12 sm:order-none sm:basis-auto sm:justify-end sm:pl-0">
          {inProgress && <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" aria-hidden="true" />}
          {statusBadges.map((badge) => <Badge key={badge.key} variant={badge.tone}>{badge.label}</Badge>)}
          <span className="sr-only" role="status" aria-live="polite">
            {loadError ? 'AI summary could not be loaded.' : `${counts.ready} of ${counts.total} files summarised, ${counts.summarising + counts.queued} in progress.`}
          </span>
        </span>
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
          ) : files.length === 0 ? (
            <p className="text-body-sm text-slate-500">The team has not uploaded any file for this checkpoint yet.</p>
          ) : (
            <>
              <div className="flex items-start gap-2 text-caption text-slate-500">
                <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                <p>AI-generated and may be inaccurate. Check the files before relying on it; it is not used for grading.</p>
              </div>

              <div className="space-y-2">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <SectionLabel icon={FileText}>Files</SectionLabel>
                  {selectableIds.length > 1 && summary.enabled && (
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => setSelected(allSelected ? [] : selectableIds)}
                      className="rounded-md px-1.5 py-0.5 text-caption font-semibold text-primary-700 transition hover:bg-primary-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 disabled:opacity-50"
                    >
                      {allSelected ? 'Clear selection' : `Select all (${selectableIds.length})`}
                    </button>
                  )}
                </div>
                <p className="text-caption text-slate-500">
                  New uploads are queued automatically. Tick files to have them summarised first; open a file to read its summary.
                </p>
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

              <FigureConflicts conflicts={summary.conflicts ?? []} acrossFiles />

              {summary.requirements.length > 0 && (
                <Card tone="inset" padding="p-4" hover={false} className="space-y-3">
                  <SectionLabel icon={ListChecks}>Requirements &amp; rubric criteria</SectionLabel>
                  <p className="text-caption text-slate-500">Combined from the files read so far. Quotes are copied from the files, not written by the AI.</p>
                  {isNothingFound(summary.requirements) ? (
                    <NothingFound titles={summary.requirements.filter(isSearchable).map((item) => item.title)} subject="The files read so far mention" />
                  ) : (
                    <ul className="space-y-3">
                      {sortByWeight(summary.requirements.filter(isSearchable)).map((requirement) => {
                        const finding = summaryFindingView[requirement.finding];
                        return (
                          <li key={requirement.index} className="space-y-1">
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="text-body-sm font-semibold text-slate-900">{requirement.index + 1}. {requirement.title}</span>
                              {requirement.weight != null && <Badge variant="neutral">{requirement.weight}%</Badge>}
                              <Badge variant="neutral">{KIND_LABEL[requirement.kind] ?? requirement.kind}</Badge>
                              <Badge variant={finding.tone}>{finding.label}</Badge>
                            </div>
                            {requirement.evidence
                              ? <Quoted text={requirement.evidence} source={requirement.source} />
                              : <p className="text-caption text-slate-500">{finding.hint}</p>}
                            <Cues cues={requirement.cues ?? []} />
                          </li>
                        );
                      })}
                    </ul>
                  )}
                  <OtherCriteria findings={summary.requirements} />
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
