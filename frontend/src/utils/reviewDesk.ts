import type { InboxReason, ReviewDeskInboxItem } from '../types/reviewDesk';
import i18n from '../i18n/index.ts';

type Tone = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/** Why a team is in "Needs a look today". Overdue first, then new versions, then deadlines coming up. */
export const inboxReasonView: Record<InboxReason, { label: string; tone: Tone }> = {
  Overdue: { get label() { return i18n.t('utils:reviewDesk.overdue'); }, tone: 'danger' },
  NewVersion: { get label() { return i18n.t('utils:reviewDesk.newVersion'); }, tone: 'info' },
  DueSoon: { get label() { return i18n.t('utils:reviewDesk.dueSoon'); }, tone: 'warning' },
};

/** Opens the team's workspace with the checkpoint's panel already open. */
export const teamCheckpointPath = (teamId: string, checkpointNumber: number) =>
  `/workspace/teams/${teamId}?checkpoint=${checkpointNumber}`;

/** "just now", "5 min ago", "3 h ago", "2 d ago". */
export function relativeTime(value: string | null | undefined, now: number = Date.now()): string {
  if (!value) return '';
  const then = new Date(value).getTime();
  if (Number.isNaN(then)) return '';
  const elapsed = Math.max(0, now - then);
  if (elapsed < MINUTE) return 'just now';
  if (elapsed < HOUR) return `${Math.floor(elapsed / MINUTE)} min ago`;
  if (elapsed < DAY) return `${Math.floor(elapsed / HOUR)} h ago`;
  return `${Math.floor(elapsed / DAY)} d ago`;
}

/** "5 h left" before a deadline, "2 d overdue" after it. */
export function deadlineText(value: string | null | undefined, now: number = Date.now()): string {
  if (!value) return '';
  const due = new Date(value).getTime();
  if (Number.isNaN(due)) return '';
  const distance = due - now;
  const span = (ms: number) => (ms >= DAY ? `${Math.floor(ms / DAY)} d` : ms >= HOUR ? `${Math.floor(ms / HOUR)} h` : `${Math.max(1, Math.floor(ms / MINUTE))} min`);
  return distance >= 0 ? `${span(distance)} left` : `${span(-distance)} overdue`;
}

/** The second line of an inbox row. */
export function inboxLine(item: ReviewDeskInboxItem, now: number = Date.now()): string {
  if (item.reason === 'NewVersion') {
    const version = item.versionNumber ? `Version v${item.versionNumber}` : 'New version';
    return `${version} submitted ${relativeTime(item.submittedAtUtc, now)}`.trim();
  }
  return `Deadline: ${deadlineText(item.dueAtUtc, now)}`.trim();
}
