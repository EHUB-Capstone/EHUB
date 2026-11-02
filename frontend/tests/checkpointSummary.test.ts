import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import axiosClient from '../src/api/axiosClient.ts';
import { checkpointSummaryApi, checkpointSummaryPath } from '../src/api/checkpointSummaryApi.ts';
import { enableApiMocks } from '../src/mocks/mockApi.ts';
import { getMockState, resetMockState } from '../src/mocks/mockHelpers.ts';
import type { CheckpointSummary, FileSummary } from '../src/types/checkpointSummary.ts';
import {
  CHECKPOINT_SUMMARY_POLL_MS, canViewCheckpointSummary, defaultFileSelection, fileStatusView, isFileSelectable,
  keepSelectable, nextSummaryPollDelay, summaryErrorMessage, summaryFindingView,
} from '../src/utils/checkpointSummary.ts';

resetMockState();
enableApiMocks();

const TEAM_ID = '00000000-0000-4000-8000-000000000601';

const file = (fileId: string, status: FileSummary['status'], overrides: Partial<FileSummary> = {}): FileSummary => ({
  fileId, name: `${fileId}.pdf`, versionNumber: 1, uploadedAt: '2026-11-01T00:00:00Z', status, queueAhead: null,
  requestedAt: null, generatedAt: null, errorCode: null, inputTruncated: false, content: null, ...overrides,
});

const summary = (...files: FileSummary[]): CheckpointSummary => ({
  checkpointNumber: 1, enabled: true, filePromptCharacters: 12_000, files, requirements: [], links: [],
});

const login = (email: string) => axiosClient.post('/auth/login', { email, password: 'Mock123!' });

async function errorCode(promise: Promise<unknown>) {
  try {
    await promise;
  } catch (error) {
    const response = (error as { response?: { status: number; data?: { code?: string } } }).response;
    return { status: response?.status, code: response?.data?.code };
  }
  assert.fail('Expected the request to fail');
}

test('only admins, lecturers and mentors can see the AI summary card', () => {
  for (const role of ['ADMIN', 'LECTURER', 'MENTOR', 'mentor']) assert.equal(canViewCheckpointSummary(role), true);
  for (const role of ['STUDENT', '', null, undefined]) assert.equal(canViewCheckpointSummary(role), false);
  const panel = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointPanel.tsx', import.meta.url), 'utf8');
  assert.match(panel, /canViewCheckpointSummary\(user\?\.role\) && \(\s*<CheckpointAiSummaryCard/);
});

test('only files without a current summary can be chosen, and they are chosen by default', () => {
  const files = [file('a', 'None'), file('b', 'Ready'), file('c', 'Pending'), file('d', 'Failed'), file('e', 'Cancelled'), file('f', 'Processing')];
  assert.deepEqual(files.filter(isFileSelectable).map((item) => item.fileId), ['a', 'd', 'e']);
  assert.deepEqual(defaultFileSelection(summary(...files)), ['a', 'd', 'e']);
  // A choice that became impossible (queued or summarised meanwhile) is dropped; the reviewer's other choices stay.
  assert.deepEqual(keepSelectable(['a', 'b', 'd'], summary(file('a', 'Pending'), file('b', 'Ready'), file('d', 'Failed'))), ['d']);
});

test('polls only while a file is queued or being summarised', () => {
  assert.equal(nextSummaryPollDelay(summary(file('a', 'Ready'), file('b', 'Pending'))), CHECKPOINT_SUMMARY_POLL_MS);
  assert.equal(nextSummaryPollDelay(summary(file('a', 'Processing'))), CHECKPOINT_SUMMARY_POLL_MS);
  assert.equal(nextSummaryPollDelay(summary(file('a', 'Ready'), file('b', 'Cancelled'), file('c', 'None'))), null);
  assert.equal(nextSummaryPollDelay(null), null);
});

test('file status labels show the queue and findings never read as pass or fail', () => {
  assert.equal(fileStatusView(file('a', 'Pending', { queueAhead: 2 })).label, 'Queued · 2 ahead');
  assert.equal(fileStatusView(file('a', 'Pending', { queueAhead: 0 })).label, 'Queued');
  assert.equal(fileStatusView(file('a', 'Ready')).tone, 'success');
  for (const view of Object.values(summaryFindingView)) assert.doesNotMatch(view.label, /pass|fail|đạt/i);
  assert.match(summaryErrorMessage('WORKSPACE_SUMMARY_QUEUE_FULL'), /try again in a few minutes/);
  assert.match(summaryErrorMessage('AUTH_RATE_LIMITED'), /last hour/);
  assert.equal(summaryErrorMessage('SOMETHING_NEW'), 'The AI summary could not be loaded. Please try again.');
});

test('the summary API uses the checkpoint ai-summary route', () => {
  assert.equal(checkpointSummaryPath('team-1', 2), '/workspace/checkpoints/teams/team-1/checkpoints/2/ai-summary');
});

test('mock: chosen files are summarised one at a time and students are refused', async (t) => {
  resetMockState();
  await login('giang.lecturer@ehub.local');
  const start = Date.now();
  let now = start;
  t.mock.method(Date, 'now', () => now);
  const extra = { _id: 'mock-second-file', versionNumber: 2, originalName: 'Slides.pptx', fileType: 'pptx', fileSize: 100,
    uploadedAt: new Date(start).toISOString(), uploadedBy: { _id: '', name: 'Team leader' } };
  const state = getMockState();
  const first = (await checkpointSummaryApi.get(TEAM_ID, 1)).data.files[0];
  state.checkpointFiles[`${TEAM_ID}:1`] = [extra, { ...first, _id: first.fileId, originalName: first.name, versionNumber: 1 } as never];

  const initial = await checkpointSummaryApi.get(TEAM_ID, 1);
  assert.deepEqual(initial.data.files.map((item) => item.status), ['None', 'None']);
  assert.equal(initial.data.requirements[0]?.finding ?? 'NotChecked', 'NotChecked');

  const queued = await checkpointSummaryApi.request(TEAM_ID, 1, initial.data.files.map((item) => item.fileId));
  // The newest version goes first; the other waits behind it.
  assert.equal(queued.data.files[0].name, 'Slides.pptx');
  assert.deepEqual(queued.data.files.map((item) => item.status), ['Processing', 'Pending']);
  assert.equal(queued.data.files[1].queueAhead, 1);

  now = start + 7_000;
  assert.deepEqual((await checkpointSummaryApi.get(TEAM_ID, 1)).data.files.map((item) => item.status), ['Ready', 'Processing']);
  now = start + 13_000;
  const ready = await checkpointSummaryApi.get(TEAM_ID, 1);
  assert.deepEqual(ready.data.files.map((item) => item.status), ['Ready', 'Ready']);
  assert.ok(ready.data.files[0].content?.overview.includes('Slides.pptx'));
  assert.equal(ready.data.files[0].content?.highlights.length, 2);

  assert.deepEqual(await errorCode(checkpointSummaryApi.request(TEAM_ID, 1, [])), { status: 400, code: 'WORKSPACE_VALIDATION_ERROR' });
  assert.deepEqual(await errorCode(checkpointSummaryApi.request(TEAM_ID, 1, ['not-a-file'])), { status: 404, code: 'WORKSPACE_NOT_FOUND' });

  await login('se200001@fpt.edu.vn');
  assert.deepEqual(await errorCode(checkpointSummaryApi.get(TEAM_ID, 1)), { status: 403, code: 'WORKSPACE_ACCESS_DENIED' });
  assert.deepEqual(await errorCode(checkpointSummaryApi.request(TEAM_ID, 1, [first.fileId])), { status: 403, code: 'WORKSPACE_ACCESS_DENIED' });
  assert.deepEqual(await errorCode(checkpointSummaryApi.cancel(TEAM_ID, 1)), { status: 403, code: 'WORKSPACE_ACCESS_DENIED' });
});

test('mock: cancelling stops queued files and they can be chosen again', async (t) => {
  resetMockState();
  await login('giang.lecturer@ehub.local');
  let now = Date.now() + 3_600_000;
  t.mock.method(Date, 'now', () => now);
  // A file of its own: summaries in the mock are kept per file across tests, like the real ones.
  const fileId = 'mock-cancel-file';
  getMockState().checkpointFiles[`${TEAM_ID}:1`] = [{ _id: fileId, versionNumber: 3, originalName: 'Plan.docx', fileType: 'docx',
    fileSize: 100, uploadedAt: new Date(now).toISOString(), uploadedBy: { _id: '', name: 'Team leader' } }];

  assert.equal((await checkpointSummaryApi.request(TEAM_ID, 1, [fileId])).data.files[0].status, 'Processing');
  assert.equal((await checkpointSummaryApi.cancel(TEAM_ID, 1)).data.files[0].status, 'Cancelled');
  now += 60_000;
  assert.equal((await checkpointSummaryApi.get(TEAM_ID, 1)).data.files[0].status, 'Cancelled', 'a cancelled file never completes');
  assert.equal((await checkpointSummaryApi.request(TEAM_ID, 1, [fileId])).data.files[0].status, 'Processing');
});

test('the card cleans up, locks double clicks and uses a red Cancel without a checkbox-like icon', () => {
  const card = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointAiSummaryCard.tsx', import.meta.url), 'utf8');
  assert.match(card, /window\.clearTimeout\(timer\)/);
  assert.match(card, /actionControllerRef\.current\?\.abort\(\)/);
  assert.match(card, /if \(busyRef\.current\) return;/);
  assert.match(card, /variant="danger-outline" size="sm" icon=\{X\}/);
  assert.doesNotMatch(card, /\bSquare\b/);
  // The model name is kept for audit but not shown to reviewers.
  assert.doesNotMatch(card, /\.model\b/);
  assert.doesNotMatch(card, /dangerouslySetInnerHTML/);
});
