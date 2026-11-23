// The review brief: how much of the checklist the files mention, what to look at first, and the brief as text to copy or
// to drop into the feedback box. Lives in the overview of the AI summary card.
import toast from 'react-hot-toast';
import { AlertTriangle, Check, ClipboardList, Copy, Info, MessageSquarePlus } from 'lucide-react';
import Button from '../../../ui/Button';
import Card from '../../../ui/Card';
import { buildFeedbackDraft, buildReviewBrief, requirementCoverage, summaryAttentionItems } from '../../../../utils/checkpointSummary';
import type { CheckpointSummary } from '../../../../types/checkpointSummary';
import { SectionLabel } from './SummaryParts';
import { useTranslation } from 'react-i18next';

const ATTENTION_ICON = { danger: AlertTriangle, warning: AlertTriangle, info: Info } as const;
const ATTENTION_COLOR = { danger: 'text-danger-dark', warning: 'text-warning-dark', info: 'text-slate-500' } as const;

/**
 * The state of the checkpoint at a glance: how much of the checklist the files mention and what to look at first, with the
 * whole brief as text to copy or drop into the feedback box. Counts come from what the card already holds; nothing is graded.
 */
export function ReviewBrief({ summary, onQuote }: { summary: CheckpointSummary; onQuote?: (text: string) => void }) {
  const { t } = useTranslation('checkpoint');
  const hasSummary = summary.files.some((file) => file.status === 'Ready' && file.content !== null);
  const coverage = requirementCoverage(summary.requirements);
  const attention = summaryAttentionItems(summary);
  if (!hasSummary && attention.length === 0) return null;
  const checked = coverage.found + coverage.notFound;
  const share = (count: number) => (checked > 0 ? `${(count / checked) * 100}%` : '0%');

  const copyBrief = async () => {
    try {
      await navigator.clipboard.writeText(buildReviewBrief(summary));
      toast.success(t('reviewBrief.briefCopied'));
    } catch {
      toast.error(t('reviewBrief.couldNotCopyTheBriefPlease'));
    }
  };
  // The feedback box is read by the team, so what goes there is a draft for the reviewer to edit, not the internal brief.
  const feedbackDraft = buildFeedbackDraft(summary);
  const insertDraft = () => {
    onQuote?.(feedbackDraft);
    toast.success(t('reviewBrief.draftAddedToTheFeedbackBox'));
  };

  return (
    <Card tone="inset" padding="p-4" hover={false} className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <SectionLabel icon={ClipboardList}>{t('reviewBrief.reviewBrief')}</SectionLabel>
        {hasSummary && (
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="outline" size="sm" icon={Copy} onClick={copyBrief}>{t('reviewBrief.copyBrief')}</Button>
            {onQuote && feedbackDraft && <Button variant="outline" size="sm" icon={MessageSquarePlus} onClick={insertDraft}>{t('reviewBrief.draftFeedback')}</Button>}
          </div>
        )}
      </div>

      {checked > 0 && (
        <div className="space-y-1.5">
          <p className="text-body-sm text-slate-700">
            {t('reviewBrief.relatedContentFoundFor')} <span className="font-semibold text-slate-900">{t('reviewBrief.of', { found: coverage.found, searchable: coverage.searchable })}</span> {t('reviewBrief.requirementsAndRubricCriteriaAFile')}
          </p>
          <div
            className="flex h-2 w-full overflow-hidden rounded-full bg-slate-100"
            role="img"
            aria-label={t('reviewBrief.foundNotFoundNotChecked', { found: coverage.found, notFound: coverage.notFound, notChecked: coverage.notChecked })}
          >
            <span className="h-full bg-success" style={{ width: share(coverage.found) }} />
            <span className="h-full bg-slate-400" style={{ width: share(coverage.notFound) }} />
          </div>
          <p className="text-caption text-slate-500">
            {t('reviewBrief.foundNotFoundFoundMeansA', { found: coverage.found, notFound: coverage.notFound, v3: coverage.notChecked > 0 ? ` · ${t('reviewBrief.notCheckedCount', { count: coverage.notChecked })}` : '', v4: coverage.elsewhere > 0 ? ` · ${t('reviewBrief.judgedElsewhereCount', { count: coverage.elsewhere })}` : '' })}
          </p>
        </div>
      )}

      {attention.length > 0 ? (
        <ul className="space-y-1.5" aria-label={t('reviewBrief.worthChecking')}>
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
          {t('reviewBrief.nothingFlaggedInTheSummarisedFiles')}
        </p>
      )}
    </Card>
  );
}
