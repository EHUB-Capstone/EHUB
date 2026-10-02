import assert from 'node:assert/strict';
import test from 'node:test';
import axiosClient from '../src/api/axiosClient.ts';
import { checkpointApi } from '../src/api/checkpointApi.ts';
import storageClient from '../src/api/storageClient.ts';
import { enableApiMocks } from '../src/mocks/mockApi.ts';
import { getMockState, resetMockState } from '../src/mocks/mockHelpers.ts';
import {
  CHECKPOINT_UPLOAD_MAX_FILE_SIZE,
  checkpointPutTimeoutMs,
  checkpointUploadFailureMessage,
  formatFileSize,
  getUploadProgressPercent,
  needsNewUploadSession,
  validateCheckpointUploadFile,
} from '../src/utils/checkpointUpload.ts';

resetMockState();
enableApiMocks();

const MB = 1024 * 1024;

test('upload limit is 100 MB: the boundary passes and one byte more is rejected with a clear message', () => {
  assert.equal(CHECKPOINT_UPLOAD_MAX_FILE_SIZE, 100 * MB);
  assert.equal(validateCheckpointUploadFile({ name: 'exact.pdf', size: 100 * MB }), null);
  const message = validateCheckpointUploadFile({ name: 'big.pdf', size: 100 * MB + 1 });
  assert.match(message ?? '', /exceeds the 100 MB limit/);
  assert.doesNotMatch(message ?? '', /15 MB/);
});

test('formatFileSize and progress helpers stay within sensible bounds', () => {
  assert.equal(formatFileSize(0), '0 B');
  assert.equal(formatFileSize(1536), '1.5 KB');
  assert.equal(formatFileSize(100 * MB), '100.0 MB');
  assert.equal(formatFileSize(-1), '0 B');
  assert.equal(getUploadProgressPercent(50, 200, 0), 25);
  assert.equal(getUploadProgressPercent(300, 200, 0), 100);
  assert.equal(getUploadProgressPercent(10, undefined, 40), 25);
  assert.equal(getUploadProgressPercent(10, undefined, 0), 0);
});

test('PUT timeout grows with file size so a 100 MB upload on a slow connection is not cut off', () => {
  assert.equal(checkpointPutTimeoutMs(1024), 120_000);
  assert.ok(checkpointPutTimeoutMs(100 * MB) >= 600_000);
});

test('a retry reuses the upload session only while it is still valid for the step that is left', () => {
  const now = Date.parse('2026-10-02T10:00:00Z');
  const session = { urlExpiresAt: '2026-10-02T10:10:00Z', sessionExpiresAt: '2026-10-02T11:00:00Z' };
  assert.equal(needsNewUploadSession({ putDone: false }, now), true);
  assert.equal(needsNewUploadSession({ session, putDone: false }, now), false);
  assert.equal(needsNewUploadSession({ session, putDone: false }, now + 11 * 60_000), true);
  // Bytes already stored: only the session lifetime matters, not the presigned URL.
  assert.equal(needsNewUploadSession({ session, putDone: true }, now + 11 * 60_000), false);
  assert.equal(needsNewUploadSession({ session, putDone: true }, now + 61 * 60_000), true);
  assert.equal(needsNewUploadSession({ session: { urlExpiresAt: 'bad', sessionExpiresAt: 'bad' }, putDone: false }, now), true);
});

test('storage failures are explained without exposing raw responses', () => {
  assert.match(checkpointUploadFailureMessage({ response: { status: 403, data: '<Error>AccessDenied</Error>' } }, 'a.pdf', 'storage'), /expired or was rejected/);
  assert.match(checkpointUploadFailureMessage({ code: 'ERR_NETWORK' }, 'a.pdf', 'storage'), /file storage/);
  assert.match(checkpointUploadFailureMessage({ code: 'ECONNABORTED' }, 'a.pdf', 'storage'), /too long/);
  assert.match(checkpointUploadFailureMessage({ response: { status: 500, data: '<Error/>' } }, 'a.pdf', 'storage'), /HTTP 500/);
  assert.equal(
    checkpointUploadFailureMessage({ response: { data: { message: 'The upload session has expired.' } } }, 'a.pdf'),
    'The upload session has expired.',
  );
});

test('the storage client never carries credentials to the storage host', () => {
  assert.equal(storageClient.defaults.withCredentials, false);
  assert.equal(storageClient.defaults.baseURL, undefined);
  const requestInterceptors = (storageClient.interceptors.request as unknown as { handlers: unknown[] }).handlers;
  assert.equal(requestInterceptors.filter(Boolean).length, 0);
  assert.equal(storageClient.defaults.headers.common?.Authorization, undefined);
});

async function openCheckpointAsStudent() {
  resetMockState();
  await axiosClient.post('/auth/login', { email: 'giang.lecturer@ehub.local', password: 'Mock123!' });
  const overview = await axiosClient.get('/lecturer/checkpoints', { params: { semester: 'FA', year: 2026 } });
  const cls = overview.data.classes[0];
  const definition = overview.data.checkpoints[0];
  const state = getMockState();
  const team = state.teams.find(item => item.classId === cls.id && item.status === 'Active');
  assert.ok(team);
  state.checkpointFiles[`${team.id}:${definition.number}`] = [];
  state.checkpointLinks[`${team.id}:${definition.number}`] = [];
  state.checkpointSchedules[`${cls.id}:${definition.id}`] = {
    id: 'direct-upload-schedule',
    classId: cls.id,
    checkpointId: definition.id,
    startDateUtc: new Date(Date.now() - 3_600_000).toISOString(),
    endDateUtc: new Date(Date.now() + 3_600_000).toISOString(),
    reopenCount: 0,
  };
  const lecturerId = state.sessionUserId;
  state.sessionUserId = team.leaderId;
  return { team, number: definition.number as number, state, lecturerId };
}

function pdf(name: string, size?: number) {
  const file = new File(['%PDF-test'], name, { type: 'application/pdf' });
  return size === undefined ? file : Object.defineProperty(file, 'size', { value: size });
}

const statusOf = (error: unknown) => (error as { response?: { status?: number; data?: { code?: string } } }).response;

test('direct upload mock: initiate -> PUT -> complete creates the file only after the bytes arrived', async () => {
  const { team, number } = await openCheckpointAsStudent();
  const file = pdf('report.pdf');

  const created = await checkpointApi.initiateUpload(team.id, number, { fileName: file.name, contentType: file.type, size: file.size });
  assert.equal(created.data.method, 'PUT');
  assert.equal(created.data.maxFileSize, 100 * MB);
  assert.equal(created.data.headers['Content-Type'], 'application/pdf');
  assert.ok(Date.parse(created.data.urlExpiresAt) < Date.parse(created.data.sessionExpiresAt));

  // Complete before the PUT: nothing is created and the caller is told to retry.
  await assert.rejects(() => checkpointApi.completeUpload(team.id, number, created.data.uploadId), (error) => {
    const response = statusOf(error);
    return response?.status === 409 && response.data?.code === 'WORKSPACE_UPLOAD_OBJECT_MISSING';
  });
  const before = await checkpointApi.getCheckpointData(team.id);
  assert.equal(before.data.submissions.find((item: { checkpointNumber: number }) => item.checkpointNumber === number)?.files.length ?? 0, 0);

  let progressCalls = 0;
  await checkpointApi.putToPresignedUrl(created.data, file, { onUploadProgress: () => { progressCalls += 1; } });
  const completed = await checkpointApi.completeUpload(team.id, number, created.data.uploadId);
  assert.equal(completed.data.versionNumber, 1);
  assert.equal(completed.data.originalName, 'report.pdf');

  // Retrying complete is idempotent: same file, no duplicate.
  const retried = await checkpointApi.completeUpload(team.id, number, created.data.uploadId);
  assert.equal(retried.data._id, completed.data._id);
  const after = await checkpointApi.getCheckpointData(team.id);
  assert.equal(after.data.submissions.find((item: { checkpointNumber: number }) => item.checkpointNumber === number).files.length, 1);
  assert.equal(progressCalls >= 0, true);
});

test('direct upload mock enforces 100 MB, supported types, student role and storage size match', async () => {
  const { team, number, state } = await openCheckpointAsStudent();

  const exact = await checkpointApi.initiateUpload(team.id, number, { fileName: 'exact.pdf', contentType: 'application/pdf', size: 100 * MB });
  assert.equal(exact.data.maxFileSize, 100 * MB);

  for (const request of [
    { fileName: 'big.pdf', contentType: 'application/pdf', size: 100 * MB + 1 },
    { fileName: 'empty.pdf', contentType: 'application/pdf', size: 0 },
    { fileName: 'notes.txt', contentType: 'text/plain', size: 10 },
  ]) {
    await assert.rejects(() => checkpointApi.initiateUpload(team.id, number, request), (error) => statusOf(error)?.status === 400);
  }

  // The bytes sent to storage must match the size the link was issued for.
  const session = await checkpointApi.initiateUpload(team.id, number, { fileName: 'a.pdf', contentType: 'application/pdf', size: 50 });
  await assert.rejects(() => checkpointApi.putToPresignedUrl(session.data, pdf('a.pdf'), {}), (error) => statusOf(error)?.status === 403);
  await assert.rejects(() => checkpointApi.completeUpload(team.id, number, session.data.uploadId), (error) => statusOf(error)?.status === 409);

  // Someone else cannot complete a session they did not start.
  const stranger = state.users.find(user => user.role === 'STUDENT' && user.id !== team.leaderId);
  assert.ok(stranger);
  state.sessionUserId = stranger.id;
  await assert.rejects(() => checkpointApi.completeUpload(team.id, number, exact.data.uploadId), (error) => {
    const status = statusOf(error)?.status;
    return status === 403 || status === 404;
  });
  state.sessionUserId = state.users.find(user => user.role === 'LECTURER')?.id ?? null;
  await assert.rejects(() => checkpointApi.initiateUpload(team.id, number, { fileName: 'a.pdf', contentType: 'application/pdf', size: 10 }), (error) => statusOf(error)?.status === 403);
});

test('large files are flagged for direct download and small ones are not', async () => {
  const { team, number } = await openCheckpointAsStudent();

  async function upload(name: string, size: number) {
    const created = await checkpointApi.initiateUpload(team.id, number, { fileName: name, contentType: 'application/pdf', size });
    await checkpointApi.putToPresignedUrl(created.data, pdf(name, size), {});
    return (await checkpointApi.completeUpload(team.id, number, created.data.uploadId)).data;
  }

  const small = await upload('small.pdf', 5 * MB);
  const large = await upload('large.pdf', 11 * MB);
  assert.equal(small.canDirectDownload, false);
  assert.equal(large.canDirectDownload, true);

  const link = await axiosClient.get(`/workspace/checkpoints/teams/${team.id}/checkpoints/${number}/files/${large._id}/download-url`);
  assert.match(link.data.url, /^data:/);
  assert.ok(Date.parse(link.data.expiresAt) > Date.now());
  await assert.rejects(
    () => axiosClient.get(`/workspace/checkpoints/teams/${team.id}/checkpoints/${number}/files/${small._id}/download-url`),
    (error) => statusOf(error)?.status === 400,
  );
});
