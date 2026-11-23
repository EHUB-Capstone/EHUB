import type { ReactNode } from 'react';
import { Line, LineChart, ResponsiveContainer } from 'recharts';
import Card from '../../../../components/ui/Card';
import { CHART_COLORS } from '../../../../constants/chartColors';
import { formatScore, type LatestSemesterSummary } from '../../../../utils/projectTrends';
import { CountChange, ScoreChange } from './TrendDelta';
import { useTranslation } from 'react-i18next';
import { formatNumber as fmtNumber } from '../../../../i18n/format';

function Sparkline({ values, label }: { values: (number | null)[]; label: string }) {
  const data = values.map((value, index) => ({ index, value }));
  return (
    <div className="h-8 w-24 shrink-0" role="img" aria-label={label}>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data} margin={{ top: 4, right: 4, bottom: 4, left: 4 }}>
          <Line
            type="linear"
            dataKey="value"
            stroke={CHART_COLORS.brand}
            strokeWidth={2}
            dot={data.length === 1 ? { r: 3 } : false}
            isAnimationActive={false}
            connectNulls={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

function Tile({ title, caption, children, aside }: { title: string; caption: string; children: ReactNode; aside?: ReactNode }) {
  return (
    <Card tone="inset" hover={false} padding="p-4" className="min-w-0">
      <p className="text-overline uppercase text-slate-500">{title}</p>
      <div className="mt-1 flex items-end justify-between gap-3">
        <div className="min-w-0">{children}</div>
        {aside}
      </div>
      <p className="mt-1 text-caption text-slate-500">{caption}</p>
    </Card>
  );
}

/** Three headline numbers for the newest semester, each with how it moved since the one before. */
export default function TrendKpiTiles({ summary }: { summary: LatestSemesterSummary }) {
  const { t } = useTranslation('projectData');
  const scores = summary.scoreHistory.map(item => item.score);
  const potential = summary.potentialHistory.map(item => item.count);

  return (
    <div className="mt-3 grid gap-3 md:grid-cols-3" aria-label={t('trendKpiTiles.latestSemester', { semesterCode: summary.semesterCode })}>
      <Tile
        title={t('trendKpiTiles.averageScore')}
        caption={summary.averageScore === null ? t('trendKpiTiles.notGradedYet', { semesterCode: summary.semesterCode }) : t('trendKpiTiles.outOf10', { semesterCode: summary.semesterCode })}
        aside={<Sparkline values={scores} label={t('trendKpiTiles.averageScoreBySemesterLatest', { v1: formatScore(summary.averageScore) })} />}
      >
        <p className="text-heading font-semibold text-secondary">{formatScore(summary.averageScore)}</p>
        <p className="text-body-sm"><ScoreChange change={summary.scoreChange} /></p>
      </Tile>

      <Tile title={t('trendKpiTiles.gradedGroups')} caption={t('trendKpiTiles.ofGroupsHaveAnOfficialScore', { coveragePercent: summary.coveragePercent })}>
        <p className="text-heading font-semibold text-secondary">
          {fmtNumber(summary.gradedGroups)} <span className="text-body text-slate-500">/ {fmtNumber(summary.totalGroups)}</span>
        </p>
        <span className="mt-2 block h-2 w-full min-w-24 rounded-full bg-slate-200" aria-hidden="true">
          <span className="block h-2 rounded-full bg-primary" style={{ width: `${summary.coveragePercent}%` }} />
        </span>
      </Tile>

      <Tile
        title={t('trendKpiTiles.potentialProjects')}
        caption={t('trendKpiTiles.groupsLabelledPotential', { semesterCode: summary.semesterCode })}
        aside={<Sparkline values={potential} label={t('trendKpiTiles.potentialProjectsBySemesterLatest', { potentialGroups: summary.potentialGroups })} />}
      >
        <p className="text-heading font-semibold text-secondary">{fmtNumber(summary.potentialGroups)}</p>
        <p className="text-body-sm"><CountChange change={summary.potentialChange} /></p>
      </Tile>
    </div>
  );
}
