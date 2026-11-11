import { Flame, MessageSquare, SquareArrowOutUpRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import type { ShowcaseCardDto } from '../../../types/showcase';

/** Compact row for the side rails (featured / newly published). The whole row is one stretched link. */
export default function ProjectRailItem({ project, backTo, backLabel, index }: { project: ShowcaseCardDto; backTo?: string; backLabel?: string; index: number }): React.ReactElement {
  return (
    <li className="group relative flex items-start gap-2 rounded-xl p-2.5 transition-colors hover:bg-slate-50 has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-primary/40 dark:hover:bg-slate-800">
      <span className="pt-1 text-caption font-semibold text-slate-400 dark:text-slate-500" aria-label={`Project ${index}`}>{index}.</span>
      <div className="flex h-12 w-12 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm dark:border-slate-600 dark:bg-dark-surface">
        {project.logoUrl
          ? <img src={project.logoUrl} alt="" className="h-full w-full object-contain p-1" />
          : <span className="text-body-sm font-semibold text-secondary dark:text-white" aria-hidden="true">{project.projectName.slice(0, 2).toUpperCase()}</span>}
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex min-w-0 items-center gap-1">
          <Link to={`/showcase/${project.id}`} state={backTo ? { showcaseBackTo: backTo, showcaseBackLabel: backLabel } : undefined} className="block min-w-0 flex-1 truncate text-body-sm font-semibold text-secondary outline-none after:absolute after:inset-0 after:content-[''] dark:text-white">{project.projectName}</Link>
          <SquareArrowOutUpRight className="h-3.5 w-3.5 shrink-0 text-slate-400 transition-colors group-hover:text-primary-700 dark:group-hover:text-primary-300" aria-hidden="true" />
        </div>
        <p className="mt-0.5 truncate text-caption text-slate-500 dark:text-slate-300">{project.teamName}</p>
        <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-caption text-slate-500 dark:text-slate-400"><span className="inline-flex items-center gap-1"><Flame className="h-3.5 w-3.5 text-primary" aria-hidden="true" />{project.likeCount} interested</span><span className="inline-flex items-center gap-1"><MessageSquare className="h-3.5 w-3.5" aria-hidden="true" />{project.commentCount}</span></div>
        {project.industries.length > 0 && <p className="mt-1.5 line-clamp-1 text-caption font-medium text-primary-700 dark:text-primary-300">{project.industries.slice(0, 2).join(' · ')}</p>}
      </div>
    </li>
  );
}
