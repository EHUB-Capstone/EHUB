import { useId, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ChevronDown, Eye, Flame, MessageSquare, type LucideIcon } from 'lucide-react';
import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { showcaseApi } from '../../api/showcaseApi';
import { CHART_CHROME, CHART_COLORS } from '../../constants/chartColors';
import type { ShowcaseInsightsDayDto, ShowcaseInsightsDto } from '../../types/showcase';
import { parseApiError } from '../../utils/apiError';
import { insightsDelta, insightsTotal, type InsightsMetric } from '../../utils/showcase';
import { cn } from '../../utils/cn';
import ErrorState from '../ui/ErrorState';
import LoadingSkeleton from '../ui/LoadingSkeleton';
import { SHOWCASE_ROW_ACTION } from './rowActionStyles';

interface ShowcaseInsightsProps {
  showcaseId: string;
}

const METRICS: ReadonlyArray<{ key: InsightsMetric; label: string; icon: LucideIcon; color: string }> = [
  { key: 'views', label: 'Views', icon: Eye, color: CHART_COLORS.navy },
  { key: 'upvotes', label: 'Interested', icon: Flame, color: CHART_COLORS.brand },
  { key: 'comments', label: 'Comments', icon: MessageSquare, color: CHART_COLORS.success },
];

const SPARK_WIDTH = 90;
const SPARK_HEIGHT = 30;

/** Tiny trend line of one metric; flat at the bottom when nothing happened. */
function Sparkline({ daily, metric, color }: { daily: ShowcaseInsightsDayDto[]; metric: InsightsMetric; color: string }) {
  const values = daily.map((day) => day[metric]);
  const max = Math.max(1, ...values);
  const step = values.length > 1 ? SPARK_WIDTH / (values.length - 1) : 0;
  const points = values.map((value, index) => `${(index * step).toFixed(1)},${(SPARK_HEIGHT - 3 - (value / max) * (SPARK_HEIGHT - 6)).toFixed(1)}`).join(' ');
  return (
    <svg width={SPARK_WIDTH} height={SPARK_HEIGHT} viewBox={`0 0 ${SPARK_WIDTH} ${SPARK_HEIGHT}`} aria-hidden="true" className="shrink-0">
      <polyline points={points} fill="none" stroke={color} strokeWidth="2" strokeLinejoin="round" strokeLinecap="round" />
    </svg>
  );
}

const DELTA_TONE = {
  up: 'text-success-dark',
  down: 'text-slate-600',
  flat: 'text-slate-500',
} as const;

function Tile({ data, metric }: { data: ShowcaseInsightsDto; metric: (typeof METRICS)[number] }) {
  const total = insightsTotal(data.daily, metric.key);
  const delta = insightsDelta(total, data.previous[metric.key]);
  const Icon = metric.icon;
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-3.5">
      <p className="flex items-center gap-1.5 text-caption font-semibold text-slate-600">
        <Icon className="h-4 w-4" aria-hidden="true" />
        {metric.label} · 14d
      </p>
      <div className="mt-0.5 flex items-end justify-between gap-2">
        <p className="text-display text-secondary">{total.toLocaleString()}</p>
        <Sparkline daily={data.daily} metric={metric.key} color={metric.color} />
      </div>
      <p className={cn('text-caption font-semibold', DELTA_TONE[delta.direction])}>
        {delta.label}<span className="font-normal text-slate-500"> vs previous 14 days</span>
      </p>
    </div>
  );
}

/**
 * Engagement of a page over the last fourteen days: three tiles with their trend, and a larger chart on request.
 * Always on top of the card's stretched link so the button and the chart stay usable.
 */
export default function ShowcaseInsights({ showcaseId }: ShowcaseInsightsProps) {
  const [open, setOpen] = useState(false);
  const panelId = useId();
  const insights = useQuery({
    queryKey: ['showcase', showcaseId, 'insights'],
    queryFn: ({ signal }) => showcaseApi.getInsights(showcaseId, signal),
    staleTime: 30_000,
  });

  if (insights.isPending) return <div className="relative z-10 mt-4" role="status" aria-label="Loading showcase insights"><LoadingSkeleton variant="card" /></div>;
  if (insights.isError) {
    return (
      <div className="relative z-10 mt-4">
        <ErrorState title="Could not load insights" message={parseApiError(insights.error, 'Failed to load showcase insights.').message} onRetry={() => { void insights.refetch(); }} />
      </div>
    );
  }

  const data = insights.data;
  const hasActivity = data.daily.some((day) => day.views > 0 || day.upvotes > 0 || day.comments > 0);
  return (
    <section className="relative z-10 mt-4" aria-label="Showcase insights">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        {METRICS.map((metric) => <Tile key={metric.key} data={data} metric={metric} />)}
      </div>

      <div className="mt-3 flex flex-wrap items-center justify-between gap-2">
        <p className="text-caption text-slate-500"></p>
        <button type="button" aria-expanded={open} aria-controls={panelId} onClick={() => setOpen((value) => !value)} className={cn(SHOWCASE_ROW_ACTION.navy, open && 'bg-secondary-100')}>
          Details
          <ChevronDown className={cn('h-4 w-4 transition-transform', open && 'rotate-180')} aria-hidden="true" />
        </button>
      </div>

      {open && (
        <div id={panelId} className="mt-3 rounded-xl border border-slate-200 bg-white p-3.5">
          <p className="text-body-sm font-semibold text-secondary">
            Last 14 days <span className="font-normal text-slate-500"></span>
          </p>
          {hasActivity ? (
            <div className="mt-2 h-64" aria-label="Daily engagement over the last fourteen days">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={data.daily} margin={{ top: 12, right: 12, bottom: 4, left: -20 }}>
                  <CartesianGrid stroke={CHART_CHROME.grid} strokeDasharray="3 3" />
                  <XAxis dataKey="date" tickFormatter={(date: string) => date.slice(5)} tick={{ fontSize: 12 }} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 12 }} />
                  <Tooltip labelFormatter={(date) => String(date)} />
                  <Legend />
                  {METRICS.map((metric) => (
                    <Line key={metric.key} type="monotone" dataKey={metric.key} name={metric.label} stroke={metric.color} strokeWidth={2} dot={false} />
                  ))}
                </LineChart>
              </ResponsiveContainer>
            </div>
          ) : (
            <p className="mt-2 text-body-sm text-slate-600">No engagement in the last 14 days yet. Views, interest and comments will appear here.</p>
          )}
        </div>
      )}
    </section>
  );
}
