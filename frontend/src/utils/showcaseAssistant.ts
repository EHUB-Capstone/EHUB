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

const KEY_ERROR_CODES = new Set(['SHOWCASE_ASSISTANT_KEY_REQUIRED', 'SHOWCASE_ASSISTANT_KEY_INVALID']);

/** True when the server says the problem is the user's key (missing, malformed, refused or out of quota), so the key must be asked for again. */
export function isAssistantKeyError(error: unknown): boolean {
  const code = (error as { response?: { data?: { code?: unknown } } } | null)?.response?.data?.code;
  return typeof code === 'string' && KEY_ERROR_CODES.has(code);
}

export function assistantErrorMessage(error: unknown, serverMessage: string | undefined): string {
  const status = (error as { response?: { status?: number } } | null)?.response?.status;
  if (status === 429) return i18n.t('utils:showcaseAssistant.banHoiHoiNhanhHayDoi');
  // The server words key problems for the user and never repeats the key, so its message is safe to show.
  if ((status === 503 || (status === 400 && isAssistantKeyError(error))) && serverMessage) return serverMessage;
  return i18n.t('utils:showcaseAssistant.khongGuiDuocCauHoiVui');
}
