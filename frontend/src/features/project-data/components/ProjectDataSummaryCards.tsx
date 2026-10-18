import type { ComponentProps } from 'react';
import { Award, HandCoins, Landmark, Sparkles, Users, type LucideIcon } from 'lucide-react';
import StatCard from '../../../components/ui/StatCard';
import LoadingSkeleton from '../../../components/ui/LoadingSkeleton';
import type { ProjectAchievement, ProjectDataSummary } from '../../../types/projectData';
import { cn } from '../../../utils/cn';

interface ProjectDataSummaryCardsProps {
  summary: ProjectDataSummary | undefined;
  isLoading: boolean;
  isError: boolean;
  isRefreshing: boolean;
  onRetry: () => void;
  /** The achievement filter currently applied; its card shows as pressed. */
  achievement: ProjectAchievement | '';
  /** True when a search or any filter is applied; otherwise the Total Groups card shows as pressed. */
  hasFilters: boolean;
  /** Selecting the active label's card again turns the filter off. */
  onSelectAchievement: (achievement: ProjectAchievement | '') => void;
  onClearFilters: () => void;
}

const formatCount = (value: number) => value.toLocaleString();

const achievementCards = [
  { achievement: 'Potential', field: 'potentialGroups', icon: Sparkles, color: 'primary' },
  { achievement: 'Internally Funded', field: 'internallyFundedGroups', icon: Landmark, color: 'success' },
  { achievement: 'Externally Funded', field: 'externallyFundedGroups', icon: HandCoins, color: 'info' },
  { achievement: 'Awarded', field: 'awardedGroups', icon: Award, color: 'warning' },
] as const satisfies ReadonlyArray<{
  achievement: ProjectAchievement;
  field: keyof ProjectDataSummary;
  icon: LucideIcon;
  color: ComponentProps<typeof StatCard>['color'];
}>;

/**
 * Five counters (all groups, Potential, Internally Funded, Externally Funded, Awarded) for the current scope, search
 * and filters. Each is also a quick filter: a label card toggles that achievement, Total Groups clears every filter.
 */
export default function ProjectDataSummaryCards({
  summary,
  isLoading,
  isError,
  isRefreshing,
  onRetry,
  achievement,
  hasFilters,
  onSelectAchievement,
  onClearFilters,
}: ProjectDataSummaryCardsProps) {
  if (isLoading) {
    return (
      <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5" role="status" aria-label="Loading summary">
        {[0, 1, 2, 3, 4].map(index => <LoadingSkeleton key={index} variant="card" />)}
      </div>
    );
  }

  if (!summary) {
    return isError ? (
      <p role="alert" className="mb-4 flex flex-wrap items-center gap-2 text-sm text-danger">
        The summary could not be loaded.
        <button type="button" onClick={onRetry} className="font-semibold underline underline-offset-2">Retry</button>
      </p>
    ) : null;
  }

  return (
    <section
      aria-label="Project data summary"
      aria-busy={isRefreshing}
      className={cn('mb-4 grid grid-cols-2 gap-3 transition-opacity sm:grid-cols-3 lg:grid-cols-5', isRefreshing && 'opacity-60')}
    >
      <StatCard
        title="Total Groups"
        value={formatCount(summary.totalGroups)}
        icon={Users}
        color="indigo"
        selected={!hasFilters}
        onSelect={onClearFilters}
      />
      {achievementCards.map(card => (
        <StatCard
          key={card.achievement}
          title={card.achievement}
          value={formatCount(summary[card.field])}
          icon={card.icon}
          color={card.color}
          selected={achievement === card.achievement}
          onSelect={() => onSelectAchievement(achievement === card.achievement ? '' : card.achievement)}
        />
      ))}
    </section>
  );
}
