export interface ProjectWorkspaceDraft {
  projectName: string;
  description: string;
  startupIndustryIds: string[];
}

export interface ProjectProfileDraft {
  projectName: string;
  description: string;
  problem: string;
  solution: string;
  targetUsers: string;
}

interface WorkspaceCreationTeamSource {
  teamName?: string | null;
  name?: string | null;
}

interface WorkspaceCreationProposalSource {
  teamName?: string | null;
  projectName?: string | null;
  projectDescription?: string | null;
  description?: string | null;
}

export const resolveWorkspaceCreationDefaults = (
  team: WorkspaceCreationTeamSource,
  proposal?: WorkspaceCreationProposalSource | null,
): { teamName: string; draft: ProjectWorkspaceDraft } => ({
  teamName: proposal?.teamName?.trim() || team.teamName?.trim() || team.name?.trim() || '',
  draft: {
    projectName: proposal?.projectName?.trim() || team.teamName?.trim() || team.name?.trim() || '',
    description: proposal?.projectDescription?.trim() || proposal?.description?.trim() || '',
    startupIndustryIds: [],
  },
});

export type ProjectWorkspaceErrors = Partial<Record<keyof ProjectWorkspaceDraft, string>>;

const tagPattern = /^[\p{L}\p{N}.+#][\p{L}\p{N} .+#&/_-]*$/u;

export const normalizeWorkspaceTag = (value: string): string =>
  value.trim().replace(/\s+/g, ' ').toUpperCase();

export const appendWorkspaceTag = (values: string[], rawValue: string): { values: string[]; error?: string } => {
  const value = rawValue.trim().replace(/\s+/g, ' ');
  if (!value) return { values };
  if (value.length > 50 || !tagPattern.test(value)) {
    return { values, error: 'Use 1–50 letters, numbers, spaces, or . + # & / _ -.' };
  }
  if (values.some((item) => normalizeWorkspaceTag(item) === normalizeWorkspaceTag(value))) {
    return { values, error: `“${value}” is already included.` };
  }
  if (values.length >= 10) return { values, error: 'Maximum 10 entries.' };
  return { values: [...values, value] };
};

export const validateProjectWorkspace = (draft: ProjectWorkspaceDraft): ProjectWorkspaceErrors => {
  const errors: ProjectWorkspaceErrors = {};
  const nameLength = draft.projectName.trim().length;
  const descriptionLength = draft.description.trim().length;
  if (nameLength < 3 || nameLength > 200) errors.projectName = 'Project name must be 3–200 characters.';
  if (descriptionLength < 20 || descriptionLength > 2_000) errors.description = 'Description must be 20–2000 characters.';
  if (draft.startupIndustryIds.length < 1 || draft.startupIndustryIds.length > 3) {
    errors.startupIndustryIds = 'Select between 1 and 3 startup industries.';
  }
  return errors;
};

export type ProjectProfileErrors = Partial<Record<keyof ProjectProfileDraft, string>>;

const projectProfileFields: Array<keyof ProjectProfileDraft> = [
  'projectName',
  'description',
  'problem',
  'solution',
  'targetUsers',
];

export const hasPersistedProjectProfile = (
  draft: ProjectProfileDraft,
  persisted: Partial<ProjectProfileDraft> | null | undefined,
): boolean => {
  if (!persisted) return false;
  return projectProfileFields.every((field) =>
    (persisted[field] ?? '').trim() === draft[field].trim());
};

export const validateProjectProfile = (draft: ProjectProfileDraft): ProjectProfileErrors => {
  const errors: ProjectProfileErrors = {};
  const lengths = {
    projectName: draft.projectName.trim().length,
    description: draft.description.trim().length,
    problem: draft.problem.trim().length,
    solution: draft.solution.trim().length,
    targetUsers: draft.targetUsers.trim().length,
  };
  if (lengths.projectName < 3 || lengths.projectName > 200) errors.projectName = 'Project name must be 3–200 characters.';
  if (lengths.description < 20 || lengths.description > 2_000) errors.description = 'Description must be 20–2000 characters.';
  if (lengths.problem < 20 || lengths.problem > 2_000) errors.problem = 'Problem must be 20–2000 characters.';
  if (lengths.solution < 20 || lengths.solution > 2_000) errors.solution = 'Solution must be 20–2000 characters.';
  if (lengths.targetUsers < 3 || lengths.targetUsers > 2_000) errors.targetUsers = 'Target users must be 3–2000 characters.';
  return errors;
};
