import { useState } from 'react';
import { Link } from 'react-router-dom';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, ClipboardCheck } from 'lucide-react';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import EmptyState from '../../components/ui/EmptyState';
import ErrorState from '../../components/ui/ErrorState';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import PageHeader from '../../components/ui/PageHeader';
import ShowcaseModerationActions from '../../components/showcase/ShowcaseModerationActions';
import ShowcaseFeaturedToggle from '../../components/showcase/ShowcaseFeaturedToggle';
import ShowcaseStatusBadge from '../../components/showcase/ShowcaseStatusBadge';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import { SHOWCASE_STATUS_META, stageLabel } from '../../utils/showcase';
import { cn } from '../../utils/cn';
import type { ShowcaseStatus } from '../../types/showcase';

const PAGE_SIZE = 20;
const TABS: ShowcaseStatus[] = ['PendingReview', 'Published', 'Hidden', 'Draft'];

const EMPTY_COPY: Record<ShowcaseStatus, string> = {
  PendingReview: 'No pages are waiting for review.',
  Published: 'No pages are published yet.',
  Hidden: 'No pages are hidden.',
  Draft: 'No pages are in draft.',
};

export default function ShowcaseModeration() {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<ShowcaseStatus>('PendingReview');
  const [page, setPage] = useState(1);

  const queue = useQuery({
    queryKey: ['showcase', 'moderation', status, page],
    queryFn: ({ signal }) => showcaseApi.getModerationQueue({ status, page, pageSize: PAGE_SIZE }, signal),
    placeholderData: keepPreviousData,
    staleTime: 0,
  });

  const changeStatus = (next: ShowcaseStatus) => {
    setStatus(next);
    setPage(1);
  };

  const afterDecision = () => {
    // Deciding the only item of a later page would leave it empty; step back instead of showing a false "nothing here".
    if (page > 1 && queue.data?.items.length === 1) setPage(page - 1);
    void queryClient.invalidateQueries({ queryKey: ['showcase', 'moderation'] });
  };

  const totalPages = queue.data ? Math.max(1, Math.ceil(queue.data.total / queue.data.pageSize)) : 1;

  return (
    <main className="mx-auto max-w-5xl p-4 sm:p-8">
      <PageHeader
        title="Showcase Moderation"
        subtitle="Review the project pages teams want to publish. Lecturers see their own classes; administrators see every page."
        breadcrumb={[{ label: 'Showcase', to: '/showcase' }, 'Moderation']}
      />

      <div role="tablist" aria-label="Page status" className="mb-5 flex flex-wrap gap-2">
        {TABS.map((tab) => (
          <button
            key={tab}
            type="button"
            role="tab"
            aria-selected={status === tab}
            onClick={() => changeStatus(tab)}
            className={cn(
              'h-9 rounded-lg border px-3 text-body-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600',
              status === tab
                ? 'border-primary-600 bg-primary-50 text-primary-700'
                : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-50',
            )}
          >
            {SHOWCASE_STATUS_META[tab].label}
          </button>
        ))}
      </div>

      {queue.isPending && (
        <div className="space-y-4" role="status" aria-label="Loading showcase pages">
          <LoadingSkeleton variant="card" />
          <LoadingSkeleton variant="card" />
        </div>
      )}

      {queue.isError && (
        <ErrorState
          title="Could not load the review queue"
          message={parseApiError(queue.error, 'Failed to load showcase pages.').message}
          onRetry={() => { void queue.refetch(); }}
        />
      )}

      {queue.isSuccess && queue.data.items.length === 0 && (
        <Card hover={false}>
          <EmptyState icon={ClipboardCheck} title="Nothing here" description={EMPTY_COPY[status]} />
        </Card>
      )}

      {queue.isSuccess && queue.data.items.length > 0 && (
        <>
          <ul className={cn('space-y-4', queue.isPlaceholderData && 'opacity-60')} aria-busy={queue.isPlaceholderData}>
            {queue.data.items.map((item) => (
              <li key={item.id}>
                <Card hover={false} className="relative">
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0">
                      <p className="text-overline uppercase text-slate-600">{item.classCode} · {item.teamName}</p>
                      <h2 className="mt-1 break-words text-subheading text-secondary">
                        <Link
                          to={`/showcase/${item.id}`}
                          state={{ showcaseBackTo: '/showcase/moderation', showcaseBackLabel: 'Showcase Moderation' }}
                          className="after:absolute after:inset-0 after:content-[''] hover:text-primary-700 focus-visible:outline-none focus-visible:underline"
                        >
                          {item.projectName}
                        </Link>
                      </h2>
                      {item.description && <p className="mt-1 text-body text-slate-600">{item.description}</p>}
                      <p className="mt-2 text-caption text-slate-600">
                        Updated {new Date(item.submittedOrUpdatedAt).toLocaleString()}
                      </p>
                      {item.reviewNote && (
                        <p className="mt-2 text-body-sm text-slate-600">Last note: {item.reviewNote}</p>
                      )}
                    </div>
                    <div className="flex shrink-0 flex-wrap items-center gap-2">
                      <ShowcaseStatusBadge status={item.status} />
                      <Badge variant="brand" size="md">{stageLabel(item.developmentStage)}</Badge>
                      {item.isFeatured && <Badge variant="brand" size="sm">Featured</Badge>}
                      {item.status === 'Published' && <ShowcaseFeaturedToggle compact showcaseId={item.id} title={item.projectName} isFeatured={item.isFeatured} onChanged={afterDecision} />}
                    </div>
                  </div>
                  <div className="relative z-10 mt-4 border-t border-slate-200 pt-4">
                    <ShowcaseModerationActions
                      showcaseId={item.id}
                      title={item.projectName}
                      status={item.status}
                      wasEverPublished={item.publishedAt !== null}
                      onDecided={afterDecision}
                    />
                    {item.status === 'Draft' || (item.status === 'Hidden' && item.publishedAt === null) ? (
                      <p className="text-body-sm text-slate-600">
                        {item.status === 'Draft'
                          ? 'The team is still editing this page.'
                          : 'Never published. The team must submit it for review again.'}
                      </p>
                    ) : null}
                  </div>
                </Card>
              </li>
            ))}
          </ul>

          <nav className="mt-6 flex items-center justify-between gap-3" aria-label="Pagination">
            <p className="text-body-sm text-slate-600">
              {queue.data.total} page{queue.data.total === 1 ? '' : 's'} · Page {queue.data.page} of {totalPages}
            </p>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" icon={ChevronLeft} disabled={page <= 1 || queue.isPlaceholderData} onClick={() => setPage((current) => Math.max(1, current - 1))}>
                Previous
              </Button>
              <Button variant="outline" size="sm" iconRight={ChevronRight} disabled={page >= totalPages || queue.isPlaceholderData} onClick={() => setPage((current) => current + 1)}>
                Next
              </Button>
            </div>
          </nav>
        </>
      )}
    </main>
  );
}
