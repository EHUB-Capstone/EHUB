import type { MentorSemesterClass } from '../types/mentorAdmin';
import { matchesSearchQuery } from './searchText.ts';

export interface SemesterClassFilters {
  search: string;
  /** Subject code such as EXE101, or ALL. */
  subject: string;
  onlyMissing: boolean;
}

/** Open mentor slots of a class: one per team and kind that has no active mentor. */
export const missingSlots = (item: MentorSemesterClass): number => item.missingEnterpriseCount + item.missingAcademicCount;

export function filterSemesterClasses(items: readonly MentorSemesterClass[], filters: SemesterClassFilters): MentorSemesterClass[] {
  return items
    .filter(item => filters.subject === 'ALL' || item.subjectCode === filters.subject)
    .filter(item => !filters.onlyMissing || missingSlots(item) > 0)
    .filter(item => matchesSearchQuery(filters.search, [item.classCode, item.subjectCode, item.lecturerName]))
    // Classes that need the most work first, then by code.
    .sort((left, right) => missingSlots(right) - missingSlots(left) || left.classCode.localeCompare(right.classCode, undefined, { numeric: true }));
}

export const totalMissingSlots = (items: readonly MentorSemesterClass[]): number =>
  items.reduce((total, item) => total + missingSlots(item), 0);
