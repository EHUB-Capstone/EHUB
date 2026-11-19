// One file of the checkpoint: its row (status, select, retry) and, when opened, the summary the AI wrote for it with what the
// quick view found without the AI.
import { useContext, useId, useState } from 'react';
import { ChevronDown, HelpCircle, Loader2, MessageSquarePlus, Quote, RefreshCw, ThumbsDown, ThumbsUp } from 'lucide-react';
import Badge from '../../../ui/Badge';
import Button from '../../../ui/Button';
import {
  canOpenFile, canRateSummary, fileStatusView, formatSummaryTime, isFileInProgress, isFileSelectable, isSearchable, nextFeedback,
  summaryErrorMessage, SUMMARY_FEEDBACK_REASONS,
} from '../../../../utils/checkpointSummary';
import { summaryText } from '../../../../utils/summaryText';
import type {
  FigureConflict, FileRequirementFinding, FileSummary, StatusItem, SummaryFeedbackRating, SummaryFeedbackReason,
} from '../../../../types/checkpointSummary';
import { FigureConflicts, StatusLines } from './FigureNotes';
import { RequirementEvidence } from './RequirementEvidence';
import { SummaryActionsContext } from './summaryContext';
import { ActionChip, IgnoredInstructionsNote, LittleTextNote, Quoted, SectionLabel } from './SummaryParts';

interface QuickViewProps {
  highlights: string[];
  highlightPages?: number[];
  requirements: FileRequirementFinding[];
  conflicts: FigureConflict[];
  statusItems: StatusItem[];
  fileId: string;
  fileName: string;
  /** True for the only file of a checkpoint: its requirements are the checkpoint's, so they are shown without a click. */
  requirementsOpen: boolean;
}

function QuickView({ highlights, highlightPages, requirements, conflicts, statusItems, fileId, fileName, requirementsOpen }: QuickViewProps) {
  const searchable = requirements.filter(isSearchable);
  return (
    <>
      <StatusLines items={statusItems} />
      <FigureConflicts conflicts={conflicts} />
      {requirements.length > 0 && (
        <details className="text-body-sm text-slate-700" open={requirementsOpen || undefined}>
          <summary className="cursor-pointer py-2 font-medium">
            {summaryText.file.evidenceByRequirement(searchable.filter((item) => item.finding === 'Related').length, searchable.length)}
          </summary>
          <RequirementEvidence items={requirements} scope="file" fileId={fileId} fileName={fileName} />
        </details>
      )}
      {highlights.length > 0 && (
        <div className="space-y-2">
          <SectionLabel icon={Quote}>{summaryText.file.keySentences}</SectionLabel>
          {highlights.map((sentence, position) => (
            <Quoted key={sentence} text={sentence} fileId={fileId} fileName={fileName} page={highlightPages?.[position] || null} />
          ))}
        </div>
      )}
    </>
  );
}

/** "Was this summary useful?" The answer is kept per reviewer and is how the quality of the summaries is measured. */
function FeedbackBar({ file }: { file: FileSummary }) {
  const { onRate, ratingFileId } = useContext(SummaryActionsContext);
  if (!onRate || !canRateSummary(file)) return null;
  const text = summaryText.feedback;
  const current = file.myFeedback ?? null;
  const saving = ratingFileId === file.fileId;
  const press = (rating: SummaryFeedbackRating, reason: SummaryFeedbackReason | null = null) =>
    onRate(file.fileId, nextFeedback(current, rating, reason));
  const buttonClass = (active: boolean) =>
    `inline-flex h-9 w-9 items-center justify-center rounded-lg border transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 disabled:cursor-wait disabled:opacity-60 ${
      active ? 'border-primary-600 bg-primary-50 text-primary-700' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'}`;
  return (
    <div className="space-y-2 border-t border-slate-200 pt-3" role="group" aria-label={text.group}>
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-body-sm text-slate-600">{text.question}</span>
        <button type="button" className={buttonClass(current?.rating === 'Helpful')} aria-pressed={current?.rating === 'Helpful'}
          aria-label={text.useful} title={text.useful} disabled={saving} onClick={() => press('Helpful')}>
          <ThumbsUp className="h-4 w-4" aria-hidden="true" />
        </button>
        <button type="button" className={buttonClass(current?.rating === 'NotHelpful')} aria-pressed={current?.rating === 'NotHelpful'}
          aria-label={text.notUseful} title={text.notUseful} disabled={saving} onClick={() => press('NotHelpful')}>
          <ThumbsDown className="h-4 w-4" aria-hidden="true" />
        </button>
        {saving && <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" aria-hidden="true" />}
        {current && !saving && <span className="text-caption text-slate-500" role="status">{text.saved}</span>}
      </div>
      {current?.rating === 'NotHelpful' && (
        <div className="flex flex-wrap items-center gap-2" role="group" aria-label={text.whyGroup}>
          <span className="text-caption text-slate-500">{text.why}</span>
          {SUMMARY_FEEDBACK_REASONS.map((reason) => (
            <button key={reason.value} type="button" aria-pressed={current.reason === reason.value} disabled={saving}
              onClick={() => press('NotHelpful', reason.value)}
              className={`min-h-9 rounded-full border px-3 text-caption transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 disabled:cursor-wait disabled:opacity-60 ${
                current.reason === reason.value ? 'border-primary-600 bg-primary-50 text-primary-700' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'}`}>
              {reason.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

/** The summary of one file, shown when its row is opened. While the AI is still writing, the quick view is shown. */
function FileSummaryDetails({ file, maxCharacters, requirementsOpen }: { file: FileSummary; maxCharacters: number; requirementsOpen: boolean }) {
  const text = summaryText.file;
  const { onQuote } = useContext(SummaryActionsContext);
  if (file.status === 'Failed') return <p className="text-body-sm text-danger-dark">{summaryErrorMessage(file.errorCode)}</p>;
  const content = file.content;
  if (!content) {
    if (!file.preview) return null;
    return (
      <div className="space-y-3">
        <p className="flex items-center gap-2 text-caption text-slate-500" role="status">
          <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" aria-hidden="true" />
          {text.quickViewWaiting}
        </p>
        <IgnoredInstructionsNote count={file.preview.ignoredInstructions} />
        {file.preview.littleText && <LittleTextNote />}
        <QuickView highlights={file.preview.highlights} highlightPages={file.preview.highlightPages} requirements={file.preview.requirements}
          conflicts={file.preview.conflicts ?? []} statusItems={file.preview.statusItems ?? []} fileId={file.fileId} fileName={file.name}
          requirementsOpen={requirementsOpen} />
      </div>
    );
  }
  return (
    <div className="space-y-3">
      <IgnoredInstructionsNote count={content.ignoredInstructions} />
      {content.littleText && <LittleTextNote />}
      {content.overviewFromSource && <p className="text-caption text-slate-500">{text.overviewFromSource}</p>}
      {/* What the AI wrote about the submission stays in the language of the submission. */}
      <p className="whitespace-pre-line text-body text-slate-700">{content.overview}</p>
      {(content.overviewEvidence?.length ?? 0) > 0 && (
        <details className="text-body-sm text-slate-600">
          <summary className="cursor-pointer py-2 font-medium">{text.overviewSources}</summary>
          <div className="space-y-2">{content.overviewEvidence?.map((evidence) => (
            <Quoted key={evidence.quote} text={evidence.quote} fileId={file.fileId} fileName={file.name} page={evidence.page} />
          ))}</div>
        </details>
      )}
      {content.keyPoints.length > 0 && (
        <ul className="list-disc space-y-1 pl-5 text-body-sm text-slate-700">
          {content.keyPoints.map((point) => {
            const evidence = content.keyPointEvidence?.find((item) => item.text === point);
            return <li key={point}>
              {point}
              {evidence && <details className="mt-1 text-caption text-slate-600">
                <summary className="cursor-pointer py-1 font-medium">{text.showEvidence}</summary>
                <Quoted text={evidence.quote} fileId={file.fileId} fileName={file.name} page={evidence.page} />
              </details>}
            </li>;
          })}
        </ul>
      )}
      <QuickView highlights={content.highlights} highlightPages={content.highlightPages} requirements={content.requirements}
        conflicts={content.conflicts ?? []} statusItems={content.statusItems ?? []} fileId={file.fileId} fileName={file.name}
        requirementsOpen={requirementsOpen} />
      {content.questions.length > 0 && (
        <div className="space-y-2">
          <SectionLabel icon={HelpCircle}>{text.questions}</SectionLabel>
          <ul className="list-disc space-y-1 pl-5 text-body-sm text-slate-700">
            {content.questions.map((question) => (
              <li key={question}>
                {question}
                {/* A question can be put into the comment box of the checkpoint, to be asked of the team. */}
                {onQuote && (
                  <span className="ml-2 inline-block align-middle">
                    <ActionChip icon={MessageSquarePlus} onClick={() => onQuote(question)} label={summaryText.common.addToFeedback}>
                      {summaryText.common.feedback}
                    </ActionChip>
                  </span>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}
      <p className="text-caption text-slate-500">
        {file.inputTruncated && text.longFile(maxCharacters)}
        {file.generatedAt && text.summarisedAt(formatSummaryTime(file.generatedAt))}
      </p>
      <FeedbackBar file={file} />
    </div>
  );
}

interface FileRowProps {
  file: FileSummary;
  selected: boolean;
  onToggleSelected: () => void;
  onRetry: () => void;
  maxCharacters: number;
  disabled: boolean;
  /** The only file of the checkpoint: its summary is shown without a click once it is ready. */
  autoOpen: boolean;
  /** Show the requirement list of this file open (the only file of the checkpoint). */
  requirementsOpen: boolean;
}

export function FileRow({ file, selected, onToggleSelected, onRetry, maxCharacters, disabled, autoOpen, requirementsOpen }: FileRowProps) {
  // null until the reviewer opens or closes the row: then the automatic choice applies.
  const [chosen, setChosen] = useState<boolean | null>(null);
  const regionId = useId();
  const text = summaryText.file;
  const status = fileStatusView(file);
  const canOpen = canOpenFile(file);
  const selectable = isFileSelectable(file);
  const open = canOpen && (chosen ?? (autoOpen && file.status === 'Ready'));
  const refresh = file.status === 'Ready' && file.needsRefresh;
  return (
    <li className="rounded-lg border border-slate-200">
      <div className="flex items-center gap-2 px-3 py-2">
        <input
          type="checkbox"
          className="h-4 w-4 shrink-0 accent-primary disabled:opacity-40"
          checked={selected}
          disabled={!selectable || disabled}
          onChange={onToggleSelected}
          aria-label={selectable ? text.tickAria(file.name) : `${file.name}: ${status.label}`}
        />
        <button
          type="button"
          onClick={() => setChosen(!open)}
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
        {(file.status === 'Failed' || refresh) && (
          <Button variant="outline" size="sm" icon={RefreshCw} disabled={disabled} onClick={onRetry}
            aria-label={refresh ? text.regenerateAria(file.name) : text.retryAria(file.name)}>
            {refresh ? text.regenerate : summaryText.common.retry}
          </Button>
        )}
      </div>
      {open && (
        <div id={regionId} className="border-t border-slate-200 px-4 py-3">
          {refresh && <p className="mb-3 text-body-sm text-slate-600">{text.needsRefresh}</p>}
          <FileSummaryDetails file={file} maxCharacters={maxCharacters} requirementsOpen={requirementsOpen} />
        </div>
      )}
    </li>
  );
}
