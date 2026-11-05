import { Link } from 'react-router-dom';
import Badge from '../../../components/ui/Badge';
import type { ShowcaseCardDto } from '../../../types/showcase';
import { stageLabel } from '../../../utils/showcase';

const stageTone = (stage: ShowcaseCardDto['developmentStage']) =>
  ({ Idea: 'neutral', Mvp: 'success', Beta: 'info', Launched: 'warning' } as const)[stage];

/** Compact row for the side rails (featured / newly published). The whole row is one stretched link. */
export default function ProjectRailItem({ project, backTo, backLabel }: { project: ShowcaseCardDto; backTo?: string; backLabel?: string }): React.ReactElement {
  return (
    <li className="group relative flex items-center gap-3 rounded-xl p-2 transition-colors hover:bg-slate-50 has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-primary/40 dark:hover:bg-slate-800">
      <div className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-full border border-secondary-100 bg-secondary-50 dark:border-secondary-600 dark:bg-secondary-700">
        {project.logoUrl
          ? <img src={project.logoUrl} alt="" className="h-full w-full object-contain p-1" />
          : <span className="text-body-sm font-semibold text-secondary dark:text-white" aria-hidden="true">{project.projectName.slice(0, 2).toUpperCase()}</span>}
      </div>
      <div className="min-w-0 flex-1">
        <Link
          to={`/showcase/${project.id}`}
          state={backTo ? { showcaseBackTo: backTo, showcaseBackLabel: backLabel } : undefined}
          className="block truncate text-body-sm font-semibold text-secondary outline-none after:absolute after:inset-0 after:content-[''] dark:text-white"
        >
          {project.projectName}
        </Link>
        <p className="truncate text-caption text-slate-500 dark:text-slate-300">{project.teamName}</p>
      </div>
      <Badge size="sm" variant={stageTone(project.developmentStage)}>{stageLabel(project.developmentStage)}</Badge>
    </li>
  );
}
