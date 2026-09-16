import { ALL_TEAM_MAJOR_CODES } from '../constants/majors.ts';

export function requiresMajor(user: { roles: string[]; major?: string | null } | null | undefined): boolean {
  return Boolean(user?.roles.some(role => role.trim().toUpperCase() === 'STUDENT')
    && !ALL_TEAM_MAJOR_CODES.includes(user.major?.trim().toUpperCase() || ''));
}
