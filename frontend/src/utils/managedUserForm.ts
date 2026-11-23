import i18n from '../i18n/index.ts';
export type MentorKindValue = 'Enterprise' | 'Academic';

export const MENTOR_KIND_OPTIONS: ReadonlyArray<{ value: MentorKindValue; label: string }> = [
  { value: 'Enterprise', get label() { return i18n.t('utils:managedUserForm.enterpriseMentor'); } },
  { value: 'Academic', get label() { return i18n.t('utils:managedUserForm.lecturerMentor'); } },
];

/** A mentor must be either an enterprise or a lecturer mentor, because that decides which team slot they can fill. */
export function mentorTypeError(role: string, mentorType: string): string | null {
  if (role !== 'MENTOR') return null;
  return MENTOR_KIND_OPTIONS.some(option => option.value === mentorType)
    ? null
    : i18n.t('utils:managedUserForm.chooseWhetherThisMentorIsAn');
}

const MENTOR_ONLY_FIELDS = ['mentorType', 'expertise', 'bio', 'availabilityNote'] as const;

/** Mentor-only fields (type, expertise, background, availability) are sent only for mentors, so other roles never carry stale values. */
export function toManagedUserPayload<T extends { role: string; mentorType: string; expertise: string[]; bio: string; availabilityNote: string }>(
  form: T,
): Omit<T, (typeof MENTOR_ONLY_FIELDS)[number]> & Partial<Pick<T, (typeof MENTOR_ONLY_FIELDS)[number]>> {
  const { mentorType, expertise, bio, availabilityNote, ...rest } = form;
  return form.role === 'MENTOR' ? { ...rest, mentorType, expertise, bio, availabilityNote } : rest;
}
