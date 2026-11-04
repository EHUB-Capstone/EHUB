import { badgeTones } from '../../../components/ui/Badge';
import type { ProjectCompetition, ProjectCompetitionStage } from '../../../types/projectData';
import { cn } from '../../../utils/cn';
import { PROJECT_DATA_EMPTY_VALUE, competitionSummary } from '../../../utils/projectData';

// How far the project got, as a tone: just registered, in the qualifier, or in the final. The stage is always written
// out in the text, so the color is never the only signal.
const STAGE_TONES: Record<ProjectCompetitionStage, string> = {
  Registered: badgeTones.neutral,
  Qualifier: badgeTones.info,
  Final: badgeTones.success,
};

interface ProjectCompetitionBadgesProps {
  competitions: readonly ProjectCompetition[];
  /** How many badges to show before folding the rest into "+N". */
  visible?: number;
}

/** "FShark 2026 · Final round" per competition, newest first (the order the API sends). */
export default function ProjectCompetitionBadges({ competitions, visible = 2 }: ProjectCompetitionBadgesProps) {
  if (competitions.length === 0) return <span className="text-slate-400">{PROJECT_DATA_EMPTY_VALUE}</span>;

  const shown = competitions.slice(0, visible);
  const hidden = competitions.length - shown.length;

  return (
    <span className="flex flex-wrap gap-1" title={competitions.map(competitionSummary).join('\n')}>
      {shown.map(entry => (
        <span
          key={`${entry.competition}|${entry.name}|${entry.year}`}
          className={cn('inline-flex max-w-[15rem] items-center rounded-md border px-1.5 py-0.5 text-caption font-semibold', STAGE_TONES[entry.stage])}
        >
          <span className="truncate">{competitionSummary(entry)}</span>
        </span>
      ))}
      {hidden > 0 && (
        <span className="rounded-md bg-slate-100 px-1.5 py-0.5 text-xs font-medium text-slate-500">+{hidden}</span>
      )}
    </span>
  );
}
