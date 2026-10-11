import type { MentorHistoryItem } from '../types/teamManagement';

export interface MentorHistoryGroup {
  semester: string;
  items: MentorHistoryItem[];
}

/** Semesters in the order they first appear (the API returns the newest first), each with its teams. */
export function groupMentorHistoryBySemester(items: readonly MentorHistoryItem[]): MentorHistoryGroup[] {
  const groups = new Map<string, MentorHistoryItem[]>();
  for (const item of items) {
    const key = item.semesterCode || '';
    groups.set(key, [...(groups.get(key) ?? []), item]);
  }
  return [...groups.entries()].map(([semester, grouped]) => ({ semester, items: grouped }));
}

const dateText = (value: string) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleDateString('en-GB');
};

/** One short line saying how and when the assignment ended. */
export function describeMentorHistoryEnd(item: Pick<MentorHistoryItem, 'endedBecause' | 'endedAtUtc'>): string {
  const when = dateText(item.endedAtUtc);
  const suffix = when ? ` on ${when}` : '';
  return item.endedBecause === 'ClassCompleted'
    ? `Finished with the class${suffix}`
    : `Assignment ended early${suffix}`;
}
