import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import axiosClient from '../src/api/axiosClient.ts';
import { checkpointSummaryApi } from '../src/api/checkpointSummaryApi.ts';
import { enableApiMocks } from '../src/mocks/mockApi.ts';
import { getMockState, resetMockState } from '../src/mocks/mockHelpers.ts';
import type { SummaryDiagnostics } from '../src/types/checkpointSummary.ts';
import { ageText, circuitView, diagnosticsHealth, failureLabel, hasFewVerdicts, helpfulShare, waitingFiles } from '../src/utils/summaryDiagnostics.ts';

resetMockState();
enableApiMocks();

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

const diagnostics = (overrides: Partial<SummaryDiagnostics> = {}): SummaryDiagnostics => ({
  enabled: true, model: 'qwen3.5:2b-q4_K_M', promptVersion: 'v11', autoSummarize: true, modelReachable: true, modelAvailable: true,
  circuit: 'Closed', consecutiveModelFailures: 0, circuitOpenUntilUtc: null, lastModelFailureUtc: null, pendingRequested: 0, pendingAutomatic: 0,
  processing: 0, expiredLeases: 0, ready: 5, failed: 0, cancelled: 0, oldestPendingSeconds: null, lastGeneratedAtUtc: null, failuresByCode: [],
  feedbackByPromptVersion: [], maxRequestedQueue: 30, maxAutomaticQueue: 200, ...overrides,
});

test('diagnostics: one verdict for the header, the model server first', () => {
  assert.deepEqual(diagnosticsHealth(diagnostics()), { tone: 'success', label: 'Healthy', problems: [] });

  const down = diagnosticsHealth(diagnostics({ modelReachable: false, modelAvailable: false, circuit: 'Open', failed: 2 }));
  assert.equal(down.tone, 'danger');
  assert.match(down.problems[0], /not answering/, 'the model server comes before everything else');
  assert.ok(down.problems.length >= 3, 'the paused worker and the failed files are listed too');

  const noModel = diagnosticsHealth(diagnostics({ modelAvailable: false }));
  assert.equal(noModel.tone, 'danger');
  assert.match(noModel.problems[0], /qwen3\.5:2b-q4_K_M.*not been pulled/);

  // A pause or failed files need attention but do not mean the model is broken.
  assert.equal(diagnosticsHealth(diagnostics({ circuit: 'Open' })).tone, 'warning');
  assert.equal(diagnosticsHealth(diagnostics({ circuit: 'HalfOpen' })).tone, 'warning');
  assert.equal(diagnosticsHealth(diagnostics({ expiredLeases: 1 })).tone, 'warning');
  assert.equal(diagnosticsHealth(diagnostics({ failed: 3 })).tone, 'warning');
  assert.equal(diagnosticsHealth(diagnostics({ pendingRequested: 30 })).tone, 'warning', 'a full queue refuses new requests');
  assert.equal(diagnosticsHealth(diagnostics({ pendingRequested: 29 })).tone, 'success');
  assert.deepEqual(circuitView.Closed, { label: 'Running', tone: 'success' });
});

test('diagnostics: waiting time, failure reasons and the share of useful verdicts read plainly', () => {
  assert.equal(waitingFiles(diagnostics({ pendingRequested: 2, pendingAutomatic: 5 })), 7);
  assert.equal(ageText(null), '—');
  assert.equal(ageText(20), 'just now');
  assert.equal(ageText(180), '3 min');
  assert.equal(ageText(3_600), '1 h');
  assert.equal(ageText(7_500), '2 h 5 min');
  assert.equal(ageText(90_000), '1 d 1 h');
  assert.equal(ageText(-5), 'just now');

  assert.match(failureLabel('WORKSPACE_SUMMARY_UNREADABLE'), /no readable text/);
  assert.match(failureLabel('WORKSPACE_SUMMARY_MODEL_UNREACHABLE'), /could not be reached/);
  assert.equal(failureLabel('SOMETHING_NEW'), 'SOMETHING_NEW', 'an unknown code is shown as it is so it can be looked up');

  assert.equal(helpfulShare({ promptVersion: 'v11', helpful: 0, notHelpful: 0 }), null);
  assert.equal(helpfulShare({ promptVersion: 'v11', helpful: 3, notHelpful: 1 }), 75);
  assert.equal(hasFewVerdicts({ promptVersion: 'v11', helpful: 10, notHelpful: 9 }), true);
  assert.equal(hasFewVerdicts({ promptVersion: 'v11', helpful: 10, notHelpful: 10 }), false);
});

test('mock: only administrators read the diagnostics, nothing is shown while AI summaries are off, and an outage is reported', async () => {
  resetMockState();
  await login('admin@ehub.local');
  const healthy = (await checkpointSummaryApi.diagnostics()).data;
  assert.equal(healthy.modelReachable, true);
  assert.equal(healthy.circuit, 'Closed');
  assert.equal(diagnosticsHealth(healthy).label, 'Healthy');

  getMockState().checkpointSummaryModelDown = true;
  try {
    assert.equal(diagnosticsHealth((await checkpointSummaryApi.diagnostics()).data).label, 'Not working');
  } finally {
    getMockState().checkpointSummaryModelDown = false;
  }

  getMockState().checkpointSummaryDisabled = true;
  try {
    assert.deepEqual(await errorCode(checkpointSummaryApi.diagnostics()), { status: 503, code: 'WORKSPACE_SUMMARY_DISABLED' });
  } finally {
    getMockState().checkpointSummaryDisabled = false;
  }

  await login('giang.lecturer@ehub.local');
  assert.deepEqual(await errorCode(checkpointSummaryApi.diagnostics()), { status: 403, code: 'WORKSPACE_ACCESS_DENIED' });
  await login('se200001@fpt.edu.vn');
  assert.deepEqual(await errorCode(checkpointSummaryApi.diagnostics()), { status: 403, code: 'WORKSPACE_ACCESS_DENIED' });
});

test('diagnostics: the card asks for nothing while AI summaries are off, shows no raw errors, and is only on the admin dashboard', () => {
  const card = readFileSync(new URL('../src/components/admin/AiSummaryDiagnosticsCard.tsx', import.meta.url), 'utf8');
  assert.match(card, /const enabled = useAiSummaryEnabled\(\);/);
  assert.match(card, /if \(!enabled\) return undefined;/, 'no request before the server has said AI summaries are on');
  assert.match(card, /if \(!enabled\) return null;/);
  assert.match(card, /return \(\) => controller\.abort\(\);/);
  assert.match(card, /status === 429/, 'a refresh that is too frequent has its own message');
  assert.doesNotMatch(card, /dangerouslySetInnerHTML/);
  assert.doesNotMatch(card, /setInterval/, 'no automatic polling: every refresh is the administrator\'s own');
  const dashboard = readFileSync(new URL('../src/pages/admin/AdminDashboard.tsx', import.meta.url), 'utf8');
  assert.match(dashboard, /<AiSummaryDiagnosticsCard \/>/);
  for (const other of ['lecturer/LecturerDashboard', 'mentor/MentorDashboard'])
    assert.doesNotMatch(readFileSync(new URL(`../src/pages/${other}.tsx`, import.meta.url), 'utf8'), /AiSummaryDiagnosticsCard/);
});
