import type {
  AddTeachingStaffBatchResponse,
  MentorKind,
  TeachingStaffCandidateDto,
} from '../types/subjects';
import { matchesSearchQuery } from './searchText.ts';

export type MentorKindFilter = 'ALL' | MentorKind;

export interface CandidateFilters {
  search: string;
  mentorType: MentorKindFilter;
}

export interface CandidateGroups {
  /** Not in the semester yet, so they can be ticked. */
  available: TeachingStaffCandidateDto[];
  /** Already listed in the semester (active or inactive); shown for completeness but not selectable. */
  inSemester: TeachingStaffCandidateDto[];
}

const byName = (left: TeachingStaffCandidateDto, right: TeachingStaffCandidateDto) =>
  left.name.localeCompare(right.name, undefined, { sensitivity: 'base' });

/** Splits the accounts of one role into those that can be added and those already in the semester list. */
export function groupCandidates(
  candidates: TeachingStaffCandidateDto[],
  role: TeachingStaffCandidateDto['role'],
  existingUserIds: ReadonlySet<string>,
  filters: CandidateFilters,
): CandidateGroups {
  const matching = candidates
    .filter(candidate => candidate.role === role)
    .filter(candidate => role !== 'MENTOR' || filters.mentorType === 'ALL' || candidate.mentorType === filters.mentorType)
    .filter(candidate => matchesSearchQuery(filters.search, [candidate.name, candidate.email, candidate.contractType]))
    .sort(byName);

  return {
    available: matching.filter(candidate => !existingUserIds.has(candidate.userId)),
    inSemester: matching.filter(candidate => existingUserIds.has(candidate.userId)),
  };
}

export interface StaffBatchSummary {
  tone: 'success' | 'partial' | 'info' | 'error';
  message: string;
  problems: { userId: string; name: string; message: string }[];
}

const plural = (count: number, noun: string) => `${count} ${noun}${count === 1 ? '' : 's'}`;

/** Turns the server result of a batch add into one message plus the people that need attention. */
export function summarizeStaffBatch(
  response: AddTeachingStaffBatchResponse,
  nameOf: (userId: string) => string,
  roleNoun: string,
): StaffBatchSummary {
  const problems = response.results
    .filter(item => item.outcome !== 'Added')
    .map(item => ({
      userId: item.userId,
      name: nameOf(item.userId),
      message: item.message || (item.outcome === 'AlreadyInList' ? 'Already in the semester list.' : 'Could not be added.'),
    }));
  const added = response.addedCount;
  const skipped = response.alreadyInListCount + response.rejectedCount;

  if (added > 0 && skipped === 0) return { tone: 'success', message: `Added ${plural(added, roleNoun)}.`, problems };
  if (added > 0) return { tone: 'partial', message: `Added ${plural(added, roleNoun)}, ${skipped} skipped.`, problems };
  if (response.rejectedCount > 0) return { tone: 'error', message: `No ${roleNoun}s were added.`, problems };
  return { tone: 'info', message: `Everyone selected was already in the semester list.`, problems };
}
