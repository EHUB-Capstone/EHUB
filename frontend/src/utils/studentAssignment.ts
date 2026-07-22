import type {
  AssignableStudent,
  StudentAssignmentDraft,
  StudentAssignmentResult,
  StudentAssignmentValidation,
  StudentDirectoryRecord,
} from '../types/studentAssignment';
import type { ManagedTeam, TeamMember } from '../types/teamManagement';
import {
  buildStudentTeamAssignments,
  entityId,
  getTeamMemberIds,
  TEAM_MEMBER_LIMIT,
} from './teamManagement.ts';

export function studentBelongsToClass(student: AssignableStudent, classId: string): boolean {
  return Boolean(classId) && entityId(student.classId) === classId;
}

export function normalizeClassStudents(
  students: AssignableStudent[],
  classId: string,
): AssignableStudent[] {
  return students.map((student) => ({
    ...student,
    classId: entityId(student.classId) || classId,
    source: student.source || (student._id.startsWith('frontend-import-') ? 'IMPORTED' : 'CLASS_ROSTER'),
  }));
}

function directoryStudentId(record: StudentDirectoryRecord): string {
  const profile = record.studentProfileId || record.student;
  if (typeof profile === 'string') return profile;
  if (profile && typeof profile === 'object' && profile._id) return String(profile._id);
  return String(record._id || record.id || '');
}

export function directoryRecordToStudent(record: StudentDirectoryRecord): AssignableStudent | null {
  const id = directoryStudentId(record);
  const fullName = String(record.fullName || record.name || '').trim();
  if (!id || !fullName) return null;

  return {
    _id: id,
    userId: record._id || record.id || null,
    fullName,
    rollNumber: record.rollNumber || record.studentId || null,
    email: record.email || null,
    major: record.major || record.majorCode || null,
    classId: record.classId,
    teamId: record.teamId,
    source: 'USER_DIRECTORY',
  };
}

function studentIdentityKeys(student: AssignableStudent): string[] {
  return [student._id, student.email, student.rollNumber]
    .map((value) => value?.trim().toLowerCase())
    .filter((value): value is string => Boolean(value));
}

export function mergeAssignmentCandidates(
  classStudents: AssignableStudent[],
  directoryStudents: AssignableStudent[],
): AssignableStudent[] {
  const merged: AssignableStudent[] = [];
  const keyToIndex = new Map<string, number>();

  [...classStudents, ...directoryStudents].forEach((student) => {
    const keys = studentIdentityKeys(student);
    const existingIndex = keys
      .map((key) => keyToIndex.get(key))
      .find((index): index is number => index !== undefined);

    if (existingIndex !== undefined) {
      const existing = merged[existingIndex];
      const preferIncoming = existing.source === 'USER_DIRECTORY' && student.source !== 'USER_DIRECTORY';
      merged[existingIndex] = preferIncoming
        ? { ...existing, ...student }
        : { ...student, ...existing };
      studentIdentityKeys(merged[existingIndex]).forEach((key) => keyToIndex.set(key, existingIndex));
      return;
    }

    const nextIndex = merged.length;
    merged.push(student);
    keys.forEach((key) => keyToIndex.set(key, nextIndex));
  });

  return merged;
}

export function validateStudentAssignment(
  draft: StudentAssignmentDraft,
  students: AssignableStudent[],
  teams: ManagedTeam[],
): StudentAssignmentValidation {
  const errors: StudentAssignmentValidation['errors'] = {};
  const studentsOutsideClass: string[] = [];
  const teamConflicts = new Map<string, string>();
  const studentIds = [...new Set(draft.studentIds)];
  const studentMap = new Map(students.map((student) => [student._id, student]));

  if (!draft.classId) errors.classId = 'Select a class before assigning students.';
  if (studentIds.length === 0) errors.studentIds = 'Select at least one student.';
  if (studentIds.some((studentId) => !studentMap.has(studentId))) {
    errors.studentIds = 'One or more selected students could not be found.';
  }

  if (draft.mode === 'TEAM') {
    const targetTeam = teams.find((team) => team._id === draft.teamId);
    if (!draft.teamId || !targetTeam) {
      errors.teamId = 'Select a team in this class.';
    } else if (entityId(targetTeam.classId) && entityId(targetTeam.classId) !== draft.classId) {
      errors.teamId = 'The selected team does not belong to this class.';
    }

    studentIds.forEach((studentId) => {
      const student = studentMap.get(studentId);
      if (student && !studentBelongsToClass(student, draft.classId)) studentsOutsideClass.push(studentId);
    });
    if (studentsOutsideClass.length > 0) {
      errors.studentIds = `${studentsOutsideClass.length} selected student${studentsOutsideClass.length === 1 ? ' does' : 's do'} not belong to this class.`;
    }

    const teamsInClass = teams.filter((team) => !entityId(team.classId) || entityId(team.classId) === draft.classId);
    const assignments = buildStudentTeamAssignments(teamsInClass, students);
    studentIds.forEach((studentId) => {
      if (studentsOutsideClass.includes(studentId)) return;
      const assignment = assignments.get(studentId);
      if (assignment && assignment.teamId !== draft.teamId) teamConflicts.set(studentId, assignment.teamName);
    });
    if (teamConflicts.size > 0) {
      errors.studentIds = `${teamConflicts.size} selected student${teamConflicts.size === 1 ? ' is' : 's are'} already assigned to another team in this class.`;
    }

    if (targetTeam) {
      const nextMemberIds = new Set([...getTeamMemberIds(targetTeam), ...studentIds]);
      if (nextMemberIds.size > TEAM_MEMBER_LIMIT) {
        errors.studentIds = `This assignment would exceed the ${TEAM_MEMBER_LIMIT}-student team limit.`;
      }
    }
  }

  return {
    isValid: Object.keys(errors).length === 0,
    errors,
    studentsOutsideClass,
    teamConflicts,
  };
}

export function applyStudentAssignment(
  draft: StudentAssignmentDraft,
  students: AssignableStudent[],
  teams: ManagedTeam[],
): StudentAssignmentResult {
  const selectedIds = new Set(draft.studentIds);
  const targetTeam = draft.mode === 'TEAM'
    ? teams.find((team) => team._id === draft.teamId) || null
    : null;

  const updatedStudents = students.map((student) => {
    if (!selectedIds.has(student._id)) return student;
    if (draft.mode === 'CLASS') {
      const movedToAnotherClass = Boolean(entityId(student.classId) && entityId(student.classId) !== draft.classId);
      return {
        ...student,
        classId: draft.classId,
        teamId: movedToAnotherClass ? null : student.teamId,
        source: student.source || 'CLASS_ROSTER',
      };
    }
    return { ...student, classId: draft.classId, teamId: draft.teamId };
  });

  const studentMap = new Map(updatedStudents.map((student) => [student._id, student]));
  const updatedTeams = teams.map((team) => {
    if (!targetTeam || team._id !== targetTeam._id) return team;
    const existingMembers = Array.isArray(team.members)
      ? team.members
      : Array.isArray(team.teamMembers)
        ? team.teamMembers
        : [];
    const existingMemberMap = new Map<string, TeamMember>();
    existingMembers.forEach((member) => {
      const id = typeof member.studentId === 'string' ? member.studentId : member.studentId._id;
      existingMemberMap.set(id, member);
    });

    const memberIds = [...new Set([...getTeamMemberIds(team), ...draft.studentIds])];
    const members = memberIds.map((studentId) => existingMemberMap.get(studentId) || {
      studentId: studentMap.get(studentId) || studentId,
      roleInTeam: entityId(team.leaderId) === studentId ? 'LEADER' : 'MEMBER',
      joinedAt: new Date().toISOString(),
    });

    return { ...team, members, teamMembers: members, memberIds };
  });

  return {
    mode: draft.mode,
    classId: draft.classId,
    teamId: targetTeam?._id || null,
    assignedStudentIds: [...selectedIds],
    students: updatedStudents,
    teams: updatedTeams,
  };
}
