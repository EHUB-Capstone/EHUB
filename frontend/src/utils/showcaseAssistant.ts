import type { ShowcaseAssistantProjectDto, ShowcaseAssistantTurn } from '../types/showcase.ts';
import i18n from '../i18n/index.ts';

export const ASSISTANT_MAX_MESSAGE_LENGTH = 500;
/** The server keeps only the most recent turns, so the client does not send more. */
export const ASSISTANT_HISTORY_TURNS = 6;

export const ASSISTANT_STARTER_QUESTIONS: readonly string[] = [
  'Có dự án nào về giáo dục không?',
  'Dự án nào được thích nhiều nhất?',
  'Có dự án nào mới đăng gần đây?',
  'Dự án nào đã ra mắt rồi?',
];

export type AssistantChatMessage =
  | { id: string; role: 'user'; content: string }
  | { id: string; role: 'assistant'; content: string; projects: ShowcaseAssistantProjectDto[]; suggestions: string[] }
  // `userMessageId` is the question that failed, so a retry can replace both instead of duplicating the question.
  | { id: string; role: 'error'; content: string; question: string; userMessageId: string };

/** Only real conversation turns go to the server; error bubbles and any message the caller excludes do not. */
export function buildAssistantHistory(
  messages: readonly AssistantChatMessage[],
  excludeIds: readonly string[] = [],
): ShowcaseAssistantTurn[] {
  const turns: ShowcaseAssistantTurn[] = [];
  for (const message of messages) {
    if (message.role === 'error' || excludeIds.includes(message.id)) continue;
    turns.push({ role: message.role, content: message.content });
  }
  return turns.slice(-ASSISTANT_HISTORY_TURNS);
}

const ASSISTANT_ROUTE = /^\/showcase(\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})?\/?$/i;

/** The assistant lives on the gallery and on a project's detail page, not on the manage or moderation screens. */
export const isShowcaseAssistantRoute = (pathname: string): boolean => ASSISTANT_ROUTE.test(pathname);

/** The header that carries the user's own Gemini key. The server keeps no key of its own for the assistant. */
export const ASSISTANT_KEY_HEADER = 'X-Gemini-Api-Key';

export const ASSISTANT_KEY_URL = 'https://aistudio.google.com/app/apikey';

// The same bounds as the backend (ShowcaseAssistantApiKey): they only reject an empty paste and junk; the provider decides the rest.
const KEY_MIN_LENGTH = 20;
const KEY_MAX_LENGTH = 256;

/** A key pasted with a trailing space or newline is still the user's key. */
export const cleanAssistantKey = (raw: string): string => raw.trim();

/** Why a pasted key cannot be right, in words for the user; null when it looks like a key. Nothing is sent anywhere to find out. */
export function assistantKeyProblem(raw: string): string | null {
  const key = cleanAssistantKey(raw);
  if (key.length === 0) return i18n.t('utils:showcaseAssistant.hayDanApiKeyGeminiCua');
  if (/\s/.test(key)) return i18n.t('utils:showcaseAssistant.apiKeyKhongCoKhoangTrang');
  if (key.includes('://') || key.includes('=')) return i18n.t('utils:showcaseAssistant.chiDanPhanGiaTriCua');
  if (key.length < KEY_MIN_LENGTH) return i18n.t('utils:showcaseAssistant.apiKeyQuaNganHayKiem');
  if (key.length > KEY_MAX_LENGTH || !/^[\x21-\x7E]+$/.test(key)) return i18n.t('utils:showcaseAssistant.apiKeyChiGomChuCai');
  return null;
}

/** Where a key the user types is kept: on this page only, in this tab until it is closed, or encrypted on the server under their account. */
export type AssistantKeyStorage = 'session' | 'tab' | 'account';

/** Which key a question is asked with: one typed here, the one saved to the account, or none yet. */
export type AssistantKeySource = 'typed' | 'saved' | 'none';

/**
 * A key typed on this page or kept in this tab wins over the one saved to the account, exactly as it does on the server. A saved
 * key the server has just refused is not offered again until the user acts (it may be gone, revoked or out of quota).
 */
export function assistantKeySource(typedKey: string, saved: { hasKey: boolean } | null, savedRefused: boolean): AssistantKeySource {
  if (typedKey) return 'typed';
  return saved?.hasKey && !savedRefused ? 'saved' : 'none';
}

/** The last four characters, which is all that is ever shown of a key. */
export const assistantKeyLast4 = (key: string): string => key.slice(-4);

const KEY_REQUIRED_CODE = 'SHOWCASE_ASSISTANT_KEY_REQUIRED';
const KEY_ERROR_CODES = new Set([KEY_REQUIRED_CODE, 'SHOWCASE_ASSISTANT_KEY_INVALID']);
const KEY_STORAGE_UNAVAILABLE_CODE = 'SHOWCASE_ASSISTANT_KEY_STORAGE_UNAVAILABLE';

function responseOf(error: unknown): { status: number | undefined; code: string | undefined } {
  const response = (error as { response?: { status?: number; data?: { code?: unknown } } } | null)?.response;
  const code = response?.data?.code;
  return { status: response?.status, code: typeof code === 'string' ? code : undefined };
}

/** True when the server says the problem is the user's key (missing, malformed, refused or out of quota), so the key must be asked for again. */
export function isAssistantKeyError(error: unknown): boolean {
  const { code } = responseOf(error);
  return code !== undefined && KEY_ERROR_CODES.has(code);
}

/** True when the server found no key at all: none was sent and none is saved (any more) for this account. */
export const isAssistantKeyMissing = (error: unknown): boolean => responseOf(error).code === KEY_REQUIRED_CODE;

export type AssistantKeyAction = 'check' | 'save' | 'remove';

/** Words for a failed look at, save of, or removal of the key kept on the account. They never repeat a key. */
export function assistantKeyRequestError(error: unknown, action: AssistantKeyAction): string {
  const { status, code } = responseOf(error);
  if (code === KEY_STORAGE_UNAVAILABLE_CODE) return i18n.t('utils:showcaseAssistant.keyStorageUnavailable');
  if (status === 429) return i18n.t('utils:showcaseAssistant.banHoiHoiNhanhHayDoi');
  if (action === 'save') return status === 400 ? i18n.t('utils:showcaseAssistant.keyNotAccepted') : i18n.t('utils:showcaseAssistant.keySaveFailed');
  return action === 'remove' ? i18n.t('utils:showcaseAssistant.keyRemoveFailed') : i18n.t('utils:showcaseAssistant.keyCheckFailed');
}

export function assistantErrorMessage(error: unknown, serverMessage: string | undefined): string {
  const status = (error as { response?: { status?: number } } | null)?.response?.status;
  if (status === 429) return i18n.t('utils:showcaseAssistant.banHoiHoiNhanhHayDoi');
  // The server words key problems for the user and never repeats the key, so its message is safe to show.
  if ((status === 503 || (status === 400 && isAssistantKeyError(error))) && serverMessage) return serverMessage;
  return i18n.t('utils:showcaseAssistant.khongGuiDuocCauHoiVui');
}
