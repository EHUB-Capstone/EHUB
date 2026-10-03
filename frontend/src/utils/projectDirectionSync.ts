export interface ProjectDirectionReviewLike {
  id?: string | null;
  toStatus?: string | null;
  comment?: string | null;
}

export interface ProjectDirectionSyncValue {
  id?: string | null;
  status?: string | null;
  rowVersion?: string | null;
  reviewedAtUtc?: string | null;
  reviews?: ProjectDirectionReviewLike[] | null;
  title?: string | null;
  summary?: string | null;
  isProjectProfileChangeProposal?: boolean;
  currentTitle?: string | null;
  currentSummary?: string | null;
  startupIndustries?: string[] | null;
}

export const isProjectDirectionConcurrencyConflict = (code?: string | null): boolean => (
  code === 'CLASS_CONCURRENCY_CONFLICT' || code === 'PROJECT_DIRECTION_CONCURRENCY_CONFLICT'
);

export const resolveDirectionOverviewClassId = (
  classIds: string[],
  requestedClassId: string,
  currentClassId: string,
): string => {
  if (requestedClassId && classIds.includes(requestedClassId)) return requestedClassId;
  if (currentClassId && classIds.includes(currentClassId)) return currentClassId;
  return '';
};

export const directionOverviewTargetClassIds = (
  classIds: string[],
  selectedClassId: string,
): string[] => selectedClassId ? classIds.filter(classId => classId === selectedClassId) : classIds;

export const hasUnsavedProjectDirectionChanges = (
  direction: ProjectDirectionSyncValue | null | undefined,
  title: string,
  summary: string,
  startupIndustries?: string[],
): boolean => !direction
  || title.trim() !== (direction.title || '').trim()
  || summary.trim() !== (direction.summary || '').trim()
  || (startupIndustries !== undefined && !haveSameValues(startupIndustries, direction.startupIndustries || []));

export const canSubmitProjectDirection = (
  direction: ProjectDirectionSyncValue | null | undefined,
  title: string,
  summary: string,
  startupIndustries?: string[],
): boolean => direction?.status === 'Draft'
  && !hasUnsavedProjectDirectionChanges(direction, title, summary, startupIndustries);

export const getProjectDirectionSubmitGuidance = (
  direction: ProjectDirectionSyncValue | null | undefined,
  title: string,
  summary: string,
  startupIndustries?: string[],
): string => {
  const hasUnsavedChanges = hasUnsavedProjectDirectionChanges(direction, title, summary, startupIndustries);
  if (direction?.status === 'NeedsRevision') {
    return hasUnsavedChanges
      ? 'Save your revised project information as a draft to enable Submit.'
      : 'The lecturer requested changes. Update the Project Name, Project description, or Startup Industry, then select Save draft to enable Submit.';
  }
  if (direction?.status === 'Draft' && hasUnsavedChanges) {
    return 'Save your changes as a draft before submitting.';
  }
  return '';
};

const haveSameValues = (left: string[], right: string[]): boolean => {
  const normalizedLeft = [...new Set(left.map((value) => value.trim().toLocaleUpperCase()).filter(Boolean))].sort();
  const normalizedRight = [...new Set(right.map((value) => value.trim().toLocaleUpperCase()).filter(Boolean))].sort();
  return normalizedLeft.length === normalizedRight.length
    && normalizedLeft.every((value, index) => value === normalizedRight[index]);
};

interface ProjectDirectionOverviewTeam {
  _id: string;
  projectDirectionStatus?: string | null;
  projectDirectionIsProfileChangeProposal?: boolean;
  projectDirectionCurrentTitle?: string | null;
  projectDirectionCurrentSummary?: string | null;
  [key: string]: unknown;
}

const revisionKey = (direction?: ProjectDirectionSyncValue | null): string => [
  direction?.id || '',
  direction?.status || '',
  direction?.rowVersion || '',
  direction?.reviewedAtUtc || '',
  direction?.reviews?.[0]?.id || '',
  direction?.reviews?.[0]?.toStatus || '',
].join('|');

export const hasProjectDirectionChanged = (
  current?: ProjectDirectionSyncValue | null,
  incoming?: ProjectDirectionSyncValue | null,
): boolean => Boolean(incoming) && revisionKey(current) !== revisionKey(incoming);

export const isProjectProfileAvailable = (
  direction?: ProjectDirectionSyncValue | null,
): boolean => direction?.status === 'Approved';

interface ProjectProfileIdentityLike {
  projectName?: string | null;
  description?: string | null;
}

export const getApprovedProjectProfileDisplay = (
  direction?: ProjectDirectionSyncValue | null,
  project?: ProjectProfileIdentityLike | null,
): { projectName: string; description: string } => ({
  projectName: direction?.status === 'Approved' && direction.title?.trim()
    ? direction.title
    : project?.projectName || '',
  description: direction?.status === 'Approved' && direction.summary?.trim()
    ? direction.summary
    : project?.description || '',
});

export const getProjectDirectionDecisionNotice = (
  current?: ProjectDirectionSyncValue | null,
  incoming?: ProjectDirectionSyncValue | null,
): string => {
  if (current?.status !== 'Submitted') return '';
  if (current.isProjectProfileChangeProposal && incoming?.reviews?.[0]?.toStatus === 'Rejected') {
    return 'Lecturer rejected your Project Profile changes. The approved profile remains unchanged.';
  }
  if (current.isProjectProfileChangeProposal && incoming?.status === 'Approved') {
    return 'Lecturer approved your Project Profile changes. The approved profile is now updated.';
  }
  if (current.isProjectProfileChangeProposal && incoming?.status === 'NeedsRevision') {
    return 'Lecturer requested revisions to your Project Profile changes. The approved profile remains unchanged.';
  }
  if (incoming?.status === 'Approved') return 'Lecturer approved your project direction.';
  if (incoming?.status === 'NeedsRevision') return 'Lecturer reviewed your project direction and requested changes.';
  return '';
};

export const updateProjectDirectionOverviewTeams = <T extends ProjectDirectionOverviewTeam>(
  teams: T[],
  teamId: string,
  direction: ProjectDirectionSyncValue,
): T[] => teams.map((team) => {
  if (team._id !== teamId) return team;
  const projectDirectionStatus = direction.status === 'Submitted' ? 'PENDING'
    : direction.status === 'Approved' ? 'APPROVED'
      : direction.status === 'NeedsRevision' ? 'CHANGES_REQUESTED'
        : 'NOT_SUBMITTED';

  return {
    ...team,
    projectDirection: direction.summary || '',
    projectDirectionTitle: direction.title || '',
    projectDirectionIsProfileChangeProposal: Boolean(direction.isProjectProfileChangeProposal),
    projectDirectionCurrentTitle: direction.currentTitle || '',
    projectDirectionCurrentSummary: direction.currentSummary || '',
    projectDirectionStartupIndustries: direction.startupIndustries || [],
    projectDirectionStatus,
    projectDirectionReviewComment: direction.reviews?.[0]?.comment || null,
    projectDirectionRowVersion: direction.rowVersion || '',
  };
});
