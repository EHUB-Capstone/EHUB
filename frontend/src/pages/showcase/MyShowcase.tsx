import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowUpRight, Globe, Rocket } from 'lucide-react';
import Card from '../../components/ui/Card';
import PageHeader from '../../components/ui/PageHeader';
import EmptyState from '../../components/ui/EmptyState';
import ErrorState from '../../components/ui/ErrorState';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import Badge from '../../components/ui/Badge';
import ShowcaseReviewHistory, { ShowcaseLatestFeedback } from '../../components/showcase/ShowcaseReviewHistory';
import { SHOWCASE_ROW_ACTION } from '../../components/showcase/rowActionStyles';
import ShowcaseStatusBadge from '../../components/showcase/ShowcaseStatusBadge';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import { SHOWCASE_STATUS_META, stageLabel } from '../../utils/showcase';

export default function MyShowcase() {
  const mine = useQuery({
    queryKey: ['showcase', 'mine'],
    queryFn: ({ signal }) => showcaseApi.getMine(signal),
    staleTime: 0,
  });

  return (
    <main className="mx-auto max-w-5xl p-4 sm:p-8">
      <PageHeader
        title="My Showcase"
        subtitle="Introduce your project to the E-HUB community. A lecturer or administrator reviews every page before it goes public."
        breadcrumb={[{ label: 'Showcase', to: '/showcase' }, 'My Showcase']}
      />

      {mine.isPending && (
        <div className="space-y-4" role="status" aria-label="Loading your showcase pages">
          <LoadingSkeleton variant="card" />
          <LoadingSkeleton variant="card" />
        </div>
      )}

      {mine.isError && (
        <ErrorState
          title="Could not load your showcase pages"
          message={parseApiError(mine.error, 'Failed to load your showcase pages.').message}
          onRetry={() => { void mine.refetch(); }}
        />
      )}

      {mine.isSuccess && mine.data.length === 0 && (
        <Card hover={false}>
          <EmptyState
            icon={Rocket}
            title="No project to showcase yet"
            description="Showcase pages belong to a team's project. Join an active team with a project to create one."
          />
        </Card>
      )}

      {mine.isSuccess && mine.data.length > 0 && (
        <ul className="space-y-4">
          {mine.data.map((entry) => {
            const showcase = entry.showcase;
            return (
              <li key={entry.projectLineageId}>
                <Card hover={false} className="relative">
                  <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0">
                      <p className="text-overline uppercase text-slate-600">{entry.teamName}</p>
                      <h2 className="mt-1 text-subheading text-secondary">
                        <Link
                          to={`/showcase/manage/${entry.projectLineageId}`}
                          className="after:absolute after:inset-0 after:content-[''] hover:text-primary-700 focus-visible:outline-none focus-visible:underline"
                        >
                          {entry.profile.projectName}
                        </Link>
                      </h2>
                      <p className="mt-1 text-body text-slate-600">{entry.profile.description}</p>
                      {!showcase && (
                        <p className="mt-1 text-body-sm text-slate-600">No showcase page yet. Create one to introduce this project.</p>
                      )}
                    </div>
                    <div className="flex shrink-0 flex-wrap items-center gap-2 sm:justify-end">
                      {showcase ? (
                        <>
                          <ShowcaseStatusBadge status={showcase.status} />
                          <Badge variant="brand" size="md">{stageLabel(showcase.developmentStage)}</Badge>
                        </>
                      ) : (
                        <Badge variant="neutral" size="md">Not created</Badge>
                      )}
                    </div>
                  </div>
                  <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 pt-4">
                    <p className="text-body-sm text-slate-600">
                      {showcase ? SHOWCASE_STATUS_META[showcase.status].description : 'Starts as a private draft.'}
                    </p>
                    <span className="inline-flex items-center gap-1 text-body-sm font-semibold text-primary-700">
                      {showcase ? 'Open editor' : 'Create page'}
                      <ArrowUpRight className="h-4 w-4" aria-hidden="true" />
                    </span>
                  </div>
                  {showcase && <ShowcaseLatestFeedback history={entry.history} status={showcase.status} />}
                  <ShowcaseReviewHistory history={entry.history}>
                    {showcase?.status === 'Published' && (
                      <Link to={`/showcase/${showcase.id}`} className={SHOWCASE_ROW_ACTION.brand}>
                        <Globe className="h-4 w-4" aria-hidden="true" />
                        View public page
                      </Link>
                    )}
                  </ShowcaseReviewHistory>
                </Card>
              </li>
            );
          })}
        </ul>
      )}
    </main>
  );
}
