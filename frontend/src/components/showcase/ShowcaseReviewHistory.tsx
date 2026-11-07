import { useId, useState, type ReactNode } from 'react';
import { ChevronDown, GraduationCap, History, MessageSquareText } from 'lucide-react';
import Badge from '../ui/Badge';
import { SHOWCASE_ROW_ACTION } from './rowActionStyles';
import { cn } from '../../utils/cn';
import type { ShowcaseHistoryDto, ShowcaseStatus } from '../../types/showcase';
import type { ShowcaseTone } from '../../utils/showcase';

interface EventMeta {
  label: string;
  tone: ShowcaseTone;
}

// Written by the server when nobody typed a note; repeating them under the event label adds nothing.
const SYSTEM_NOTES = new Set([
  'Submitted for review.',
  'Approved for publication.',
  'Rejected.',
  'Hidden by a reviewer.',
  'Restored by a reviewer.',
  'Withdrawn by the project team.',
]);

function describeEvent(from: ShowcaseStatus, to: ShowcaseStatus): EventMeta {
  if (to === 'PendingReview') return { label: 'Submitted for review', tone: 'info' };
  if (from === 'PendingReview' && to === 'Published') return { label: 'Approved', tone: 'success' };
  if (from === 'PendingReview' && to === 'Draft') return { label: 'Rejected, returned to draft', tone: 'danger' };
  if (to === 'Hidden') return { label: 'Hidden', tone: 'neutral' };
  if (from === 'Hidden' && to === 'Published') return { label: 'Restored', tone: 'success' };
  return { label: 'Updated', tone: 'neutral' };
}

const DOT_TONE: Record<ShowcaseTone, string> = {
  success: 'bg-success',
  warning: 'bg-warning',
  danger: 'bg-danger',
  info: 'bg-info',
  brand: 'bg-primary',
  neutral: 'bg-slate-400',
};

const formatDate = (iso: string) => new Date(iso).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' });
const formatTime = (iso: string) => new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
const feedbackOf = (entry: ShowcaseHistoryDto): string | null => {
  const note = entry.note?.trim();
  return note && !SYSTEM_NOTES.has(note) ? note : null;
};

function Stamp({ iso }: { iso: string }) {
  return (
    <time dateTime={iso} className="text-caption text-slate-500">
      {formatDate(iso)} · {formatTime(iso)}
    </time>
  );
}

/**
 * The reviewer's most recent feedback while it still applies to the page (rejected back to draft, or hidden), so the
 * team sees who said what and when without opening the editor.
 */
export function ShowcaseLatestFeedback({ history, status }: { history: ShowcaseHistoryDto[]; status: ShowcaseStatus }) {
  const latest = history[0];
  if (!latest || latest.toStatus !== status) return null;
  const negative = (latest.fromStatus === 'PendingReview' && latest.toStatus === 'Draft') || latest.toStatus === 'Hidden';
  const feedback = feedbackOf(latest);
  if (!negative || !feedback) return null;

  return (
    <div role="note" className="mt-3 rounded-xl border border-warning-light bg-warning-50 px-4 py-3 text-warning-dark">
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-1">
        <p className="inline-flex items-center gap-1.5 text-body-sm font-semibold">
          <GraduationCap className="h-4 w-4" aria-hidden="true" />
          Feedback from {latest.actorName}
        </p>
        <time dateTime={latest.createdAt} className="text-caption">
          {formatDate(latest.createdAt)} · {formatTime(latest.createdAt)}
        </time>
      </div>
      <p className="mt-1.5 whitespace-pre-line break-words text-body">{feedback}</p>
    </div>
  );
}

/**
 * Action row of a My Showcase card: a "Review history" toggle followed by any extra actions (children), with the
 * newest-first timeline of status changes, their time and the reviewer feedback opening underneath.
 */
export default function ShowcaseReviewHistory({ history, children }: { history: ShowcaseHistoryDto[]; children?: ReactNode }) {
  const [open, setOpen] = useState(false);
  const panelId = useId();
  if (history.length === 0 && !children) return null;

  return (
    <div className="relative z-10 mt-4">
      <div className="flex flex-wrap items-center gap-2">
        {history.length > 0 && (
          <button
            type="button"
            aria-expanded={open}
            aria-controls={panelId}
            onClick={() => setOpen((value) => !value)}
            className={cn(SHOWCASE_ROW_ACTION.navy, open && 'bg-secondary-100')}
          >
            <History className="h-4 w-4" aria-hidden="true" />
            Review history
            <span className="rounded-full bg-secondary px-1.5 text-caption font-semibold text-white">{history.length}</span>
            <ChevronDown className={cn('h-4 w-4 transition-transform', open && 'rotate-180')} aria-hidden="true" />
          </button>
        )}
        {children}
      </div>

      {open && (
        <ol id={panelId} className="relative mt-3 space-y-5 before:absolute before:bottom-2 before:left-[5px] before:top-2 before:w-px before:bg-slate-200 dark:before:bg-slate-700">
          {history.map((entry, index) => {
            const event = describeEvent(entry.fromStatus, entry.toStatus);
            const feedback = feedbackOf(entry);
            return (
              <li key={`${entry.createdAt}-${index}`} className="relative pl-7">
                <span className={cn('absolute left-0 top-1.5 h-[11px] w-[11px] rounded-full ring-4 ring-white', DOT_TONE[event.tone])} aria-hidden="true" />
                <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant={event.tone} size="md">{event.label}</Badge>
                    <span className="text-body-sm text-slate-600">by <span className="font-medium text-secondary">{entry.actorName}</span></span>
                  </div>
                  <Stamp iso={entry.createdAt} />
                </div>
                {feedback && (
                  <div className="mt-2 flex gap-2.5 rounded-xl border border-slate-200 bg-slate-50 px-3.5 py-3">
                    <MessageSquareText className="mt-0.5 h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />
                    <div className="min-w-0">
                      <p className="text-overline uppercase text-slate-500">Feedback</p>
                      <p className="mt-0.5 whitespace-pre-line break-words text-body text-slate-700">{feedback}</p>
                    </div>
                  </div>
                )}
              </li>
            );
          })}
        </ol>
      )}
    </div>
  );
}
