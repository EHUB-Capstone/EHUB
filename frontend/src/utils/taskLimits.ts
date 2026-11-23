import { parseApiError } from './apiError';
import i18n from '../i18n/index.ts';

// Mirrors SaveWeeklyTaskRequestValidator in EHub.Application (Workspaces/WorkspaceToolValidators.cs).
export const TASK_TITLE_MAX_LENGTH = 300;
export const TASK_DESCRIPTION_MAX_LENGTH = 4_000;

export function getTaskTextError(title: string, description: string): string | null {
  if (title.trim().length > TASK_TITLE_MAX_LENGTH) {
    return i18n.t('utils:taskLimits.titleMustBeCharactersOrFewer', { TASK_TITLE_MAX_LENGTH });
  }
  if (description.trim().length > TASK_DESCRIPTION_MAX_LENGTH) {
    return i18n.t('utils:taskLimits.descriptionMustBeCharactersOrFewer', { TASK_DESCRIPTION_MAX_LENGTH });
  }
  return null;
}

// Axios rejects with its generic "Request failed with status code 400"; prefer the server's own message.
export function getTaskApiErrorMessage(error: unknown, fallback: string): string {
  const parsed = parseApiError(error, '');
  const firstFieldError = Object.values(parsed.fieldErrors)[0];
  return firstFieldError || parsed.message || (error as { message?: string } | null)?.message || fallback;
}
