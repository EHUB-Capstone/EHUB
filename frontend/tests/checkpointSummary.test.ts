import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
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

test('the collapsed header counts files by state, worst news first', () => {
  const files = [file('a', 'Processing'), file('b', 'Pending'), file('c', 'Pending'), file('d', 'Failed'), file('e', 'Ready'), file('f', 'None'), file('g', 'Cancelled')];
  assert.deepEqual(summaryStatusCounts(files), { total: 7, ready: 1, summarising: 1, queued: 2, failed: 1, notSummarised: 2 });
  assert.deepEqual(summaryStatusBadges(summary(...files), false).map((badge) => badge.label),
    ['1 đang tóm tắt', '2 đang chờ', '1 lỗi', '1/7 đã tóm tắt', '2 chưa tóm tắt']);
  assert.deepEqual(summaryStatusBadges(summary(file('a', 'Ready'), file('b', 'Ready')), false).map((badge) => [badge.label, badge.tone]),
    [['Cả 2 file đã tóm tắt', 'success']]);
  assert.equal(summaryStatusBadges(summary(file('a', 'Ready')), false)[0].label, '1 file đã tóm tắt');
  assert.equal(summaryStatusBadges(summary(), false)[0].label, 'Chưa có file');
  assert.equal(summaryStatusBadges(null, false)[0].label, 'Đang kiểm tra…');
  assert.equal(summaryStatusBadges(null, true)[0].tone, 'danger');
});

test('the card starts closed and never ticks files for the reviewer', () => {
  const card = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointAiSummaryCard.tsx', import.meta.url), 'utf8');
  assert.match(card, /const \[expanded, setExpanded\] = useState\(false\)/);
  assert.doesNotMatch(card, /defaultFileSelection|selectionTouchedRef/);
});

test('a file with sentences addressed to the AI says so, and only then', () => {
  assert.equal(ignoredInstructionsMessage(0), null);
  assert.equal(ignoredInstructionsMessage(undefined), null);
  assert.match(ignoredInstructionsMessage(1) ?? '', /1 câu gửi tới AI/);
  assert.match(ignoredInstructionsMessage(3) ?? '', /3 câu gửi tới AI/);
  const card = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointAiSummaryCard.tsx', import.meta.url), 'utf8');
  // Both the quick view and the finished summary show it.
  assert.match(card, /<IgnoredInstructionsNote count=\{file\.preview\.ignoredInstructions\}/);
  assert.match(card, /<IgnoredInstructionsNote count=\{content\.ignoredInstructions\}/);
});

test('a quote says where it is, can be pasted into a comment, and the time left is a rough guide', () => {
  assert.equal(pageLabel('Deck.pptx', 7), 'Slide 7');
  assert.equal(pageLabel('Report.pdf', 12), 'Trang 12');
  assert.equal(quoteForFeedback('Doanh thu 1,4 tỷ đồng.', 'Report.pdf', 12), '“Doanh thu 1,4 tỷ đồng.” — Report.pdf, Trang 12');
  assert.equal(quoteForFeedback('Doanh thu 1,4 tỷ đồng.', 'Report.docx'), '“Doanh thu 1,4 tỷ đồng.” — Report.docx');
  assert.equal(quoteForFeedback('Câu trích.'), '“Câu trích.”');
  assert.equal(summaryEtaText(summary(file('a', 'Ready'))), null);
  assert.equal(summaryEtaText(summary(file('a', 'Processing'))), 'Sắp xong');
  assert.equal(summaryEtaText(summary(file('a', 'Processing'), file('b', 'Pending'), file('c', 'Pending'))), 'Còn khoảng 3 phút');
  const card = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointAiSummaryCard.tsx', import.meta.url), 'utf8');
  // The quote actions are wired from the checkpoint screen: open at the page, and put a quote into the comment box.
  const panel = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointPanel.tsx', import.meta.url), 'utf8');
  assert.match(panel, /onOpenFile=\{\(file, page\) =>/);
  assert.match(panel, /onQuote=\{\(text\) =>/);
  assert.match(panel, /initialPage=\{previewPage\}/);
  assert.match(card, /page=\{requirement\.evidencePage\}/);
});

test('polls only while a file is queued or being summarised', () => {
  assert.equal(nextSummaryPollDelay(summary(file('a', 'Ready'), file('b', 'Pending'))), CHECKPOINT_SUMMARY_POLL_MS);
  assert.equal(nextSummaryPollDelay(summary(file('a', 'Processing'))), CHECKPOINT_SUMMARY_POLL_MS);
  assert.equal(nextSummaryPollDelay(summary(file('a', 'Ready'), file('b', 'Cancelled'), file('c', 'None'))), null);
  assert.equal(nextSummaryPollDelay(null), null);
});

test('file status labels show the queue and findings never read as pass or fail', () => {
  assert.equal(fileStatusView(file('a', 'Pending', { queueAhead: 2 })).label, 'Đang chờ · còn 2 file trước');
  assert.equal(fileStatusView(file('a', 'Pending', { queueAhead: 0 })).label, 'Đang chờ');
  assert.equal(fileStatusView(file('a', 'Pending', { queueAhead: 1, isAutomatic: true })).label, 'Tự xếp hàng · còn 1 file trước');
  assert.equal(fileStatusView(file('a', 'Ready')).tone, 'success');
  for (const view of Object.values(summaryFindingView)) assert.doesNotMatch(view.label, /pass|fail|đạt/i);
  assert.match(summaryErrorMessage('WORKSPACE_SUMMARY_QUEUE_FULL'), /sau vài phút/);
  assert.match(summaryErrorMessage('AUTH_RATE_LIMITED'), /một giờ qua/);
  // Each cause of a failed file has its own message, and the temporary ones point to Retry.
  for (const code of ['WORKSPACE_SUMMARY_MODEL_UNREACHABLE', 'WORKSPACE_SUMMARY_MODEL_TIMEOUT', 'WORKSPACE_SUMMARY_STORAGE_UNAVAILABLE'])
    assert.match(summaryErrorMessage(code), /Thử lại/);
  assert.notEqual(summaryErrorMessage('WORKSPACE_SUMMARY_MODEL_BAD_ANSWER'), summaryErrorMessage('WORKSPACE_SUMMARY_UNAVAILABLE'));
  assert.equal(summaryErrorMessage('SOMETHING_NEW'), 'Không tải được bản AI tóm tắt. Vui lòng thử lại.');
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
  const card = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointAiSummaryCard.tsx', import.meta.url), 'utf8');
  assert.match(card, /<FigureConflicts conflicts=\{summary\.conflicts \?\? \[\]\} acrossFiles/);

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
  assert.equal(summaryFindingView.Live.label, 'Chấm trực tiếp');
  assert.match(summaryFindingView.Live.hint, /trực tiếp/);
  // A whole-file criterion ("Startup Idea Clarity") is never reported as missing, and is listed with the other judged-elsewhere rows.
  const withOverall = [...rows, { index: 4, finding: 'Overall' as const, weight: 30 }];
  assert.equal(isSearchable(withOverall[4]), false);
  assert.equal(isNothingFound(withOverall), true);
  assert.equal(summaryFindingView.Overall.label, 'Đánh giá trên cả file');
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
  const card = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointAiSummaryCard.tsx', import.meta.url), 'utf8');
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

import { hasReplacedFiles, versionChangeCounts, versionChangeHeadline } from '../src/utils/checkpointSummary.ts';

test('version changes: only files uploaded more than once can be compared, and the headline says what a reviewer should know', () => {
  assert.equal(hasReplacedFiles([{ name: 'Plan.pdf' }, { name: 'Slides.pptx' }]), false);
  assert.equal(hasReplacedFiles([{ name: 'Plan.pdf' }, { name: ' plan.PDF' }]), true);
  assert.equal(versionChangeCounts({ changedCount: 2, addedCount: 1, removedCount: 0 }), '2 câu sửa · 1 câu thêm');
  assert.equal(versionChangeCounts({ changedCount: 0, addedCount: 0, removedCount: 0 }), '');
  const base = { fromVersion: 1, toVersion: 2, unchangedPercent: 80 };
  assert.match(versionChangeHeadline({ ...base, status: 'Compared', items: [{ figuresChanged: true }, { figuresChanged: false }] }), /giữ nguyên 80%.*1 câu đổi số liệu/);
  assert.doesNotMatch(versionChangeHeadline({ ...base, status: 'Compared', items: [] }), /số liệu/);
  assert.match(versionChangeHeadline({ ...base, status: 'Unchanged', items: [] }), /v2 giống v1/);
  assert.match(versionChangeHeadline({ ...base, status: 'Unreadable', items: [] }), /Không đọc được/);
  assert.match(versionChangeHeadline({ ...base, status: 'NewFile', items: [] }), /File mới/);
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
  const card = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointAiSummaryCard.tsx', import.meta.url), 'utf8');
  assert.match(card, /summary\.enabled && hasReplacedFiles\(/);
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
  const card = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointAiSummaryCard.tsx', import.meta.url), 'utf8');
  assert.match(card, /summary\.enabled && <FigureHistory /);
  assert.match(card, /checkpointSummaryApi\.figureHistory\(teamId, checkpointNumber, controller\.signal\)/);
  assert.match(card, /return \(\) => controller\.abort\(\);\s+\}, \[teamId, checkpointNumber\]\);\s+if \(!history/);
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
  await checkpointSummaryApi.setFeedback(TEAM_ID, 1, fileId, { rating: 'Helpful', reason: null });
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
  const card = readFileSync(new URL('../src/components/workspace/checkpoints/CheckpointAiSummaryCard.tsx', import.meta.url), 'utf8');
  assert.match(card, /if \(ratingRef\.current\) return;/);
  assert.match(card, /ratingControllerRef\.current\?\.abort\(\)/);
  // The verdict is applied to the screen only after the request succeeded.
  assert.match(card, /await checkpointSummaryApi\.setFeedback[\s\S]*?setSummary\(\(current\) => \(current \? withFeedback/);
  assert.match(card, /aria-pressed=\{current\?\.rating === 'Helpful'\}/);
  assert.match(card, /className=\{buttonClass\(/);
  assert.doesNotMatch(card, /dangerouslySetInnerHTML/);
});
