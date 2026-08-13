import type { AxiosRequestConfig } from 'axios';
import type { MockApiState, MockClass, MockRosterStudent, MockTeamMember } from './mockState.ts';
import { createInitialMockState, nextMockId, nextRowVersion } from './mockState.ts';

export type MockReply = [number, unknown, Record<string, string>?];
export type JsonBody = Record<string, unknown>;
export interface MockValidationError {
  field: string;
  message: string;
  code: string;
}

const STORAGE_KEY = 'ehub_mock_api_state_v3';

function loadState(): MockApiState {
  if (typeof window === 'undefined') return createInitialMockState();

  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    if (!stored) return createInitialMockState();
    const parsed = JSON.parse(stored) as MockApiState;
    return { ...parsed, authPasswords: parsed.authPasswords ?? {} };
  } catch {
    window.localStorage.removeItem(STORAGE_KEY);
    return createInitialMockState();
  }
}

let state = loadState();

export const getMockState = () => state;

export function persistMockState(): void {
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  }
}

export function resetMockState(): void {
  state = createInitialMockState();
  persistMockState();
}

export function parseBody(config: AxiosRequestConfig): JsonBody {
  if (!config.data) return {};
  if (typeof config.data === 'string') {
    try {
      const parsed = JSON.parse(config.data) as unknown;
      return parsed && typeof parsed === 'object' ? parsed as JsonBody : {};
    } catch {
      return {};
    }
  }
  return typeof config.data === 'object' && !(config.data instanceof FormData)
    ? config.data as JsonBody
    : {};
}

export function requestParams(config: AxiosRequestConfig): Record<string, unknown> {
  return config.params && typeof config.params === 'object'
    ? config.params as Record<string, unknown>
    : {};
}

export function ok<T>(data: T, message = 'Success'): MockReply {
  return [200, { success: true, message, code: null, data, errors: null }];
}

export function created<T>(data: T, message: string): MockReply {
  return [201, { success: true, message, code: null, data, errors: null }];
}

export function failure(
  status: number,
  code: string,
  message: string,
  errors: readonly MockValidationError[] | null = null,
): MockReply {
  return [status, {
    success: false,
    message,
    code,
    data: null,
    errors: errors ? [...errors] : null,
  }];
}

export function routeId(config: AxiosRequestConfig, pattern: RegExp, group = 1): string {
  return config.url?.match(pattern)?.[group] || '';
}

export function findClass(classId: string): MockClass | undefined {
  return state.classes.find((item) => item.id === classId);
}

export function classMutationGuard(classId: string, rowVersion?: unknown): MockReply | null {
  const cls = findClass(classId);
  if (!cls) return failure(404, 'CLASS_NOT_FOUND', 'The requested class was not found.');
  if (cls.status === 'Archived') return failure(409, 'CLASS_ARCHIVED', 'Archived classes are read-only.');
  if (typeof rowVersion === 'string' && rowVersion && rowVersion !== cls.rowVersion) {
    return failure(409, 'CLASS_CONCURRENCY_CONFLICT', 'The class was changed by another request. Refresh and try again.');
  }
  return null;
}

export function touchClass(cls: MockClass): void {
  cls.rowVersion = nextRowVersion(state);
  cls.status = cls.primaryLecturerId && cls.schedules.length > 0 ? 'Active' : 'Draft';
  cls.previousStatus = cls.status;
  refreshClassCounts(cls.id);
}

export function refreshClassCounts(classId: string): void {
  const cls = findClass(classId);
  if (!cls) return;
  cls.studentCount = (state.rosters[classId] || []).filter((student) => student.enrollmentStatus === 'Active').length;
  cls.teamCount = state.teams.filter((team) => team.classId === classId && team.status.toLowerCase() === 'active').length;
  const mentors = state.teams
    .filter((team) => team.classId === classId)
    .map((team) => team.currentMentorAssignment)
    .filter((assignment) => assignment?.status === 'Active')
    .map((assignment) => assignment!.mentor);
  cls.mentors = [...new Map(mentors.map((mentor) => [mentor.mentorProfileId, mentor])).values()];
}

export function memberFromStudent(student: MockRosterStudent, leaderId: string): MockTeamMember {
  return {
    studentId: student.studentId,
    rollNumber: student.rollNumber,
    fullName: student.fullName,
    email: student.email,
    majorCode: student.majorCode || '',
    roleInTeam: student.studentId === leaderId ? 'LEADER' : 'MEMBER',
    joinedAtUtc: new Date().toISOString(),
  };
}

export function allocateId(): string {
  return nextMockId(state);
}

export function allocateRowVersion(): string {
  return nextRowVersion(state);
}

export function asString(value: unknown, fallback = ''): string {
  return typeof value === 'string' ? value : fallback;
}

export function asNumber(value: unknown, fallback: number): number {
  const result = Number(value);
  return Number.isFinite(result) ? result : fallback;
}

export function asStringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.map(String).filter(Boolean) : [];
}
