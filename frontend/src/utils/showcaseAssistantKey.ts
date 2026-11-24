import type { DraftStorage } from './rubricDraft.ts';
import { assistantKeyProblem, cleanAssistantKey } from './showcaseAssistant.ts';

// The user's own Gemini key lives in memory by default. Only when the user ticks "remember" is it also kept in this browser tab's
// storage, under their own account, until the tab is closed, they sign out or they change the key. It is never put in storage that
// outlives the tab, and the server keeps none. This is deliberately separate from the saved conversation.
export const ASSISTANT_KEY_PREFIX = 'ehub:showcase-assistant-key:';
const ASSISTANT_KEY_VERSION = 1;
// A tab left open for days should not keep a credential around: after a day the user pastes the key again.
export const ASSISTANT_KEY_MAX_AGE_MS = 24 * 60 * 60 * 1000;

export const assistantKeyStorageName = (userId: string): string => `${ASSISTANT_KEY_PREFIX}${userId}`;

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

/** Keeps the key for this account in this tab. Returns whether it was kept; a key that does not look like a key is never stored. */
export function rememberAssistantKey(storage: DraftStorage | null, userId: string, rawKey: string, now: number = Date.now()): boolean {
  if (!storage || !userId) return false;
  const key = cleanAssistantKey(rawKey);
  if (assistantKeyProblem(key) !== null) return false;
  try {
    storage.setItem(assistantKeyStorageName(userId), JSON.stringify({ version: ASSISTANT_KEY_VERSION, savedAt: now, key }));
    return true;
  } catch {
    // Storage full or blocked: the key stays in memory only, so it has to be pasted again after a reload.
    return false;
  }
}

/** The key this account asked to be remembered in this tab, or an empty string when there is none or it can no longer be trusted. */
export function recallAssistantKey(storage: DraftStorage | null, userId: string, now: number = Date.now()): string {
  if (!storage || !userId) return '';
  const name = assistantKeyStorageName(userId);
  try {
    const raw = storage.getItem(name);
    if (!raw) return '';
    const parsed: unknown = JSON.parse(raw);
    const key = isRecord(parsed) && typeof parsed.key === 'string' ? parsed.key : '';
    const usable = isRecord(parsed) && parsed.version === ASSISTANT_KEY_VERSION && typeof parsed.savedAt === 'number'
      && parsed.savedAt <= now + 60_000 && now - parsed.savedAt <= ASSISTANT_KEY_MAX_AGE_MS && assistantKeyProblem(key) === null;
    if (!usable) {
      storage.removeItem(name);
      return '';
    }
    return key;
  } catch {
    return '';
  }
}

export function forgetAssistantKey(storage: DraftStorage | null, userId: string): void {
  if (!storage || !userId) return;
  try {
    storage.removeItem(assistantKeyStorageName(userId));
  } catch {
    // Nothing to clean up if storage is unavailable.
  }
}

// Used on logout so no remembered key outlives the sign-in it was saved for.
export function forgetAllAssistantKeys(storage: DraftStorage | null): void {
  if (!storage) return;
  try {
    const names: string[] = [];
    for (let index = 0; index < storage.length; index += 1) {
      const name = storage.key(index);
      if (name?.startsWith(ASSISTANT_KEY_PREFIX)) names.push(name);
    }
    names.forEach(name => storage.removeItem(name));
  } catch {
    // Nothing to clean up if storage is unavailable.
  }
}
