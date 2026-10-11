interface TemporaryMentorBadgeProps {
  className?: string;
}

/** Marks a mentor who has no account yet: usable in a team, but unable to log in until an email is added. */
export default function TemporaryMentorBadge({ className = '' }: TemporaryMentorBadgeProps) {
  return (
    <span
      title="This mentor has no account yet. They cannot log in or see the team until an email is added."
      className={`inline-flex items-center whitespace-nowrap rounded-full border border-amber-200 bg-amber-50 px-2 py-0 text-[10px] font-semibold text-amber-800 ${className}`}
    >
      Temporary
    </span>
  );
}
