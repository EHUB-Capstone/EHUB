import type { ReactNode } from 'react';
import type { MentorType } from '../../types/mentorAdmin';
import { cn } from '../../utils/cn';
import { MENTOR_KIND_STYLES } from '../../utils/mentorKindStyles';

interface MentorKindTagProps {
  type: MentorType;
  className?: string;
  /** Extra text after the label, for example the contract type. */
  children?: ReactNode;
}

export default function MentorKindTag({ type, className, children }: MentorKindTagProps) {
  const style = MENTOR_KIND_STYLES[type];
  const Icon = style.Icon;
  return (
    <span
      title={style.hint}
      className={cn('inline-flex items-center gap-1 whitespace-nowrap rounded-full border px-2 py-0.5 text-[11px] font-semibold', style.badge, className)}
    >
      <Icon className="h-3 w-3" aria-hidden="true" />
      {style.label}
      {children}
    </span>
  );
}
