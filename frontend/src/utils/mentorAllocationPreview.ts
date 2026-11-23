import type {
  MentorAllocationEdit,
  MentorAllocationMentorLoad,
  MentorAllocationPreview,
  MentorAllocationRowPreview,
  MentorAllocationSkipReason,
  MentorFitCriterionKey,
  MentorType,
} from '../types/mentorAdmin';
import i18n from '../i18n/index.ts';

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
  fitScore?: number | null;
  reasons?: string[];
  history?: MentorAllocationRowPreview['history'];
  fitDetails?: MentorAllocationRowPreview['fitDetails'];
  alternatives?: MentorAllocationRowPreview['alternatives'];
  needsReview?: boolean;
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

/** Keep provider/model identifiers out of product copy; numeric scoring and source reasons stay unchanged. */
export function formatAllocationReason(reason: string): string {
  const labels: Record<string, string> = {
    experience: 'Kinh nghiệm', expertise: 'Chuyên môn', startupdomain: 'Lĩnh vực',
    technology: 'Công nghệ', bio: 'Giới thiệu/học vấn',
  };
  return reason
    .replace(/\bGemini\s*\([^)]*\)/gi, 'AI')
    .replace(/\bgemini(?:-[a-z0-9.]+)*\b/gi, 'AI')
    .replace(/^AI (experience|expertise|startupDomain|technology|bio):/i,
      (_, field: string) => `${labels[field.toLowerCase()]}:`);
}

export function formatAllocationPreviewReasons(reasons: readonly string[] = []): string[] {
  return reasons.filter(reason => !/^(?:Gemini|AI) technology:/i.test(reason)).map(formatAllocationReason);
}

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

const isAiSuggestionNeedingReview = (preview: MentorAllocationPreview, row: MentorAllocationRowPreview) =>
  preview.strategy === 'Ai' && (row.source ?? 'Allocated') === 'Allocated' &&
  (row.fitScore === 0 || row.fitDetails?.hasEvidence === false);

/** Filled AI suggestions without matching evidence still need an administrator's review. */
export function getAiReviewAssignments(preview: MentorAllocationPreview): MentorAllocationRowPreview[] {
  return preview.assignments.filter(row => isAiSuggestionNeedingReview(preview, row));
}

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
      fitScore: row.fitScore,
      reasons: row.reasons,
      history: row.history,
      fitDetails: row.fitDetails,
      alternatives: row.alternatives,
      needsReview: isAiSuggestionNeedingReview(preview, row),
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

export interface MentorAllocationLoadImpact {
  selected: { mentorName: string; before: number; after: number };
  previous: { mentorName: string; before: number; after: number } | null;
}

export interface MentorAllocationQualitySummary {
  assessed: number;
  supported: number;
  needsReview: number;
  missingProfileData: number;
  unknownProfileData: number;
  averageFit: number | null;
}

export const visibleFitCriteria = ['experience', 'expertise', 'startupDomain', 'bio'] as const satisfies readonly MentorFitCriterionKey[];

export function getMissingProfileFields(details: SlotView['fitDetails']): typeof visibleFitCriteria[number][] | null {
  return details ? visibleFitCriteria.filter(key => !details.availableProfileFields.includes(key)) : null;
}

/** Describes evidence/data coverage of new AI suggestions, never an accuracy estimate. */
export function summarizeAllocationQuality(preview: MentorAllocationPreview): MentorAllocationQualitySummary | null {
  if (preview.strategy !== 'Ai') return null;
  const assessed = preview.assignments.filter(row => (row.source ?? 'Allocated') === 'Allocated' &&
    row.fitScore != null && Number.isFinite(row.fitScore) && row.fitScore >= 0 && row.fitScore <= 100);
  return {
    assessed: assessed.length,
    supported: assessed.filter(row => row.fitDetails?.hasEvidence ?? (row.fitScore! > 0)).length,
    needsReview: assessed.filter(row => row.fitScore === 0 || row.fitDetails?.hasEvidence === false).length,
    missingProfileData: assessed.filter(row => (getMissingProfileFields(row.fitDetails)?.length ?? 0) > 0).length,
    unknownProfileData: assessed.filter(row => !row.fitDetails).length,
    averageFit: assessed.length ? assessed.reduce((total, row) => total + row.fitScore!, 0) / assessed.length : null,
  };
}

export function getAvailableFitAlternatives(slot: Pick<SlotView, 'mentorProfileId' | 'alternatives'>,
  candidates: readonly MentorAllocationMentorLoad[]): NonNullable<SlotView['alternatives']> {
  const eligible = new Set(candidates.map(candidate => candidate.mentorProfileId));
  const seen = new Set<string>();
  return (slot.alternatives ?? []).filter(candidate => {
    if (candidate.mentorProfileId === slot.mentorProfileId || !eligible.has(candidate.mentorProfileId) ||
      !candidate.hasEvidence || !Number.isFinite(candidate.fitScore) || candidate.fitScore < 0 || candidate.fitScore > 100 ||
      seen.has(candidate.mentorProfileId)) return false;
    seen.add(candidate.mentorProfileId);
    return true;
  }).sort((left, right) => right.fitScore - left.fitScore || left.mentorName.localeCompare(right.mentorName)).slice(0, 3);
}

/** Forecasts this one edit against the current preview; the next server preview remains authoritative. */
export function getMentorLoadImpact(
  preview: Pick<MentorAllocationPreview, 'mentorLoads'>,
  slot: Pick<SlotView, 'mentorProfileId'>,
  selectedMentorId: string,
): MentorAllocationLoadImpact | null {
  const selected = preview.mentorLoads?.find(mentor => mentor.mentorProfileId === selectedMentorId);
  if (!selected || !Number.isFinite(selected.totalAfter) || selected.totalAfter < 0) return null;
  const sameMentor = selectedMentorId === slot.mentorProfileId;
  const previous = sameMentor ? undefined : preview.mentorLoads?.find(mentor => mentor.mentorProfileId === slot.mentorProfileId);
  return {
    selected: { mentorName: selected.mentorName, before: selected.totalAfter, after: selected.totalAfter + (sameMentor ? 0 : 1) },
    previous: previous && Number.isFinite(previous.totalAfter) && previous.totalAfter >= 0
      ? { mentorName: previous.mentorName, before: previous.totalAfter, after: Math.max(0, previous.totalAfter - 1) }
      : null,
  };
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
  get MentorNotActiveInSemester() { return i18n.t('utils:mentorAllocationPreview.notActiveThisSemesterNeedsA'); },
  get MentorUnavailable() { return i18n.t('utils:mentorProfileForm.unavailable'); },
  get SlotAlreadyFilled() { return i18n.t('utils:mentorAllocationPreview.slotAlreadyFilled'); },
  get NoContinuedTeam() { return i18n.t('utils:mentorAllocationPreview.teamDoesNotContinue'); },
};

export type TeamGridFilter = 'all' | 'attention' | 'changed';
export const ALL_CLASSES = 'ALL';

/** The classes that appear in the preview with how many of their teams are listed, for the class filter. */
export function teamClassOptions(grid: readonly TeamGridRow[]): { classCode: string; teamCount: number }[] {
  const counts = new Map<string, number>();
  for (const row of grid) counts.set(row.classCode, (counts.get(row.classCode) ?? 0) + 1);
  return [...counts.entries()]
    .map(([classCode, teamCount]) => ({ classCode, teamCount }))
    .sort((left, right) => left.classCode.localeCompare(right.classCode, undefined, { numeric: true }));
}

/** A class that is no longer in the preview (for example after regenerating it) falls back to every class. */
export function resolveClassFilter(classCode: string, options: readonly { classCode: string }[]): string {
  return classCode !== ALL_CLASSES && options.some(option => option.classCode === classCode) ? classCode : ALL_CLASSES;
}

export function filterTeamGrid(
  grid: readonly TeamGridRow[],
  filters: { filter: TeamGridFilter; search: string; classCode: string },
): TeamGridRow[] {
  const term = filters.search.trim().toLowerCase();
  return grid.filter(row => {
    if (filters.classCode !== ALL_CLASSES && row.classCode !== filters.classCode) return false;
    const needsAttention = [row.enterprise, row.academic].some(slot => slot.state === 'unfilled' || slot.needsReview);
    const changed = [row.enterprise, row.academic].some(slot => slot.state !== 'existing');
    if (filters.filter === 'attention' && !needsAttention) return false;
    if (filters.filter === 'changed' && !changed) return false;
    if (!term) return true;
    return [row.teamName, row.classCode, row.enterprise.mentorName, row.academic.mentorName]
      .some(value => value?.toLowerCase().includes(term));
  });
}
