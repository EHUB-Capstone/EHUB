import type { ManagedTeam, MentorAssignment } from '../types/teamManagement';
import { canAssignMentorTypeToTeam } from './teamManagement.ts';
import { matchesSearchQuery } from './searchText.ts';

export type MentorSlot = MentorAssignment['slot'];

export interface MentorOption {
  _id: string;
  name: string;
  email: string;
  organization?: string | null;
  mentorType: MentorSlot;
  contractType?: string | null;
  activeTeamCount: number;
}

export interface TeamSlotRow {
  team: ManagedTeam;
  enterprise: MentorAssignment | null;
  academic: MentorAssignment | null;
  /** How many of the two slots have no active mentor: 0 means the team is complete. */
  missingCount: number;
}

const isActive = (assignment: MentorAssignment) => assignment.status.trim().toLowerCase() === 'active';
const compareNatural = (left: string, right: string) => left.localeCompare(right, undefined, { numeric: true, sensitivity: 'base' });

/** One row per team that can take mentors, with the active mentor of each slot, ordered by team code. */
export function buildTeamSlotRows(teams: readonly ManagedTeam[]): TeamSlotRow[] {
  return teams
    .filter(team => {
      const status = team.status?.trim().toLowerCase();
      return !status || status === 'active' || status === 'approved';
    })
    .map(team => {
      const active = (team.currentMentorAssignments || []).filter(isActive);
      const enterprise = active.find(item => item.slot === 'Enterprise') ?? null;
      const academic = active.find(item => item.slot === 'Academic') ?? null;
      return { team, enterprise, academic, missingCount: (enterprise ? 0 : 1) + (academic ? 0 : 1) };
    })
    .sort((left, right) => compareNatural(left.team.teamCode || left.team.teamName || '', right.team.teamCode || right.team.teamName || ''));
}

export type SlotFilter = 'all' | 'missing';

export function filterTeamSlotRows(rows: readonly TeamSlotRow[], filter: SlotFilter, search: string): TeamSlotRow[] {
  return rows.filter(row =>
    (filter === 'all' || row.missingCount > 0)
    && matchesSearchQuery(search, [row.team.teamName, row.team.teamCode, row.team.groupName]));
}

export type MentorKindFilter = 'ALL' | MentorSlot;

/** Mentors matching the search text and kind chip. */
export function filterMentorOptions(options: readonly MentorOption[], search: string, kind: MentorKindFilter): MentorOption[] {
  return options.filter(option =>
    (kind === 'ALL' || option.mentorType === kind)
    && matchesSearchQuery(search, [option.name, option.email, option.contractType ?? '']));
}

/** Mentors that can take a given slot: the right kind, and never the mentor who already holds it. */
export function slotCandidates(options: readonly MentorOption[], slot: MentorSlot, currentMentorProfileId?: string | null): MentorOption[] {
  return options.filter(option => option.mentorType === slot && option._id !== currentMentorProfileId);
}

/** Teams that can still take a mentor of this kind, ordered by team code. */
export function eligibleTeamsForKind(teams: readonly ManagedTeam[], kind: MentorSlot): ManagedTeam[] {
  return teams
    .filter(team => canAssignMentorTypeToTeam(team, kind))
    .sort((left, right) => compareNatural(left.teamCode || left.teamName || '', right.teamCode || right.teamName || ''));
}

/** Circular Tab order for a focus trap: `current` is the focused index, or -1 when focus is outside the dialog. */
export function nextFocusIndex(current: number, count: number, backwards: boolean): number {
  if (count <= 0) return -1;
  if (current < 0) return backwards ? count - 1 : 0;
  return backwards ? (current - 1 + count) % count : (current + 1) % count;
}
