import { useTranslation } from 'react-i18next';
interface TemporaryMentorBadgeProps {
  className?: string;
}

/** Marks a mentor who has no account yet: usable in a team, but unable to log in until an email is added. */
export default function TemporaryMentorBadge({ className = '' }: TemporaryMentorBadgeProps) {
  const { t } = useTranslation('admin');
  return (
    <span
      title={t('temporaryMentorBadge.thisMentorHasNoAccountYet')}
      className={`inline-flex items-center whitespace-nowrap rounded-full border border-amber-200 bg-amber-50 px-2 py-0 text-overline font-semibold text-amber-800 ${className}`}
    >
      {t('subjectManagement.temporary')}
    </span>
  );
}
