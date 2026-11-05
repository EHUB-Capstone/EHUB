import type { ReactNode } from 'react';
import { CalendarDays, Flame, MessageSquare, Star } from 'lucide-react';
import { Link } from 'react-router-dom';
import Badge from '../../../components/ui/Badge';
import Card from '../../../components/ui/Card';
import type { ShowcaseCardDto } from '../../../types/showcase';
import { stageLabel } from '../../../utils/showcase';

interface ProjectSummaryCardProps {
  project: ShowcaseCardDto;
  /** Supplied by the featured API when it becomes available. */
  isFeatured?: boolean;
  /** Secondary moderator control. It renders above the card's stretched link. */
  secondaryAction?: ReactNode;
  /** The discovery URL to return to after visiting the project detail. */
  backTo?: string;
  backLabel?: string;
}

const stageTone = (stage: ShowcaseCardDto['developmentStage']) =>
  ({ Idea: 'neutral', Mvp: 'success', Beta: 'info', Launched: 'warning' } as const)[stage];

/** Reusable discovery/moderation card. Its main destination is the full card, not just the title. */
export default function ProjectSummaryCard({ project, isFeatured = false, secondaryAction, backTo, backLabel }: ProjectSummaryCardProps): React.ReactElement {
  const hasSignals = project.likeCount > 0 || project.commentCount > 0;

  return (
    <Card hover className="group relative flex h-full flex-col overflow-hidden p-5 dark:border-slate-700 dark:bg-dark-surface">
      <div className="flex items-start gap-3">
        <div className="flex h-16 w-16 shrink-0 items-center justify-center overflow-hidden rounded-2xl border border-secondary-100 bg-secondary-50 dark:border-secondary-600 dark:bg-secondary-700">
          {project.logoUrl
            ? <img src={project.logoUrl} alt={`${project.projectName} logo`} className="h-full w-full object-contain p-1" />
            : <span className="text-heading font-semibold tracking-tight text-secondary dark:text-white" aria-hidden="true">{project.projectName.slice(0, 2).toUpperCase()}</span>}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <Badge size="sm" variant={stageTone(project.developmentStage)}>{stageLabel(project.developmentStage)}</Badge>
            {isFeatured && <Badge size="sm" variant="brand"><Star className="h-3 w-3" aria-hidden="true" />Featured</Badge>}
            {project.publishedAt && <span className="inline-flex items-center gap-1 text-caption text-slate-500 dark:text-slate-300"><CalendarDays className="h-3.5 w-3.5" aria-hidden="true" />{new Date(project.publishedAt).toLocaleDateString()}</span>}
          </div>
          <h2 className="mt-2 line-clamp-1 text-subheading text-secondary dark:text-white">
            <Link
              to={`/showcase/${project.id}`}
              state={backTo ? { showcaseBackTo: backTo, showcaseBackLabel: backLabel } : undefined}
              className="after:absolute after:inset-0 after:content-[''] focus-visible:outline-none focus-visible:underline"
            >
              {project.projectName}
            </Link>
          </h2>
          <p className="mt-1 line-clamp-1 text-body-sm text-slate-600 dark:text-slate-300">{project.teamName}</p>
        </div>
        {secondaryAction && <div className="relative z-10 shrink-0">{secondaryAction}</div>}
      </div>
      <p className="mt-5 line-clamp-3 min-h-15 text-body-sm leading-relaxed text-slate-600 dark:text-slate-300">{project.description}</p>
      {project.industries.length > 0 && <div className="mt-4 flex flex-wrap gap-1.5">{project.industries.slice(0, 3).map(industry => <Badge key={industry} size="sm" variant="neutral">{industry}</Badge>)}</div>}
      <div className="mt-auto flex min-h-9 items-center justify-between gap-3 border-t border-slate-100 pt-4 dark:border-slate-700">
        {hasSignals ? <span className="inline-flex items-center gap-3 text-caption text-slate-500 dark:text-slate-300">
          {project.likeCount > 0 && <span className="inline-flex items-center gap-1"><Flame className="h-3.5 w-3.5 text-primary" aria-hidden="true" />{project.likeCount} interested</span>}
          {project.commentCount > 0 && <span className="inline-flex items-center gap-1"><MessageSquare className="h-3.5 w-3.5" aria-hidden="true" />{project.commentCount} comments</span>}
        </span> : <span />}
        <span className="text-body-sm font-semibold text-primary-700 dark:text-primary-300">View project</span>
      </div>
    </Card>
  );
}
