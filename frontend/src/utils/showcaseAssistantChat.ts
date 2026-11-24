import type { ShowcaseAssistantProjectDto, ShowcaseStage } from '../types/showcase.ts';
import { ASSISTANT_MAX_MESSAGE_LENGTH, type AssistantChatMessage } from './showcaseAssistant.ts';
import type { DraftStorage } from './rubricDraft.ts';

// The conversation with the Showcase assistant is kept per browser tab, like the unsaved rubric drafts, so reloading the page (F5)
// does not wipe it. It belongs to one account, is removed when that account signs out, and it never contains the API key.
export const ASSISTANT_CHAT_PREFIX = 'ehub:showcase-assistant:';
const ASSISTANT_CHAT_VERSION = 1;
export const ASSISTANT_CHAT_MAX_MESSAGES = 40;
// Project cards show likes and views from the moment of the answer, so an old conversation is not worth restoring.
export const ASSISTANT_CHAT_MAX_AGE_MS = 24 * 60 * 60 * 1000;

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const MAX_ANSWER_LENGTH = 4_000;

export const assistantChatKey = (userId: string): string => `${ASSISTANT_CHAT_PREFIX}${userId}`;

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

const textOf = (value: unknown, maximum: number): string | null =>
  typeof value === 'string' && value.length <= maximum ? value : null;

const countOf = (value: unknown): number | null =>
  typeof value === 'number' && Number.isFinite(value) && value >= 0 ? value : null;

// Stored data is untrusted input: only plain web addresses are allowed as a logo, so nothing odd ends up in an image source.
const logoOf = (value: unknown): string | null =>
  typeof value === 'string' && value.length <= 2_048 && /^https?:\/\//i.test(value) ? value : null;

function readProject(value: unknown): ShowcaseAssistantProjectDto | null {
  if (!isRecord(value)) return null;
  const id = textOf(value.id, 36);
  const projectName = textOf(value.projectName, 200);
  const description = textOf(value.description, 400);
  const stage = textOf(value.developmentStage, 30);
  const teamName = textOf(value.teamName, 200);
  const reason = textOf(value.reason, 300);
  const likeCount = countOf(value.likeCount);
  const viewCount = countOf(value.viewCount);
  // The id becomes part of a link to the project page, so it has to be a real project id and nothing else.
  if (!id || !UUID.test(id) || projectName === null || description === null || !stage || teamName === null
    || reason === null || likeCount === null || viewCount === null) return null;
  const industries = Array.isArray(value.industries)
    ? value.industries.filter((item): item is string => typeof item === 'string' && item.length <= 100).slice(0, 10) : [];
  return {
    id, projectName, description, logoUrl: logoOf(value.logoUrl), developmentStage: stage as ShowcaseStage,
    teamName, industries, likeCount, viewCount, reason,
  };
}

function readMessage(value: unknown): AssistantChatMessage | null {
  if (!isRecord(value)) return null;
  const id = textOf(value.id, 64);
  if (!id) return null;
  if (value.role === 'user') {
    const content = textOf(value.content, ASSISTANT_MAX_MESSAGE_LENGTH);
    return content && content.trim() ? { id, role: 'user', content } : null;
  }
  if (value.role === 'assistant') {
    const content = textOf(value.content, MAX_ANSWER_LENGTH);
    if (content === null) return null;
    const projects = Array.isArray(value.projects)
      ? value.projects.map(readProject).filter((item): item is ShowcaseAssistantProjectDto => item !== null).slice(0, 6) : [];
    const suggestions = Array.isArray(value.suggestions)
      ? value.suggestions.filter((item): item is string => typeof item === 'string' && item.length > 0 && item.length <= 200).slice(0, 3) : [];
    return { id, role: 'assistant', content, projects, suggestions };
  }
  // Error bubbles are never restored: they describe a request that no longer exists.
  return null;
}

/**
 * The part of a conversation worth keeping: finished question and answer pairs only. A failed question (with its error bubble)
 * and a question still waiting for its answer are left out, only the latest messages are kept, and the first one is a question.
 */
export function persistableMessages(messages: readonly AssistantChatMessage[]): AssistantChatMessage[] {
  const failedQuestions = new Set<string>();
  for (const message of messages) if (message.role === 'error') failedQuestions.add(message.userMessageId);
  const finished = messages.filter(message => message.role !== 'error' && !failedQuestions.has(message.id));
  while (finished.length > 0 && finished[finished.length - 1].role === 'user') finished.pop();
  const recent = finished.slice(-ASSISTANT_CHAT_MAX_MESSAGES);
  const firstQuestion = recent.findIndex(message => message.role === 'user');
  return firstQuestion < 0 ? [] : recent.slice(firstQuestion);
}

export function saveAssistantChat(
  storage: DraftStorage | null, userId: string, messages: readonly AssistantChatMessage[], now: number = Date.now(),
): void {
  if (!storage || !userId) return;
  const kept = persistableMessages(messages);
  try {
    if (kept.length === 0) storage.removeItem(assistantChatKey(userId));
    else storage.setItem(assistantChatKey(userId), JSON.stringify({ version: ASSISTANT_CHAT_VERSION, savedAt: now, messages: kept }));
  } catch {
    // Storage full or blocked: the chat still works, it just will not survive a reload.
  }
}

export function loadAssistantChat(storage: DraftStorage | null, userId: string, now: number = Date.now()): AssistantChatMessage[] {
  if (!storage || !userId) return [];
  const key = assistantChatKey(userId);
  try {
    const raw = storage.getItem(key);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    const usable = isRecord(parsed) && parsed.version === ASSISTANT_CHAT_VERSION && typeof parsed.savedAt === 'number'
      && Array.isArray(parsed.messages) && parsed.savedAt <= now + 60_000 && now - parsed.savedAt <= ASSISTANT_CHAT_MAX_AGE_MS;
    if (!usable) {
      storage.removeItem(key);
      return [];
    }
    const messages = (parsed.messages as unknown[]).slice(-ASSISTANT_CHAT_MAX_MESSAGES * 2)
      .map(readMessage).filter((item): item is AssistantChatMessage => item !== null);
    return persistableMessages(messages);
  } catch {
    return [];
  }
}

export function clearAssistantChat(storage: DraftStorage | null, userId: string): void {
  if (!storage || !userId) return;
  try {
    storage.removeItem(assistantChatKey(userId));
  } catch {
    // Nothing to clean up if storage is unavailable.
  }
}

// Used on logout so the next person using this tab never sees another account's conversation.
export function clearAllAssistantChats(storage: DraftStorage | null): void {
  if (!storage) return;
  try {
    const keys: string[] = [];
    for (let index = 0; index < storage.length; index += 1) {
      const key = storage.key(index);
      if (key?.startsWith(ASSISTANT_CHAT_PREFIX)) keys.push(key);
    }
    keys.forEach(key => storage.removeItem(key));
  } catch {
    // Nothing to clean up if storage is unavailable.
  }
}
