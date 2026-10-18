import type { ReactNode } from 'react';
import { Line, LineChart, ResponsiveContainer } from 'recharts';
import Card from '../../../../components/ui/Card';
import { CHART_COLORS } from '../../../../constants/chartColors';
import { formatScore, type LatestSemesterSummary } from '../../../../utils/projectTrends';
import { CountChange, ScoreChange } from './TrendDelta';

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
  const scores = summary.scoreHistory.map(item => item.score);
  const potential = summary.potentialHistory.map(item => item.count);

  return (
    <div className="mt-3 grid gap-3 md:grid-cols-3" aria-label={`Latest semester ${summary.semesterCode}`}>
      <Tile
        title="Average score"
        caption={summary.averageScore === null ? `${summary.semesterCode}: not graded yet` : `${summary.semesterCode}, out of 10`}
        aside={<Sparkline values={scores} label={`Average score by semester, latest ${formatScore(summary.averageScore)}`} />}
      >
        <p className="text-heading font-semibold text-secondary">{formatScore(summary.averageScore)}</p>
        <p className="text-body-sm"><ScoreChange change={summary.scoreChange} /></p>
      </Tile>

      <Tile title="Graded groups" caption={`${summary.coveragePercent}% of groups have an official score`}>
        <p className="text-heading font-semibold text-secondary">
          {summary.gradedGroups.toLocaleString()} <span className="text-body text-slate-500">/ {summary.totalGroups.toLocaleString()}</span>
        </p>
        <span className="mt-2 block h-2 w-full min-w-24 rounded-full bg-slate-200" aria-hidden="true">
          <span className="block h-2 rounded-full bg-primary" style={{ width: `${summary.coveragePercent}%` }} />
        </span>
      </Tile>

      <Tile
        title="Potential projects"
        caption={`${summary.semesterCode}, groups labelled Potential`}
        aside={<Sparkline values={potential} label={`Potential projects by semester, latest ${summary.potentialGroups}`} />}
      >
        <p className="text-heading font-semibold text-secondary">{summary.potentialGroups.toLocaleString()}</p>
        <p className="text-body-sm"><CountChange change={summary.potentialChange} /></p>
      </Tile>
    </div>
  );
}
