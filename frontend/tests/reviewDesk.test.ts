import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import axiosClient from '../src/api/axiosClient.ts';
import { reviewDeskApi } from '../src/api/reviewDeskApi.ts';
import { enableApiMocks } from '../src/mocks/mockApi.ts';
import { getMockState, resetMockState } from '../src/mocks/mockHelpers.ts';
import type { ReviewDeskInboxItem } from '../src/types/reviewDesk.ts';
import { deadlineText, inboxLine, inboxReasonView, relativeTime, teamCheckpointPath } from '../src/utils/reviewDesk.ts';

resetMockState();
enableApiMocks();

const TEAM_ID = '00000000-0000-4000-8000-000000000601';
const login = (email: string) => axiosClient.post('/auth/login', { email, password: 'Mock123!' });
const statusOf = (error: { response?: { status: number } }) => error.response?.status;

const NOW = Date.parse('2026-11-10T12:00:00Z');
const hoursAgo = (hours: number) => new Date(NOW - hours * 3_600_000).toISOString();

test('times read as a person would say them, before and after a deadline', () => {
  assert.equal(relativeTime(hoursAgo(0.01), NOW), 'just now');
  assert.equal(relativeTime(new Date(NOW - 5 * 60_000).toISOString(), NOW), '5 min ago');
  assert.equal(relativeTime(hoursAgo(3), NOW), '3 h ago');
  assert.equal(relativeTime(hoursAgo(50), NOW), '2 d ago');
  assert.equal(relativeTime(null, NOW), '');
  assert.equal(deadlineText(new Date(NOW + 5 * 3_600_000).toISOString(), NOW), '5 h left');
  assert.equal(deadlineText(new Date(NOW - 2 * 86_400_000).toISOString(), NOW), '2 d overdue');
  assert.equal(deadlineText(undefined, NOW), '');
});

test('inbox rows say why the team needs a look and link to its checkpoint', () => {
  const item = (overrides: Partial<ReviewDeskInboxItem>): ReviewDeskInboxItem => ({
    teamId: 't', teamCode: 'T01', teamName: 'Phoenix', classId: 'c', classCode: 'EXE101_01', checkpointNumber: 2, reason: 'NewVersion',
    versionNumber: 3, submittedAtUtc: hoursAgo(2), dueAtUtc: null, ...overrides,
  });
  assert.equal(inboxLine(item({}), NOW), 'Version v3 submitted 2 h ago');
  assert.equal(inboxLine(item({ reason: 'Overdue', dueAtUtc: hoursAgo(30) }), NOW), 'Deadline: 1 d overdue');
  assert.equal(inboxLine(item({ reason: 'DueSoon', dueAtUtc: new Date(NOW + 5 * 3_600_000).toISOString() }), NOW), 'Deadline: 5 h left');
  assert.equal(teamCheckpointPath('abc', 2), '/workspace/teams/abc?checkpoint=2');
  // Overdue sorts before a new version, and every reason has a label.
  assert.deepEqual(Object.keys(inboxReasonView), ['Overdue', 'NewVersion', 'DueSoon']);
});

test('mock: a lecturer sees new versions, opening a team clears it, switched off nothing exists, students are refused', async () => {
  resetMockState();
  await login('giang.lecturer@ehub.local');
  getMockState().checkpointFiles[`${TEAM_ID}:1`] = [{
    _id: 'mock-inbox-file', versionNumber: 2, originalName: 'Plan.pdf', fileType: 'pdf', fileSize: 10,
    uploadedAt: new Date().toISOString(), uploadedBy: { _id: '', name: 'Team leader' },
  } as never];

  assert.equal((await reviewDeskApi.getStatus()).data.enabled, true);
  const fresh = (await reviewDeskApi.getDesk()).data.inbox.find((item) => item.teamId === TEAM_ID && item.checkpointNumber === 1);
  assert.equal(fresh?.reason, 'NewVersion');
  assert.equal(fresh?.versionNumber, 2);

  await reviewDeskApi.markSeen(TEAM_ID, 1);
  const after = (await reviewDeskApi.getDesk()).data;
  assert.equal(after.inbox.some((item) => item.teamId === TEAM_ID && item.checkpointNumber === 1 && item.reason === 'NewVersion'), false);

  // Switched off, like production: the status says so and the desk and the visit marker do not exist.
  getMockState().checkpointSummaryDisabled = true;
  assert.equal((await reviewDeskApi.getStatus()).data.enabled, false);
  await assert.rejects(reviewDeskApi.getDesk(), (error: { response?: { status: number } }) => statusOf(error) === 503);
  await assert.rejects(reviewDeskApi.markSeen(TEAM_ID, 1), (error: { response?: { status: number } }) => statusOf(error) === 503);
  getMockState().checkpointSummaryDisabled = false;

  await login('se200001@fpt.edu.vn');
  await assert.rejects(reviewDeskApi.getStatus(), (error: { response?: { status: number } }) => statusOf(error) === 403);
  await assert.rejects(reviewDeskApi.getDesk(), (error: { response?: { status: number } }) => statusOf(error) === 403);
});

test('the review inbox lives on the dashboards and the class checkpoint tab, and shows nothing unless the server turns AI on', () => {
  const read = (path: string) => readFileSync(new URL(path, import.meta.url), 'utf8');
  // No standalone page, route or sidebar entry.
  assert.doesNotMatch(read('../src/App.tsx'), /review-desk|ReviewDesk/);
  assert.doesNotMatch(read('../src/components/layout/Sidebar.tsx'), /review-desk|review_desk/);
  // The widget is on both reviewer dashboards and hides itself while the server reports AI off.
  assert.match(read('../src/pages/lecturer/LecturerDashboard.tsx'), /<ReviewInboxCard \/>/);
  assert.match(read('../src/pages/mentor/MentorDashboard.tsx'), /<ReviewInboxCard \/>/);
  assert.match(read('../src/components/workspace/ReviewInboxCard.tsx'), /if \(!enabled\) return null;/);
  // The hook asks nothing of the desk until the status says enabled; the visit marker waits for it too.
  assert.match(read('../src/hooks/useReviewInbox.ts'), /if \(!enabled\) return undefined;/);
  assert.match(read('../src/components/workspace/checkpoints/CheckpointPanel.tsx'), /!aiSummaryEnabled \|\|/);
  assert.match(read('../src/components/class/LecturerCheckpointManagement.tsx'), /newVersionKeys\.has/);
  assert.match(read('../src/components/workspace/checkpoints/CheckpointSection.tsx'), /searchParams\.get\('checkpoint'\)/);
  assert.match(read('../src/components/workspace/checkpoints/CheckpointPanel.tsx'), /reviewDeskApi\.markSeen\(String\(teamId\), checkpoint\.number\)/);
});
