import type { ApiEnvelope, WorkspaceOption } from '../types/workspaceTools';

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
): { semester: string; year: string; isDefault: boolean } {
  const isDefault = !params.has('semester') && !params.has('year');
  return {
    semester: isDefault ? activeSemester?.semester ?? 'none' : params.get('semester') || 'all',
    year: isDefault ? String(activeSemester?.year ?? 'none') : params.get('year') || 'all',
    isDefault,
  };
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
  const search = filters.search?.trim().toLowerCase() || '';
  return workspaces.filter(workspace => {
    const semester = parseWorkspaceSemester(workspace.semester);
    return (
      (!search || [workspace.teamName, workspace.classCode, workspace.courseCode, workspace.semester]
        .some(value => value.toLowerCase().includes(search))) &&
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
