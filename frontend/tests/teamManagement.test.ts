import assert from 'node:assert/strict';
import test from 'node:test';
import type { ManagedTeam, TeamDraft, TeamStudent } from '../src/types/teamManagement.ts';
import {
  applyTeamDraft,
  getTeamMemberIds,
  getTeamProject,
  validateTeamDraft,
} from '../src/utils/teamManagement.ts';

const students: TeamStudent[] = [
  { _id: 'student-1', fullName: 'Nguyen Van An', rollNumber: 'SE170001', email: 'an@fpt.edu.vn' },
  { _id: 'student-2', fullName: 'Tran Thi B', rollNumber: 'SE170002', email: 'b@fpt.edu.vn' },
  { _id: 'student-3', fullName: 'Le Van C', rollNumber: 'SE170003', email: 'c@fpt.edu.vn' },
];

const validDraft: TeamDraft = {
  teamName: 'Nova Founders',
  classId: 'class-1',
  memberIds: ['student-1', 'student-2'],
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
  assert.equal(result.errors.teamName, 'Team name must be between 3 and 60 characters.');
  assert.equal(result.errors.classId, 'A class is required.');
  assert.equal(result.errors.memberIds, 'Select at least one student.');
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

test('applies member changes and linked project information', () => {
  const team = applyTeamDraft(validDraft, students, null, 'CLS01_TEAM_01', 'team-new');

  assert.equal(team.teamName, 'Nova Founders');
  assert.deepEqual(getTeamMemberIds(team), ['student-1', 'student-2']);
  assert.equal(team.members?.[0].roleInTeam, 'LEADER');
  assert.equal(team.members?.[1].roleInTeam, 'MEMBER');
  assert.deepEqual(getTeamProject(team), {
    _id: 'frontend-project-team-new',
    name: 'EcoTrack',
    description: 'A platform for measuring and reducing personal carbon emissions.',
    status: 'IN_PROGRESS',
    startupField: 'GreenTech',
  });
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
