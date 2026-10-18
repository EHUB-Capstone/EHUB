import { useMemo, useState } from 'react';
import Card from '../../../components/ui/Card';
import LoadingSkeleton from '../../../components/ui/LoadingSkeleton';
import { CHART_COLORS, CHART_SERIES } from '../../../constants/chartColors';
import type { ProjectDataTrends as ProjectDataTrendsData } from '../../../types/projectData';
import { cn } from '../../../utils/cn';
import {
  TREND_LEVELS,
  TREND_LEVEL_LABELS,
  latestSemesterSummary,
  toOutcomeChartData,
  toScoreChartData,
  trendRows,
  type TrendLevel,
} from '../../../utils/projectTrends';
import ProjectDataTrendsTable from './trends/ProjectDataTrendsTable';
import TrendKpiTiles from './trends/TrendKpiTiles';
import TrendOutcomeChart, { OUTCOME_SERIES } from './trends/TrendOutcomeChart';
import TrendScoreChart from './trends/TrendScoreChart';

interface ProjectDataTrendsProps {
  trends: ProjectDataTrendsData | undefined;
  isLoading: boolean;
  isError: boolean;
  isRefreshing: boolean;
  onRetry: () => void;
  /** Subject codes of the user's whole scope, so a subject keeps its color when a filter removes another. */
  subjectOrder?: readonly string[];
}

type TrendView = 'chart' | 'table';

function Legend({ items }: { items: { label: string; color: string }[] }) {
  return (
    <ul className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-caption text-slate-500" aria-label="Legend">
      {items.map(item => (
        <li key={item.label} className="flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-sm" style={{ backgroundColor: item.color }} aria-hidden="true" />
          {item.label}
        </li>
      ))}
    </ul>
  );
}

function ToggleGroup<T extends string>({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value: T;
  options: { value: T; label: string }[];
  onChange: (value: T) => void;
}) {
  return (
    <div role="group" aria-label={label} className="flex gap-1">
      {options.map(option => (
        <button
          key={option.value}
          type="button"
          aria-pressed={option.value === value}
          onClick={() => onChange(option.value)}
          className={cn(
            'rounded-lg border px-3 py-1 text-body-sm font-semibold',
            option.value === value
              ? 'border-primary bg-primary-50 text-primary-700'
              : 'border-slate-200 text-slate-500 hover:bg-slate-50',
          )}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}

const NO_SUBJECT_ORDER: readonly string[] = [];

/**
 * Official course score and project outcomes per semester, so quality can be compared over time. The score counts only
 * groups whose every weighted component is published; draft and unpublished evaluations never reach it.
 */
export default function ProjectDataTrends({
  trends,
  isLoading,
  isError,
  isRefreshing,
  onRetry,
  subjectOrder = NO_SUBJECT_ORDER,
}: ProjectDataTrendsProps) {
  const [level, setLevel] = useState<TrendLevel>('semester');
  const [view, setView] = useState<TrendView>('chart');

  const scoreModel = useMemo(
    () => (trends ? toScoreChartData(trends, level, subjectOrder) : null),
    [trends, level, subjectOrder],
  );
  const outcomeData = useMemo(() => (trends ? toOutcomeChartData(trends) : []), [trends]);
  const summary = useMemo(() => (trends ? latestSemesterSummary(trends) : null), [trends]);

  if (isLoading) {
    return (
      <div className="mb-4" role="status" aria-label="Loading semester trends">
        <LoadingSkeleton variant="table" lines={5} />
      </div>
    );
  }

  if (!trends || !scoreModel) {
    return isError ? (
      <p role="alert" className="mb-4 flex flex-wrap items-center gap-2 text-sm text-danger">
        The semester trends could not be loaded.
        <button type="button" onClick={onRetry} className="font-semibold underline underline-offset-2">Retry</button>
      </p>
    ) : null;
  }

  const rows = trendRows(trends, level);
  // A chart per class would be unreadable, so that level is always the table.
  const showCharts = view === 'chart' && level !== 'class';
  const scoreLegend = scoreModel.series.length > 1
    ? scoreModel.series.map(item => ({ label: item.label, color: item.slot === null ? CHART_COLORS.brand : CHART_SERIES[item.slot] }))
    : [];

  return (
    <section aria-label="Semester trends" aria-busy={isRefreshing} className={cn('mb-4 transition-opacity', isRefreshing && 'opacity-60')}>
      <Card hover={false} padding="p-4" className="min-w-0">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0">
            <h3 className="text-subheading text-secondary">Semester trends</h3>
            <p className="text-caption text-slate-500">
              Average course score (0-10) of groups whose every component is published, with the change since the previous
              semester of the same {level === 'class' ? 'class code' : 'subject'}. Semester and Year filters do not apply here.
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <ToggleGroup<TrendLevel>
              label="Trend level"
              value={level}
              options={TREND_LEVELS.map(item => ({ value: item, label: TREND_LEVEL_LABELS[item] }))}
              onChange={setLevel}
            />
            <ToggleGroup<TrendView>
              label="Trend view"
              value={view}
              options={[{ value: 'chart', label: 'Chart' }, { value: 'table', label: 'Table' }]}
              onChange={setView}
            />
          </div>
        </div>

        {rows.length === 0 ? (
          <p className="mt-3 text-body-sm text-slate-500" role="status">No projects match the current filters.</p>
        ) : (
          <>
            {summary && <TrendKpiTiles summary={summary} />}

            {showCharts ? (
              <div className="mt-4 grid gap-4 lg:grid-cols-2">
                <figure className="min-w-0">
                  <figcaption className="text-body font-semibold text-secondary">Average course score (0-10)</figcaption>
                  <TrendScoreChart model={scoreModel} />
                  {scoreLegend.length > 0 && <Legend items={scoreLegend} />}
                  {scoreModel.hiddenSubjects > 0 && (
                    <p className="mt-1 text-caption text-slate-500">
                      {scoreModel.hiddenSubjects} more {scoreModel.hiddenSubjects === 1 ? 'subject is' : 'subjects are'} in the table view.
                    </p>
                  )}
                </figure>
                <figure className="min-w-0">
                  <figcaption className="text-body font-semibold text-secondary">Project outcomes</figcaption>
                  <TrendOutcomeChart data={outcomeData} />
                  <Legend items={OUTCOME_SERIES.map(item => ({ label: item.key, color: item.color }))} />
                </figure>
              </div>
            ) : (
              <>
                {level === 'class' && view === 'chart' && (
                  <p className="mt-3 text-caption text-slate-500">Classes are shown as a table because there are too many for a chart.</p>
                )}
                <ProjectDataTrendsTable rows={rows} level={level} />
              </>
            )}
          </>
        )}
      </Card>
    </section>
  );
}
