import i18n from '../i18n/index.ts';
/** Ready-made reasons for replacing the mentor of a team, so an admin picks one instead of typing. */
export const MENTOR_CHANGE_REASONS = [
  { value: 'unavailable', get label() { return i18n.t('utils:mentorChangeReasons.mentorIsNoLongerAvailable'); } },
  { value: 'overloaded', get label() { return i18n.t('utils:mentorChangeReasons.mentorIsOverloaded'); } },
  { value: 'schedule', get label() { return i18n.t('utils:mentorChangeReasons.scheduleConflict'); } },
  { value: 'team-request', get label() { return i18n.t('utils:mentorChangeReasons.teamRequestedAChange'); } },
  { value: 'rebalance', get label() { return i18n.t('utils:mentorChangeReasons.rebalanceMentorWorkload'); } },
  { value: 'other', get label() { return i18n.t('utils:mentorChangeReasons.other'); } },
] as const;

export type MentorChangeReasonCode = (typeof MENTOR_CHANGE_REASONS)[number]['value'];

export const OTHER_REASON_CODE: MentorChangeReasonCode = 'other';
export const MIN_CHANGE_NOTE_LENGTH = 3;
export const MAX_CHANGE_NOTE_LENGTH = 500;

const labelOf = (code: string) => MENTOR_CHANGE_REASONS.find(item => item.value === code)?.label ?? '';

/** Whether a choice can be saved: a ready-made reason is enough, "Other" needs a written note. */
export function isChangeReasonValid(code: string, note: string): boolean {
  if (!labelOf(code)) return false;
  const length = note.trim().length;
  if (length > MAX_CHANGE_NOTE_LENGTH) return false;
  return code === OTHER_REASON_CODE ? length >= MIN_CHANGE_NOTE_LENGTH : true;
}

/** The text sent to the server and kept in the assignment history, for example "Mentor is overloaded: 6 teams". */
export function buildChangeReason(code: string, note: string): string {
  const trimmed = note.trim();
  if (code === OTHER_REASON_CODE) return trimmed;
  const label = labelOf(code);
  return trimmed ? `${label}: ${trimmed}` : label;
}
