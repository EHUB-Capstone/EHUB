import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { CHART_CHROME, CHART_SERIES } from '../../../../constants/chartColors';
import type { OutcomeChartRow } from '../../../../utils/projectTrends';

export const OUTCOME_SERIES = [
  { key: 'Potential', color: CHART_SERIES[0] },
  { key: 'Internally Funded', color: CHART_SERIES[1] },
  { key: 'Externally Funded', color: CHART_SERIES[2] },
  { key: 'Awarded', color: CHART_SERIES[3] },
] as const;

interface TooltipProps {
  active?: boolean;
  label?: string | number;
  payload?: ReadonlyArray<{ payload?: OutcomeChartRow }>;
}

function OutcomeTooltip({ active, label, payload }: TooltipProps) {
  const row = payload?.[0]?.payload;
  if (!active || !row) return null;

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-3 text-body-sm shadow-card">
      <p className="mb-1 font-semibold text-secondary">{label}</p>
      <ul className="space-y-1">
        {OUTCOME_SERIES.map(item => (
          <li key={item.key} className="flex items-center gap-2">
            <span className="h-0.5 w-3 shrink-0 rounded-full" style={{ backgroundColor: item.color }} aria-hidden="true" />
            <span className="font-semibold text-secondary">{row[item.key]}</span>
            <span className="text-slate-500">{item.key}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** Potential, Internally Funded, Externally Funded and Awarded groups per semester. Grouped (not stacked) because the labels overlap. */
export default function TrendOutcomeChart({ data }: { data: OutcomeChartRow[] }) {
  return (
    <div className="h-64 w-full">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data} barGap={2} margin={{ top: 20, right: 12, bottom: 4, left: 0 }}>
          <CartesianGrid stroke={CHART_CHROME.grid} vertical={false} />
          <XAxis dataKey="semester" tick={{ fill: CHART_CHROME.axis, fontSize: 12 }} tickLine={false} axisLine={{ stroke: CHART_CHROME.grid }} />
          <YAxis
            allowDecimals={false}
            width={32}
            tick={{ fill: CHART_CHROME.axis, fontSize: 12 }}
            tickLine={false}
            axisLine={false}
          />
          <Tooltip
            cursor={{ fill: CHART_CHROME.grid, fillOpacity: 0.35 }}
            content={props => <OutcomeTooltip active={props.active} label={props.label} payload={props.payload} />}
          />
          {OUTCOME_SERIES.map(item => (
            <Bar
              key={item.key}
              dataKey={item.key}
              fill={item.color}
              maxBarSize={22}
              radius={[4, 4, 0, 0]}
              isAnimationActive={false}
            />
          ))}
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
