import { AlertTriangle } from 'lucide-react';
import type { TeamMajorComposition } from '../../types/teamManagement';

interface OwnTeamMajorWarningProps {
  warning: TeamMajorComposition;
  testId?: string;
}

/** Notice for a student whose own team does not meet the major requirement. */
export default function OwnTeamMajorWarning({ warning, testId = 'own-team-major-warning' }: OwnTeamMajorWarningProps) {
  return (
    <div role="status" data-testid={testId} className="flex items-start gap-2 rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm leading-6 text-amber-800">
      <AlertTriangle className="mt-1 h-4 w-4 shrink-0 text-amber-500" aria-hidden="true" />
      <div>
        <p className="font-semibold">Your team does not meet the major requirement</p>
        <p className="text-xs">{warning.message}</p>
        <p className="mt-1 text-xs text-amber-700">Your team and its members are unchanged. Please contact your lecturer to adjust the team.</p>
      </div>
    </div>
  );
}
