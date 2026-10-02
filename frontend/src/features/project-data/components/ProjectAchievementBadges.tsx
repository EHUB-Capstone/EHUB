import { cn } from '../../../utils/cn';
import { PROJECT_DATA_EMPTY_VALUE, orderAchievements } from '../../../utils/projectData';
import type { ProjectAchievement } from '../../../types/projectData';
import { ACHIEVEMENT_STYLES } from '../achievementStyles';

interface ProjectAchievementBadgesProps {
  achievements: readonly ProjectAchievement[];
  className?: string;
}

/** One independent badge per label; a project may carry several at once. */
export default function ProjectAchievementBadges({ achievements, className }: ProjectAchievementBadgesProps) {
  const ordered = orderAchievements(achievements);
  if (ordered.length === 0) return <span className="text-slate-400">{PROJECT_DATA_EMPTY_VALUE}</span>;

  return (
    <ul className={cn('flex flex-wrap gap-1.5', className)} aria-label="Achievements">
      {ordered.map(name => {
        const { icon: Icon, className: tone } = ACHIEVEMENT_STYLES[name];
        return (
          <li
            key={name}
            className={cn('inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[11px] font-semibold', tone)}
          >
            <Icon className="h-3 w-3" aria-hidden="true" />
            {name}
          </li>
        );
      })}
    </ul>
  );
}
