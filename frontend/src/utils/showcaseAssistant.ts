import type { ShowcaseAssistantProjectDto, ShowcaseAssistantTurn } from '../types/showcase.ts';

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

export function assistantErrorMessage(error: unknown, serverMessage: string | undefined): string {
  const status = (error as { response?: { status?: number } } | null)?.response?.status;
  if (status === 429) return 'Bạn hỏi hơi nhanh. Hãy đợi một chút rồi thử lại nhé.';
  if (status === 503 && serverMessage) return serverMessage;
  return 'Không gửi được câu hỏi. Vui lòng thử lại.';
}
