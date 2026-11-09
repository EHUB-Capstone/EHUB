import { useEffect, useRef, type ReactNode } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, CalendarDays, ExternalLink, Eye, Lightbulb, Pencil, Sparkles, Target, UserRound, Users, type LucideIcon } from 'lucide-react';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorState from '../../components/ui/ErrorState';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import ShowcaseModerationActions from '../../components/showcase/ShowcaseModerationActions';
import ShowcaseFeaturedToggle from '../../components/showcase/ShowcaseFeaturedToggle';
import ShowcaseComments from '../../components/showcase/ShowcaseComments';
import ShowcaseStatusBadge from '../../components/showcase/ShowcaseStatusBadge';
import ShowcaseUpvoteButton from '../../components/showcase/ShowcaseUpvoteButton';
import { PROJECT_PROFILE_LABELS } from '../../constants/projectProfile';
import { showcaseApi } from '../../api/showcaseApi';
import { useAuthContext } from '../../context/AuthContext';
import type { ShowcaseDetailDto } from '../../types/showcase';
import { parseApiError } from '../../utils/apiError';
import { SHOWCASE_ERROR_CODES, stageLabel } from '../../utils/showcase';

function StoryBlock({ icon: Icon, title, children }: { icon: LucideIcon; title: string; children: ReactNode }) {
  if (typeof children === 'string' ? !children.trim() : !children) return null;
  return (
    <section className="flex gap-4">
      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary-50 text-primary-700" aria-hidden="true"><Icon className="h-5 w-5" /></span>
      <div className="min-w-0 flex-1">
        <h2 className="text-subheading text-secondary">{title}</h2>
        {typeof children === 'string'
          ? <p className="mt-1.5 whitespace-pre-line text-body-lg leading-relaxed text-slate-600">{children}</p>
          : <div className="mt-1.5">{children}</div>}
      </div>
    </section>
  );
}

function Stat({ icon: Icon, label, value }: { icon: LucideIcon; label: string; value: number }) {
  return (
    <div className="flex flex-col items-center px-2 py-3 text-caption text-slate-500">
      <span className="text-heading text-secondary">{value.toLocaleString()}</span>
      <span className="mt-0.5 inline-flex items-center gap-1"><Icon className="h-3.5 w-3.5 text-slate-400" aria-hidden="true" />{label}</span>
    </div>
  );
}

export default function ShowcaseDetail() {
  const { id = '' } = useParams();
  const location = useLocation();
  const { user } = useAuthContext();
  const viewerId = user?.id;
  const viewerRole = user?.role;
  const queryClient = useQueryClient();
  const detail = useQuery({
    queryKey: ['showcase', 'detail', id, viewerId, viewerRole],
    queryFn: ({ signal }) => showcaseApi.get(id, signal),
    staleTime: 0,
    retry: (count, error) => parseApiError(error, '').code !== SHOWCASE_ERROR_CODES.notFound && count < 1,
  });
  const trackedVisit = useRef('');
  const visitKey = `${location.key}:${id}:${viewerId}:${viewerRole}`;
  const shouldTrack = Boolean(viewerId) && detail.isSuccess &&
    detail.data.showcase.status === 'Published' && !detail.data.isOwner;

  useEffect(() => {
    if (!shouldTrack || trackedVisit.current === visitKey) return;

    const controller = new AbortController();
    // Let effect cleanup cancel the probe mount before sending a request in React Strict Mode.
    queueMicrotask(() => {
      if (controller.signal.aborted) return;
      trackedVisit.current = visitKey;
      void showcaseApi.recordView(id, controller.signal)
        .then(({ viewCount }) => {
          if (controller.signal.aborted) return;
          queryClient.setQueryData<ShowcaseDetailDto>(['showcase', 'detail', id, viewerId, viewerRole], current => {
            if (!current || current.showcase.viewCount >= viewCount) return current;
            return { ...current, showcase: { ...current.showcase, viewCount } };
          });
        })
        .catch(() => {
          // Tracking is best effort; the detail query owns the page's error state.
        });
    });

    return () => controller.abort();
  }, [id, queryClient, shouldTrack, viewerId, viewerRole, visitKey]);

  if (detail.isPending) {
    return (
      <main className="mx-auto max-w-[88rem] space-y-4 px-4 py-6 sm:px-6 sm:py-8 lg:px-8" role="status" aria-label="Loading showcase page">
        <LoadingSkeleton variant="card" />
        <LoadingSkeleton lines={6} />
      </main>
    );
  }

  if (detail.isError) {
    const notFound = parseApiError(detail.error, '').code === SHOWCASE_ERROR_CODES.notFound;
    return (
      <main className="mx-auto max-w-[88rem] px-4 py-6 sm:px-6 sm:py-8 lg:px-8">
        <ErrorState
          title={notFound ? 'This page is not available' : 'Could not load this page'}
          message={notFound
            ? 'It may not exist, may not be published yet, or you may not have access to it.'
            : parseApiError(detail.error, 'Failed to load this showcase page.').message}
          onRetry={notFound ? undefined : () => { void detail.refetch(); }}
        />
      </main>
    );
  }

  const { showcase, members, isOwner, hasUpvoted, canModerate, teamName } = detail.data;
  const { profile } = showcase;
  const privileged = isOwner || canModerate;
  const navigationState = location.state as { showcaseBackTo?: unknown; showcaseBackLabel?: unknown } | null;
  const suppliedBackTo = typeof navigationState?.showcaseBackTo === 'string' && navigationState.showcaseBackTo.startsWith('/showcase')
    ? navigationState.showcaseBackTo
    : null;
  const suppliedBackLabel = typeof navigationState?.showcaseBackLabel === 'string' ? navigationState.showcaseBackLabel : null;
  const backTo = suppliedBackTo ?? (isOwner ? '/showcase/manage' : canModerate ? '/showcase/moderation' : '/showcase');
  const backLabel = suppliedBackLabel ?? (isOwner ? 'My Showcase' : canModerate ? 'Showcase Moderation' : 'Project Showcase');
  const [cover, ...gallery] = showcase.media;
  const hasStory = showcase.links.length > 0 || [profile.problem, profile.targetUsers, profile.solution, showcase.highlights].some((value) => value?.trim());

  const applyDecision = () => {
    void queryClient.invalidateQueries({ queryKey: ['showcase', 'moderation'] });
    void detail.refetch();
  };

  return (
    <main className="mx-auto max-w-[88rem] space-y-6 px-4 py-6 sm:px-6 sm:py-8 lg:px-8">
      <Link
        to={backTo}
        className="inline-flex items-center gap-1 text-body-sm font-semibold text-primary-700 hover:underline"
      >
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        {backLabel}
      </Link>

      <header className="space-y-4">
        <div className="flex flex-col gap-5 sm:flex-row sm:items-center">
          {showcase.logoUrl ? (
            <img src={showcase.logoUrl} alt={`${profile.projectName} logo`} className="h-20 w-20 shrink-0 rounded-2xl border border-slate-200 bg-white object-contain p-1.5 shadow-soft sm:h-22 sm:w-22" />
          ) : (
            <div className="grid h-20 w-20 shrink-0 place-items-center rounded-2xl border border-secondary-100 bg-secondary-50 text-heading text-secondary shadow-soft sm:h-22 sm:w-22" aria-hidden="true">
              {profile.projectName.slice(0, 2).toUpperCase()}
            </div>
          )}
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="brand" size="md">{stageLabel(showcase.developmentStage)}</Badge>
              {privileged && <ShowcaseStatusBadge status={showcase.status} />}
              {showcase.isFeatured && <Badge variant="brand" size="md">Featured</Badge>}
            </div>
            <h1 className="mt-1.5 break-words text-display text-secondary">{profile.projectName}</h1>
            <p className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-body-sm text-slate-600">
              <span className="inline-flex items-center gap-1.5"><Users className="h-4 w-4 text-slate-400" aria-hidden="true" />{teamName}</span>
              {showcase.publishedAt && <span className="inline-flex items-center gap-1.5"><CalendarDays className="h-4 w-4 text-slate-400" aria-hidden="true" />Published {new Date(showcase.publishedAt).toLocaleDateString()}</span>}
            </p>
          </div>
          {isOwner && showcase.canEdit && (
            <Link to={`/showcase/manage/${showcase.projectLineageId}`} className="shrink-0">
              <Button variant="outline" icon={Pencil}>Edit page</Button>
            </Link>
          )}
        </div>

        {profile.description && <p className="max-w-3xl text-body-lg leading-relaxed text-slate-600">{profile.description}</p>}
      </header>

      {privileged && showcase.reviewNote && (
        <div role="note" className="rounded-xl border border-warning-light bg-warning-50 px-4 py-3 text-body text-warning-dark">
          <p className="font-semibold">Reviewer note</p>
          <p className="mt-1 whitespace-pre-line">{showcase.reviewNote}</p>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start">
        <div className="min-w-0 space-y-6">
          {cover && (
            <section aria-label="Screenshots" className="space-y-3">
              <a href={cover.url} target="_blank" rel="noopener noreferrer" className="block overflow-hidden rounded-2xl border border-slate-200 shadow-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600">
                <img src={cover.url} alt={cover.caption ?? `${profile.projectName} screenshot`} className="aspect-video w-full bg-slate-50 object-contain" />
                <span className="sr-only">(opens full size in a new tab)</span>
              </a>
              {gallery.length > 0 && (
                <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3">
                  {gallery.map((item) => (
                    <li key={item.id}>
                      <a href={item.url} target="_blank" rel="noopener noreferrer" className="block overflow-hidden rounded-xl border border-slate-200 transition-shadow hover:shadow-elevated focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600">
                        <img src={item.url} alt={item.caption ?? `${profile.projectName} screenshot`} loading="lazy" className="aspect-video w-full bg-slate-50 object-contain" />
                        <span className="sr-only">(opens full size in a new tab)</span>
                      </a>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          )}

          {hasStory && (
            <Card hover={false} className="grid gap-x-8 gap-y-7 sm:grid-cols-2">
              <StoryBlock icon={Target} title={PROJECT_PROFILE_LABELS.problem}>{profile.problem}</StoryBlock>
              <StoryBlock icon={UserRound} title={PROJECT_PROFILE_LABELS.targetUsers}>{profile.targetUsers}</StoryBlock>
              <div className="space-y-7">
                <StoryBlock icon={Lightbulb} title={PROJECT_PROFILE_LABELS.solution}>{profile.solution}</StoryBlock>
                {showcase.links.length > 0 && (
                  <StoryBlock icon={ExternalLink} title="Links">
                    <ul className="space-y-2">
                      {showcase.links.map((link) => (
                        <li key={link.id}>
                          <a
                            href={link.url}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="flex min-h-10 w-full items-center gap-2 rounded-lg border border-primary-200 bg-primary-50 px-3 py-1.5 text-body-sm font-semibold text-primary-800 transition-colors hover:border-primary-300 hover:bg-primary-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
                          >
                            <span className="min-w-0 flex-1 truncate">{link.label}</span>
                            <span className="shrink-0 border-l border-primary-200 pl-2 text-caption font-medium text-slate-600">{link.linkType}</span>
                            <ExternalLink className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                            <span className="sr-only">(opens in a new tab)</span>
                          </a>
                        </li>
                      ))}
                    </ul>
                  </StoryBlock>
                )}
              </div>
              <StoryBlock icon={Sparkles} title="What makes it different">{showcase.highlights}</StoryBlock>
            </Card>
          )}

          {showcase.status === 'Published' && <ShowcaseComments showcaseId={showcase.id} commentCount={showcase.commentCount} />}
        </div>

        <aside className="min-w-0 space-y-4 lg:sticky lg:top-6">
          <Card hover={false} padding="p-5">
            <div className="grid grid-cols-2 divide-x divide-slate-200 overflow-hidden rounded-xl border border-slate-200 text-center">
              <Stat icon={Eye} label="Views" value={showcase.viewCount} />
              <ShowcaseUpvoteButton
                key={`${showcase.id}:${user?.id}:${user?.role}`}
                showcaseId={showcase.id}
                likeCount={showcase.likeCount}
                hasUpvoted={hasUpvoted}
                isOwner={isOwner}
                isPublished={showcase.status === 'Published'}
                tile
                className="h-auto w-full flex-col gap-0 rounded-none px-2 py-3"
              />
            </div>
          </Card>

          {members.length > 0 && (
            <Card hover={false} padding="p-5">
              <Card.Header className="mb-3">
                <Card.Title icon={Users}>Team</Card.Title>
              </Card.Header>
              <ul className="space-y-3">
                {members.map((member) => (
                  <li key={`${member.fullName}-${member.role}`} className="flex items-center gap-3">
                    {member.avatarUrl ? (
                      <img src={member.avatarUrl} alt="" className="h-10 w-10 rounded-full object-cover" />
                    ) : (
                      <span className="grid h-10 w-10 place-items-center rounded-full bg-secondary-50 text-body-sm font-semibold text-secondary" aria-hidden="true">
                        {member.fullName.charAt(0).toUpperCase()}
                      </span>
                    )}
                    <div className="min-w-0">
                      <p className="truncate text-body font-medium text-secondary">{member.fullName}</p>
                      <p className="text-caption text-slate-600">{member.role}</p>
                    </div>
                  </li>
                ))}
              </ul>
            </Card>
          )}

          {canModerate && (
            <div className="rounded-xl border border-dashed border-secondary-200 bg-secondary-50 p-4">
              <p className="mb-2 text-body-sm font-semibold text-secondary">Review this page</p>
              <div className="flex flex-wrap items-center gap-2">
                <ShowcaseModerationActions
                  showcaseId={showcase.id}
                  title={profile.projectName}
                  status={showcase.status}
                  wasEverPublished={showcase.publishedAt !== null}
                  onDecided={applyDecision}
                />
                {showcase.status === 'Published' && <ShowcaseFeaturedToggle showcaseId={showcase.id} title={profile.projectName} isFeatured={showcase.isFeatured} onChanged={applyDecision} />}
              </div>
            </div>
          )}
        </aside>
      </div>
    </main>
  );
}
