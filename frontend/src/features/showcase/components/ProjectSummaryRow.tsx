import { MessageSquare, SquareArrowOutUpRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import ShowcaseUpvoteButton from '../../../components/showcase/ShowcaseUpvoteButton';
import type { ShowcaseCardDto } from '../../../types/showcase';

export default function ProjectSummaryRow({ project, backTo, index }: { project: ShowcaseCardDto; backTo: string; index: number }) {
  return (
    <li className="group relative grid grid-cols-[3.5rem_minmax(0,1fr)] gap-x-3 gap-y-3 border-l-4 border-l-transparent p-4 transition-colors hover:border-l-primary hover:bg-slate-50 hover:shadow-sm focus-within:border-l-primary sm:grid-cols-[4.5rem_minmax(0,1fr)_auto] sm:gap-x-5 sm:p-6 dark:hover:bg-slate-800">
      <div className="flex h-14 w-14 shrink-0 items-center justify-center overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm sm:h-18 sm:w-18 dark:border-slate-600 dark:bg-dark-surface">
        {project.logoUrl
          ? <img src={project.logoUrl} alt={`${project.projectName} logo`} className="h-full w-full object-contain p-1" loading="lazy" />
          : <span className="text-body-lg font-semibold text-secondary dark:text-slate-100" aria-hidden="true">{project.projectName.slice(0, 2).toUpperCase()}</span>}
      </div>
      <div className="min-w-0">
        <h3 className="flex min-w-0 items-center gap-2 text-subheading font-bold text-secondary group-hover:text-primary-700 dark:text-white dark:group-hover:text-primary-300">
          <span className="shrink-0 text-body-sm font-semibold text-slate-400 dark:text-slate-500" aria-label={`Project ${index}`}>{index}.</span>
          <Link to={`/showcase/${project.id}`} state={{ showcaseBackTo: backTo, showcaseBackLabel: 'Project Showcase' }} className="line-clamp-1 after:absolute after:inset-0 after:content-[''] focus-visible:outline-none focus-visible:underline">
            {project.projectName}
          </Link>
          <Link to={`/showcase/${project.id}`} state={{ showcaseBackTo: backTo, showcaseBackLabel: 'Project Showcase' }} aria-label={`View ${project.projectName} showcase detail`} className="relative z-10 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-primary-50 hover:text-primary-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:hover:bg-primary-900/30 dark:hover:text-primary-300">
            <SquareArrowOutUpRight className="h-4 w-4" aria-hidden="true" />
          </Link>
        </h3>
        <p className="mt-1.5 line-clamp-2 text-body leading-relaxed text-slate-600 dark:text-slate-300">{project.description}</p>
        {project.industries.length > 0 && (
          <ul aria-label="Startup industries" className="mt-3 flex flex-wrap gap-1.5">
            {project.industries.map(industry => (
              <li key={industry} className="rounded-full border border-primary-200 bg-primary-50 px-2.5 py-0.5 text-caption font-medium text-primary-800 dark:border-primary-700 dark:bg-primary-900/30 dark:text-primary-200">{industry}</li>
            ))}
          </ul>
        )}
      </div>
      <div className="col-span-2 flex flex-wrap items-center justify-between gap-3 pl-17 sm:col-span-1 sm:justify-end sm:pl-0">
        <div className="relative z-10 flex items-center gap-3 text-body-sm text-slate-500 dark:text-slate-400">
          <ShowcaseUpvoteButton showcaseId={project.id} likeCount={project.likeCount} hasUpvoted={project.hasUpvoted} isOwner={project.isOwner} showLabel className="h-9 min-w-0 rounded-lg px-2" />
          <span className="inline-flex items-center gap-1" title="Comments">
            <MessageSquare className="h-4 w-4" aria-hidden="true" />{project.commentCount.toLocaleString()}<span className="sr-only"> comments</span>
          </span>
        </div>
      </div>
    </li>
  );
}
