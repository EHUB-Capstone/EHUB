import type { ApiEnvelope, WorkspaceOption } from '../types/workspaceTools';
import { matchesSearchQuery } from './searchText.ts';

export interface WorkspaceClassGroup {
  classId: string;
  classCode: string;
  courseCode: string;
  semester: string;
  workspaces: WorkspaceOption[];
}

export interface WorkspaceHubFilters {
  search?: string;
  subject?: string;
  semester?: string;
  year?: string;
  workspaceStatus?: string;
  access?: string;
}

export function resolveWorkspaceSemesterScope(
  params: URLSearchParams,
  activeSemester: { semester: string; year: number } | null,
  /** Used when no semester is active, so the page still opens on the most recent semester that has teams. */
  fallbackSemester: { semester: string; year: number } | null = null,
): { semester: string; year: string; isDefault: boolean; usesFallback: boolean } {
  const isDefault = !params.has('semester') && !params.has('year');
  const defaultSemester = activeSemester ?? fallbackSemester;
  return {
    semester: isDefault ? defaultSemester?.semester ?? 'none' : params.get('semester') || 'all',
    year: isDefault ? String(defaultSemester?.year ?? 'none') : params.get('year') || 'all',
    isDefault,
    usesFallback: isDefault && !activeSemester && fallbackSemester !== null,
  };
}

const TERM_ORDER: Record<string, number> = { SP: 0, SU: 1, FA: 2 };

/** The most recent semester among the workspaces: the latest year, then Fall over Summer over Spring. */
export function latestWorkspaceSemester(workspaces: readonly { semester: string }[]): { semester: string; year: number } | null {
  let best: { semester: string; year: number } | null = null;
  for (const workspace of workspaces) {
    const parsed = parseWorkspaceSemester(workspace.semester);
    if (!parsed) continue;
    const candidate = { semester: parsed.semester, year: Number(parsed.year) };
    if (!best || candidate.year > best.year || (candidate.year === best.year && TERM_ORDER[candidate.semester] > TERM_ORDER[best.semester])) {
      best = candidate;
    }
  }
  return best;
}

const naturalCollator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });

export function parseWorkspaceSemester(code: string): { semester: string; year: string } | null {
  const match = /^(SP|SU|FA)(\d{2}|\d{4})$/i.exec(code.trim().replace(/[\s_-]/g, ''));
  if (!match) return null;
  return {
    semester: match[1].toUpperCase(),
    year: match[2].length === 2 ? String(2000 + Number(match[2])) : match[2],
  };
}

export function filterWorkspaces(workspaces: WorkspaceOption[], filters: WorkspaceHubFilters): WorkspaceOption[] {
  return workspaces.filter(workspace => {
    const semester = parseWorkspaceSemester(workspace.semester);
    return (
      matchesSearchQuery(filters.search || '', [workspace.teamName, workspace.classCode, workspace.courseCode, workspace.semester]) &&
      (!filters.subject || workspace.courseCode.toUpperCase() === filters.subject.toUpperCase()) &&
      (!filters.semester || filters.semester === 'all' || semester?.semester === filters.semester.toUpperCase()) &&
      (!filters.year || filters.year === 'all' || semester?.year === filters.year) &&
      (filters.workspaceStatus !== 'created' || workspace.hasWorkspace) &&
      (filters.workspaceStatus !== 'not-created' || !workspace.hasWorkspace) &&
      (!filters.access || workspace.accessMode === filters.access)
    );
  });
}

export function normalizeAccessibleWorkspaces(
  response: ApiEnvelope<WorkspaceOption[]>,
): WorkspaceOption[] {
  return response.success && Array.isArray(response.data) ? response.data : [];
}

export function groupWorkspacesByClass(workspaces: WorkspaceOption[], sort = 'class-asc'): WorkspaceClassGroup[] {
  const sorted = [...workspaces].sort((left, right) => {
    const classCompare = naturalCollator.compare(left.classCode, right.classCode);
    if (classCompare !== 0) return classCompare;

    const semesterCompare = naturalCollator.compare(left.semester, right.semester);
    if (semesterCompare !== 0) return semesterCompare;

    return naturalCollator.compare(left.teamName, right.teamName);
  });

  const groups = sorted.reduce<WorkspaceClassGroup[]>((groups, workspace) => {
    const existing = groups.find(group => group.classId === workspace.classId);
    if (existing) {
      existing.workspaces.push(workspace);
      return groups;
    }

    groups.push({
      classId: workspace.classId,
      classCode: workspace.classCode,
      courseCode: workspace.courseCode,
      semester: workspace.semester,
      workspaces: [workspace],
    });
    return groups;
  }, []);

  if (sort === 'class-desc') groups.reverse();
  if (sort === 'team-desc') groups.forEach(group => group.workspaces.reverse());
  return groups;
}
