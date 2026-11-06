import { useState, type ReactNode } from 'react';
import { ArrowUpRight, CalendarDays, MessageSquare, Star, Users } from 'lucide-react';
import { Link } from 'react-router-dom';
import Badge from '../../../components/ui/Badge';
import Card from '../../../components/ui/Card';
import ShowcaseUpvoteButton from '../../../components/showcase/ShowcaseUpvoteButton';
import type { ShowcaseCardDto } from '../../../types/showcase';
import { stageLabel } from '../../../utils/showcase';
import { cn } from '../../../utils/cn';
import { parseShowcaseVideo } from '../../../utils/showcaseVideo';
import ProjectMedia from './ProjectMedia';

interface ProjectSummaryCardProps {
  project: ShowcaseCardDto;
  /** An icon action such as the moderator Featured toggle; it renders above the stretched link. */
  secondaryAction?: ReactNode;
  /** The discovery URL to return to after visiting the project detail. */
  backTo?: string;
  backLabel?: string;
  /** A preview image of the project. The post has no image area when it is missing; the logo is never used here. */
  coverUrl?: string | null;
  /** Overrides the video link of the project card; it takes the image area and plays muted while the card is hovered or focused. */
  videoUrl?: string | null;
}

const stageTone = (stage: ShowcaseCardDto['developmentStage']) =>
  ({ Idea: 'neutral', Mvp: 'success', Beta: 'info', Launched: 'warning' } as const)[stage];

/**
 * Reusable discovery/moderation card laid out like a feed post: author header, text, preview band and a footer with the
 * community counters. Its main destination is the full card, not just the title.
 */
export default function ProjectSummaryCard({ project, secondaryAction, backTo, backLabel, coverUrl, videoUrl }: ProjectSummaryCardProps): React.ReactElement {
  const initials = project.projectName.slice(0, 2).toUpperCase();
  const [active, setActive] = useState(false);
  const video = parseShowcaseVideo(videoUrl ?? project.videoUrl);

  return (
    <Card hover padding="p-0" className="group relative overflow-hidden dark:border-slate-700 dark:bg-dark-surface" onMouseEnter={() => setActive(true)} onMouseLeave={() => setActive(false)} onFocus={() => setActive(true)} onBlur={() => setActive(false)}>
      <div className="flex items-start gap-3 px-5 pt-4">
        <div className="flex h-12 w-12 shrink-0 items-center justify-center overflow-hidden rounded-full border border-secondary-100 bg-secondary-50 dark:border-secondary-600 dark:bg-secondary-700">
          {project.logoUrl
            ? <img src={project.logoUrl} alt={`${project.projectName} logo`} className="h-full w-full object-contain p-1.5" />
            : <span className="text-body font-semibold tracking-tight text-secondary dark:text-white" aria-hidden="true">{initials}</span>}
        </div>
        <div className="min-w-0 flex-1">
          <h2 className="line-clamp-1 text-body-lg font-semibold text-secondary transition-colors group-hover:text-primary-700 dark:text-white dark:group-hover:text-primary-300">
            <Link
              to={`/showcase/${project.id}`}
              state={backTo ? { showcaseBackTo: backTo, showcaseBackLabel: backLabel } : undefined}
              className="after:absolute after:inset-0 after:content-[''] focus-visible:outline-none focus-visible:underline"
            >
              {project.projectName}
            </Link>
          </h2>
          <p className="mt-0.5 flex flex-wrap items-center gap-x-2 text-caption text-slate-500 dark:text-slate-300">
            <span className="inline-flex min-w-0 items-center gap-1"><Users className="h-3.5 w-3.5 shrink-0" aria-hidden="true" /><span className="truncate">{project.teamName}</span></span>
            {project.publishedAt && <span className="inline-flex items-center gap-1"><CalendarDays className="h-3.5 w-3.5" aria-hidden="true" />{new Date(project.publishedAt).toLocaleDateString()}</span>}
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <Badge size="sm" variant={stageTone(project.developmentStage)}>{stageLabel(project.developmentStage)}</Badge>
          {secondaryAction && <div className="relative z-10">{secondaryAction}</div>}
        </div>
      </div>

      <div className="px-5 pb-1 pt-3">
        {project.isFeatured && <Badge size="sm" variant="brand" className="mb-2"><Star className="h-3 w-3" aria-hidden="true" />Featured</Badge>}
        <p className="line-clamp-4 text-body leading-relaxed text-slate-700 dark:text-slate-200">{project.description}</p>
        {project.industries.length > 0 && (
          <p className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-body-sm font-medium text-primary-700 dark:text-primary-300">
            {project.industries.slice(0, 3).map(industry => <span key={industry}>#{industry.replace(/\s+/g, '')}</span>)}
          </p>
        )}
      </div>

      <ProjectMedia id={project.id} name={project.projectName} video={video} coverUrl={coverUrl} active={active} />

      <div className={cn('mt-3 grid grid-cols-3 divide-x divide-slate-100 border-t border-slate-100 text-body-sm font-medium dark:divide-slate-700 dark:border-slate-700')}>
        <ShowcaseUpvoteButton
          showcaseId={project.id}
          likeCount={project.likeCount}
          hasUpvoted={project.hasUpvoted}
          isOwner={project.isOwner}
          className="relative z-10 h-11 w-full rounded-none"
        />
        <span className="flex min-h-11 items-center justify-center gap-1.5 text-slate-600 dark:text-slate-300"><MessageSquare className="h-4 w-4 text-slate-400" aria-hidden="true" />{project.commentCount}<span className="sr-only"> comments</span></span>
        <span className="flex min-h-11 items-center justify-center gap-1 font-semibold text-primary-700 dark:text-primary-300">View project <ArrowUpRight className="h-4 w-4 transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5" aria-hidden="true" /></span>
      </div>
    </Card>
  );
}
