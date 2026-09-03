export interface ProjectWorkspaceDraft {
  projectName: string;
  description: string;
  startupField: string;
  technologyStack: string[];
  keywords: string[];
}

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
  const fieldLength = draft.startupField.trim().length;
  if (nameLength < 3 || nameLength > 200) errors.projectName = 'Project name must be 3–200 characters.';
  if (descriptionLength < 20 || descriptionLength > 2_000) errors.description = 'Description must be 20–2000 characters.';
  if (fieldLength < 2 || fieldLength > 100) errors.startupField = 'Startup field must be 2–100 characters.';
  if (draft.technologyStack.length === 0) errors.technologyStack = 'Add at least one technology.';
  return errors;
};
