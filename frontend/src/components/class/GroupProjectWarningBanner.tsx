import { AlertTriangle } from 'lucide-react';
import type { GroupProjectWarning } from '../../types/groupProjectConsistency';
import { useTranslation } from 'react-i18next';

interface GroupProjectWarningBannerProps {
  warnings: GroupProjectWarning[];
}

const Code = ({ children }: { children: string }) => (
  <code className="rounded bg-white/70 px-1 py-0.5 font-mono text-caption text-amber-900">{children}</code>
);

const joinCodes = (values: string[]) => values.map((value, index) => (
  <span key={value}>{index > 0 ? ', ' : ''}<Code>{value}</Code></span>
));

/** Class-level notice for Group/Project data that breaks the 1-1 rule. Hidden when the data is consistent. */
export default function GroupProjectWarningBanner({ warnings }: GroupProjectWarningBannerProps) {
  const { t } = useTranslation('class');
  if (warnings.length === 0) return null;

  return (
    <div role="status" data-testid="class-group-project-warning" className="rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
      <p className="flex items-center gap-2 font-semibold">
        <AlertTriangle className="h-4 w-4 shrink-0 text-amber-500" aria-hidden="true" />
        {t('groupProjectWarningBanner.groupAndProjectDataIsInconsistent')}{warnings.length} {t('groupProjectWarningBanner.issue')}{warnings.length === 1 ? '' : 's'})
      </p>
      <p className="mt-1 text-xs text-amber-700">{t('groupProjectWarningBanner.eachGroupMustBelongToExactly')}</p>
      <ul className="mt-2 space-y-1.5 text-xs leading-5">
        {warnings.map((warning) => (
          <li key={`${warning.type}:${warning.subject}`}>
            {warning.type === 'GROUP_HAS_MULTIPLE_PROJECTS' ? t('groupProjectWarningBanner.group') : t('groupProjectWarningBanner.project')} <Code>{warning.subject}</Code>{' '}
            {t('groupProjectWarningBanner.isAssignedToMultiple')} {warning.type === 'GROUP_HAS_MULTIPLE_PROJECTS' ? 'projects' : 'groups'}: {joinCodes(warning.related)}.
          </li>
        ))}
      </ul>
    </div>
  );
}
