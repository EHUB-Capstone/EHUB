// The review brief: how much of the checklist the files mention, what to look at first, and the brief as text to copy or
// to drop into the feedback box. Lives in the overview of the AI summary card.
import toast from 'react-hot-toast';
import { AlertTriangle, Check, ClipboardList, Copy, Info, MessageSquarePlus } from 'lucide-react';
import Button from '../../../ui/Button';
import Card from '../../../ui/Card';
import { buildFeedbackDraft, buildReviewBrief, requirementCoverage, summaryAttentionItems } from '../../../../utils/checkpointSummary';
import type { CheckpointSummary } from '../../../../types/checkpointSummary';
import { SectionLabel } from './SummaryParts';

const ATTENTION_ICON = { danger: AlertTriangle, warning: AlertTriangle, info: Info } as const;
const ATTENTION_COLOR = { danger: 'text-danger-dark', warning: 'text-warning-dark', info: 'text-slate-500' } as const;

/**
 * The state of the checkpoint at a glance: how much of the checklist the files mention and what to look at first, with the
 * whole brief as text to copy or drop into the feedback box. Counts come from what the card already holds; nothing is graded.
 */
export function ReviewBrief({ summary, onQuote }: { summary: CheckpointSummary; onQuote?: (text: string) => void }) {
  const hasSummary = summary.files.some((file) => file.status === 'Ready' && file.content !== null);
  const coverage = requirementCoverage(summary.requirements);
  const attention = summaryAttentionItems(summary);
  if (!hasSummary && attention.length === 0) return null;
  const checked = coverage.found + coverage.notFound;
  const share = (count: number) => (checked > 0 ? `${(count / checked) * 100}%` : '0%');

  const copyBrief = async () => {
    try {
      await navigator.clipboard.writeText(buildReviewBrief(summary));
      toast.success('Brief copied.');
    } catch {
      toast.error('Could not copy the brief. Please try again.');
    }
  };
  // The feedback box is read by the team, so what goes there is a draft for the reviewer to edit, not the internal brief.
  const feedbackDraft = buildFeedbackDraft(summary);
  const insertDraft = () => {
    onQuote?.(feedbackDraft);
    toast.success('Draft added to the feedback box. Edit it before posting.');
  };

  return (
    <Card tone="inset" padding="p-4" hover={false} className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SectionLabel icon={ClipboardList}>Review brief</SectionLabel>
        {hasSummary && (
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="outline" size="sm" icon={Copy} onClick={copyBrief}>Copy brief</Button>
            {onQuote && feedbackDraft && <Button variant="outline" size="sm" icon={MessageSquarePlus} onClick={insertDraft}>Draft feedback</Button>}
          </div>
        )}
      </div>

      {checked > 0 && (
        <div className="space-y-1.5">
          <p className="text-body-sm text-slate-700">
            Related content found for <span className="font-semibold text-slate-900">{coverage.found} of {coverage.searchable}</span> requirements
            and rubric criteria a file can show.
          </p>
          <div
            className="flex h-2 w-full overflow-hidden rounded-full bg-slate-100"
            role="img"
            aria-label={`${coverage.found} found, ${coverage.notFound} not found, ${coverage.notChecked} not checked`}
          >
            <span className="h-full bg-success" style={{ width: share(coverage.found) }} />
            <span className="h-full bg-slate-400" style={{ width: share(coverage.notFound) }} />
          </div>
          <p className="text-caption text-slate-500">
            {coverage.found} found · {coverage.notFound} not found
            {coverage.notChecked > 0 && ` · ${coverage.notChecked} not checked`}
            {coverage.elsewhere > 0 && ` · ${coverage.elsewhere} judged live, on the product or on the whole file`}.
            “Found” means a file mentions the item, not that the team met it.
          </p>
        </div>
      )}

      {attention.length > 0 ? (
        <ul className="space-y-1.5" aria-label="Worth checking">
          {attention.map((item) => {
            const Icon = ATTENTION_ICON[item.tone];
            return (
              <li key={item.key} className="flex items-start gap-2 text-body-sm text-slate-700">
                <Icon className={`mt-0.5 h-3.5 w-3.5 shrink-0 ${ATTENTION_COLOR[item.tone]}`} aria-hidden="true" />
                {item.text}
              </li>
            );
          })}
        </ul>
      ) : (
        <p className="flex items-start gap-2 text-body-sm text-slate-600">
          <Check className="mt-0.5 h-3.5 w-3.5 shrink-0 text-success-dark" aria-hidden="true" />
          Nothing flagged in the summarised files. The AI does not judge whether the work is good or complete.
        </p>
      )}
    </Card>
  );
}
