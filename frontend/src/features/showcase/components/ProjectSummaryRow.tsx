import { Eye, MessageSquare } from 'lucide-react';
import { Link } from 'react-router-dom';
import ShowcaseUpvoteButton from '../../../components/showcase/ShowcaseUpvoteButton';
import type { ShowcaseCardDto } from '../../../types/showcase';

export default function ProjectSummaryRow({ project, backTo }: { project: ShowcaseCardDto; backTo: string }) {
  return (
    <li className="group relative grid grid-cols-[3.5rem_minmax(0,1fr)] gap-x-3 gap-y-3 border-l-4 border-l-transparent p-4 transition-colors hover:border-l-primary hover:bg-slate-50 hover:shadow-sm focus-within:border-l-primary sm:grid-cols-[4.5rem_minmax(0,1fr)_auto] sm:gap-x-5 sm:p-6 dark:hover:bg-slate-800">
      <div className="flex h-14 w-14 shrink-0 items-center justify-center overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm sm:h-18 sm:w-18 dark:border-slate-600 dark:bg-dark-surface">
        {project.logoUrl
          ? <img src={project.logoUrl} alt={`${project.projectName} logo`} className="h-full w-full object-contain p-1" loading="lazy" />
          : <span className="text-body-lg font-semibold text-secondary dark:text-slate-100" aria-hidden="true">{project.projectName.slice(0, 2).toUpperCase()}</span>}
      </div>
      <div className="min-w-0">
        <h3 className="text-subheading font-bold text-secondary group-hover:text-primary-700 dark:text-white dark:group-hover:text-primary-300">
          <Link to={`/showcase/${project.id}`} state={{ showcaseBackTo: backTo, showcaseBackLabel: 'Project Showcase' }} className="line-clamp-1 after:absolute after:inset-0 after:content-[''] focus-visible:outline-none focus-visible:underline">
            {project.projectName}
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
      <div className="col-span-2 flex flex-wrap items-center justify-between gap-3 pl-17 sm:col-span-1 sm:flex-col sm:items-end sm:justify-start sm:pl-0">
        <ShowcaseUpvoteButton showcaseId={project.id} likeCount={project.likeCount} hasUpvoted={project.hasUpvoted} isOwner={project.isOwner} className="relative z-10 h-12 min-w-22 rounded-xl border border-slate-300 text-body-lg font-bold shadow-sm hover:border-primary dark:border-slate-600" />
        <div className="flex max-w-full flex-wrap items-center justify-end gap-x-3 gap-y-1 text-body-sm text-slate-500 dark:text-slate-400">
          <span className="inline-flex items-center gap-1" title="Product views">
            <Eye className="h-4 w-4" aria-hidden="true" />{(project.viewCount ?? 0).toLocaleString()}<span className="sr-only"> views</span>
          </span>
          <span className="inline-flex items-center gap-1" title="Comments">
            <MessageSquare className="h-4 w-4" aria-hidden="true" />{project.commentCount.toLocaleString()}<span className="sr-only"> comments</span>
          </span>
        </div>
      </div>
    </li>
  );
}
