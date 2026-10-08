import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildTeamSlotRows,
  eligibleTeamsForKind,
  filterMentorOptions,
  filterTeamSlotRows,
  nextFocusIndex,
  slotCandidates,
  type MentorOption,
} from '../src/utils/mentorSlotBoard.ts';
import type { ManagedTeam, MentorAssignment } from '../src/types/teamManagement.ts';

const assignment = (slot: 'Enterprise' | 'Academic', name: string, status = 'Active'): MentorAssignment => ({
  assignmentId: `${slot}-${name}`,
  teamId: 't',
  mentor: { mentorProfileId: `p-${name}`, userId: `u-${name}`, fullName: name, email: `${name}@x.vn`, mentorType: slot },
  slot,
  status,
} as MentorAssignment);

const team = (code: string, assignments: MentorAssignment[], status = 'Active'): ManagedTeam => ({
  _id: code, teamCode: code, teamName: `Team ${code}`, groupName: code, status, currentMentorAssignments: assignments,
} as unknown as ManagedTeam);

const mentor = (id: string, mentorType: 'Enterprise' | 'Academic', name = id, contractType: string | null = null): MentorOption => ({
  _id: id, name, email: `${id}@x.vn`, mentorType, contractType, activeTeamCount: 0,
});

test('buildTeamSlotRows lists each team with both slots and counts what is missing', () => {
  const rows = buildTeamSlotRows([
    team('T10', [assignment('Enterprise', 'A'), assignment('Academic', 'B')]),
    team('T2', [assignment('Enterprise', 'C')]),
    team('T1', []),
    team('T3', [assignment('Academic', 'D', 'Ended')]),
    team('T4', [], 'Disbanded'),
  ]);

  assert.deepEqual(rows.map(row => row.team.teamCode), ['T1', 'T2', 'T3', 'T10'], 'natural order, inactive teams left out');
  assert.deepEqual(rows.map(row => row.missingCount), [2, 1, 2, 0]);
  assert.equal(rows[1].enterprise?.mentor.fullName, 'C');
  assert.equal(rows[1].academic, null);
  assert.equal(rows[2].academic, null, 'an ended assignment does not count');
});

test('filterTeamSlotRows narrows to teams that still miss a mentor and to the search text', () => {
  const rows = buildTeamSlotRows([
    team('T1', [assignment('Enterprise', 'A'), assignment('Academic', 'B')]),
    team('T2', [assignment('Enterprise', 'C')]),
  ]);
  assert.equal(filterTeamSlotRows(rows, 'all', '').length, 2);
  assert.deepEqual(filterTeamSlotRows(rows, 'missing', '').map(row => row.team.teamCode), ['T2']);
  assert.deepEqual(filterTeamSlotRows(rows, 'all', 't1').map(row => row.team.teamCode), ['T1']);
});

test('filterMentorOptions combines the kind chip with the search text, including the contract type', () => {
  const options = [mentor('a', 'Enterprise', 'An', 'Thỉnh giảng'), mentor('b', 'Academic', 'Binh'), mentor('c', 'Enterprise', 'Chi', 'Khoán')];
  assert.equal(filterMentorOptions(options, '', 'ALL').length, 3);
  assert.deepEqual(filterMentorOptions(options, '', 'Enterprise').map(item => item._id), ['a', 'c']);
  assert.deepEqual(filterMentorOptions(options, 'khoan', 'ALL').map(item => item._id), ['c']);
});

test('slotCandidates only offers the right kind and never the current holder of the slot', () => {
  const options = [mentor('a', 'Enterprise'), mentor('b', 'Academic'), mentor('c', 'Enterprise')];
  assert.deepEqual(slotCandidates(options, 'Enterprise').map(item => item._id), ['a', 'c']);
  assert.deepEqual(slotCandidates(options, 'Enterprise', 'a').map(item => item._id), ['c']);
});

test('eligibleTeamsForKind skips teams whose slot is already taken', () => {
  const teams = [team('T2', [assignment('Enterprise', 'A')]), team('T1', [])];
  assert.deepEqual(eligibleTeamsForKind(teams, 'Enterprise').map(item => item.teamCode), ['T1']);
  assert.deepEqual(eligibleTeamsForKind(teams, 'Academic').map(item => item.teamCode), ['T1', 'T2']);
});

test('nextFocusIndex wraps in both directions and enters the dialog from outside', () => {
  assert.equal(nextFocusIndex(2, 3, false), 0);
  assert.equal(nextFocusIndex(0, 3, true), 2);
  assert.equal(nextFocusIndex(-1, 3, false), 0);
  assert.equal(nextFocusIndex(-1, 3, true), 2);
  assert.equal(nextFocusIndex(0, 0, false), -1);
});
