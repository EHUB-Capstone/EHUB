import assert from 'node:assert/strict';
import test from 'node:test';
import MockAdapter from 'axios-mock-adapter';
import axiosClient from '../src/api/axiosClient.ts';
import { showcaseApi } from '../src/api/showcaseApi.ts';
import { readFileSync } from 'node:fs';
import {
  ASSISTANT_HISTORY_TURNS,
  ASSISTANT_KEY_HEADER,
  assistantErrorMessage,
  assistantKeyProblem,
  buildAssistantHistory,
  cleanAssistantKey,
  isAssistantKeyError,
  isShowcaseAssistantRoute,
  type AssistantChatMessage,
} from '../src/utils/showcaseAssistant.ts';

const KEY = 'AIzaSyFrontendTestKey0123456789abcd';

const user = (id: string, content: string): AssistantChatMessage => ({ id, role: 'user', content });
const assistant = (id: string, content: string): AssistantChatMessage => ({ id, role: 'assistant', content, projects: [], suggestions: [] });

test('assistant history keeps only conversation turns and the most recent ones', () => {
  const messages: AssistantChatMessage[] = [
    user('1', 'q1'), assistant('2', 'a1'),
    { id: '3', role: 'error', content: 'failed', question: 'q2', userMessageId: '4' },
    user('4', 'q2'), assistant('5', 'a2'), user('6', 'q3'), assistant('7', 'a3'), user('8', 'q4'),
  ];
  const history = buildAssistantHistory(messages);
  assert.equal(history.length, ASSISTANT_HISTORY_TURNS);
  assert.deepEqual(history.map(turn => turn.content), ['a1', 'q2', 'a2', 'q3', 'a3', 'q4']);
  assert.ok(history.every(turn => turn.role === 'user' || turn.role === 'assistant'));
});

test('a retry leaves the failed question out of the history it resends', () => {
  const messages: AssistantChatMessage[] = [
    user('1', 'q1'), assistant('2', 'a1'), user('3', 'q2'),
    { id: '4', role: 'error', content: 'failed', question: 'q2', userMessageId: '3' },
  ];
  assert.deepEqual(buildAssistantHistory(messages, ['4', '3']), [
    { role: 'user', content: 'q1' }, { role: 'assistant', content: 'a1' },
  ]);
});

test('the assistant is offered on the gallery and project pages only', () => {
  assert.equal(isShowcaseAssistantRoute('/showcase'), true);
  assert.equal(isShowcaseAssistantRoute('/showcase/'), true);
  assert.equal(isShowcaseAssistantRoute('/showcase/3f2b8c1e-5d4a-4b6f-9c0d-1a2b3c4d5e6f'), true);
  assert.equal(isShowcaseAssistantRoute('/showcase/manage'), false);
  assert.equal(isShowcaseAssistantRoute('/showcase/moderation'), false);
  assert.equal(isShowcaseAssistantRoute('/showcase/manage/3f2b8c1e-5d4a-4b6f-9c0d-1a2b3c4d5e6f'), false);
  assert.equal(isShowcaseAssistantRoute('/student'), false);
});

test('assistant errors never expose raw exceptions', () => {
  assert.match(assistantErrorMessage({ response: { status: 429 } }, undefined), /đợi/);
  assert.equal(assistantErrorMessage({ response: { status: 503 } }, 'Trợ lý AI chưa được bật.'), 'Trợ lý AI chưa được bật.');
  assert.match(assistantErrorMessage(new Error('System.NullReferenceException at Foo'), 'System.NullReferenceException'), /Không gửi được/);
  assert.match(assistantErrorMessage({ response: { status: 500 } }, 'stack trace here'), /Không gửi được/);
});

test('assistant client posts the question with its history and returns the reply', async () => {
  const mock = new MockAdapter(axiosClient);
  const controller = new AbortController();
  mock.onPost('/showcase/assistant/chat').reply(config => {
    assert.deepEqual(JSON.parse(config.data), { message: 'Có dự án giáo dục không?', history: [{ role: 'user', content: 'chào' }] });
    assert.equal(config.signal, controller.signal);
    assert.equal(config.headers?.[ASSISTANT_KEY_HEADER], KEY, 'the user own key travels in a header');
    assert.doesNotMatch(String(config.url) + String(config.data), /AIzaSy/, 'never in the URL or the body');
    assert.equal(config.timeout, 90_000, 'the chat request must cover the backend model fallback budget');
    return [200, { data: { answer: 'Có EduBot', projects: [], suggestions: ['Còn gì nữa?'] } }];
  });
  try {
    const reply = await showcaseApi.askAssistant({ message: 'Có dự án giáo dục không?', history: [{ role: 'user', content: 'chào' }] }, KEY, controller.signal);
    assert.equal(reply.answer, 'Có EduBot');
    assert.deepEqual(reply.suggestions, ['Còn gì nữa?']);
  } finally {
    mock.restore();
  }
});

test('the assistant timeout does not change the timeout of other showcase requests', async () => {
  const mock = new MockAdapter(axiosClient);
  const controller = new AbortController();
  const id = '3f2b8c1e-5d4a-4b6f-9c0d-1a2b3c4d5e6f';
  mock.onPost('/showcase/assistant/chat').reply(200, { data: { answer: 'Có EduBot', projects: [], suggestions: [] } });
  mock.onGet(`/showcase/${id}/insights`).reply(config => {
    assert.equal(config.timeout, 15_000);
    assert.equal(config.signal, controller.signal);
    return [200, { data: {} }];
  });
  try {
    await showcaseApi.askAssistant({ message: 'Có dự án giáo dục không?', history: [] }, KEY);
    await showcaseApi.getInsights(id, controller.signal);
    assert.equal(axiosClient.defaults.timeout, 15_000);
  } finally {
    mock.restore();
  }
});

test('a pasted key is checked for shape only, with a reason the user can act on', () => {
  assert.equal(assistantKeyProblem(KEY), null);
  assert.equal(assistantKeyProblem('  ' + KEY + '\n'), null, 'a trailing space or newline is still the key');
  assert.equal(cleanAssistantKey('  ' + KEY + '\n'), KEY);
  assert.match(assistantKeyProblem('') ?? '', /dán/);
  assert.match(assistantKeyProblem('AIza with spaces inside the key 123') ?? '', /khoảng trắng/);
  assert.match(assistantKeyProblem('GEMINI_API_KEY=' + KEY) ?? '', /giá trị/);
  assert.match(assistantKeyProblem('https://aistudio.google.com/app/apikey') ?? '', /giá trị/);
  assert.match(assistantKeyProblem('AIzaShort') ?? '', /quá ngắn/);
  assert.match(assistantKeyProblem('A'.repeat(300)) ?? '', /ký tự thông thường/);
  // No message ever repeats what the user pasted.
  for (const bad of ['GEMINI_API_KEY=' + KEY, 'AIza with spaces inside the key 123']) assert.doesNotMatch(assistantKeyProblem(bad) ?? '', /AIza/);
});

test('a key problem reported by the server is told apart from a server fault', () => {
  const keyError = { response: { status: 400, data: { code: 'SHOWCASE_ASSISTANT_KEY_INVALID' } } };
  assert.equal(isAssistantKeyError(keyError), true);
  assert.equal(isAssistantKeyError({ response: { status: 400, data: { code: 'SHOWCASE_ASSISTANT_KEY_REQUIRED' } } }), true);
  assert.equal(isAssistantKeyError({ response: { status: 503, data: { code: 'SHOWCASE_ASSISTANT_UNAVAILABLE' } } }), false);
  assert.equal(isAssistantKeyError({ response: { status: 400, data: { code: 'SHOWCASE_VALIDATION_ERROR' } } }), false);
  assert.equal(isAssistantKeyError(new Error('boom')), false);
  assert.equal(isAssistantKeyError(null), false);
  // The server words it for the user and never repeats the key, so it is shown; any other 400 stays generic.
  assert.equal(assistantErrorMessage(keyError, 'Google không chấp nhận API key này.'), 'Google không chấp nhận API key này.');
  assert.match(assistantErrorMessage({ response: { status: 400, data: { code: 'SHOWCASE_VALIDATION_ERROR' } } }, 'raw validation text'), /Không gửi được/);
});

test('the key stays in memory: never stored, never sent anywhere but the chat request', () => {
  const component = readFileSync(new URL('../src/features/showcase/components/ShowcaseAssistant.tsx', import.meta.url), 'utf8');
  const client = readFileSync(new URL('../src/api/showcaseApi.ts', import.meta.url), 'utf8');
  // The only storage this component touches is the one-time greeting flag, and never next to the key.
  assert.doesNotMatch(component, /localStorage|indexedDB|document\.cookie/);
  assert.equal((component.match(/sessionStorage/g) ?? []).length, 2, 'only the greeting flag is read and written');
  assert.match(component, /const \[apiKey, setApiKey\] = useState\(''\)/);
  assert.match(component, /type="password"/);
  assert.match(component, /autoComplete="off"/);
  assert.match(component, /showcaseApi\.askAssistant\(\{ message: question, history \}, apiKey, request\.signal\)/);
  // A key the server refuses is forgotten and the question is given back, so nothing is lost and nothing is retried with it.
  assert.match(component, /if \(isAssistantKeyError\(error\)\)[\s\S]*?setDraft\(question\)[\s\S]*?setApiKey\(''\)/);
  assert.match(client, /headers: \{ \[ASSISTANT_KEY_HEADER\]: apiKey \}/);
  assert.doesNotMatch(client, /assistant\/chat\?/, 'the key is never put in the URL');
});
