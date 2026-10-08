export type MentorKindValue = 'Enterprise' | 'Academic';

export const MENTOR_KIND_OPTIONS: ReadonlyArray<{ value: MentorKindValue; label: string }> = [
  { value: 'Enterprise', label: 'Enterprise mentor' },
  { value: 'Academic', label: 'Lecturer mentor' },
];

/** A mentor must be either an enterprise or a lecturer mentor, because that decides which team slot they can fill. */
export function mentorTypeError(role: string, mentorType: string): string | null {
  if (role !== 'MENTOR') return null;
  return MENTOR_KIND_OPTIONS.some(option => option.value === mentorType)
    ? null
    : 'Choose whether this mentor is an Enterprise mentor or a Lecturer mentor';
}

/** The mentor type is only sent for mentors, so other roles never carry a stale value from the form. */
export function toManagedUserPayload<T extends { role: string; mentorType: string }>(form: T): Omit<T, 'mentorType'> & { mentorType?: string } {
  const { mentorType, ...rest } = form;
  return form.role === 'MENTOR' ? { ...rest, mentorType } : rest;
}
