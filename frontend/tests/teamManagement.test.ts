import assert from 'node:assert/strict';
import test from 'node:test';
import type { ManagedTeam, TeamDraft, TeamStudent } from '../src/types/teamManagement.ts';
import {
  getTeamMemberIds,
  getTeamProject,
  validateTeamDraft,
  validateTeamSelection,
} from '../src/utils/teamManagement.ts';

const students: TeamStudent[] = [
  { _id: 'student-1', fullName: 'Nguyen Van An', rollNumber: 'SE170001', email: 'an@fpt.edu.vn', major: 'BEN' },
  { _id: 'student-2', fullName: 'Tran Thi B', rollNumber: 'SE170002', email: 'b@fpt.edu.vn', major: 'BIT_SE' },
  { _id: 'student-3', fullName: 'Le Van C', rollNumber: 'SE170003', email: 'c@fpt.edu.vn', major: 'BIT_AI' },
  { _id: 'student-4', fullName: 'Pham Thi D', rollNumber: 'SE170004', email: 'd@fpt.edu.vn', major: 'BIT_IS' },
];

const validDraft: TeamDraft = {
  teamName: 'Nova Founders',
  classId: 'class-1',
  memberIds: ['student-1', 'student-2', 'student-3', 'student-4'],
  leaderId: 'student-1',
  description: 'A cross-functional startup team.',
  projectName: 'EcoTrack',
  projectDescription: 'A platform for measuring and reducing personal carbon emissions.',
  projectStatus: 'IN_PROGRESS',
  startupField: 'GreenTech',
};

test('accepts a valid team name, class and member list', () => {
  const result = validateTeamDraft(validDraft, [], students);

  assert.equal(result.isValid, true);
  assert.deepEqual(result.errors, {});
  assert.equal(result.conflicts.size, 0);
});

test('reports missing required team information', () => {
  const result = validateTeamDraft({
    ...validDraft,
    teamName: '',
    classId: '',
    memberIds: [],
    leaderId: '',
  }, [], students);

  assert.equal(result.isValid, false);
  assert.equal(result.errors.teamName, undefined);
  assert.equal(result.errors.classId, 'A class is required.');
  assert.equal(result.errors.memberIds, 'A team must have 4-6 students.');
});

test('prevents duplicate team assignment in the same class', () => {
  const existingTeam: ManagedTeam = {
    _id: 'team-1',
    classId: 'class-1',
    teamName: 'Existing Team',
    members: [{ studentId: students[0], roleInTeam: 'MEMBER' }],
  };
  const result = validateTeamDraft(validDraft, [existingTeam], students);

  assert.equal(result.isValid, false);
  assert.equal(result.conflicts.get('student-1'), 'Existing Team');
  assert.equal(result.errors.memberIds, '1 selected student is already assigned to another team.');
});

test('allows an update to keep members of the current team', () => {
  const currentTeam: ManagedTeam = {
    _id: 'team-1',
    classId: 'class-1',
    teamName: 'Nova Founders',
    members: [{ studentId: students[0], roleInTeam: 'LEADER' }],
  };
  const result = validateTeamDraft(validDraft, [currentTeam], students, currentTeam._id);

  assert.equal(result.isValid, true);
  assert.equal(result.conflicts.size, 0);
});

test('does not treat an assignment from another class as a conflict', () => {
  const anotherClassTeam: ManagedTeam = {
    _id: 'team-other',
    classId: 'class-2',
    teamName: 'Other Class Team',
    members: [{ studentId: students[0] }],
  };
  const result = validateTeamDraft(validDraft, [anotherClassTeam], students);

  assert.equal(result.isValid, true);
});

test('rejects 3- and 7-member teams', () => {
  const result = validateTeamDraft({
    ...validDraft,
    memberIds: ['student-1', 'student-2', 'student-3'],
  }, [], students);

  assert.equal(result.isValid, false);
  assert.equal(result.errors.memberIds, 'A team must have 4-6 students.');

  const sevenMemberResult = validateTeamDraft({
    ...validDraft,
    memberIds: ['student-1', 'student-2', 'student-3', 'student-4', 'student-5', 'student-6', 'student-7'],
  }, [], [
    ...students,
    { _id: 'student-5', fullName: 'Student 5', major: 'BBA_HM' },
    { _id: 'student-6', fullName: 'Student 6', major: 'BIT_GD' },
    { _id: 'student-7', fullName: 'Student 7', major: 'BIT_SE' },
  ]);

  assert.equal(sevenMemberResult.isValid, false);
  assert.equal(sevenMemberResult.errors.memberIds, 'A team must have 4-6 students.');
});

test('does not count an unlisted major as GROUP_2 evidence', () => {
  const studentsWithUnlistedMajor = [
    ...students,
    { _id: 'student-5', fullName: 'Le Thi E', major: 'BIT_IS' },
    { _id: 'student-6', fullName: 'Pham Van F', major: 'BBA_FIN' },
  ];
  const result = validateTeamDraft({
    ...validDraft,
    memberIds: ['student-1', 'student-4', 'student-5', 'student-6'],
  }, [], studentsWithUnlistedMajor);

  assert.equal(result.isValid, false);
  assert.equal(result.errors.memberIds, 'A team must include at least one GROUP_1 major and one GROUP_2 major.');
});

test('summarizes real-time team selection constraints', () => {
  const result = validateTeamSelection([
    students[0],
    students[1],
    { _id: 'student-5', fullName: 'Missing Major', major: 'UNDECLARED' },
    { _id: 'student-6', fullName: 'Finance Major', major: 'BBA_FIN' },
  ], 'student-1');

  assert.equal(result.memberCount, 4);
  assert.equal(result.isMemberCountValid, true);
  assert.equal(result.hasGroupOne, true);
  assert.equal(result.hasGroupTwo, true);
  assert.equal(result.isTeamLeaderValid, true);
  assert.equal(result.canCreateTeam, true);
  assert.deepEqual(result.missingMajorStudents.map(student => student._id), ['student-5']);
  assert.deepEqual(result.unclassifiedMajorCodes, ['BBA_FIN']);
});

test('reads linked project information from legacy team fields', () => {
  const project = getTeamProject({
    _id: 'team-legacy',
    teamName: 'Legacy Team',
    projectName: 'Legacy Startup',
    projectDescription: 'Existing project information.',
    projectStatus: 'VALIDATED',
  });

  assert.deepEqual(project, {
    name: 'Legacy Startup',
    description: 'Existing project information.',
    status: 'VALIDATED',
  });
});
