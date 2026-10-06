import { AlertTriangle } from 'lucide-react';
import type { GroupProjectWarning } from '../../types/groupProjectConsistency';

interface GroupProjectWarningBannerProps {
  warnings: GroupProjectWarning[];
}

const Code = ({ children }: { children: string }) => (
  <code className="rounded bg-white/70 px-1 py-0.5 font-mono text-[11px] text-amber-900">{children}</code>
);

const joinCodes = (values: string[]) => values.map((value, index) => (
  <span key={value}>{index > 0 ? ', ' : ''}<Code>{value}</Code></span>
));

/** Class-level notice for Group/Project data that breaks the 1-1 rule. Hidden when the data is consistent. */
export default function GroupProjectWarningBanner({ warnings }: GroupProjectWarningBannerProps) {
  if (warnings.length === 0) return null;

  return (
    <div role="status" data-testid="class-group-project-warning" className="rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
      <p className="flex items-center gap-2 font-semibold">
        <AlertTriangle className="h-4 w-4 shrink-0 text-amber-500" aria-hidden="true" />
        Group and project data is inconsistent ({warnings.length} issue{warnings.length === 1 ? '' : 's'})
      </p>
      <p className="mt-1 text-xs text-amber-700">Each group must belong to exactly one project, and each project to exactly one group.</p>
      <ul className="mt-2 space-y-1.5 text-xs leading-5">
        {warnings.map((warning) => (
          <li key={`${warning.type}:${warning.subject}`}>
            {warning.type === 'GROUP_HAS_MULTIPLE_PROJECTS' ? 'Group' : 'Project'} <Code>{warning.subject}</Code>{' '}
            is assigned to multiple {warning.type === 'GROUP_HAS_MULTIPLE_PROJECTS' ? 'projects' : 'groups'}: {joinCodes(warning.related)}.
          </li>
        ))}
      </ul>
    </div>
  );
}
