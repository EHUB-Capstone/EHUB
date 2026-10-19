import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { CHART_CHROME, CHART_COLORS, CHART_SERIES } from '../../../../constants/chartColors';
import {
  formatScore,
  formatScoreChange,
  type ScoreChartModel,
  type ScoreChartRow,
  type ScoreSeries,
} from '../../../../utils/projectTrends';

const seriesColor = (series: ScoreSeries): string =>
  series.slot === null ? CHART_COLORS.brand : CHART_SERIES[series.slot];

const numberOrNull = (value: string | number | null | undefined): number | null =>
  typeof value === 'number' ? value : null;

interface TooltipProps {
  active?: boolean;
  label?: string | number;
  payload?: ReadonlyArray<{ payload?: ScoreChartRow }>;
  series: ScoreSeries[];
}

/** The reader has the semester and wants the numbers, so the value leads and the name follows. */
function ScoreTooltip({ active, label, payload, series }: TooltipProps) {
  const row = payload?.[0]?.payload;
  if (!active || !row) return null;

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-3 text-body-sm shadow-card">
      <p className="mb-1 font-semibold text-secondary">{label}</p>
      <ul className="space-y-1">
        {series.map(item => {
          const score = numberOrNull(row[item.key]);
          if (score === null && numberOrNull(row[`${item.key}:total`]) === null) return null;
          return (
            <li key={item.key} className="flex items-center gap-2">
              <span className="h-0.5 w-3 shrink-0 rounded-full" style={{ backgroundColor: seriesColor(item) }} aria-hidden="true" />
              <span className="font-semibold text-secondary">{formatScore(score)}</span>
              <span className="text-slate-500">
                {item.label}, {numberOrNull(row[`${item.key}:graded`]) ?? 0}/{numberOrNull(row[`${item.key}:total`]) ?? 0} graded
                {numberOrNull(row[`${item.key}:change`]) !== null && ` (${formatScoreChange(numberOrNull(row[`${item.key}:change`]))})`}
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

interface DotProps {
  cx?: number;
  cy?: number;
  index?: number;
  value?: number | string | null;
}

/** Average course score per semester. A semester with nothing graded leaves a gap in the line, not a zero. */
export default function TrendScoreChart({ model }: { model: ScoreChartModel }) {
  const lastIndexBySeries = new Map<string, number>();
  model.data.forEach((row, index) => {
    for (const item of model.series) {
      if (numberOrNull(row[item.key]) !== null) lastIndexBySeries.set(item.key, index);
    }
  });
  const singleSeries = model.series.length === 1;

  return (
    <div className="h-64 w-full">
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={model.data} margin={{ top: 20, right: 28, bottom: 4, left: 0 }}>
          <CartesianGrid stroke={CHART_CHROME.grid} vertical={false} />
          <XAxis dataKey="semester" tick={{ fill: CHART_CHROME.axis, fontSize: 12 }} tickLine={false} axisLine={{ stroke: CHART_CHROME.grid }} />
          <YAxis
            domain={[0, 10]}
            ticks={[0, 2, 4, 6, 8, 10]}
            width={32}
            tick={{ fill: CHART_CHROME.axis, fontSize: 12 }}
            tickLine={false}
            axisLine={false}
          />
          <Tooltip
            cursor={{ stroke: CHART_CHROME.grid }}
            content={props => <ScoreTooltip active={props.active} label={props.label} payload={props.payload} series={model.series} />}
          />
          {model.series.map(item => {
            const color = seriesColor(item);
            const lastIndex = lastIndexBySeries.get(item.key);
            return (
              <Line
                key={item.key}
                type="linear"
                dataKey={item.key}
                name={item.label}
                stroke={color}
                strokeWidth={2}
                connectNulls={false}
                isAnimationActive={false}
                activeDot={{ r: 6, stroke: CHART_CHROME.surface, strokeWidth: 2 }}
                dot={(dot: DotProps) => {
                  if (typeof dot.value !== 'number' || dot.cx === undefined || dot.cy === undefined) {
                    return <g key={`${item.key}-${dot.index}`} />;
                  }
                  // Selective direct label: only the newest point of a single line. With several lines the legend names them,
                  // because labels at the same end point would overlap.
                  const label = singleSeries && dot.index === lastIndex ? dot.value.toFixed(1) : null;
                  return (
                    <g key={`${item.key}-${dot.index}`}>
                      <circle cx={dot.cx} cy={dot.cy} r={5} fill={color} stroke={CHART_CHROME.surface} strokeWidth={2} />
                      {label !== null && (
                        <text x={dot.cx} y={dot.cy - 12} textAnchor="middle" fontSize={12} fontWeight={600} fill={CHART_CHROME.axis}>
                          {label}
                        </text>
                      )}
                    </g>
                  );
                }}
              />
            );
          })}
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
