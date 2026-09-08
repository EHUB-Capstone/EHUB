import type { ApiEnvelope, WorkspaceOption } from '../types/workspaceTools';

export interface WorkspaceClassGroup {
  classId: string;
  classCode: string;
  courseCode: string;
  semester: string;
  workspaces: WorkspaceOption[];
}

const naturalCollator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });

export function normalizeAccessibleWorkspaces(
  response: ApiEnvelope<WorkspaceOption[]>,
): WorkspaceOption[] {
  return response.success && Array.isArray(response.data) ? response.data : [];
}

export function groupWorkspacesByClass(workspaces: WorkspaceOption[]): WorkspaceClassGroup[] {
  const sorted = [...workspaces].sort((left, right) => {
    const classCompare = naturalCollator.compare(left.classCode, right.classCode);
    if (classCompare !== 0) return classCompare;

    const semesterCompare = naturalCollator.compare(left.semester, right.semester);
    if (semesterCompare !== 0) return semesterCompare;

    return naturalCollator.compare(left.teamName, right.teamName);
  });

  return sorted.reduce<WorkspaceClassGroup[]>((groups, workspace) => {
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
}
