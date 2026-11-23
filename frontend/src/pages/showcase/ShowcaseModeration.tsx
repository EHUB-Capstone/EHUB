import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
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
import ShowcaseReports from '../../components/showcase/ShowcaseReports';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import { SHOWCASE_STATUS_META, stageLabel } from '../../utils/showcase';
import { cn } from '../../utils/cn';
import type { ShowcaseStatus } from '../../types/showcase';
import { useTranslation } from 'react-i18next';
import i18n from '../../i18n';

const PAGE_SIZE = 20;
type ModerationTab = ShowcaseStatus | 'Reports';
const TABS: ModerationTab[] = ['PendingReview', 'Published', 'Hidden', 'Draft', 'Reports'];

const EMPTY_COPY: Record<ShowcaseStatus, string> = {
  get PendingReview() { return i18n.t('showcase:showcaseModeration.empty.PendingReview'); },
  get Published() { return i18n.t('showcase:showcaseModeration.empty.Published'); },
  get Hidden() { return i18n.t('showcase:showcaseModeration.empty.Hidden'); },
  get Draft() { return i18n.t('showcase:showcaseModeration.empty.Draft'); },
};

export default function ShowcaseModeration() {
  const { t } = useTranslation('showcase');
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const [status, setStatus] = useState<ModerationTab>(searchParams.get('tab') === 'reports' ? 'Reports' : 'PendingReview');
  const [page, setPage] = useState(1);

  const queue = useQuery({
    queryKey: ['showcase', 'moderation', status, page],
    queryFn: ({ signal }) => showcaseApi.getModerationQueue({ status: status === 'Reports' ? undefined : status, page, pageSize: PAGE_SIZE }, signal),
    enabled: status !== 'Reports',
    placeholderData: keepPreviousData,
    staleTime: 0,
  });

  const changeStatus = (next: ModerationTab) => {
    setStatus(next);
    setPage(1);
    if (next === 'Reports') setSearchParams({ tab: 'reports' });
    else setSearchParams({});
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
        title={t('showcaseModeration.showcaseModeration')}
        subtitle={t('showcaseModeration.reviewTheProjectPagesTeamsWant')}
        breadcrumb={[{ label: t('showcaseReports.showcase'), to: '/showcase' }, 'Moderation']}
      />

      <div role="tablist" aria-label={t('showcaseModeration.pageStatus')} className="mb-5 flex flex-wrap gap-2">
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
            {tab === 'Reports' ? t('showcaseModeration.reports') : SHOWCASE_STATUS_META[tab].label}
          </button>
        ))}
      </div>

      {status === 'Reports' && <ShowcaseReports />}

      {status !== 'Reports' && queue.isPending && (
        <div className="space-y-4" role="status" aria-label={t('showcaseModeration.loadingShowcasePages')}>
          <LoadingSkeleton variant="card" />
          <LoadingSkeleton variant="card" />
        </div>
      )}

      {status !== 'Reports' && queue.isError && (
        <ErrorState
          title={t('showcaseModeration.couldNotLoadTheReviewQueue')}
          message={parseApiError(queue.error, t('showcaseModeration.failedToLoadShowcasePages')).message}
          onRetry={() => { void queue.refetch(); }}
        />
      )}

      {status !== 'Reports' && queue.isSuccess && queue.data.items.length === 0 && (
        <Card hover={false}>
          <EmptyState icon={ClipboardCheck} title={t('showcaseModeration.nothingHere')} description={EMPTY_COPY[status]} />
        </Card>
      )}

      {status !== 'Reports' && queue.isSuccess && queue.data.items.length > 0 && (
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
                        {t('showcaseModeration.updated', { v1: new Date(item.submittedOrUpdatedAt).toLocaleString() })}
                      </p>
                      {item.reviewNote && (
                        <p className="mt-2 text-body-sm text-slate-600">{t('showcaseModeration.lastNote', { reviewNote: item.reviewNote })}</p>
                      )}
                    </div>
                    <div className="flex shrink-0 flex-wrap items-center gap-2">
                      <ShowcaseStatusBadge status={item.status} />
                      <Badge variant="brand" size="md">{stageLabel(item.developmentStage)}</Badge>
                      {item.isFeatured && <Badge variant="brand" size="sm">{t('featureCurator.featured')}</Badge>}
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
                          ? t('showcaseModeration.theTeamIsStillEditingThis')
                          : t('showcaseModeration.neverPublishedTheTeamMustSubmit')}
                      </p>
                    ) : null}
                  </div>
                </Card>
              </li>
            ))}
          </ul>

          <nav className="mt-6 flex items-center justify-between gap-3" aria-label={t('showcaseModeration.pagination')}>
            <p className="text-body-sm text-slate-600">
              {t('showcaseModeration.queueSummary', { count: queue.data.total, page: queue.data.page, totalPages })}
            </p>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" icon={ChevronLeft} disabled={page <= 1 || queue.isPlaceholderData} onClick={() => setPage((current) => Math.max(1, current - 1))}>
                {t('common:actions.previous')}
              </Button>
              <Button variant="outline" size="sm" iconRight={ChevronRight} disabled={page >= totalPages || queue.isPlaceholderData} onClick={() => setPage((current) => current + 1)}>
                {t('common:actions.next')}
              </Button>
            </div>
          </nav>
        </>
      )}
    </main>
  );
}
