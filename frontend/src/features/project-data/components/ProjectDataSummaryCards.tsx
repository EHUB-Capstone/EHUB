import { Award, Banknote, Sparkles, Users } from 'lucide-react';
import StatCard from '../../../components/ui/StatCard';
import LoadingSkeleton from '../../../components/ui/LoadingSkeleton';
import type { ProjectDataSummary } from '../../../types/projectData';
import { cn } from '../../../utils/cn';

interface ProjectDataSummaryCardsProps {
  summary: ProjectDataSummary | undefined;
  isLoading: boolean;
  isError: boolean;
  isRefreshing: boolean;
  onRetry: () => void;
}

const formatCount = (value: number) => value.toLocaleString();

/** Four counters (all groups, Potential, Funded, Awarded) for the current scope, search and filters. */
export default function ProjectDataSummaryCards({
  summary,
  isLoading,
  isError,
  isRefreshing,
  onRetry,
}: ProjectDataSummaryCardsProps) {
  if (isLoading) {
    return (
      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4" role="status" aria-label="Loading summary">
        {[0, 1, 2, 3].map(index => <LoadingSkeleton key={index} variant="card" />)}
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
      className={cn('mb-4 grid grid-cols-2 gap-3 transition-opacity lg:grid-cols-4', isRefreshing && 'opacity-60')}
    >
      <StatCard title="Total Groups" value={formatCount(summary.totalGroups)} icon={Users} color="indigo" />
      <StatCard title="Potential" value={formatCount(summary.potentialGroups)} icon={Sparkles} color="primary" />
      <StatCard title="Funded" value={formatCount(summary.fundedGroups)} icon={Banknote} color="success" />
      <StatCard title="Awarded" value={formatCount(summary.awardedGroups)} icon={Award} color="warning" />
    </section>
  );
}
