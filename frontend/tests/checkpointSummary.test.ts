import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import test from 'node:test';
import axiosClient from '../src/api/axiosClient.ts';
import { checkpointSummaryApi, checkpointSummaryPath } from '../src/api/checkpointSummaryApi.ts';
import { enableApiMocks } from '../src/mocks/mockApi.ts';
import { getMockState, resetMockState } from '../src/mocks/mockHelpers.ts';
import type { CheckpointSummary, FileSummary } from '../src/types/checkpointSummary.ts';
import {
  CHECKPOINT_SUMMARY_POLL_MS, canOpenFile, canViewCheckpointSummary, isNothingFound, isSearchable, isSummaryFeatureVisible, sortByWeight, statusItemView, fileStatusView, isFileSelectable,
  displayNumbers, ignoredInstructionsMessage, keepSelectable, pageLabel, quoteForFeedback, summaryEtaText, nextSummaryPollDelay, selectableFileIds, summaryErrorMessage, summaryFindingView, summaryStatusBadges, summaryStatusCounts,
} from '../src/utils/checkpointSummary.ts';

resetMockState();
enableApiMocks();

// The card is split into pieces under ai-summary/; assertions about its source look at all of them.
const checkpointsFolder = new URL('../src/components/workspace/checkpoints/', import.meta.url);
const aiSummarySources = () => ['CheckpointAiSummaryCard.tsx', ...readdirSync(new URL('ai-summary/', checkpointsFolder)).map((name) => `ai-summary/${name}`)]
  .map((name) => readFileSync(new URL(name, checkpointsFolder), 'utf8')).join('\n');

const TEAM_ID = '00000000-0000-4000-8000-000000000601';

const file = (fileId: string, status: FileSummary['status'], overrides: Partial<FileSummary> = {}): FileSummary => ({
  fileId, name: `${fileId}.pdf`, versionNumber: 1, uploadedAt: '2026-11-01T00:00:00Z', status, queueAhead: null,
  isAutomatic: false, preview: null, requestedAt: null, generatedAt: null, errorCode: null, inputTruncated: false, content: null, ...overrides,
});

const summary = (...files: FileSummary[]): CheckpointSummary => ({
  checkpointNumber: 1, enabled: true, filePromptCharacters: 5_000, files, requirements: [], links: [],
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

test('only files without a current summary can be chosen, and none is chosen by default', () => {
  const files = [file('a', 'None'), file('b', 'Ready'), file('c', 'Pending'), file('d', 'Failed'), file('e', 'Cancelled'), file('f', 'Processing')];
  assert.deepEqual(files.filter(isFileSelectable).map((item) => item.fileId), ['a', 'd', 'e']);
  // "Select all" offers exactly these; nothing is ticked until the reviewer asks.
  assert.deepEqual(selectableFileIds(summary(...files)), ['a', 'd', 'e']);
  assert.deepEqual(keepSelectable([], summary(...files)), []);
  // A file queued automatically after upload can still be chosen: that moves it to the front of the queue.
  assert.equal(isFileSelectable(file('g', 'Pending', { isAutomatic: true })), true);
  assert.equal(isFileSelectable(file('h', 'Processing', { isAutomatic: true })), false);
  // A choice that became impossible (queued or summarised meanwhile) is dropped; the reviewer's other choices stay.
  assert.deepEqual(keepSelectable(['a', 'b', 'd'], summary(file('a', 'Pending'), file('b', 'Ready'), file('d', 'Failed'))), ['d']);
});

test('outdated ready summaries can be refreshed while current ready summaries stay cached', () => {
  const stale = file('stale', 'Ready', { needsRefresh: true });
  const current = file('current', 'Ready', { needsRefresh: false });
  assert.deepEqual(selectableFileIds(summary(stale, current)), ['stale']);
  assert.deepEqual(keepSelectable(['stale', 'current'], summary(stale, current)), ['stale']);
  assert.equal(fileStatusView(stale).label, 'Needs update');
  assert.equal(fileStatusView(stale).tone, 'warning');
  assert.ok(summaryStatusBadges(summary(stale, current), false).some((badge) => badge.label === '1 need updating'));
});

test('the collapsed header counts files by state, worst news first', () => {
  const files = [file('a', 'Processing'), file('b', 'Pending'), file('c', 'Pending'), file('d', 'Failed'), file('e', 'Ready'), file('f', 'None'), file('g', 'Cancelled')];
  assert.deepEqual(summaryStatusCounts(files), { total: 7, ready: 1, summarising: 1, queued: 2, failed: 1, notSummarised: 2 });
  assert.deepEqual(summaryStatusBadges(summary(...files), false).map((badge) => badge.label),
    ['1 summarising', '2 queued', '1 failed', '1/7 summarised', '2 not summarised']);
  assert.deepEqual(summaryStatusBadges(summary(file('a', 'Ready'), file('b', 'Ready')), false).map((badge) => [badge.label, badge.tone]),
    [['All 2 files summarised', 'success']]);
  assert.equal(summaryStatusBadges(summary(file('a', 'Ready')), false)[0].label, '1 file summarised');
  assert.equal(summaryStatusBadges(summary(), false)[0].label, 'No files');
  assert.equal(summaryStatusBadges(null, false)[0].label, 'Checking…');
  assert.equal(summaryStatusBadges(null, true)[0].tone, 'danger');
});

test('the card starts closed and never ticks files for the reviewer', () => {
  const card = aiSummarySources();
  assert.match(card, /const \[expanded, setExpanded\] = useState\(false\)/);
  assert.doesNotMatch(card, /defaultFileSelection|selectionTouchedRef/);
});

test('a file with sentences addressed to the AI says so, and only then', () => {
  assert.equal(ignoredInstructionsMessage(0), null);
  assert.equal(ignoredInstructionsMessage(undefined), null);
  assert.match(ignoredInstructionsMessage(1) ?? '', /1 sentence addressed to the AI/);
  assert.match(ignoredInstructionsMessage(3) ?? '', /3 sentences addressed to the AI/);
  const card = aiSummarySources();
  // Both the quick view and the finished summary show it.
  assert.match(card, /<IgnoredInstructionsNote count=\{file\.preview\.ignoredInstructions\}/);
  assert.match(card, /<IgnoredInstructionsNote count=\{content\.ignoredInstructions\}/);
});

test('a quote says where it is, can be pasted into a comment, and the time left is a rough guide', () => {
  assert.equal(pageLabel('Deck.pptx', 7), 'slide 7');
  assert.equal(pageLabel('Report.pdf', 12), 'page 12');
  assert.equal(pageLabel('Deck.pptx', 7), 'slide 7');
  assert.equal(quoteForFeedback('Doanh thu 1,4 tỷ đồng.', 'Report.pdf', 12), '“Doanh thu 1,4 tỷ đồng.” — Report.pdf, page 12');
  assert.equal(quoteForFeedback('Doanh thu 1,4 tỷ đồng.', 'Report.docx'), '“Doanh thu 1,4 tỷ đồng.” — Report.docx');
  assert.equal(quoteForFeedback('Câu trích.'), '“Câu trích.”');
  assert.equal(summaryEtaText(summary(file('a', 'Ready'))), null);
  assert.equal(summaryEtaText(summary(file('a', 'Processing'))), 'Almost done');
  assert.equal(summaryEtaText(summary(file('a', 'Processing'), file('b', 'Pending'), file('c', 'Pending'))), 'About 3 min left');
  const card = aiSummarySources();
  // The quote actions are wired from the checkpoint screen: open at the page, and put a quote into the comment box.
  const panel = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointPanel.tsx', import.meta.url), 'utf8');
  assert.match(panel, /onOpenFile=\{\(file, page\) =>/);
  assert.match(panel, /onQuote=\{\(text\) =>/);
  assert.match(panel, /initialPage=\{previewPage\}/);
  assert.match(card, /page=\{item\.evidencePage\}/);
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
  assert.equal(fileStatusView(file('a', 'Pending', { queueAhead: 1, isAutomatic: true })).label, 'Auto-queued · 1 ahead');
  assert.equal(fileStatusView(file('a', 'Ready')).tone, 'success');
  for (const view of Object.values(summaryFindingView)) assert.doesNotMatch(view.label, /pass|fail/i);
  assert.match(summaryErrorMessage('WORKSPACE_SUMMARY_QUEUE_FULL'), /few minutes/);
  assert.match(summaryErrorMessage('AUTH_RATE_LIMITED'), /last hour/);
  // Each cause of a failed file has its own message, and the temporary ones point to Retry.
  for (const code of ['WORKSPACE_SUMMARY_MODEL_UNREACHABLE', 'WORKSPACE_SUMMARY_MODEL_TIMEOUT', 'WORKSPACE_SUMMARY_STORAGE_UNAVAILABLE'])
    assert.match(summaryErrorMessage(code), /Retry/);
  assert.notEqual(summaryErrorMessage('WORKSPACE_SUMMARY_MODEL_BAD_ANSWER'), summaryErrorMessage('WORKSPACE_SUMMARY_UNAVAILABLE'));
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
  // Two summarised files that disagree on a figure are reported once for the whole checkpoint, each value with its file.
  assert.equal(ready.data.conflicts?.length, 1);
  assert.deepEqual(ready.data.conflicts?.[0].values.map((item) => item.source), ready.data.files.map((item) => item.name));
  const card = aiSummarySources();
  assert.match(card, /<FigureConflicts conflicts=\{conflictsAcrossFiles\} acrossFiles/);

  assert.deepEqual(await errorCode(checkpointSummaryApi.request(TEAM_ID, 1, [])), { status: 400, code: 'WORKSPACE_VALIDATION_ERROR' });
  assert.deepEqual(await errorCode(checkpointSummaryApi.request(TEAM_ID, 1, ['not-a-file'])), { status: 404, code: 'WORKSPACE_NOT_FOUND' });

  await login('se200001@fpt.edu.vn');
  assert.deepEqual(await errorCode(checkpointSummaryApi.get(TEAM_ID, 1)), { status: 403, code: 'WORKSPACE_ACCESS_DENIED' });
  assert.deepEqual(await errorCode(checkpointSummaryApi.request(TEAM_ID, 1, [first.fileId])), { status: 403, code: 'WORKSPACE_ACCESS_DENIED' });
  assert.deepEqual(await errorCode(checkpointSummaryApi.cancel(TEAM_ID, 1)), { status: 403, code: 'WORKSPACE_ACCESS_DENIED' });
});

test('the quick view can be opened while the AI is still working', () => {
  const preview = { highlights: ['Câu quan trọng.'], requirements: [] };
  assert.equal(canOpenFile(file('a', 'Processing', { preview })), true);
  assert.equal(canOpenFile(file('b', 'Pending', { preview })), true);
  assert.equal(canOpenFile(file('c', 'Pending')), false, 'nothing could be read yet');
  assert.equal(canOpenFile(file('d', 'Ready')), true);
  assert.equal(canOpenFile(file('e', 'None')), false);
});

test('mock: a summarised file lists figures that disagree, with a quote for each value', async (t) => {
  resetMockState();
  await login('giang.lecturer@ehub.local');
  let now = Date.now() + 1_800_000;
  t.mock.method(Date, 'now', () => now);
  const fileId = 'mock-conflict-file';
  getMockState().checkpointFiles[`${TEAM_ID}:1`] = [{ _id: fileId, versionNumber: 4, originalName: 'Deck.pdf', fileType: 'pdf',
    fileSize: 100, uploadedAt: new Date(now).toISOString(), uploadedBy: { _id: '', name: 'Team leader' } }];

  await checkpointSummaryApi.request(TEAM_ID, 1, [fileId]);
  now += 60_000;
  const content = (await checkpointSummaryApi.get(TEAM_ID, 1)).data.files[0].content;
  assert.equal(content?.conflicts[0].subject, 'Conversion rate');
  assert.deepEqual(content?.conflicts[0].values.map((item) => item.value), ['15%', '30%']);
  assert.ok(content?.conflicts[0].values.every((item) => item.quote.length > 0));
  assert.deepEqual(content?.statusItems.map((item) => item.status), ['Done', 'Pending', 'InProgress']);
  assert.match(content?.statusItems[1].request ?? '', /trao đổi giữa mentor/);
});

test('live criteria are not searched, so "nothing found" refers only to what a document can show', () => {
  const rows = [
    { index: 0, finding: 'NotFound' as const, weight: 30 },
    { index: 1, finding: 'Live' as const, weight: 15 },
    { index: 2, finding: 'Product' as const, weight: 25 },
    { index: 3, finding: 'NotFound' as const, weight: 30 },
  ];
  assert.equal(isNothingFound(rows), true, 'the live and product rows do not count as missing');
  assert.equal(isNothingFound(rows.slice(1, 3)), false, 'nothing was searched');
  assert.deepEqual(rows.filter(isSearchable).map((row) => row.index), [0, 3]);
  assert.equal(summaryFindingView.Live.label, 'Assessed live');
  assert.match(summaryFindingView.Live.hint, /assessed live/);
  // A whole-file criterion ("Startup Idea Clarity") is never reported as missing, and is listed with the other judged-elsewhere rows.
  const withOverall = [...rows, { index: 4, finding: 'Overall' as const, weight: 30 }];
  assert.equal(isSearchable(withOverall[4]), false);
  assert.equal(isNothingFound(withOverall), true);
  assert.equal(summaryFindingView.Overall.label, 'Judged on the whole file');
});

test('numbers follow the order the lists show, not the position in the checklist', () => {
  // 8 requirements (positions 0-7, no weight), then 4 weighted criteria and 2 live ones (positions 8-13), as one checkpoint stores them.
  const rows = [
    ...Array.from({ length: 8 }, (_, index) => ({ index, finding: (index % 2 ? 'NotFound' : 'Related') as 'Related' | 'NotFound', weight: null })),
    ...[8, 9, 10, 11].map((index) => ({ index, finding: 'Related' as const, weight: 20 })),
    ...[12, 13].map((index) => ({ index, finding: 'Live' as const, weight: 10 })),
  ];
  const numbers = displayNumbers(rows);
  // Weighted criteria come first and are numbered 1-4, then the requirements 5-12, then the rows judged elsewhere.
  assert.deepEqual([8, 9, 10, 11].map((index) => numbers.get(index)), [1, 2, 3, 4]);
  assert.deepEqual([0, 1, 2, 3, 4, 5, 6, 7].map((index) => numbers.get(index)), [5, 6, 7, 8, 9, 10, 11, 12]);
  assert.deepEqual([12, 13].map((index) => numbers.get(index)), [13, 14]);
  assert.equal(new Set(numbers.values()).size, rows.length, 'every number is used once');
  const card = aiSummarySources();
  assert.doesNotMatch(card, /index \+ 1/, 'the stored index is never shown');
});

test('criteria are listed heaviest first and keep their order when weights are equal', () => {
  const rows = [
    { index: 0, weight: 20 }, { index: 1, weight: 35 }, { index: 2, weight: 20 }, { index: 3, weight: null }, { index: 4, weight: 35 },
  ];
  assert.deepEqual(sortByWeight(rows).map((row) => row.index), [1, 4, 0, 2, 3]);
  assert.deepEqual(rows.map((row) => row.index), [0, 1, 2, 3, 4], 'the input is not reordered');
});

test('status badges use the semantic tones', () => {
  assert.equal(statusItemView.Done.tone, 'success');
  assert.equal(statusItemView.InProgress.tone, 'info');
  assert.equal(statusItemView.Pending.tone, 'warning');
});

test('a checklist where nothing is found collapses to one line', () => {
  const item = (finding: 'Related' | 'NotFound' | 'NotChecked') => ({ finding });
  assert.equal(isNothingFound([item('NotFound'), item('NotFound')]), true);
  assert.equal(isNothingFound([item('NotFound'), item('Related')]), false);
  assert.equal(isNothingFound([item('NotChecked')]), false, 'not read yet is not the same as not found');
  assert.equal(isNothingFound([]), false);
});

test('the card is hidden until the server says AI summaries are on', () => {
  assert.equal(isSummaryFeatureVisible(null), false, 'nothing is drawn before the first answer');
  assert.equal(isSummaryFeatureVisible({ ...summary(), enabled: false }), false);
  assert.equal(isSummaryFeatureVisible(summary()), true);
});

test('mock: an environment with AI summaries switched off returns nothing and refuses requests', async () => {
  resetMockState();
  await login('giang.lecturer@ehub.local');
  getMockState().checkpointSummaryDisabled = true;
  try {
    const read = await checkpointSummaryApi.get(TEAM_ID, 1);
    assert.equal(read.data.enabled, false);
    assert.deepEqual(read.data.files, []);
    assert.deepEqual(await errorCode(checkpointSummaryApi.request(TEAM_ID, 1, ['any-file'])), { status: 503, code: 'WORKSPACE_SUMMARY_DISABLED' });
    assert.deepEqual(await errorCode(checkpointSummaryApi.cancel(TEAM_ID, 1)), { status: 503, code: 'WORKSPACE_SUMMARY_DISABLED' });
  } finally {
    getMockState().checkpointSummaryDisabled = false;
  }
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
  const card = aiSummarySources();
  assert.match(card, /window\.clearTimeout\(timer\)/);
  assert.match(card, /actionControllerRef\.current\?\.abort\(\)/);
  assert.match(card, /if \(busyRef\.current\) return;/);
  assert.match(card, /variant="danger-outline" size="sm" icon=\{X\}/);
  assert.doesNotMatch(card, /\bSquare\b/);
  // The model name is kept for audit but not shown to reviewers.
  assert.doesNotMatch(card, /\.model\b/);
  assert.doesNotMatch(card, /dangerouslySetInnerHTML/);
});

import { hasReplacedFiles, versionChangeCounts, versionChangeHeadline } from '../src/utils/checkpointSummary.ts';

test('version changes: only files uploaded more than once can be compared, and the headline says what a reviewer should know', () => {
  assert.equal(hasReplacedFiles([{ name: 'Plan.pdf' }, { name: 'Slides.pptx' }]), false);
  assert.equal(hasReplacedFiles([{ name: 'Plan.pdf' }, { name: ' plan.PDF' }]), true);
  assert.equal(versionChangeCounts({ changedCount: 2, addedCount: 1, removedCount: 0 }), '2 sentences edited · 1 added');
  assert.equal(versionChangeCounts({ changedCount: 0, addedCount: 0, removedCount: 0 }), '');
  const base = { fromVersion: 1, toVersion: 2, unchangedPercent: 80 };
  assert.match(versionChangeHeadline({ ...base, status: 'Compared', items: [{ figuresChanged: true }, { figuresChanged: false }] }), /80% of the content is unchanged.*1 sentence with changed figures/);
  assert.doesNotMatch(versionChangeHeadline({ ...base, status: 'Compared', items: [] }), /figures/);
  assert.match(versionChangeHeadline({ ...base, status: 'Unchanged', items: [] }), /v2 is the same as v1/);
  assert.match(versionChangeHeadline({ ...base, status: 'Unreadable', items: [] }), /could not be read/);
  assert.match(versionChangeHeadline({ ...base, status: 'NewFile', items: [] }), /New file/);
});

test('mock: a file uploaded again is compared with its previous version, and nothing exists when AI summaries are off', async () => {
  resetMockState();
  await login('giang.lecturer@ehub.local');
  const upload = (id: string, name: string, version: number) => ({
    _id: id, versionNumber: version, originalName: name, fileType: 'pdf', fileSize: 10,
    uploadedAt: new Date().toISOString(), uploadedBy: { _id: '', name: 'Team leader' },
  });
  getMockState().checkpointFiles[`${TEAM_ID}:1`] = [upload('f1', 'Plan.pdf', 1), upload('f2', 'Plan.pdf', 2), upload('f3', 'Slides.pdf', 3)] as never;

  const result = (await checkpointSummaryApi.changes(TEAM_ID, 1)).data;
  const plan = result.files.find((item) => item.fileName === 'Plan.pdf')!;
  assert.deepEqual([plan.status, plan.fileId, plan.fromVersion, plan.toVersion], ['Compared', 'f2', 1, 2]);
  assert.ok(plan.items.some((item) => item.kind === 'Changed' && item.figuresChanged && item.before));
  assert.equal(result.files.find((item) => item.fileName === 'Slides.pdf')?.status, 'NewFile');

  getMockState().checkpointSummaryDisabled = true;
  await assert.rejects(checkpointSummaryApi.changes(TEAM_ID, 1), (error: { response?: { status: number } }) => error.response?.status === 503);
  getMockState().checkpointSummaryDisabled = false;

  await login('se200001@fpt.edu.vn');
  await assert.rejects(checkpointSummaryApi.changes(TEAM_ID, 1), (error: { response?: { status: number } }) => error.response?.status === 403);
});

test('the version comparison is a card block that appears only with AI summaries on and a replaced file, and loads on request', () => {
  const card = aiSummarySources();
  assert.match(card, /summary\?\.enabled === true && hasReplacedFiles\(/);
  assert.match(card, /\{replacedFiles && \(\s*<CheckpointVersionChangesBlock/);
  const block = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointVersionChanges.tsx', import.meta.url), 'utf8');
  // Nothing is fetched on render; the reviewer asks, and an unfinished request is dropped on unmount or when the checkpoint changes.
  assert.doesNotMatch(block, /useEffect\(\(\) => \{[^}]*checkpointSummaryApi\.changes/);
  assert.match(block, /onClick=\{load\}/);
  assert.match(block, /controllerRef\.current\?\.abort\(\)/);
});

import { comparedCheckpointsText, figureHistoryHasNews } from '../src/utils/checkpointSummary.ts';

test('figure history: names the checkpoints compared and says when there is something to report', () => {
  assert.equal(comparedCheckpointsText([1, 2]), 'CP1, CP2');
  assert.equal(figureHistoryHasNews(null), false);
  assert.equal(figureHistoryHasNews({ conflicts: [], uncheckedFiles: [] }), false);
  assert.equal(figureHistoryHasNews({ conflicts: [], uncheckedFiles: ['CP1 · a.pdf'] }), true);
  assert.equal(figureHistoryHasNews({ conflicts: [{}], uncheckedFiles: [] }), true);
});

test('mock: figures are compared with earlier checkpoints for reviewers, and nothing exists when AI summaries are off', async () => {
  resetMockState();
  await login('giang.lecturer@ehub.local');
  const upload = (id: string) => ({
    _id: id, versionNumber: 1, originalName: `${id}.pdf`, fileType: 'pdf', fileSize: 10,
    uploadedAt: new Date().toISOString(), uploadedBy: { _id: '', name: 'Team leader' },
  });
  getMockState().checkpointFiles[`${TEAM_ID}:1`] = [upload('a')] as never;

  // Nothing earlier than checkpoint 1: nothing to compare with.
  const first = (await checkpointSummaryApi.figureHistory(TEAM_ID, 1)).data;
  assert.deepEqual([first.comparedCheckpoints, first.conflicts.length], [[], 0]);
  const later = (await checkpointSummaryApi.figureHistory(TEAM_ID, 2)).data;
  assert.deepEqual(later.comparedCheckpoints, [1]);
  assert.equal(later.conflicts[0].values.some((value) => value.source?.startsWith('CP1')), true);

  getMockState().checkpointSummaryDisabled = true;
  await assert.rejects(checkpointSummaryApi.figureHistory(TEAM_ID, 2), (error: { response?: { status: number } }) => error.response?.status === 503);
  getMockState().checkpointSummaryDisabled = false;
  await login('se200001@fpt.edu.vn');
  await assert.rejects(checkpointSummaryApi.figureHistory(TEAM_ID, 2), (error: { response?: { status: number } }) => error.response?.status === 403);
});

test('the figure history sits in the card only with AI summaries on, loads by itself and drops its request on unmount', () => {
  const card = aiSummarySources();
  // The card reads it, and only while it is open and AI summaries are on, so it can tell whether "Needs attention" has anything to say.
  assert.match(card, /useFigureHistory\(teamId, checkpointNumber, expanded && summary\?\.enabled === true\)/);
  assert.match(card, /checkpointSummaryApi\.figureHistory\(teamId, checkpointNumber, controller\.signal\)/);
  assert.match(card, /if \(!enabled \|\| checkpointNumber <= 1\) return undefined;/);
  assert.match(card, /return \(\) => controller\.abort\(\);\s+\}, \[teamId, checkpointNumber, enabled\]\);/);
});

import { canRateSummary, nextFeedback, withFeedback, SUMMARY_FEEDBACK_REASONS } from '../src/utils/checkpointSummary.ts';

test('feedback: the buttons give, replace and take back a verdict', () => {
  // Nothing given yet.
  assert.deepEqual(nextFeedback(null, 'Helpful'), { rating: 'Helpful', reason: null });
  assert.deepEqual(nextFeedback(undefined, 'NotHelpful'), { rating: 'NotHelpful', reason: null });
  // The same button again takes the verdict back; the other button replaces it and drops the reason.
  assert.equal(nextFeedback({ rating: 'Helpful', reason: null }, 'Helpful'), null);
  assert.equal(nextFeedback({ rating: 'NotHelpful', reason: 'Other' }, 'NotHelpful'), null);
  assert.deepEqual(nextFeedback({ rating: 'Helpful', reason: null }, 'NotHelpful'), { rating: 'NotHelpful', reason: null });
  assert.deepEqual(nextFeedback({ rating: 'NotHelpful', reason: 'Inaccurate' }, 'Helpful'), { rating: 'Helpful', reason: null });
  // A reason chosen after "not useful" keeps the verdict; choosing it again drops only the reason.
  assert.deepEqual(nextFeedback({ rating: 'NotHelpful', reason: null }, 'NotHelpful', 'WrongEvidence'), { rating: 'NotHelpful', reason: 'WrongEvidence' });
  assert.deepEqual(nextFeedback({ rating: 'NotHelpful', reason: 'WrongEvidence' }, 'NotHelpful', 'WrongEvidence'), { rating: 'NotHelpful', reason: null });
  assert.deepEqual(nextFeedback({ rating: 'NotHelpful', reason: 'WrongEvidence' }, 'NotHelpful', 'Other'), { rating: 'NotHelpful', reason: 'Other' });
  // The reasons are the server's fixed list, in Vietnamese, and there is no free text.
  assert.deepEqual(SUMMARY_FEEDBACK_REASONS.map((item) => item.value), ['Inaccurate', 'MissingKeyPoint', 'WrongEvidence', 'Other']);
});

test('feedback: only a finished summary can be rated and only the chosen file changes', () => {
  const content = { overview: 'x', keyPoints: [], highlights: [], highlightPages: [], requirements: [], questions: [], conflicts: [], statusItems: [], littleText: false };
  assert.equal(canRateSummary(file('a', 'Ready', { content })), true);
  for (const status of ['None', 'Pending', 'Processing', 'Failed', 'Cancelled'] as const) assert.equal(canRateSummary(file('b', status)), false, status);
  assert.equal(canRateSummary(file('c', 'Ready')), false, 'a ready file without its content has nothing to judge');

  const before = summary(file('a', 'Ready', { content }), file('b', 'Ready', { content, myFeedback: { rating: 'Helpful', reason: null } }));
  const after = withFeedback(before, 'a', { rating: 'NotHelpful', reason: 'Inaccurate' });
  assert.deepEqual(after.files[0].myFeedback, { rating: 'NotHelpful', reason: 'Inaccurate' });
  assert.deepEqual(after.files[1].myFeedback, { rating: 'Helpful', reason: null });
  assert.equal(before.files[0].myFeedback, undefined, 'the earlier summary is not changed');
  assert.equal(withFeedback(after, 'a', null).files[0].myFeedback, null);
});

test('late feedback responses never attach an old verdict to a regenerated result', () => {
  const latest = summary(file('a', 'Ready', { generationId: 'new-generation', myFeedback: null }));
  assert.equal(withFeedback(latest, 'a', { rating: 'Helpful', reason: null }, 'old-generation').files[0].myFeedback, null);
  assert.deepEqual(withFeedback(latest, 'a', { rating: 'Helpful', reason: null }, 'new-generation').files[0].myFeedback,
    { rating: 'Helpful', reason: null });
});

test('mock: a reviewer rates a finished summary, changes the verdict and takes it back; the rest is refused', async (t) => {
  resetMockState();
  await login('giang.lecturer@ehub.local');
  let now = Date.now() + 7_200_000;
  t.mock.method(Date, 'now', () => now);
  const fileId = 'mock-feedback-file';
  getMockState().checkpointFiles[`${TEAM_ID}:1`] = [{ _id: fileId, versionNumber: 4, originalName: 'Plan.docx', fileType: 'docx',
    fileSize: 100, uploadedAt: new Date(now).toISOString(), uploadedBy: { _id: '', name: 'Team leader' } }];

  await checkpointSummaryApi.request(TEAM_ID, 1, [fileId]);
  // A summary that is not finished cannot be rated.
  assert.deepEqual(await errorCode(checkpointSummaryApi.setFeedback(TEAM_ID, 1, fileId, { rating: 'Helpful', reason: null })), { status: 404, code: 'WORKSPACE_NOT_FOUND' });

  now += 7_000;
  assert.equal((await checkpointSummaryApi.get(TEAM_ID, 1)).data.files[0].myFeedback, null);
  const generationId = (await checkpointSummaryApi.get(TEAM_ID, 1)).data.files[0].generationId;
  assert.ok(generationId);
  assert.deepEqual(await errorCode(checkpointSummaryApi.setFeedback(TEAM_ID, 1, fileId,
    { rating: 'Helpful', reason: null, generationId: crypto.randomUUID() })), { status: 409, code: 'WORKSPACE_CONCURRENCY_CONFLICT' });
  assert.equal((await checkpointSummaryApi.get(TEAM_ID, 1)).data.files[0].myFeedback, null);
  await checkpointSummaryApi.setFeedback(TEAM_ID, 1, fileId, { rating: 'Helpful', reason: null });
  assert.deepEqual((await checkpointSummaryApi.get(TEAM_ID, 1)).data.files[0].myFeedback, { rating: 'Helpful', reason: null });
  assert.deepEqual(await errorCode(checkpointSummaryApi.clearFeedback(TEAM_ID, 1, fileId, crypto.randomUUID())),
    { status: 409, code: 'WORKSPACE_CONCURRENCY_CONFLICT' });
  assert.deepEqual((await checkpointSummaryApi.get(TEAM_ID, 1)).data.files[0].myFeedback, { rating: 'Helpful', reason: null });
  await checkpointSummaryApi.setFeedback(TEAM_ID, 1, fileId, { rating: 'NotHelpful', reason: 'MissingKeyPoint' });
  assert.deepEqual((await checkpointSummaryApi.get(TEAM_ID, 1)).data.files[0].myFeedback, { rating: 'NotHelpful', reason: 'MissingKeyPoint' });

  // Only the fixed ratings and reasons are accepted, and a reason goes only with "not useful".
  const invalid = { status: 400, code: 'WORKSPACE_VALIDATION_ERROR' };
  assert.deepEqual(await errorCode(checkpointSummaryApi.setFeedback(TEAM_ID, 1, fileId, { rating: 'Great' as never, reason: null })), invalid);
  assert.deepEqual(await errorCode(checkpointSummaryApi.setFeedback(TEAM_ID, 1, fileId, { rating: 'Helpful', reason: 'Other' })), invalid);
  assert.deepEqual(await errorCode(checkpointSummaryApi.setFeedback(TEAM_ID, 1, fileId, { rating: 'NotHelpful', reason: 'Because' as never })), invalid);

  await checkpointSummaryApi.clearFeedback(TEAM_ID, 1, fileId);
  assert.equal((await checkpointSummaryApi.get(TEAM_ID, 1)).data.files[0].myFeedback, null);

  getMockState().checkpointSummaryDisabled = true;
  try {
    assert.deepEqual(await errorCode(checkpointSummaryApi.setFeedback(TEAM_ID, 1, fileId, { rating: 'Helpful', reason: null })), { status: 503, code: 'WORKSPACE_SUMMARY_DISABLED' });
    assert.deepEqual(await errorCode(checkpointSummaryApi.clearFeedback(TEAM_ID, 1, fileId)), { status: 503, code: 'WORKSPACE_SUMMARY_DISABLED' });
  } finally {
    getMockState().checkpointSummaryDisabled = false;
  }

  await login('se200001@fpt.edu.vn');
  assert.deepEqual(await errorCode(checkpointSummaryApi.setFeedback(TEAM_ID, 1, fileId, { rating: 'Helpful', reason: null })), { status: 403, code: 'WORKSPACE_ACCESS_DENIED' });
  assert.deepEqual(await errorCode(checkpointSummaryApi.clearFeedback(TEAM_ID, 1, fileId)), { status: 403, code: 'WORKSPACE_ACCESS_DENIED' });
});

test('feedback: the card saves before it shows the verdict, locks double clicks and cleans up', () => {
  const card = aiSummarySources();
  assert.match(card, /if \(ratingRef\.current\) return;/);
  assert.match(card, /ratingControllerRef\.current\?\.abort\(\)/);
  // The verdict is applied to the screen only after the request succeeded.
  assert.match(card, /await checkpointSummaryApi\.setFeedback[\s\S]*?setSummary\(\(current\) => \(current \? withFeedback/);
  assert.match(card, /aria-pressed=\{current\?\.rating === 'Helpful'\}/);
  assert.match(card, /className=\{buttonClass\(/);
  assert.doesNotMatch(card, /dangerouslySetInnerHTML/);
});

test('quotes only offer to open the file; the Feedback button belongs to the suggested questions', () => {
  const sources = aiSummarySources();
  // Only the review brief (a whole-checkpoint action) copies text; quotes and questions do not.
  assert.doesNotMatch(sources.replace(readFileSync(new URL('ai-summary/ReviewBrief.tsx', checkpointsFolder), 'utf8'), ''), /navigator\.clipboard|icon=\{Copy\}/);
  assert.match(sources, /content\.questions\.map\(\(question\) => \(/);
  assert.match(sources, /onQuote\(question\)/);
});

import { summariseEvidence } from '../src/utils/checkpointSummary.ts';
import { plural, summaryText } from '../src/utils/summaryText.ts';

const item = (index: number, finding: 'Related' | 'NotFound' | 'NotChecked' | 'Live' | 'Product' | 'Overall', kind = 'Criterion', weight: number | null = null) =>
  ({ index, kind, finding, weight });

test('evidence: found items first by weight, missing ones in one group, judged-elsewhere ones apart', () => {
  const view = summariseEvidence([
    item(0, 'NotFound', 'Requirement'), item(1, 'Related', 'Criterion', 20), item(2, 'Live', 'Criterion', 10), item(3, 'Related', 'Criterion', 30),
    item(4, 'NotFound', 'Requirement'), item(5, 'NotChecked', 'Requirement'), item(6, 'Product', 'Criterion', 15),
  ]);
  assert.deepEqual(view.found.map((row) => row.index), [3, 1], 'heaviest first');
  assert.deepEqual(view.notFound.map((row) => row.index), [0, 4], 'one group, not one row each');
  assert.deepEqual(view.notChecked.map((row) => row.index), [5]);
  assert.deepEqual(view.others.map((row) => row.index), [6, 2], 'live and product criteria are not "missing"');
  assert.equal(view.searchable.length, 5);
  assert.equal(view.nothingFound, false);
  assert.equal(summaryText.requirements.foundOf(view.found.length, view.searchable.length), '2 of 5 found in the files');
});

test('evidence: kind headings appear only when both kinds are among the found items', () => {
  assert.equal(summariseEvidence([item(0, 'Related', 'Criterion'), item(1, 'Related', 'Requirement')]).groupedByKind, true);
  assert.equal(summariseEvidence([item(0, 'Related', 'Criterion'), item(1, 'Related', 'Criterion'), item(2, 'NotFound', 'Requirement')]).groupedByKind, false);
  assert.equal(summariseEvidence([]).groupedByKind, false);
  assert.equal(summariseEvidence([item(0, 'NotFound'), item(1, 'NotFound', 'Requirement')]).nothingFound, true);
});

test('the interface text is English; only what the AI wrote and the files said may be in the language of the submission', () => {
  const vietnamese = /[àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ]/i;
  const checkpoints = new URL('../src/components/workspace/checkpoints/', import.meta.url);
  const files = [
    ...['CheckpointAiSummaryCard.tsx', 'CheckpointVersionChanges.tsx'].map((name) => new URL(name, checkpoints)),
    ...readdirSync(new URL('ai-summary/', checkpoints)).map((name) => new URL(`ai-summary/${name}`, checkpoints)),
    new URL('../src/utils/summaryText.ts', import.meta.url),
    new URL('../src/utils/checkpointSummary.ts', import.meta.url),
  ];
  for (const url of files) assert.doesNotMatch(readFileSync(url, 'utf8'), vietnamese, `${url.pathname.split('/').pop()} has Vietnamese interface text`);
  assert.equal(plural(1, 'file'), 'file');
  assert.equal(plural(2, 'file'), 'files');
});

test('the card lays out files, then what needs attention, then the requirements, and shows one list for a one-file checkpoint', () => {
  const card = readFileSync(new URL('CheckpointAiSummaryCard.tsx', checkpointsFolder), 'utf8');
  const at = (marker: string) => card.indexOf(marker);
  assert.ok(at('text.filesLabel') > 0 && at('text.filesLabel') < at('text.needsAttention'));
  assert.ok(at('text.needsAttention') < at('text.requirementsTitle'));
  assert.ok(at('text.requirementsTitle') < at('<CheckpointVersionChangesBlock'));
  assert.ok(at('<CheckpointVersionChangesBlock') < at('text.linksTitle'));
  // One file: its summary and its requirement list open by themselves, and the overview leaves the (identical) combined list out.
  assert.match(card, /const singleFile = files\.length === 1;/);
  assert.match(card, /autoOpen=\{singleFile\}/);
  assert.match(card, /requirementsOpen=\{singleFile\}/);
  assert.match(card, /const overviewRequirements = singleFile \? \[\] :/);
  // Everything about the checkpoint as a whole is behind one closed "Checkpoint overview"; the card itself is about the files.
  assert.match(card, /const \[overviewOpen, setOverviewOpen\] = useState\(false\);/);
  assert.match(card, /aria-expanded=\{overviewOpen\}/);
  assert.match(card, /\{overviewOpen && \(\s*<div id=\{overviewId\}/);
  // ...but a warning about figures that disagree is never hidden in it.
  assert.ok(at('text.needsAttention') < at('text.overviewTitle'));
  assert.ok(at('text.overviewTitle') < at('text.requirementsTitle'));
  // Both lists are drawn by one component.
  assert.equal((card.match(/<RequirementEvidence /g) ?? []).length, 1);
  const row = readFileSync(new URL('ai-summary/FileSummaryRow.tsx', checkpointsFolder), 'utf8');
  assert.match(row, /<RequirementEvidence items=\{requirements\} scope="file"/);
});

import { overviewHeadline } from '../src/utils/checkpointSummary.ts';

test('the closed overview says what is in it, so a reviewer knows whether to open it', () => {
  const requirement = (index: number, finding: 'Related' | 'NotFound' | 'Live') =>
    ({ index, kind: 'Criterion' as const, title: `Item ${index}`, finding, evidence: null, source: null });
  const withLinks = (count: number) => Array.from({ length: count }, (_, position) => ({ name: `Link ${position}`, domain: 'example.com' }));
  const base = summary();

  assert.deepEqual(overviewHeadline(base, false), [], 'nothing to say');
  assert.deepEqual(
    overviewHeadline({ ...base, requirements: [requirement(0, 'Related'), requirement(1, 'NotFound'), requirement(2, 'Live')], links: withLinks(2) }, true),
    ['Requirements 1 of 2 found', 'Files re-uploaded', '2 links'],
    'a criterion judged live is not counted as missing',
  );
  assert.deepEqual(overviewHeadline({ ...base, links: withLinks(1) }, false), ['1 link']);
  assert.deepEqual(overviewHeadline({ ...base, requirements: [requirement(0, 'Live')] }, false), [], 'only live criteria: no count to give');
});

import { buildFeedbackDraft, buildReviewBrief, requirementCoverage, summaryAttentionItems } from '../src/utils/checkpointSummary.ts';
import type { FileSummaryContent } from '../src/types/checkpointSummary.ts';

const content = (overrides: Partial<FileSummaryContent> = {}): FileSummaryContent => ({
  overview: 'A platform that connects students with campus services.', keyPoints: ['Interviewed 18 students.', 'Charges an 8% commission.'],
  highlights: [], requirements: [], questions: [], conflicts: [], statusItems: [], littleText: false, ...overrides,
});

const checklistItem = (index: number, finding: CheckpointSummary['requirements'][number]['finding'], title = `Item ${index}`): CheckpointSummary['requirements'][number] =>
  ({ index, kind: 'Criterion', title, finding, evidence: null, source: null });

test('review brief: coverage counts what a file can show and keeps live criteria apart', () => {
  const coverage = requirementCoverage([checklistItem(0, 'Related'), checklistItem(1, 'Related'), checklistItem(2, 'NotFound'), checklistItem(3, 'NotChecked'), checklistItem(4, 'Live'), checklistItem(5, 'Overall')]);
  assert.deepEqual(coverage, { total: 6, searchable: 4, found: 2, notFound: 1, notChecked: 1, elsewhere: 2 });
  assert.deepEqual(requirementCoverage([]), { total: 0, searchable: 0, found: 0, notFound: 0, notChecked: 0, elsewhere: 0 });
});

test('review brief: flags are counts of what the files showed, worst first, and a clean summary has none', () => {
  assert.deepEqual(summaryAttentionItems(null), []);
  assert.deepEqual(summaryAttentionItems(summary(file('a', 'Ready', { content: content() }))), []);

  const flagged = summary(
    file('a', 'Ready', { content: content({
      conflicts: [{ subject: 'Conversion rate', values: [{ value: '15%', quote: 'q1' }, { value: '30%', quote: 'q2' }] }],
      statusItems: [{ status: 'Done', text: 'x' }, { status: 'Pending', text: 'y' }, { status: 'InProgress', text: 'z' }],
      ignoredInstructions: 2, littleText: true }) }),
    file('b', 'Ready', { needsRefresh: true, content: content() }),
    file('c', 'Failed'),
    file('d', 'None'),
  );
  flagged.conflicts = [{ subject: 'Sample size', values: [{ value: '51', quote: 'q', source: 'a.pdf' }, { value: '52', quote: 'q', source: 'b.pdf' }] }];
  const items = summaryAttentionItems(flagged);
  assert.deepEqual(items.map((entry) => entry.key), ['failed', 'stale', 'figures', 'instructions', 'open', 'images', 'partial']);
  assert.equal(items[0].tone, 'danger');
  assert.match(items.find((entry) => entry.key === 'figures')?.text ?? '', /^2 figures are stated with different values/);
  assert.match(items.find((entry) => entry.key === 'open')?.text ?? '', /^2 items are reported as pending or in progress/);
  assert.match(items.find((entry) => entry.key === 'partial')?.text ?? '', /^1 file is not summarised yet/);
  // A file still being written is read through its quick view, so its flags show before the AI is done.
  const preview = { highlights: [], requirements: [], conflicts: [], statusItems: [], ignoredInstructions: 1, littleText: false };
  assert.deepEqual(summaryAttentionItems(summary(file('p', 'Processing', { preview }))).map((entry) => entry.key), ['instructions']);
});

test('review brief: the text names each summarised file, the coverage and what to check, and says it came from an AI', () => {
  const ready = summary(
    file('a', 'Ready', { name: 'Plan.pdf', versionNumber: 2, content: content({ statusItems: [{ status: 'Pending', text: 'Add chat.', request: '1. Add chat' }, { status: 'Done', text: 'Archive built.' }] }) }),
    file('b', 'None'),
  );
  ready.requirements = [checklistItem(0, 'Related'), checklistItem(1, 'NotFound', 'Competitors'), checklistItem(2, 'NotFound', 'Market size')];
  const brief = buildReviewBrief(ready);
  assert.match(brief, /^AI review brief · Checkpoint 1\n/);
  assert.match(brief, /do not use it for grading/);
  assert.match(brief, /1 of 2 files summarised\./);
  assert.match(brief, /Plan\.pdf \(v2\)\nA platform that connects students with campus services\.\n- Interviewed 18 students\./);
  assert.match(brief, /Still open in the file:\n- Pending: 1\. Add chat – Add chat\./);
  assert.doesNotMatch(brief, /Archive built/, 'finished work is not listed as open');
  assert.match(brief, /related content found for 1 of 3 items/);
  assert.match(brief, /Not found in the files: Competitors; Market size/);
  assert.match(brief, /Worth checking:\n- 1 item is reported as pending[^\n]*\n- 1 file is not summarised yet/);
  // A summary the AI has not written yet has no file section, and no claim about coverage it does not have.
  const empty = buildReviewBrief(summary(file('a', 'None')));
  assert.match(empty, /0 of 1 file summarised\./);
  assert.doesNotMatch(empty, /Requirements and rubric/);
});

test('review brief: the card shows it only when there is something to say, and puts it into the feedback box on request', () => {
  const card = aiSummarySources();
  assert.match(card, /<ReviewBrief summary=\{summary\} onQuote=\{onQuote\} \/>/);
  assert.match(card, /if \(!hasSummary && attention\.length === 0\) return null;/);
  // What reaches the feedback box is the draft for the team, never the internal brief.
  assert.match(card, /onQuote\?\.\(feedbackDraft\)/);
  assert.doesNotMatch(card, /onQuote\?\.\(buildReviewBrief/);
  // The coverage bar does not claim "found" for items nobody has looked for.
  assert.match(card, /\{checked > 0 && \(/);
});

test('the AI summary screens are written in English', () => {
  const vietnamese = /[àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ]/i;
  for (const path of [
    '../src/components/workspace/checkpoints/CheckpointAiSummaryCard.tsx',
    '../src/components/workspace/checkpoints/CheckpointVersionChanges.tsx',
    '../src/components/workspace/checkpoints/CheckpointProgressBlock.tsx',
    '../src/utils/checkpointSummary.ts',
  ]) {
    const source = readFileSync(new URL(path, import.meta.url), 'utf8');
    assert.doesNotMatch(source, vietnamese, `${path} still has Vietnamese text`);
  }
});

test('feedback draft: covers, gaps, open work, figures and questions for the team, without internal notes or a grade', () => {
  const ready = summary(
    file('a', 'Ready', { name: 'Plan.pdf', content: content({
      questions: ['Which suppliers confirmed the 8% fee?', 'which suppliers confirmed the 8% fee?'],
      statusItems: [{ status: 'Pending', text: 'Add chat.', request: '1. Add chat' }, { status: 'Done', text: 'Archive built.' }],
      conflicts: [{ subject: 'Conversion rate', values: [{ value: '15%', quote: 'q1' }, { value: '30%', quote: 'q2' }] }],
      ignoredInstructions: 3, littleText: true }) }),
    file('b', 'None'),
  );
  ready.requirements = [
    { ...checklistItem(0, 'Related', 'Persona'), evidence: 'Primary segment: first-year students.', source: 'Plan.pdf', evidencePage: 4, weight: 20 },
    { ...checklistItem(1, 'Related', 'Roadmap'), weight: 40 },
    checklistItem(2, 'NotFound', 'Competitors'),
    checklistItem(3, 'Live', 'Presentation'),
  ];
  const draft = buildFeedbackDraft(ready);
  assert.match(draft, /^Checkpoint 1 feedback\n\nWhat the files cover:\n- Persona: “Primary segment: first-year students\.” \(Plan\.pdf, page 4\)/);
  assert.doesNotMatch(draft, /Roadmap/, 'a related item without a quote has nothing to show the team');
  assert.match(draft, /Could not find in the submitted files[^\n]*\n- Competitors/);
  assert.doesNotMatch(draft, /Presentation/, 'items judged live are not reported as missing');
  assert.match(draft, /Still open according to the files:\n- Pending: 1\. Add chat – Add chat\./);
  assert.doesNotMatch(draft, /Archive built/);
  assert.match(draft, /Conversion rate: 15% vs 30%/);
  assert.equal(draft.match(/suppliers confirmed/gi)?.length, 1, 'the same question is asked once');
  // The team reads this box: nothing about the AI, the file's quality or instructions hidden in it.
  assert.doesNotMatch(draft, /\bAI\b|ignored|addressed|images|grading|grade/i);
  assert.equal(buildFeedbackDraft(summary(file('a', 'None'))), '');
});

import { hasProgress, partiallyReadCheckpoints, progressChangeView, progressStateText } from '../src/utils/checkpointSummary.ts';
import type { CheckpointProgress, ProgressChange, ProgressState } from '../src/types/checkpointSummary.ts';

const progress = (overrides: Partial<CheckpointProgress> = {}): CheckpointProgress => ({
  checkpointNumber: 2,
  checkpoints: [{ number: 1, fileCount: 2, readFileCount: 1 }, { number: 2, fileCount: 1, readFileCount: 1 }],
  concepts: [{ label: 'Persona', change: 'Gained', cells: [{ checkpointNumber: 1, state: 'NotMentioned' }, { checkpointNumber: 2, state: 'Found' }] }],
  ...overrides,
});

test('progress: wording says what the files show and never reads as a grade', () => {
  const changes: ProgressChange[] = ['Gained', 'Dropped', 'Held', 'Missing', 'None'];
  assert.deepEqual(changes.map((change) => progressChangeView[change]?.label ?? null),
    ['Newly mentioned', 'No longer mentioned', 'Still mentioned', 'Still not mentioned', null]);
  // A thing that is no longer mentioned is worth a look, a thing that appeared is good news; neither is called a failure or a pass.
  assert.equal(progressChangeView.Dropped?.tone, 'warning');
  assert.equal(progressChangeView.Gained?.tone, 'success');
  for (const view of Object.values(progressChangeView)) if (view) assert.doesNotMatch(view.label, /pass|fail|good|bad|improv|worse/i);
  const states: ProgressState[] = ['Found', 'NotMentioned', 'NotAsked', 'NotRead'];
  assert.deepEqual(states.map((state) => progressStateText[state]), ['Mentioned', 'Not mentioned', 'Not in the rubric', 'Files not read']);
});

test('progress: shown only with two checkpoints and a thing to compare, and says when only part of the files were read', () => {
  assert.equal(hasProgress(null), false);
  assert.equal(hasProgress(progress({ checkpoints: [], concepts: [] })), false);
  assert.equal(hasProgress(progress({ concepts: [] })), false);
  assert.equal(hasProgress(progress({ checkpoints: [{ number: 2, fileCount: 1, readFileCount: 1 }] })), false);
  assert.equal(hasProgress(progress()), true);
  assert.deepEqual(partiallyReadCheckpoints(progress()), ['CP1: 1 of 2 files read']);
  assert.deepEqual(partiallyReadCheckpoints(progress({ checkpoints: [{ number: 1, fileCount: 1, readFileCount: 1 }] })), []);
});

test('mock: progress compares checkpoints for reviewers only, and is absent when summaries are off', async () => {
  resetMockState();
  await login('giang.lecturer@ehub.local');
  const upload = (id: string) => ({
    _id: id, versionNumber: 1, originalName: `${id}.pdf`, fileType: 'pdf', fileSize: 10,
    uploadedAt: new Date().toISOString(), uploadedBy: { _id: '', name: 'Team leader' },
  });
  getMockState().checkpointFiles[`${TEAM_ID}:1`] = [upload('a')] as never;
  getMockState().checkpointFiles[`${TEAM_ID}:2`] = [upload('b')] as never;

  const first = (await checkpointSummaryApi.progress(TEAM_ID, 1)).data;
  assert.deepEqual([first.checkpoints.length, first.concepts.length], [0, 0], 'nothing earlier to compare with');
  const second = (await checkpointSummaryApi.progress(TEAM_ID, 2)).data;
  assert.deepEqual(second.checkpoints.map((item) => item.number), [1, 2]);
  assert.equal(hasProgress(second), true);
  assert.ok(second.concepts.every((concept) => concept.cells.length === 2));

  getMockState().checkpointSummaryDisabled = true;
  await assert.rejects(checkpointSummaryApi.progress(TEAM_ID, 2), (error: { response?: { status: number } }) => error.response?.status === 503);
  getMockState().checkpointSummaryDisabled = false;
  await login('se200001@fpt.edu.vn');
  await assert.rejects(checkpointSummaryApi.progress(TEAM_ID, 2), (error: { response?: { status: number } }) => error.response?.status === 403);
});

test('progress: the block loads by itself from the second checkpoint, handles loading, error and retry, and cleans up', () => {
  const block = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointProgressBlock.tsx', import.meta.url), 'utf8');
  assert.match(block, /if \(checkpointNumber <= 1\) return undefined;/);
  assert.match(block, /checkpointSummaryApi\.progress\(teamId, checkpointNumber, signal\)/);
  assert.match(block, /return \(\) => controller\.abort\(\);/);
  assert.match(block, /role="alert"/);
  assert.match(block, /setAttempt\(\(value\) => value \+ 1\)/);
  const card = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointAiSummaryCard.tsx', import.meta.url), 'utf8');
  assert.match(card, /<CheckpointProgressBlock teamId=\{teamId\} checkpointNumber=\{checkpointNumber\} \/>/);
  assert.doesNotMatch(block, /dangerouslySetInnerHTML/);
  assert.doesNotMatch(block, /[àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ]/i, 'written in English like the rest of the card');
});
