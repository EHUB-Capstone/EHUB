import assert from 'node:assert/strict';
import test from 'node:test';
import {
  PREVIEW_PREPARE_TIMEOUT_MS,
  PreviewPreparationTimeoutError,
  PreviewUnavailableError,
  previewPollDelayMs,
  resolvePreviewSource,
  type PreviewSourceResponse,
} from '../src/utils/previewSource.ts';

function scripted(...answers: PreviewSourceResponse[]) {
  const calls: boolean[] = [];
  let index = 0;
  return {
    calls,
    fetchSource: async (retry: boolean) => {
      calls.push(retry);
      return answers[Math.min(index++, answers.length - 1)];
    },
  };
}

function fakeClock() {
  let time = 0;
  const sleeps: number[] = [];
  return {
    sleeps,
    now: () => time,
    sleep: async (ms: number) => { sleeps.push(ms); time += ms; },
  };
}

test('a ready preview resolves immediately without waiting', async () => {
  const api = scripted({ status: 'Ready', url: 'https://r2.example/file.pdf' });
  const clock = fakeClock();
  const source = await resolvePreviewSource(api.fetchSource, { sleep: clock.sleep, now: clock.now });
  assert.equal(source.url, 'https://r2.example/file.pdf');
  assert.deepEqual(clock.sleeps, []);
});

test('Preparing is polled with a growing back-off until the preview is ready', async () => {
  const api = scripted({ status: 'Preparing' }, { status: 'Preparing' }, { status: 'Preparing' }, { status: 'Preparing' }, { status: 'Ready', url: 'u' });
  const clock = fakeClock();
  const elapsed: number[] = [];
  const source = await resolvePreviewSource(api.fetchSource, { sleep: clock.sleep, now: clock.now, onPreparing: ms => elapsed.push(ms) });
  assert.equal(source.status, 'Ready');
  assert.deepEqual(clock.sleeps, [1000, 2000, 4000, 4000]);
  assert.deepEqual(elapsed, [0, 1000, 3000, 7000]);
  assert.equal(previewPollDelayMs(50), 4000);
});

test('the retry flag is only sent with the first request', async () => {
  const api = scripted({ status: 'Preparing' }, { status: 'Ready', url: 'u' });
  const clock = fakeClock();
  await resolvePreviewSource(api.fetchSource, { retry: true, sleep: clock.sleep, now: clock.now });
  assert.deepEqual(api.calls, [true, false]);
});

test('Proxy tells the caller to use the server-side preview endpoint', async () => {
  const source = await resolvePreviewSource(scripted({ status: 'Proxy' }).fetchSource);
  assert.equal(source.status, 'Proxy');
});

test('Failed, TooLarge and Unsupported surface the API message and stop polling', async () => {
  for (const status of ['Failed', 'TooLarge', 'Unsupported'] as const) {
    const api = scripted({ status, message: `message for ${status}` });
    await assert.rejects(
      () => resolvePreviewSource(api.fetchSource),
      (error: unknown) => error instanceof PreviewUnavailableError && error.message === `message for ${status}`,
    );
    assert.equal(api.calls.length, 1);
  }
  await assert.rejects(
    () => resolvePreviewSource(scripted({ status: 'Failed' }).fetchSource),
    /could not be prepared/,
  );
});

test('a preview that never becomes ready times out instead of polling forever', async () => {
  const api = scripted({ status: 'Preparing' });
  const clock = fakeClock();
  await assert.rejects(
    () => resolvePreviewSource(api.fetchSource, { sleep: clock.sleep, now: clock.now }),
    (error: unknown) => error instanceof PreviewPreparationTimeoutError,
  );
  assert.ok(clock.now() >= PREVIEW_PREPARE_TIMEOUT_MS);
  assert.ok(clock.now() < PREVIEW_PREPARE_TIMEOUT_MS + 4000);
});

test('aborting stops the loop without another request', async () => {
  const controller = new AbortController();
  const api = scripted({ status: 'Preparing' });
  await assert.rejects(
    () => resolvePreviewSource(api.fetchSource, {
      signal: controller.signal,
      sleep: async () => { controller.abort(); },
    }),
    (error: unknown) => (error as Error).name === 'AbortError',
  );
  assert.equal(api.calls.length, 1);
});

test('the default sleep rejects as soon as the signal aborts', async () => {
  const controller = new AbortController();
  const api = scripted({ status: 'Preparing' });
  const pending = resolvePreviewSource(api.fetchSource, { signal: controller.signal });
  setTimeout(() => controller.abort(), 20);
  const started = Date.now();
  await assert.rejects(() => pending, (error: unknown) => (error as Error).name === 'AbortError');
  assert.ok(Date.now() - started < 500);
});
