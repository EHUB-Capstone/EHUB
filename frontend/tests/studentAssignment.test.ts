import assert from 'node:assert/strict';
import test from 'node:test';
import type { AssignableStudent, StudentAssignmentDraft } from '../src/types/studentAssignment.ts';
import type { ManagedTeam } from '../src/types/teamManagement.ts';
import {
  applyStudentAssignment,
  mergeAssignmentCandidates,
  validateStudentAssignment,
} from '../src/utils/studentAssignment.ts';
import { getTeamMemberIds } from '../src/utils/teamManagement.ts';

const students: AssignableStudent[] = [
  { _id: 'student-1', fullName: 'Nguyen Van An', rollNumber: 'SE170001', email: 'an@fpt.edu.vn', classId: 'class-1' },
  { _id: 'student-2', fullName: 'Tran Thi B', rollNumber: 'SE170002', email: 'b@fpt.edu.vn', classId: 'class-1' },
  { _id: 'student-3', fullName: 'Le Van C', rollNumber: 'SE170003', email: 'c@fpt.edu.vn', classId: 'class-2', teamId: 'old-team' },
];

const teams: ManagedTeam[] = [
  {
    _id: 'team-1',
    classId: 'class-1',
    teamName: 'Nova Founders',
    members: [{ studentId: students[0] }],
    memberIds: ['student-1'],
  },
  {
    _id: 'team-2',
    classId: 'class-1',
    teamName: 'Green Pioneers',
    members: [{ studentId: students[1] }],
    memberIds: ['student-2'],
  },
];

test('assigns selected students to a class and clears a team from the previous class', () => {
  const draft: StudentAssignmentDraft = {
    mode: 'CLASS',
    classId: 'class-1',
    teamId: '',
    studentIds: ['student-3'],
  };

  const validation = validateStudentAssignment(draft, students, teams);
  const result = applyStudentAssignment(draft, students, teams);
  const assigned = result.students.find((student) => student._id === 'student-3');

  assert.equal(validation.isValid, true);
  assert.equal(assigned?.classId, 'class-1');
  assert.equal(assigned?.teamId, null);
});

test('assigns a class student to a team and updates both student and team detail data', () => {
  const availableStudents = students.map((student) => (
    student._id === 'student-2' ? { ...student, teamId: null } : student
  ));
  const availableTeams = teams.map((team) => (
    team._id === 'team-2' ? { ...team, members: [], memberIds: [] } : team
  ));
  const draft: StudentAssignmentDraft = {
    mode: 'TEAM',
    classId: 'class-1',
    teamId: 'team-2',
    studentIds: ['student-2'],
  };

  const validation = validateStudentAssignment(draft, availableStudents, availableTeams);
  const result = applyStudentAssignment(draft, availableStudents, availableTeams);
  const assigned = result.students.find((student) => student._id === 'student-2');
  const updatedTeam = result.teams.find((team) => team._id === 'team-2');

  assert.equal(validation.isValid, true);
  assert.equal(assigned?.teamId, 'team-2');
  assert.deepEqual(getTeamMemberIds(updatedTeam!), ['student-2']);
});

test('blocks team assignment when a selected student does not belong to the class', () => {
  const draft: StudentAssignmentDraft = {
    mode: 'TEAM',
    classId: 'class-1',
    teamId: 'team-1',
    studentIds: ['student-3'],
  };

  const result = validateStudentAssignment(draft, students, teams);

  assert.equal(result.isValid, false);
  assert.deepEqual(result.studentsOutsideClass, ['student-3']);
  assert.equal(result.errors.studentIds, '1 selected student does not belong to this class.');
});

test('prevents duplicate assignment to another team in the same class', () => {
  const draft: StudentAssignmentDraft = {
    mode: 'TEAM',
    classId: 'class-1',
    teamId: 'team-1',
    studentIds: ['student-2'],
  };

  const result = validateStudentAssignment(draft, students, teams);

  assert.equal(result.isValid, false);
  assert.equal(result.teamConflicts.get('student-2'), 'Green Pioneers');
  assert.match(result.errors.studentIds || '', /already assigned to another team/);
});

test('rejects a team that belongs to a different class', () => {
  const foreignTeam: ManagedTeam = { _id: 'team-3', classId: 'class-2', teamName: 'Other Class' };
  const draft: StudentAssignmentDraft = {
    mode: 'TEAM',
    classId: 'class-1',
    teamId: foreignTeam._id,
    studentIds: ['student-1'],
  };

  const result = validateStudentAssignment(draft, students, [...teams, foreignTeam]);

  assert.equal(result.isValid, false);
  assert.equal(result.errors.teamId, 'The selected team does not belong to this class.');
});

test('enforces the six-student team capacity during assignment', () => {
  const fullTeam: ManagedTeam = {
    _id: 'team-full',
    classId: 'class-1',
    teamName: 'Full Team',
    memberIds: ['member-1', 'member-2', 'member-3', 'member-4', 'member-5', 'member-6'],
  };
  const draft: StudentAssignmentDraft = {
    mode: 'TEAM',
    classId: 'class-1',
    teamId: fullTeam._id,
    studentIds: ['student-1'],
  };

  const result = validateStudentAssignment(draft, students, [fullTeam]);

  assert.equal(result.isValid, false);
  assert.equal(result.errors.studentIds, 'This assignment would exceed the 6-student team limit.');
});

test('merges directory and class records without duplicating the same student', () => {
  const classRecord: AssignableStudent = {
    _id: 'profile-1',
    fullName: 'Nguyen Van An',
    email: 'an@fpt.edu.vn',
    classId: 'class-1',
    source: 'CLASS_ROSTER',
  };
  const directoryRecord: AssignableStudent = {
    _id: 'user-1',
    fullName: 'Nguyen Van An',
    email: 'AN@fpt.edu.vn',
    source: 'USER_DIRECTORY',
  };

  const result = mergeAssignmentCandidates([classRecord], [directoryRecord]);

  assert.equal(result.length, 1);
  assert.equal(result[0]._id, 'profile-1');
  assert.equal(result[0].classId, 'class-1');
});
