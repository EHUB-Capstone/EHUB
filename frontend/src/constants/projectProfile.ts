/**
 * Field limits of the Project Profile. The Project Showcase page reads these same fields, so there is one set of
 * names and limits. Keep in sync with the backend validators (ProjectWorkspaceHandler, ProjectDirectionHandler,
 * class student import) and the `projects` table.
 */
export const PROJECT_NAME_MIN_LENGTH = 3;
export const PROJECT_NAME_MAX_LENGTH = 100;
export const PROJECT_DESCRIPTION_MIN_LENGTH = 20;
export const PROJECT_DESCRIPTION_MAX_LENGTH = 300;

/** One name per field, used by the Project Profile page and by the Project Showcase page that reads it. */
export const PROJECT_PROFILE_LABELS = {
  projectName: 'Project name',
  description: 'Description',
  problem: 'Problem',
  solution: 'Solution',
  targetUsers: 'Target users',
} as const;
