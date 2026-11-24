import assert from 'node:assert/strict';
import test from 'node:test';
import MockAdapter from 'axios-mock-adapter';
import axiosClient from '../src/api/axiosClient.ts';
import i18n from '../src/i18n/index.ts';
import { showcaseApi } from '../src/api/showcaseApi.ts';
import { readFileSync } from 'node:fs';
import {
  ASSISTANT_HISTORY_TURNS,
  ASSISTANT_KEY_HEADER,
  assistantErrorMessage,
  assistantKeyLast4,
  assistantKeyProblem,
  assistantKeyRequestError,
  assistantKeySource,
  buildAssistantHistory,
  cleanAssistantKey,
  isAssistantKeyError,
  isAssistantKeyMissing,
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

test('assistant errors never expose raw exceptions (Vietnamese)', async () => {
  await i18n.changeLanguage('vi');
  try {
    assert.match(assistantErrorMessage({ response: { status: 429 } }, undefined), /đợi/);
    assert.equal(assistantErrorMessage({ response: { status: 503 } }, 'Trợ lý AI chưa được bật.'), 'Trợ lý AI chưa được bật.');
    assert.match(assistantErrorMessage(new Error('System.NullReferenceException at Foo'), 'System.NullReferenceException'), /Không gửi được/);
    assert.match(assistantErrorMessage({ response: { status: 500 } }, 'stack trace here'), /Không gửi được/);
  } finally {
    await i18n.changeLanguage('en');
  }
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

test('a pasted key is checked for shape only, with a reason the user can act on (Vietnamese)', async () => {
  await i18n.changeLanguage('vi');
  try {
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
  } finally {
    await i18n.changeLanguage('en');
  }
});

test('a key problem reported by the server is told apart from a server fault (Vietnamese)', async () => {
  await i18n.changeLanguage('vi');
  try {
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
  } finally {
    await i18n.changeLanguage('en');
  }
});

test('a key given with the question has a header of its own and nothing else; without one no key header is sent', async () => {
  const mock = new MockAdapter(axiosClient);
  mock.onPost('/showcase/assistant/chat').reply(config => {
    assert.equal(config.headers?.[ASSISTANT_KEY_HEADER], undefined, 'the server then uses the key saved to the account');
    assert.deepEqual(JSON.parse(config.data), { message: 'Có dự án giáo dục không?', history: [] });
    return [200, { data: { answer: 'Có EduBot', projects: [], suggestions: [] } }];
  });
  try {
    const reply = await showcaseApi.askAssistant({ message: 'Có dự án giáo dục không?', history: [] }, null);
    assert.equal(reply.answer, 'Có EduBot');
  } finally {
    mock.restore();
  }
});

test('the key saved to the account is looked at, saved and removed through its own endpoint; the key only travels in the body of a save', async () => {
  const mock = new MockAdapter(axiosClient);
  const controller = new AbortController();
  const status = { storageAvailable: true, hasKey: true, last4: KEY.slice(-4), updatedAt: '2026-11-24T09:00:00Z' };
  const none = { storageAvailable: true, hasKey: false, last4: null, updatedAt: null };
  mock.onGet('/showcase/assistant/key').reply(config => {
    assert.equal(config.data, undefined);
    assert.equal(config.signal, controller.signal);
    assert.equal(config.headers?.[ASSISTANT_KEY_HEADER], undefined);
    return [200, { data: status }];
  });
  mock.onPut('/showcase/assistant/key').reply(config => {
    assert.deepEqual(JSON.parse(config.data), { apiKey: KEY }, 'the key is in the body, nowhere else');
    assert.doesNotMatch(String(config.url) + JSON.stringify(config.params ?? {}), /AIzaSy/, 'never in the URL');
    assert.equal(config.headers?.[ASSISTANT_KEY_HEADER], undefined, 'not in the chat header either');
    return [200, { data: status }];
  });
  mock.onDelete('/showcase/assistant/key').reply(config => {
    assert.equal(config.data, undefined);
    return [200, { data: none }];
  });
  try {
    assert.deepEqual(await showcaseApi.getAssistantKey(controller.signal), status);
    assert.deepEqual(await showcaseApi.saveAssistantKey(KEY), status);
    assert.deepEqual(await showcaseApi.deleteAssistantKey(), none);
    for (const answer of [status, none]) assert.ok(!('apiKey' in answer) && !JSON.stringify(answer).includes('AIzaSy'), 'an answer never carries the key');
  } finally {
    mock.restore();
  }
});

test('which key a question uses: one kept here wins over the saved one, and a saved key that was just refused is not offered again', () => {
  assert.equal(assistantKeySource(KEY, { hasKey: true }, false), 'typed');
  assert.equal(assistantKeySource(KEY, null, false), 'typed');
  assert.equal(assistantKeySource(KEY, { hasKey: true }, true), 'typed', 'a refused saved key does not hide a key typed since');
  assert.equal(assistantKeySource('', { hasKey: true }, false), 'saved');
  assert.equal(assistantKeySource('', { hasKey: true }, true), 'none');
  assert.equal(assistantKeySource('', { hasKey: false }, false), 'none');
  assert.equal(assistantKeySource('', null, false), 'none');
  assert.equal(assistantKeyLast4(KEY), KEY.slice(-4));
  assert.equal(assistantKeyLast4('abc'), 'abc');
});

test('a missing key is told apart from a refused one, and trouble with the saved key is worded for the user (English)', () => {
  const missing = { response: { status: 400, data: { code: 'SHOWCASE_ASSISTANT_KEY_REQUIRED' } } };
  const refused = { response: { status: 400, data: { code: 'SHOWCASE_ASSISTANT_KEY_INVALID' } } };
  assert.equal(isAssistantKeyMissing(missing), true);
  assert.equal(isAssistantKeyMissing(refused), false);
  assert.equal(isAssistantKeyMissing(new Error('boom')), false);
  assert.equal(isAssistantKeyMissing(null), false);

  const off = { response: { status: 503, data: { code: 'SHOWCASE_ASSISTANT_KEY_STORAGE_UNAVAILABLE' } } };
  assert.match(assistantKeyRequestError(off, 'save'), /cannot keep keys/i);
  assert.match(assistantKeyRequestError({ response: { status: 429 } }, 'save'), /wait/i);
  assert.match(assistantKeyRequestError({ response: { status: 400, data: { code: 'SHOWCASE_VALIDATION_ERROR' } } }, 'save'), /not accepted/i);
  assert.match(assistantKeyRequestError({ response: { status: 500 } }, 'save'), /Could not save/);
  assert.match(assistantKeyRequestError(new Error('System.NullReferenceException at Foo'), 'save'), /Could not save/);
  assert.match(assistantKeyRequestError({ response: { status: 500 } }, 'remove'), /Could not remove/);
  assert.match(assistantKeyRequestError({ response: { status: 500 } }, 'check'), /Could not check/);
  assert.doesNotMatch(assistantKeyRequestError({ response: { status: 500, data: { message: 'stack trace here' } } }, 'save'), /stack trace/);
});

test('trouble with the saved key is worded in Vietnamese too', async () => {
  await i18n.changeLanguage('vi');
  try {
    const off = { response: { status: 503, data: { code: 'SHOWCASE_ASSISTANT_KEY_STORAGE_UNAVAILABLE' } } };
    assert.match(assistantKeyRequestError(off, 'save'), /chưa bật việc lưu key/);
    assert.match(assistantKeyRequestError({ response: { status: 429 } }, 'remove'), /đợi/);
    assert.match(assistantKeyRequestError({ response: { status: 500 } }, 'save'), /Không lưu được/);
    assert.match(assistantKeyRequestError({ response: { status: 500 } }, 'remove'), /Không xóa được/);
    assert.match(assistantKeyRequestError({ response: { status: 500 } }, 'check'), /Không kiểm tra được/);
  } finally {
    await i18n.changeLanguage('en');
  }
});

test('the key stays on this page unless the user chooses to keep it: in this tab, or encrypted on the server under their account', () => {
  const read = (path: string) => readFileSync(new URL(path, import.meta.url), 'utf8');
  const component = read('../src/features/showcase/components/ShowcaseAssistant.tsx');
  const settings = read('../src/features/showcase/components/ShowcaseAssistantKeySettings.tsx');
  const hook = read('../src/features/showcase/hooks/useAssistantServerKey.ts');
  const client = read('../src/api/showcaseApi.ts');
  const keyStore = read('../src/utils/showcaseAssistantKey.ts');
  const types = read('../src/types/showcase.ts');
  const auth = read('../src/context/AuthContext.tsx');
  // Nothing that outlives the tab is ever used for the key: not by the screens, nor by the helper that keeps it on request.
  for (const source of [component, settings, hook, keyStore]) assert.doesNotMatch(source, /localStorage|indexedDB|document\.cookie/);
  // The screens only touch the one-time greeting flag; a key kept in this tab goes through the helper and this tab's storage.
  assert.equal((component.match(/sessionStorage/g) ?? []).length, 2, 'only the greeting flag is read and written');
  assert.doesNotMatch(settings + hook, /sessionStorage|getBrowserStorage/);
  // The key only ever starts from what the helper recalls for this account, and keeping it anywhere is a choice that starts at "this page only".
  assert.match(component, /const \[apiKey, setApiKey\] = useState\(\(\) => recallAssistantKey\(/);
  assert.match(component, /const \[keyStorage, setKeyStorage\] = useState<AssistantKeyStorage>\('session'\)/);
  assert.match(component, /if \(keyStorage === 'tab'\) setKeyRemembered\(rememberAssistantKey\(/);
  assert.match(component, /type="password"/);
  assert.match(component, /autoComplete="off"/);
  // A key kept here goes with the question; without one the server uses the saved key.
  assert.match(component, /showcaseApi\.askAssistant\(\{ message: question, history \}, apiKey \|\| null, request\.signal\)/);
  // Saving to the account sends the key once; nothing is lost when that fails, and when it works this page keeps no copy of it.
  assert.match(component, /if \(keyStorage === 'account'\) \{(?:(?!setKeyDraft\(''\))[\s\S])*?await savedKey\.save\(key\)[\s\S]*?assistantKeyRequestError\(error, 'save'\)[\s\S]*?return;[\s\S]*?setApiKey\(''\)[\s\S]*?forgetAssistantKey\(/);
  assert.match(component, /if \(saving\.current\) return;/, 'a second press while saving does nothing, even before React re-renders');
  // A key kept here that the server refuses is forgotten, also from this tab's storage, and the question is given back: nothing
  // is lost and nothing is retried with it. A saved key that is refused stays on the account, only not offered again.
  assert.match(component, /if \(isAssistantKeyError\(error\)\)[\s\S]*?setDraft\(question\)[\s\S]*?if \(apiKey\) \{[\s\S]*?setApiKey\(''\)[\s\S]*?forgetAssistantKey\([\s\S]*?\} else \{[\s\S]*?setSavedRefused\(true\)/);
  assert.equal((component.match(/savedKey\.remove\(/g) ?? []).length, 1, 'the saved key is only ever removed by the user, through the one confirmed button');
  assert.match(settings, /removeSavedKeyPrompt[\s\S]*?removeSavedKeyConfirm/, 'removing asks first');
  // Letting go of the key kept here forgets it everywhere on this page, and signing out removes every key kept in this tab.
  assert.match(component, /const forgetTypedKey = \(\) => \{[\s\S]*?setApiKey\(''\)[\s\S]*?forgetAssistantKey\(/);
  assert.match(auth, /forgetAllAssistantKeys\(getBrowserStorage\(\)\)/);
  assert.doesNotMatch(auth, /deleteAssistantKey|showcaseApi/, 'signing out does not remove the key saved to the account: that is the point of saving it');
  // The key header only exists when a key is given; the key is only ever in that header or in the body of a save, never in a URL.
  assert.match(client, /\.\.\.\(apiKey \? \{ headers: \{ \[ASSISTANT_KEY_HEADER\]: apiKey \} \} : \{\}\)/);
  assert.match(client, /put\('\/showcase\/assistant\/key', \{ apiKey \}/);
  assert.doesNotMatch(client, /assistant\/(chat|key)\?/, 'the key is never put in the URL');
  // What the server tells about the saved key never includes the key.
  assert.match(types, /interface ShowcaseAssistantKeyStatusDto \{[^}]*last4: string \| null;[^}]*\}/);
  assert.doesNotMatch(types.match(/interface ShowcaseAssistantKeyStatusDto \{[^}]*\}/)?.[0] ?? '', /apiKey|key:/);
});

test('assistant messages are English by default and never expose raw exceptions', () => {
  assert.match(assistantErrorMessage({ response: { status: 429 } }, undefined), /wait/i);
  assert.match(assistantErrorMessage(new Error('System.NullReferenceException at Foo'), 'System.NullReferenceException'), /Could not send/);
  assert.match(assistantKeyProblem('') ?? '', /paste/i);
  assert.match(assistantKeyProblem('AIzaShort') ?? '', /too short/i);
});
