// Small pieces shared by every part of the AI summary: a section label, a quote with its actions, the notes about a file.
import { useContext } from 'react';
import { AlertTriangle, Check, FileSearch, FileText, Minus } from 'lucide-react';
import { ignoredInstructionsMessage, pageLabel } from '../../../../utils/checkpointSummary';
import { summaryText } from '../../../../utils/summaryText';
import type { CriterionCue } from '../../../../types/checkpointSummary';
import { SummaryActionsContext } from './summaryContext';

export const SectionLabel = ({ icon: Icon, children }: { icon: typeof FileText; children: string }) => (
  <h4 className="flex items-center gap-1.5 text-overline font-bold uppercase text-slate-500">
    <Icon className="h-3.5 w-3.5" aria-hidden="true" />
    {children}
  </h4>
);

/** `label` is the full name, read by screen readers and shown as a tooltip when the visible text is shorter or absent. */
export const ActionChip = ({ icon: Icon, onClick, label, children }: { icon: typeof FileText; onClick: () => void; label: string; children?: string }) => (
  <button
    type="button"
    onClick={onClick}
    aria-label={label}
    title={label}
    className="inline-flex min-h-6 items-center gap-1 rounded border border-slate-200 bg-white px-1.5 py-0.5 text-overline font-medium text-slate-600 transition hover:bg-slate-50 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
  >
    <Icon className="h-2.5 w-2.5" aria-hidden="true" />
    {children}
  </button>
);

interface QuotedProps {
  text: string;
  /** The file name shown after the quote (when several files are mixed in one list). */
  source?: string | null;
  /** The file the quote comes from and the page in it, so the quote can be opened there. */
  fileId?: string | null;
  fileName?: string | null;
  page?: number | null;
}

/** A sentence copied from a file, with one thing a reviewer does next: open the file at the page it comes from. */
export const Quoted = ({ text, source, fileId, fileName, page }: QuotedProps) => {
  const { onOpenFile } = useContext(SummaryActionsContext);
  const name = fileName ?? source ?? null;
  return (
    <blockquote className="border-l-2 border-slate-300 pl-3 text-body-sm italic text-slate-600">
      “{text}”{source && <span className="ml-1 text-caption not-italic text-slate-500">— {source}</span>}
      {/* Inline, at the end of the quote: a row of its own would cost a line of height for every quote. */}
      <span className="ml-2 inline-block align-middle not-italic">
        {onOpenFile && fileId && name && (
          <ActionChip icon={FileSearch} onClick={() => onOpenFile({ id: fileId, name }, page ?? undefined)}
            label={page ? summaryText.quote.openAt(pageLabel(name, page)) : summaryText.common.openFile}>
            {page ? pageLabel(name, page) : summaryText.common.open}
          </ActionChip>
        )}
      </span>
    </blockquote>
  );
};

/** What the Excellent level expects, and which of it the file mentions: presence of evidence, not a grade. */
export function Cues({ cues }: { cues: CriterionCue[] }) {
  if (cues.length === 0) return null;
  const text = summaryText.requirements;
  return (
    <ul className="flex flex-wrap gap-1.5" aria-label={text.expectedContent}>
      {cues.map((cue) => (
        <li key={cue.label} title={cue.evidence ?? (cue.found ? text.cueMentionedNoQuote : text.cueNotMentioned)}
          className={`flex items-center gap-1 rounded-md border px-1.5 py-0.5 text-caption ${cue.found
            ? 'border-success-light bg-success-50 text-success-dark' : 'border-slate-200 bg-slate-50 text-slate-500'}`}>
          {cue.found ? <Check className="h-3 w-3" aria-hidden="true" /> : <Minus className="h-3 w-3" aria-hidden="true" />}
          <span className="sr-only">{cue.found ? text.mentioned : text.notMentioned}</span>
          {cue.label}
        </li>
      ))}
    </ul>
  );
}

/** The file tried to instruct the AI; the sentences were dropped before summarising, but a reviewer should know. */
export const IgnoredInstructionsNote = ({ count }: { count?: number }) => {
  const message = ignoredInstructionsMessage(count);
  if (!message) return null;
  return (
    <p className="flex items-start gap-2 rounded-lg border border-warning-light bg-warning-50 px-3 py-2 text-caption text-warning-dark" role="note">
      <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
      {message}
    </p>
  );
};

export const LittleTextNote = () => (
  <p className="flex items-start gap-2 rounded-lg border border-warning-light bg-warning-50 px-3 py-2 text-caption text-warning-dark">
    <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
    {summaryText.file.littleText}
  </p>
);
