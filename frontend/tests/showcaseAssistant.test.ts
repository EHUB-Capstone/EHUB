import assert from 'node:assert/strict';
import test from 'node:test';
import MockAdapter from 'axios-mock-adapter';
import axiosClient from '../src/api/axiosClient.ts';
import { showcaseApi } from '../src/api/showcaseApi.ts';
import {
  ASSISTANT_HISTORY_TURNS,
  assistantErrorMessage,
  buildAssistantHistory,
  isShowcaseAssistantRoute,
  type AssistantChatMessage,
} from '../src/utils/showcaseAssistant.ts';

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
    assert.equal(config.timeout, 90_000, 'the chat request must cover the backend model fallback budget');
    return [200, { data: { answer: 'Có EduBot', projects: [], suggestions: ['Còn gì nữa?'] } }];
  });
  try {
    const reply = await showcaseApi.askAssistant({ message: 'Có dự án giáo dục không?', history: [{ role: 'user', content: 'chào' }] }, controller.signal);
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
    await showcaseApi.askAssistant({ message: 'Có dự án giáo dục không?', history: [] });
    await showcaseApi.getInsights(id, controller.signal);
    assert.equal(axiosClient.defaults.timeout, 15_000);
  } finally {
    mock.restore();
  }
});
