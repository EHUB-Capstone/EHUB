import { AlertTriangle } from 'lucide-react';
import type { ManagedTeam } from '../../types/teamManagement';
import { getTeamsWithMajorWarning } from '../../utils/teamManagement';

interface TeamMajorWarningBannerProps {
  teams: ManagedTeam[];
  onViewTeams?: () => void;
}

/** Class-level notice listing every team that does not meet the GROUP_1 + GROUP_2 major requirement. */
export default function TeamMajorWarningBanner({ teams, onViewTeams }: TeamMajorWarningBannerProps) {
  const warnings = getTeamsWithMajorWarning(teams);
  if (warnings.length === 0) return null;

  return (
    <div role="status" data-testid="class-team-major-warning" className="rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
      <div className="flex items-start justify-between gap-3">
        <p className="flex items-center gap-2 font-semibold">
          <AlertTriangle className="h-4 w-4 shrink-0 text-amber-500" aria-hidden="true" />
          {warnings.length} team{warnings.length === 1 ? '' : 's'} in this class {warnings.length === 1 ? 'does' : 'do'} not meet the major requirement
        </p>
        {onViewTeams && (
          <button type="button" onClick={onViewTeams} className="shrink-0 rounded-lg bg-white px-2.5 py-1 text-xs font-semibold text-amber-700 border border-amber-200 hover:bg-amber-100">
            View teams
          </button>
        )}
      </div>
      <ul className="mt-2 space-y-1.5 text-xs leading-5">
        {warnings.map(({ team, warning }) => (
          <li key={team._id}>
            <span className="font-semibold">{team.teamName}</span>
            {team.teamCode ? <span className="font-mono text-amber-600"> ({team.teamCode})</span> : null}
            {' — '}{warning.message}
          </li>
        ))}
      </ul>
    </div>
  );
}
