import type { ReactNode } from 'react';
import Button from '../../../components/ui/Button';
import Card from '../../../components/ui/Card';
import LoadingSkeleton from '../../../components/ui/LoadingSkeleton';
import type {
  ProjectAchievement,
  ProjectDataDistribution as ProjectDataDistributionData,
  ProjectDataDistributionBucket,
  ProjectDataQuery,
} from '../../../types/projectData';
import { cn } from '../../../utils/cn';
import { hasFilterValue, toggleAchievement, toggleFilterValue } from '../../../utils/projectData';
import {
  distributionBarWidth,
  distributionPercent,
  limitDistributionBuckets,
  majorLabel,
} from '../../../utils/projectDistribution';
import { useTranslation } from 'react-i18next';
import { formatNumber } from '../../../i18n/format';

interface ProjectDataDistributionProps {
  distribution: ProjectDataDistributionData | undefined;
  query: ProjectDataQuery;
  isLoading: boolean;
  isError: boolean;
  isRefreshing: boolean;
  onRetry: () => void;
  onFilter: (patch: Partial<ProjectDataQuery>) => void;
  /** Opens the Overview tab, where the table shows the groups behind these numbers. */
  onViewTable: () => void;
}

const MAX_INDUSTRY_ROWS = 8;

interface Row {
  key: string;
  label: string;
  count: number;
  /** Null when the row cannot filter the table (no matching filter, or a catch-all bucket). */
  onSelect: (() => void) | null;
  selected?: boolean;
}

function BarList({ rows, total, label }: { rows: Row[]; total: number; label: string }) {
  const { t } = useTranslation('projectData');
  const max = Math.max(0, ...rows.map(row => row.count));
  if (rows.length === 0) return <p className="text-body-sm text-slate-500">{t('projectDataDistribution.nothingToShow')}</p>;

  return (
    <ul aria-label={label} className="space-y-1">
      {rows.map(row => {
        const percent = distributionPercent(row.count, total);
        const content: ReactNode = (
          <>
            <span className="flex items-baseline justify-between gap-3 text-body-sm">
              <span className="min-w-0 truncate text-slate-700">{row.label}</span>
              <span className="shrink-0 font-semibold text-secondary">
                {row.count.toLocaleString()} <span className="font-normal text-slate-500">({percent}%)</span>
              </span>
            </span>
            <span className="mt-1 block h-2 w-full rounded-full bg-slate-100" aria-hidden="true">
              <span
                className={cn('block h-2 rounded-full', row.selected ? 'bg-primary-700' : 'bg-primary')}
                style={{ width: `${distributionBarWidth(row.count, max)}%` }}
              />
            </span>
          </>
        );

        return (
          <li key={row.key}>
            {row.onSelect ? (
              <button
                type="button"
                onClick={row.onSelect}
                aria-pressed={row.selected ?? false}
                aria-label={t('projectDataDistribution.groupsFilterTheTableByThis', { label: row.label, countValue: row.count })}
                className="block w-full rounded-lg px-2 py-1.5 text-left hover:bg-slate-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
              >
                {content}
              </button>
            ) : (
              <div className="px-2 py-1.5">{content}</div>
            )}
          </li>
        );
      })}
    </ul>
  );
}

function Panel({ title, note, children }: { title: string; note?: string; children: ReactNode }) {
  return (
    <Card hover={false} padding="p-4" className="min-w-0">
      <h3 className="text-subheading text-secondary">{title}</h3>
      {note && <p className="mb-2 text-caption text-slate-500">{note}</p>}
      <div className={note ? undefined : 'mt-2'}>{children}</div>
    </Card>
  );
}

const toRows = (
  buckets: readonly ProjectDataDistributionBucket[],
  select: ((bucket: ProjectDataDistributionBucket) => (() => void) | null) | null,
  isSelected?: (bucket: ProjectDataDistributionBucket) => boolean,
  label: (name: string) => string = name => name,
): Row[] => buckets.map(bucket => ({
  key: bucket.name,
  label: label(bucket.name),
  count: bucket.count,
  onSelect: select ? select(bucket) : null,
  selected: isSelected?.(bucket),
}));

/**
 * Projects grouped by semester, major, startup industry (also the project topic) and outcome, for the same scope,
 * search and filters as the table. Semester, major, industry and outcome rows filter the table when clicked; major,
 * industry and outcome rows add to (or take out of) the filter, so several can be on at once.
 */
export default function ProjectDataDistribution({
  distribution,
  query,
  isLoading,
  isError,
  isRefreshing,
  onRetry,
  onFilter,
  onViewTable,
}: ProjectDataDistributionProps) {
  const { t } = useTranslation('projectData');
  if (isLoading) {
    return (
      <div className="mb-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4" role="status" aria-label={t('projectDataDistribution.loadingDistribution')}>
        {[0, 1, 2, 3].map(index => <LoadingSkeleton key={index} variant="card" />)}
      </div>
    );
  }

  if (!distribution) {
    return isError ? (
      <p role="alert" className="mb-4 flex flex-wrap items-center gap-2 text-sm text-danger">
        {t('projectDataDistribution.theProjectDistributionCouldNotBe')}
        <button type="button" onClick={onRetry} className="font-semibold underline underline-offset-2">{t('projectAchievementHistory.retry')}</button>
      </p>
    ) : null;
  }

  const total = distribution.totalGroups;
  if (total === 0) {
    return (
      <p className="mb-4 text-body-sm text-slate-500" role="status">
        {t('projectDataDistribution.noProjectsMatchTheCurrentFilters')}
      </p>
    );
  }

  const semesterRows: Row[] = distribution.semesters.map(semester => {
    const selected = query.semester === semester.term && query.year === String(semester.year);
    return {
      key: `${semester.code}`,
      label: semester.code,
      count: semester.count,
      selected,
      onSelect: () => onFilter(selected
        ? { semester: '', year: '' }
        : { semester: semester.term, year: String(semester.year) }),
    };
  });

  const majorRows = toRows(
    distribution.majors,
    bucket => () => onFilter({ major: toggleFilterValue(query.major, bucket.name) }),
    bucket => hasFilterValue(query.major, bucket.name),
    majorLabel,
  );

  const industries = limitDistributionBuckets(distribution.startupIndustries, MAX_INDUSTRY_ROWS);
  const industryRows: Row[] = toRows(
    industries.items,
    bucket => () => onFilter({ startupIndustry: toggleFilterValue(query.startupIndustry, bucket.name) }),
    bucket => hasFilterValue(query.startupIndustry, bucket.name),
  );
  if (industries.otherCount > 0) {
    industryRows.push({ key: '__other', label: t('projectDataDistribution.moreIndustries', { otherBuckets: industries.otherBuckets }), count: industries.otherCount, onSelect: null });
  }
  if (distribution.unspecifiedStartupIndustryCount > 0) {
    industryRows.push({ key: '__none', label: t('projectDataDistribution.noIndustrySet'), count: distribution.unspecifiedStartupIndustryCount, onSelect: null });
  }

  const outcomeRows: Row[] = toRows(
    distribution.outcomes,
    bucket => () => onFilter({ achievement: toggleAchievement(query.achievement, bucket.name as ProjectAchievement) }),
    bucket => query.achievement.includes(bucket.name as ProjectAchievement),
  );
  if (distribution.noOutcomeCount > 0) {
    outcomeRows.push({ key: '__none', label: t('projectDataDistribution.noLabel'), count: distribution.noOutcomeCount, onSelect: null });
  }

  return (
    <section
      aria-label={t('projectDataDistribution.projectDistribution')}
      aria-busy={isRefreshing}
      className={cn('mb-4 transition-opacity', isRefreshing && 'opacity-60')}
    >
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <Panel title={t('projectDataDistribution.bySemester')}>
          <BarList rows={semesterRows} total={total} label={t('projectDataDistribution.groupsBySemester')} />
        </Panel>
        <Panel title={t('projectDataDistribution.byMajor')} note="A group counts once for each major among its members.">
          <BarList rows={majorRows} total={total} label={t('projectDataDistribution.groupsByMajor')} />
        </Panel>
        <Panel title={t('projectDataDistribution.byStartupIndustryTopic')} note="A group can have up to three industries.">
          <BarList rows={industryRows} total={total} label={t('projectDataDistribution.groupsByStartupIndustry')} />
        </Panel>
        <Panel title={t('projectDataDistribution.byOutcome')} note="Labels are independent, so a group can have several.">
          <BarList rows={outcomeRows} total={total} label={t('projectDataDistribution.groupsByOutcome')} />
        </Panel>
      </div>
      <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
        <p className="text-caption text-slate-500">
          {t('projectDataDistribution.percentagesAreSharesOfGroupsSo', { v1: total.toLocaleString() })}
        </p>
        <Button variant="outline" size="sm" onClick={onViewTable}>
          {t('projectDataDistribution.viewGroupsInTable', { count: total, formatted: formatNumber(total) })}
        </Button>
      </div>
    </section>
  );
}
