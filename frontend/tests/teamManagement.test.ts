import assert from 'node:assert/strict';
import test from 'node:test';
import type { ManagedTeam, TeamDraft, TeamStudent } from '../src/types/teamManagement.ts';
import { evaluateGroupProjectConsistency } from '../src/utils/groupProjectConsistency.ts';
import {
  canAssignMentorTypeToTeam,
  evaluateTeamMajorComposition,
  getTeamMajorWarning,
  getTeamsWithMajorWarning,
  getTeamProject,
  isMissingTeamMajor,
  isVerifiedEnrollmentMajor,
  mergeTeamsWithLinkedProposals,
  normalizeManagedTeam,
  normalizeTeamProposal,
  resolveEffectiveTeamMajor,
  validateTeamDraft,
  validateTeamSelection,
} from '../src/utils/teamManagement.ts';
import {
  appendWorkspaceTag,
  hasPersistedProjectProfile,
  resolveWorkspaceCreationDefaults,
  validateProjectProfile,
  validateProjectWorkspace,
} from '../src/utils/projectWorkspace.ts';

test('offers only active teams with the selected mentor slot still open', () => {
  const enterpriseAssignment = {
    assignmentId: 'enterprise-assignment',
    teamId: 'team-1',
    mentor: {
      mentorProfileId: 'enterprise-mentor',
      userId: 'enterprise-user',
      fullName: 'Enterprise Mentor',
      email: 'enterprise@example.com',
      mentorType: 'Enterprise' as const,
    },
    slot: 'Enterprise' as const,
    status: 'Active',
    assignedAtUtc: new Date().toISOString(),
  };
  const team: ManagedTeam = {
    _id: 'team-1',
    teamName: 'Launch Team',
    status: 'APPROVED',
    currentMentorAssignments: [enterpriseAssignment],
  };

  assert.equal(canAssignMentorTypeToTeam(team, 'Enterprise'), false);
  assert.equal(canAssignMentorTypeToTeam(team, 'Academic'), true);
  assert.equal(canAssignMentorTypeToTeam({ ...team, status: 'Archived' }, 'Academic'), false);
  assert.equal(canAssignMentorTypeToTeam({ ...team, currentMentorAssignments: [{ ...enterpriseAssignment, status: 'Ended' }] }, 'Enterprise'), true);
});

test('uses imported enrollment major before the temporary profile major', () => {
  assert.equal(resolveEffectiveTeamMajor(' bba_mkt ', 'BIT_SE'), 'BBA_MKT');
});

test('falls back to the registration profile major while enrollment major is missing', () => {
  assert.equal(resolveEffectiveTeamMajor('UNDECLARED', ' bit_se '), 'BIT_SE');
  assert.equal(resolveEffectiveTeamMajor('', 'BEN'), 'BEN');
});

test('class setup treats blank and legacy sentinel majors as action required', () => {
  assert.equal(isMissingTeamMajor(null), true);
  assert.equal(isMissingTeamMajor(''), true);
  assert.equal(isMissingTeamMajor('UNDECLARED'), true);
  assert.equal(isMissingTeamMajor(' bit_se '), false);
});

test('class setup treats only a matched enrollment major as verified', () => {
  assert.equal(isVerifiedEnrollmentMajor('Matched'), true);
  assert.equal(isVerifiedEnrollmentMajor(' matched '), true);
  assert.equal(isVerifiedEnrollmentMajor('Verified'), false);
  assert.equal(isVerifiedEnrollmentMajor('Unverified'), false);
  assert.equal(isVerifiedEnrollmentMajor('Mismatched'), false);
  assert.equal(isVerifiedEnrollmentMajor(null), false);
});

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
  assert.deepEqual(result.unclassifiedMajorCodes, []);
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

test('merges a project proposal into its linked team instead of rendering a duplicate team card', () => {
  const team = normalizeManagedTeam({
    id: 'team-1',
    classId: 'class-1',
    teamCode: 'EXE101_1_TEAM_2',
    teamName: 'SMEP',
    status: 'APPROVED',
    members: [],
  });
  const proposal = normalizeTeamProposal({
    id: 'proposal-1',
    classId: 'class-1',
    approvedTeamId: 'team-1',
    teamName: 'SMEP',
    projectName: 'SMEP',
    description: 'Project proposal description.',
    status: 'Approved',
    latestReviewComment: 'Approved with the agreed project scope.',
    members: [],
  });

  const result = mergeTeamsWithLinkedProposals([team], [proposal]);

  assert.equal(result.length, 1);
  assert.equal(result[0]._id, 'team-1');
  assert.equal(result[0].teamCode, 'EXE101_1_TEAM_2');
  assert.equal(result[0].projectName, 'SMEP');
  assert.equal(result[0].projectDescription, 'Project proposal description.');
  assert.equal(result[0].linkedProposal?._id, 'proposal-1');
  assert.equal(result[0].linkedProposal?.rejectReason, 'Approved with the agreed project scope.');
});

test('keeps an unlinked proposal visible as a standalone proposal card', () => {
  const proposal = normalizeTeamProposal({
    id: 'proposal-1',
    classId: 'class-1',
    teamName: 'Future Team',
    status: 'Draft',
    members: [],
  });

  const result = mergeTeamsWithLinkedProposals([], [proposal]);

  assert.deepEqual(result, [proposal]);
});

test('validates required project workspace information', () => {
  const errors = validateProjectWorkspace({
    teamName: '',
    projectName: '',
    description: 'too short',
    zaloGroupUrl: '',
    startupIndustryIds: [],
  });
  assert.equal(errors.teamName, 'Team name must be 3–100 characters.');
  assert.equal(errors.projectName, 'Project name must be 3–200 characters.');
  assert.equal(errors.description, 'Description must be 20–2000 characters.');
  assert.equal(errors.zaloGroupUrl, 'Zalo group link is required.');
  assert.equal(errors.startupIndustryIds, 'Select between 1 and 3 startup industries.');
});

test('accepts one to three startup industries for a project workspace', () => {
  assert.deepEqual(validateProjectWorkspace({
    teamName: 'Renamed team',
    projectName: 'Valid project',
    description: 'A sufficiently detailed project workspace description.',
    zaloGroupUrl: 'https://zalo.me/g/valid-project',
    startupIndustryIds: ['industry-1', 'industry-2', 'industry-3'],
  }), {});

  const errors = validateProjectWorkspace({
    teamName: 'Renamed team',
    projectName: 'Valid project',
    description: 'A sufficiently detailed project workspace description.',
    zaloGroupUrl: 'https://zalo.me/g/valid-project',
    startupIndustryIds: ['1', '2', '3', '4'],
  });
  assert.equal(errors.startupIndustryIds, 'Select between 1 and 3 startup industries.');
});

test('workspace creation defaults come from the linked student proposal', () => {
  const defaults = resolveWorkspaceCreationDefaults(
    { teamName: 'Fallback team' },
    {
      teamName: 'Student Venture Team',
      projectName: 'Student Venture Project',
      projectDescription: 'A balanced student-created proposal ready for lecturer review.',
    },
  );

  assert.equal(defaults.draft.teamName, 'Fallback team');
  assert.equal(defaults.draft.projectName, 'Student Venture Project');
  assert.equal(defaults.draft.description, 'A balanced student-created proposal ready for lecturer review.');
  assert.equal(defaults.draft.zaloGroupUrl, '');
});

test('project workspace creation requires an HTTPS zalo.me link', () => {
  const draft = {
    teamName: 'Campus Circular Team',
    projectName: 'Campus Circular',
    description: 'A sufficiently detailed project workspace description.',
    zaloGroupUrl: '',
    startupIndustryIds: ['industry-1'],
  };

  assert.match(validateProjectWorkspace(draft).zaloGroupUrl || '', /required/);
  assert.equal(validateProjectWorkspace({ ...draft, zaloGroupUrl: 'https://zalo.me/g/campus-circular' }).zaloGroupUrl, undefined);
  assert.match(validateProjectWorkspace({ ...draft, zaloGroupUrl: 'https://example.com/team' }).zaloGroupUrl || '', /zalo\.me/);
  assert.match(validateProjectWorkspace({ ...draft, zaloGroupUrl: 'http://zalo.me/g/campus-circular' }).zaloGroupUrl || '', /HTTPS/);
});

test('project profile requires name, description, and Zalo link while problem, solution, and target users are optional', () => {
  const invalid = validateProjectProfile({
    projectName: 'x',
    description: '',
    problem: '',
    solution: '',
    targetUsers: '',
    zaloGroupUrl: '',
  });
  assert.deepEqual(Object.keys(invalid).sort(), ['description', 'projectName', 'zaloGroupUrl']);

  assert.deepEqual(validateProjectProfile({
    projectName: 'Campus Circular Hub',
    description: 'A complete description of the approved project profile.',
    problem: '',
    solution: '',
    targetUsers: '',
    zaloGroupUrl: 'https://zalo.me/g/campus-circular',
  }), {});

  const optionalFieldErrors = validateProjectProfile({
    projectName: 'Campus Circular Hub',
    description: 'A complete description of the approved project profile.',
    problem: 'Too short',
    solution: 'Also too short',
    targetUsers: '',
    zaloGroupUrl: 'https://zalo.me/g/campus-circular',
  });
  assert.match(optionalFieldErrors.problem || '', /when provided/);
  assert.match(optionalFieldErrors.solution || '', /when provided/);
});

test('project profile success requires the server to return every persisted field', () => {
  const draft = {
    projectName: 'Campus Circular Hub',
    description: 'A complete description of the approved project profile.',
    problem: 'Students struggle to reuse useful equipment safely on campus.',
    solution: 'A verified marketplace supports safe exchanges between students.',
    targetUsers: 'University students and student clubs',
    zaloGroupUrl: 'https://zalo.me/g/campus-circular',
  };

  assert.equal(hasPersistedProjectProfile(draft, draft), true);
  assert.equal(hasPersistedProjectProfile(draft, { ...draft, targetUsers: '' }), false);
  assert.equal(hasPersistedProjectProfile(draft, { ...draft, zaloGroupUrl: '' }), false);
});

test('project profile requires an HTTPS zalo.me link', () => {
  const draft = {
    projectName: 'Campus Circular Hub',
    description: 'A complete description of the approved project profile.',
    problem: 'Students struggle to reuse useful equipment safely on campus.',
    solution: 'A verified marketplace supports safe exchanges between students.',
    targetUsers: 'University students and student clubs',
    zaloGroupUrl: '',
  };

  assert.match(validateProjectProfile(draft).zaloGroupUrl || '', /required/);
  assert.equal(validateProjectProfile({ ...draft, zaloGroupUrl: 'https://zalo.me/g/campus-circular' }).zaloGroupUrl, undefined);
  assert.match(validateProjectProfile({ ...draft, zaloGroupUrl: 'https://example.com/team' }).zaloGroupUrl || '', /zalo\.me/);
  assert.match(validateProjectProfile({ ...draft, zaloGroupUrl: 'http://zalo.me/g/campus-circular' }).zaloGroupUrl || '', /HTTPS/);
});

test('normalizes and rejects duplicated or invalid workspace tags', () => {
  const first = appendWorkspaceTag([], ' React ');
  assert.deepEqual(first.values, ['React']);
  assert.equal(appendWorkspaceTag(first.values, '  react  ').error, '“react” is already included.');
  assert.ok(appendWorkspaceTag(first.values, '<script>').error);
});

test('accepts a team with at least one GROUP_1 and one GROUP_2 major', () => {
  const composition = evaluateTeamMajorComposition([
    { fullName: 'Ada', majorCode: 'BBA_MKT' },
    { fullName: 'Ben', majorCode: ' bit_se ' },
    { fullName: 'Cam', majorCode: 'BIT_AI' },
    { fullName: 'Dee', majorCode: 'BEN' },
  ]);

  assert.equal(composition.isValid, true);
  assert.equal(composition.message, null);
  assert.deepEqual(composition.missingGroups, []);
});

test('warns which group is missing and which members have no valid major', () => {
  const onlyBusiness = evaluateTeamMajorComposition([
    { fullName: 'Ada', majorCode: 'BBA_MKT' },
    { fullName: 'Ben', majorCode: 'BBA_FIN' },
    { fullName: 'Cam', majorCode: 'UNDECLARED' },
    { fullName: 'Dee', majorCode: null },
  ]);

  assert.equal(onlyBusiness.isValid, false);
  assert.deepEqual(onlyBusiness.missingGroups, ['GROUP_2']);
  assert.deepEqual(onlyBusiness.membersWithoutValidMajor, ['Cam', 'Dee']);
  assert.match(onlyBusiness.message || '', /no member from GROUP_2/);
  assert.match(onlyBusiness.message || '', /Cam, Dee/);

  const onlyTechnology = evaluateTeamMajorComposition([
    { fullName: 'Ada', majorCode: 'BIT_SE' },
    { fullName: 'Ben', majorCode: 'BIT_AI' },
  ]);
  assert.deepEqual(onlyTechnology.missingGroups, ['GROUP_1']);
});

test('shows a team major warning only for an invalid team and hides it once fixed', () => {
  const invalid = normalizeManagedTeam({
    id: 'team-1',
    teamName: 'Imbalanced',
    members: [],
    majorComposition: evaluateTeamMajorComposition([{ fullName: 'Ada', majorCode: 'BBA_MKT' }]),
  });
  assert.ok(getTeamMajorWarning(invalid));
  assert.match(getTeamMajorWarning(invalid)?.message || '', /GROUP_2/);

  const fixed = { ...invalid, majorComposition: evaluateTeamMajorComposition([
    { fullName: 'Ada', majorCode: 'BBA_MKT' },
    { fullName: 'Ben', majorCode: 'BIT_SE' },
  ]) };
  assert.equal(getTeamMajorWarning(fixed), null);
  assert.equal(getTeamMajorWarning({ _id: 'legacy', teamName: 'No evaluation' }), null);
  assert.equal(getTeamMajorWarning({ ...invalid, isProposal: true }), null);
});

test('class banner lists only teams that fail the major requirement and clears once fixed', () => {
  const valid = evaluateTeamMajorComposition([
    { fullName: 'Ada', majorCode: 'BBA_MKT' },
    { fullName: 'Ben', majorCode: 'BIT_SE' },
  ]);
  const invalid = evaluateTeamMajorComposition([
    { fullName: 'Cam', majorCode: 'BIT_SE' },
    { fullName: 'Dee', majorCode: 'BIT_AI' },
  ]);
  const teams: ManagedTeam[] = [
    { _id: 'ok', teamName: 'Balanced', majorComposition: valid },
    { _id: 'bad', teamName: 'All BIT', majorComposition: invalid },
    { _id: 'proposal', teamName: 'Proposal', majorComposition: invalid, isProposal: true },
    { _id: 'legacy', teamName: 'No evaluation' },
  ];

  const flagged = getTeamsWithMajorWarning(teams);
  assert.deepEqual(flagged.map(({ team }) => team._id), ['bad']);
  assert.deepEqual(flagged[0].warning.missingGroups, ['GROUP_1']);
  assert.deepEqual(getTeamsWithMajorWarning([{ ...teams[1], majorComposition: valid }]), []);
});

test('accepts one-to-one group and project data', () => {
  const result = evaluateGroupProjectConsistency([
    { group: 'G01', project: 'Project A' },
    { group: 'G01', project: 'Project A' },
    { group: 'G02', project: 'Project B' },
    { group: 'G02', project: 'Project B' },
  ]);

  assert.equal(result.isConsistent, true);
  assert.deepEqual(result.warnings, []);
});

test('warns when a group has several projects', () => {
  const result = evaluateGroupProjectConsistency([
    { group: 'G01', project: 'Project A' },
    { group: 'G01', project: 'Project A' },
    { group: 'G01', project: 'Project B' },
  ]);

  assert.equal(result.isConsistent, false);
  assert.equal(result.warnings.length, 1);
  assert.equal(result.warnings[0].type, 'GROUP_HAS_MULTIPLE_PROJECTS');
  assert.equal(result.warnings[0].message, 'Group `G01` is assigned to multiple projects: `Project A`, `Project B`.');
});

test('warns when a project has several groups, sorted naturally and ignoring blanks and case', () => {
  const result = evaluateGroupProjectConsistency([
    { group: 'G10', project: 'Project A' },
    { group: 'G2', project: 'project a' },
    { group: 'G1', project: ' Project A ' },
    { group: '', project: 'Project A' },
    { group: 'G3', project: null },
  ]);

  assert.deepEqual(result.warnings.map((warning) => warning.type), ['PROJECT_HAS_MULTIPLE_GROUPS']);
  assert.deepEqual(result.warnings[0].related, ['G1', 'G2', 'G10']);
});
