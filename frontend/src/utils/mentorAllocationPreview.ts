import type {
  MentorAllocationEdit,
  MentorAllocationMentorLoad,
  MentorAllocationPreview,
  MentorAllocationRowPreview,
  MentorAllocationSkipReason,
  MentorType,
} from '../types/mentorAdmin';

export type SlotState = 'existing' | 'retained' | 'allocated' | 'manual' | 'unfilled';

export interface SlotView {
  state: SlotState;
  mentorProfileId: string | null;
  mentorName: string | null;
  /** The assignment currently in the slot (kept as existing, or the one a manual edit replaces). */
  currentAssignmentId: string | null;
  currentMentorProfileId: string | null;
  currentMentorName: string | null;
  /** True when a manual row in this slot ends the current assignment. */
  replacesCurrent: boolean;
}

export interface TeamGridRow {
  teamId: string;
  teamCode: string;
  teamName: string;
  classCode: string;
  subjectCode: string;
  enterprise: SlotView;
  academic: SlotView;
}

export interface AllocationSummary {
  teams: number;
  retained: number;
  allocated: number;
  manual: number;
  replacements: number;
  unfilledEnterprise: number;
  unfilledAcademic: number;
}

export const MIN_REPLACE_REASON_LENGTH = 3;
export const MAX_REPLACE_REASON_LENGTH = 1000;

const emptySlot = (): SlotView => ({
  state: 'unfilled',
  mentorProfileId: null,
  mentorName: null,
  currentAssignmentId: null,
  currentMentorProfileId: null,
  currentMentorName: null,
  replacesCurrent: false,
});

const stateOfRow = (row: MentorAllocationRowPreview): SlotState => {
  if (row.source === 'Retained') return 'retained';
  if (row.source === 'Manual') return 'manual';
  return 'allocated';
};

const compareNatural = (left: string, right: string) =>
  left.localeCompare(right, undefined, { numeric: true, sensitivity: 'base' });

/**
 * One row per team with the final state of its two mentor slots: a mentor that is already there, one kept from the
 * previous semester, one newly allocated, one picked by hand, or a slot that is still empty.
 */
export function buildTeamGrid(preview: MentorAllocationPreview): TeamGridRow[] {
  const teams = new Map<string, TeamGridRow>();
  const ensure = (teamId: string, teamCode: string, teamName: string, classCode: string, subjectCode: string) => {
    const existing = teams.get(teamId);
    if (existing) {
      if (!existing.subjectCode && subjectCode) existing.subjectCode = subjectCode;
      return existing;
    }
    const created: TeamGridRow = { teamId, teamCode, teamName, classCode, subjectCode, enterprise: emptySlot(), academic: emptySlot() };
    teams.set(teamId, created);
    return created;
  };
  const slotOf = (team: TeamGridRow, type: MentorType) => (type === 'Enterprise' ? team.enterprise : team.academic);
  const assign = (team: TeamGridRow, type: MentorType, slot: SlotView) => {
    if (type === 'Enterprise') team.enterprise = slot;
    else team.academic = slot;
  };

  for (const item of preview.existingAssignments ?? []) {
    const team = ensure(item.teamId, item.teamCode, item.teamName, item.classCode, item.subjectCode);
    assign(team, item.mentorType, {
      ...slotOf(team, item.mentorType),
      state: 'existing',
      mentorProfileId: item.mentorProfileId,
      mentorName: item.mentorName,
      currentAssignmentId: item.assignmentId,
      currentMentorProfileId: item.mentorProfileId,
      currentMentorName: item.mentorName,
    });
  }

  for (const row of preview.assignments) {
    const team = ensure(row.teamId, row.teamCode, row.teamName, row.classCode, '');
    const current = slotOf(team, row.mentorType);
    assign(team, row.mentorType, {
      state: stateOfRow(row),
      mentorProfileId: row.mentorProfileId,
      mentorName: row.mentorName,
      currentAssignmentId: row.replacesAssignmentId ?? null,
      currentMentorProfileId: row.replacesMentorProfileId ?? current.currentMentorProfileId,
      currentMentorName: row.replacesMentorName ?? current.currentMentorName,
      replacesCurrent: Boolean(row.replacesAssignmentId),
    });
  }

  for (const item of preview.unfilled ?? []) {
    const team = ensure(item.teamId, item.teamCode, item.teamName, item.classCode, item.subjectCode);
    // An unfilled slot never overrides a mentor already known for that slot.
    if (slotOf(team, item.mentorType).state === 'unfilled') assign(team, item.mentorType, emptySlot());
  }

  return [...teams.values()].sort((left, right) =>
    compareNatural(left.classCode, right.classCode) || compareNatural(left.teamCode, right.teamCode));
}

export function summarizePreview(preview: MentorAllocationPreview): AllocationSummary {
  const sources = preview.assignments.map(row => row.source ?? 'Allocated');
  return {
    teams: preview.teamCount,
    retained: sources.filter(source => source === 'Retained').length,
    allocated: sources.filter(source => source === 'Allocated').length,
    manual: sources.filter(source => source === 'Manual').length,
    replacements: preview.replacementCount ?? 0,
    unfilledEnterprise: preview.unfilledEnterpriseCount ?? 0,
    unfilledAcademic: preview.unfilledAcademicCount ?? 0,
  };
}

/** Active mentors of the semester that can fill a slot of the given type, lightest load first. */
export function getMentorCandidates(preview: MentorAllocationPreview, type: MentorType): MentorAllocationMentorLoad[] {
  return (preview.mentorLoads ?? [])
    .filter(mentor => mentor.mentorType === type)
    .sort((left, right) => left.totalAfter - right.totalAfter || left.mentorName.localeCompare(right.mentorName));
}

/** Subject codes present in the mentor loads, in a stable order, so the mentor table can show one column per subject. */
export function getSubjectColumns(preview: MentorAllocationPreview): string[] {
  const codes = new Set<string>();
  for (const mentor of preview.mentorLoads ?? []) for (const subject of mentor.subjects) codes.add(subject.subjectCode);
  return [...codes].sort(compareNatural);
}

export function isReplaceReasonValid(reason: string | undefined | null): boolean {
  const length = (reason ?? '').trim().length;
  return length >= MIN_REPLACE_REASON_LENGTH && length <= MAX_REPLACE_REASON_LENGTH;
}

const sameSlot = (left: Pick<MentorAllocationEdit, 'teamId' | 'mentorType'>, right: Pick<MentorAllocationEdit, 'teamId' | 'mentorType'>) =>
  left.teamId === right.teamId && left.mentorType === right.mentorType;

/** Adds an edit, replacing any earlier edit of the same team slot. */
export function upsertEdit(edits: MentorAllocationEdit[], edit: MentorAllocationEdit): MentorAllocationEdit[] {
  return [...edits.filter(item => !sameSlot(item, edit)), edit];
}

export function removeEdit(edits: MentorAllocationEdit[], teamId: string, mentorType: MentorType): MentorAllocationEdit[] {
  return edits.filter(item => !sameSlot(item, { teamId, mentorType }));
}

export function findEdit(edits: MentorAllocationEdit[], teamId: string, mentorType: MentorType): MentorAllocationEdit | undefined {
  return edits.find(item => sameSlot(item, { teamId, mentorType }));
}

/**
 * Drops edits the server did not apply (rejected or still waiting for the admin to choose "replace"), so a failed
 * edit is reported once instead of being re-sent with every later preview.
 */
export function keepAppliedEdits(edits: MentorAllocationEdit[], preview: MentorAllocationPreview): MentorAllocationEdit[] {
  const grid = new Map(buildTeamGrid(preview).map(row => [row.teamId, row]));
  return edits.filter(edit => {
    const row = grid.get(edit.teamId);
    if (!row) return false;
    const slot = edit.mentorType === 'Enterprise' ? row.enterprise : row.academic;
    if (edit.mentorProfileId === null) return slot.state === 'unfilled';
    return slot.state === 'manual' && slot.mentorProfileId === edit.mentorProfileId;
  });
}

export const skipReasonLabel: Record<MentorAllocationSkipReason, string> = {
  MentorNotActiveInSemester: 'Not active this semester',
  MentorUnavailable: 'Unavailable',
  SlotAlreadyFilled: 'Slot already filled',
  NoContinuedTeam: 'Team does not continue',
};
