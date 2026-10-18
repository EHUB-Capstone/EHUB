/** Chart palette, aligned with the design tokens in src/index.css. */
export const CHART_COLORS = {
  navy: '#17365D',
  brand: '#F56A2A',
  success: '#16A34A',
  info: '#2563EB',
  warning: '#D97706',
  danger: '#DC2626',
  neutral: '#94A3B8',
  grid: '#D9E2EC',
} as const;

/**
 * Categorical series for charts with several lines or bars, in a fixed order. They are CSS variables so the light and
 * dark steps swap in index.css; pass them straight to Recharts `stroke` / `fill`. A seventh series is never generated:
 * fold the tail into "Other" or use the table view.
 */
export const CHART_SERIES = [
  'var(--chart-series-1)',
  'var(--chart-series-2)',
  'var(--chart-series-3)',
  'var(--chart-series-4)',
  'var(--chart-series-5)',
  'var(--chart-series-6)',
] as const;

export const CHART_CHROME = {
  grid: 'var(--chart-grid)',
  axis: 'var(--chart-axis)',
  surface: 'var(--chart-surface)',
} as const;
