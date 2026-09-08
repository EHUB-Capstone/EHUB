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
}

export const hasUnsavedProjectDirectionChanges = (
  direction: ProjectDirectionSyncValue | null | undefined,
  title: string,
  summary: string,
): boolean => !direction
  || title.trim() !== (direction.title || '').trim()
  || summary.trim() !== (direction.summary || '').trim();

export const canSubmitProjectDirection = (
  direction: ProjectDirectionSyncValue | null | undefined,
  title: string,
  summary: string,
): boolean => direction?.status === 'Draft'
  && !hasUnsavedProjectDirectionChanges(direction, title, summary);

export const getProjectDirectionSubmitGuidance = (
  direction: ProjectDirectionSyncValue | null | undefined,
  title: string,
  summary: string,
): string => {
  const hasUnsavedChanges = hasUnsavedProjectDirectionChanges(direction, title, summary);
  if (direction?.status === 'NeedsRevision') {
    return hasUnsavedChanges
      ? 'Save your revised project direction as a draft to enable Submit.'
      : 'The lecturer requested changes. Update the title or summary, then select Save draft to enable Submit.';
  }
  if (direction?.status === 'Draft' && hasUnsavedChanges) {
    return 'Save your changes as a draft before submitting.';
  }
  return '';
};

interface ProjectDirectionOverviewTeam {
  _id: string;
  projectDirectionStatus?: string | null;
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

export const getProjectDirectionDecisionNotice = (
  current?: ProjectDirectionSyncValue | null,
  incoming?: ProjectDirectionSyncValue | null,
): string => {
  if (current?.status !== 'Submitted') return '';
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
    projectDirectionStatus,
    projectDirectionReviewComment: direction.reviews?.[0]?.comment || null,
    projectDirectionRowVersion: direction.rowVersion || '',
  };
});
