import { AlertCircle, History } from 'lucide-react';
import type { TeamContinuationItem, TeamContinuationSummary } from '../../types/classes';
import {
  continuationHeadline,
  continuationNeedsAttention,
  continuationOutcomeLabel,
  hasContinuationActivity,
} from '../../utils/teamContinuity';
import { useTranslation } from 'react-i18next';

interface TeamContinuationPanelProps {
  summary: TeamContinuationSummary | null | undefined;
  /** false while previewing (nothing saved yet), true after the import was committed. */
  applied: boolean;
}

const outcomeStyles: Record<TeamContinuationItem['outcome'], string> = {
  Created: 'bg-green-100 text-green-700',
  MembersAdded: 'bg-blue-100 text-blue-700',
  NoChange: 'bg-slate-100 text-slate-600',
  NotEligible: 'bg-amber-100 text-amber-800',
  Dissolved: 'bg-slate-100 text-slate-600',
  ContinuedElsewhere: 'bg-slate-100 text-slate-600',
};

export default function TeamContinuationPanel({ summary, applied }: TeamContinuationPanelProps) {
  const { t } = useTranslation('class');
  if (!summary || !hasContinuationActivity(summary)) return null;

  const attention = summary.items.some(continuationNeedsAttention);

  return (
    <section
      aria-label={t('teamContinuationPanel.teamContinuationFromThePreviousSemester')}
      className={`overflow-hidden rounded-xl border ${attention ? 'border-amber-200' : 'border-blue-200'}`}
    >
      <div className={`flex items-start gap-2.5 px-4 py-3 text-sm ${attention ? 'bg-amber-50 text-amber-900' : 'bg-blue-50 text-blue-900'}`}>
        <History className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
        <div>
          <p className="font-semibold">{t('teamContinuationPanel.teamsContinuedFromThePreviousSemester')}</p>
          <p className="mt-0.5 text-xs">{continuationHeadline(summary, applied)}.</p>
        </div>
      </div>
      <ul className="max-h-64 divide-y divide-slate-100 overflow-y-auto bg-white">
        {summary.items.map((item) => (
          <li key={item.sourceTeamId} className="px-4 py-3 text-xs">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-semibold text-slate-900">{item.teamName}</span>
              <span className={`rounded-full px-2 py-0.5 text-overline font-bold uppercase ${outcomeStyles[item.outcome] ?? outcomeStyles.NoChange}`}>
                {continuationOutcomeLabel(item.outcome)}
              </span>
              <span className="text-slate-500">
                {t('teamContinuationPanel.fromClass', { classCode: item.sourceClassCode })}
                {item.memberCount > 0 ? ` · ${t('teamContinuationPanel.members', { count: item.memberCount })}` : ''}
              </span>
            </div>
            {item.reasons.length > 0 && (
              <ul className="mt-1.5 space-y-0.5 text-amber-800">
                {item.reasons.map((reason) => (
                  <li key={reason} className="flex items-start gap-1.5">
                    <AlertCircle className="mt-0.5 h-3 w-3 shrink-0" aria-hidden="true" />
                    <span>{reason}</span>
                  </li>
                ))}
              </ul>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
