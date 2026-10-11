import assert from 'node:assert/strict';
import test from 'node:test';
import { describeMentorHistoryEnd, groupMentorHistoryBySemester } from '../src/utils/mentorHistory.ts';
import { latestWorkspaceSemester, resolveWorkspaceSemesterScope } from '../src/utils/workspaceHub.ts';
import type { MentorHistoryItem } from '../src/types/teamManagement.ts';

const item = (id: string, semesterCode: string, endedBecause: MentorHistoryItem['endedBecause'] = 'ClassCompleted'): MentorHistoryItem => ({
  assignmentId: id, teamId: `t-${id}`, teamName: `Team ${id}`, classId: 'c', classCode: 'EXE101_1', subjectCode: 'EXE101',
  semesterCode, slot: 'Enterprise', assignedAtUtc: '2026-09-01T00:00:00Z', endedAtUtc: '2026-12-01T00:00:00Z', endedBecause,
});

test('groupMentorHistoryBySemester keeps the order of the API and groups by semester', () => {
  const groups = groupMentorHistoryBySemester([item('1', 'FA26'), item('2', 'SP26'), item('3', 'FA26')]);
  assert.deepEqual(groups.map(group => group.semester), ['FA26', 'SP26']);
  assert.deepEqual(groups[0].items.map(entry => entry.assignmentId), ['1', '3']);
});

test('describeMentorHistoryEnd tells a finished class apart from an assignment that ended early', () => {
  assert.match(describeMentorHistoryEnd(item('1', 'FA26', 'ClassCompleted')), /^Finished with the class on /);
  assert.match(describeMentorHistoryEnd(item('2', 'FA26', 'EndedEarly')), /^Assignment ended early on /);
  assert.equal(describeMentorHistoryEnd({ endedBecause: 'EndedEarly', endedAtUtc: 'not a date' }), 'Assignment ended early');
});

test('latestWorkspaceSemester picks the newest year, then Fall over Summer over Spring', () => {
  assert.deepEqual(latestWorkspaceSemester([{ semester: 'SP26' }, { semester: 'FA26' }, { semester: 'SU26' }]), { semester: 'FA', year: 2026 });
  assert.deepEqual(latestWorkspaceSemester([{ semester: 'FA26' }, { semester: 'SP27' }]), { semester: 'SP', year: 2027 });
  assert.equal(latestWorkspaceSemester([{ semester: 'unknown' }]), null);
  assert.equal(latestWorkspaceSemester([]), null);
});

test('the workspace scope uses the active semester, then the fallback, and never overrides explicit filters', () => {
  const active = { semester: 'FA', year: 2026 };
  const fallback = { semester: 'SP', year: 2027 };
  assert.deepEqual(resolveWorkspaceSemesterScope(new URLSearchParams(), active, fallback), { semester: 'FA', year: '2026', isDefault: true, usesFallback: false });
  assert.deepEqual(resolveWorkspaceSemesterScope(new URLSearchParams(), null, fallback), { semester: 'SP', year: '2027', isDefault: true, usesFallback: true });
  assert.deepEqual(resolveWorkspaceSemesterScope(new URLSearchParams(), null, null), { semester: 'none', year: 'none', isDefault: true, usesFallback: false });
  assert.deepEqual(resolveWorkspaceSemesterScope(new URLSearchParams('semester=all&year=all'), null, fallback), { semester: 'all', year: 'all', isDefault: false, usesFallback: false });
});
