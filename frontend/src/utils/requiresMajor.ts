import { ALL_TEAM_MAJOR_CODES } from '../constants/majors.ts';

export const MAJOR_COMPLETION_PATH = '/profile';

export function requiresMajor(user: { roles: string[]; major?: string | null } | null | undefined): boolean {
  return Boolean(user?.roles.some(role => role.trim().toUpperCase() === 'STUDENT')
    && !ALL_TEAM_MAJOR_CODES.includes(user.major?.trim().toUpperCase() || ''));
}

export function getMajorCompletionRedirect(
  user: { roles: string[]; major?: string | null } | null | undefined,
  pathname: string,
): string | null {
  return requiresMajor(user) && pathname !== MAJOR_COMPLETION_PATH
    ? MAJOR_COMPLETION_PATH
    : null;
}
