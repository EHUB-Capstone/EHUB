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
import ShowcaseInsights from '../../components/showcase/ShowcaseInsights';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import { SHOWCASE_STATUS_META, stageLabel } from '../../utils/showcase';
import { useTranslation } from 'react-i18next';

export default function MyShowcase() {
  const { t } = useTranslation('showcase');
  const mine = useQuery({
    queryKey: ['showcase', 'mine'],
    queryFn: ({ signal }) => showcaseApi.getMine(signal),
    staleTime: 0,
  });

  return (
    <main className="mx-auto max-w-5xl p-4 sm:p-8">
      <PageHeader
        title={t('myShowcase.myShowcase')}
        subtitle={t('myShowcase.introduceYourProjectToTheE')}
        breadcrumb={[{ label: t('showcaseReports.showcase'), to: '/showcase' }, 'My Showcase']}
      />

      {mine.isPending && (
        <div className="space-y-4" role="status" aria-label={t('myShowcase.loadingYourShowcasePages')}>
          <LoadingSkeleton variant="card" />
          <LoadingSkeleton variant="card" />
        </div>
      )}

      {mine.isError && (
        <ErrorState
          title={t('myShowcase.couldNotLoadYourShowcasePages')}
          message={parseApiError(mine.error, t('myShowcase.failedToLoadYourShowcasePages')).message}
          onRetry={() => { void mine.refetch(); }}
        />
      )}

      {mine.isSuccess && mine.data.length === 0 && (
        <Card hover={false}>
          <EmptyState
            icon={Rocket}
            title={t('myShowcase.noProjectToShowcaseYet')}
            description={t('myShowcase.showcasePagesBelongToATeam')}
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
                        <p className="mt-1 text-body-sm text-slate-600">{t('myShowcase.noShowcasePageYetCreateOne')}</p>
                      )}
                    </div>
                    <div className="flex shrink-0 flex-wrap items-center gap-2 sm:justify-end">
                      {showcase ? (
                        <>
                          <ShowcaseStatusBadge status={showcase.status} />
                          <Badge variant="brand" size="md">{stageLabel(showcase.developmentStage)}</Badge>
                        </>
                      ) : (
                        <Badge variant="neutral" size="md">{t('myShowcase.notCreated')}</Badge>
                      )}
                    </div>
                  </div>
                  {showcase?.publishedAt && <ShowcaseInsights showcaseId={showcase.id} />}
                  <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 pt-4">
                    <p className="text-body-sm text-slate-600">
                      {showcase ? SHOWCASE_STATUS_META[showcase.status].description : t('myShowcase.startsAsAPrivateDraft')}
                    </p>
                    <span className="inline-flex items-center gap-1 text-body-sm font-semibold text-primary-700">
                      {showcase ? t('myShowcase.openEditor') : t('myShowcase.createPage')}
                      <ArrowUpRight className="h-4 w-4" aria-hidden="true" />
                    </span>
                  </div>
                  {showcase && <ShowcaseLatestFeedback history={entry.history} status={showcase.status} />}
                  <ShowcaseReviewHistory history={entry.history}>
                    {showcase?.status === 'Published' && (
                      <Link to={`/showcase/${showcase.id}`} className={SHOWCASE_ROW_ACTION.brand}>
                        <Globe className="h-4 w-4" aria-hidden="true" />
                        {t('myShowcase.viewPublicPage')}
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
