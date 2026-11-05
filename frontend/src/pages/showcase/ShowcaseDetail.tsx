import { Link, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, ExternalLink, Pencil, Users } from 'lucide-react';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ErrorState from '../../components/ui/ErrorState';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import ShowcaseModerationActions from '../../components/showcase/ShowcaseModerationActions';
import ShowcaseStatusBadge from '../../components/showcase/ShowcaseStatusBadge';
import { PROJECT_PROFILE_LABELS } from '../../constants/projectProfile';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import { SHOWCASE_ERROR_CODES, stageLabel } from '../../utils/showcase';

const formatDateTime = (value: string): string => new Date(value).toLocaleString();

function Section({ title, children }: { title: string; children: string | null }) {
  if (!children?.trim()) return null;
  return (
    <section>
      <h2 className="text-subheading text-secondary">{title}</h2>
      <p className="mt-2 whitespace-pre-line text-body-lg text-slate-600">{children}</p>
    </section>
  );
}

export default function ShowcaseDetail() {
  const { id = '' } = useParams();
  const queryClient = useQueryClient();
  const detail = useQuery({
    queryKey: ['showcase', 'detail', id],
    queryFn: ({ signal }) => showcaseApi.get(id, signal),
    staleTime: 0,
    retry: (count, error) => parseApiError(error, '').code !== SHOWCASE_ERROR_CODES.notFound && count < 1,
  });

  if (detail.isPending) {
    return (
      <main className="mx-auto max-w-4xl space-y-4 p-4 sm:p-8" role="status" aria-label="Loading showcase page">
        <LoadingSkeleton variant="card" />
        <LoadingSkeleton lines={6} />
      </main>
    );
  }

  if (detail.isError) {
    const notFound = parseApiError(detail.error, '').code === SHOWCASE_ERROR_CODES.notFound;
    return (
      <main className="mx-auto max-w-4xl p-4 sm:p-8">
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

  const { showcase, members, history, isOwner, canModerate, teamName } = detail.data;
  const { profile } = showcase;
  const privileged = isOwner || canModerate;

  const applyDecision = () => {
    void queryClient.invalidateQueries({ queryKey: ['showcase', 'moderation'] });
    void detail.refetch();
  };

  return (
    <main className="mx-auto max-w-4xl space-y-6 p-4 sm:p-8">
      <Link
        to={isOwner ? '/showcase/manage' : canModerate ? '/showcase/moderation' : '/'}
        className="inline-flex items-center gap-1 text-body-sm font-semibold text-primary-700 hover:underline"
      >
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        {isOwner ? 'My Showcase' : canModerate ? 'Showcase Moderation' : 'Home'}
      </Link>

      <Card hover={false}>
        <div className="flex flex-col gap-5 sm:flex-row sm:items-start">
          {showcase.logoUrl ? (
            <img src={showcase.logoUrl} alt={`${profile.projectName} logo`} className="h-20 w-20 shrink-0 rounded-2xl border border-slate-200 object-cover" />
          ) : (
            <div className="grid h-20 w-20 shrink-0 place-items-center rounded-2xl bg-primary-50 text-heading text-primary-700" aria-hidden="true">
              {profile.projectName.charAt(0).toUpperCase()}
            </div>
          )}
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="brand" size="md">{stageLabel(showcase.developmentStage)}</Badge>
              {privileged && <ShowcaseStatusBadge status={showcase.status} />}
              {showcase.isFeatured && <Badge variant="brand" size="md">Faculty selected</Badge>}
            </div>
            <h1 className="mt-2 break-words text-display text-secondary">{profile.projectName}</h1>
            {profile.description && <p className="mt-1 text-body-lg text-slate-600">{profile.description}</p>}
            <p className="mt-2 text-body-sm text-slate-600">
              {teamName}
              {showcase.publishedAt ? ` · Published ${new Date(showcase.publishedAt).toLocaleDateString()}` : ''}
            </p>
            {showcase.links.length > 0 && (
              <ul className="mt-4 flex flex-wrap gap-2">
                {showcase.links.map((link) => (
                  <li key={link.id}>
                    <a
                      href={link.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 text-body-sm font-medium text-slate-700 hover:border-slate-400 hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
                    >
                      {link.label}
                      <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
                      <span className="sr-only">(opens in a new tab)</span>
                    </a>
                  </li>
                ))}
              </ul>
            )}
          </div>
          {isOwner && showcase.canEdit && (
            <Link to={`/showcase/manage/${showcase.projectLineageId}`} className="shrink-0">
              <Button variant="outline" icon={Pencil}>Edit page</Button>
            </Link>
          )}
        </div>

        {canModerate && (
          <div className="mt-5 border-t border-slate-200 pt-4">
            <p className="mb-2 text-body-sm font-semibold text-secondary">Review this page</p>
            <ShowcaseModerationActions
              showcaseId={showcase.id}
              title={profile.projectName}
              status={showcase.status}
              wasEverPublished={showcase.publishedAt !== null}
              isFeatured={showcase.isFeatured}
              onDecided={applyDecision}
            />
          </div>
        )}
      </Card>

      {privileged && showcase.reviewNote && (
        <div role="note" className="rounded-xl border border-warning-light bg-warning-50 px-4 py-3 text-body text-warning-dark">
          <p className="font-semibold">Reviewer note</p>
          <p className="mt-1 whitespace-pre-line">{showcase.reviewNote}</p>
        </div>
      )}

      {showcase.media.length > 0 && (
        <section aria-label="Screenshots">
          <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            {showcase.media.map((item) => (
              <li key={item.id}>
                <a href={item.url} target="_blank" rel="noopener noreferrer" className="block overflow-hidden rounded-xl border border-slate-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600">
                  <img src={item.url} alt={item.caption ?? `${profile.projectName} screenshot`} loading="lazy" className="aspect-video w-full object-cover" />
                  <span className="sr-only">(opens full size in a new tab)</span>
                </a>
              </li>
            ))}
          </ul>
        </section>
      )}

      <Card hover={false} className="space-y-6">
        <Section title={PROJECT_PROFILE_LABELS.problem}>{profile.problem}</Section>
        <Section title={PROJECT_PROFILE_LABELS.targetUsers}>{profile.targetUsers}</Section>
        <Section title={PROJECT_PROFILE_LABELS.solution}>{profile.solution}</Section>
        <Section title="What makes it different">{showcase.highlights}</Section>
      </Card>

      {members.length > 0 && (
        <Card hover={false}>
          <Card.Header>
            <Card.Title icon={Users}>Team</Card.Title>
          </Card.Header>
          <ul className="grid gap-3 sm:grid-cols-2">
            {members.map((member) => (
              <li key={`${member.fullName}-${member.role}`} className="flex items-center gap-3">
                {member.avatarUrl ? (
                  <img src={member.avatarUrl} alt="" className="h-9 w-9 rounded-full object-cover" />
                ) : (
                  <span className="grid h-9 w-9 place-items-center rounded-full bg-primary-50 text-body-sm font-semibold text-primary-700" aria-hidden="true">
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

      {privileged && history.length > 0 && (
        <Card hover={false}>
          <Card.Header>
            <Card.Title>Review history</Card.Title>
          </Card.Header>
          <ol className="space-y-3">
            {history.map((entry) => (
              <li key={`${entry.createdAt}-${entry.toStatus}`} className="text-body-sm">
                <p className="text-slate-700">
                  <span className="font-semibold text-secondary">{entry.actorName}</span>
                  {entry.fromStatus === entry.toStatus ? ' updated the page' : ` moved the page from ${entry.fromStatus} to ${entry.toStatus}`}
                  <span className="text-slate-600"> · {formatDateTime(entry.createdAt)}</span>
                </p>
                {entry.note && <p className="mt-0.5 whitespace-pre-line text-slate-600">{entry.note}</p>}
              </li>
            ))}
          </ol>
        </Card>
      )}
    </main>
  );
}
