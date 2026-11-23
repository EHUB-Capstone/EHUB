import type { ApiResponse } from '../types/auth';
import { localizeApiMessage } from '../i18n/messages.ts';

export interface ParsedApiError {
  code: string;
  message: string;
  fieldErrors: Record<string, string>;
}

type ApiErrorResponse = Partial<ApiResponse<unknown>>;

export function parseApiError(error: unknown, fallbackMessage: string): ParsedApiError {
  const response = (error as { response?: { data?: ApiErrorResponse } }).response?.data;
  const fieldErrors = (response?.errors ?? []).reduce<Record<string, string>>((result, item) => {
    if (item.field && !result[item.field]) {
      result[item.field] = item.message;
    }
    return result;
  }, {});

  const code = response?.code
    ?? (response as { errorCode?: string | null } | undefined)?.errorCode
    ?? '';

  return {
    code,
    message: localizeApiMessage(code, response?.message, fallbackMessage),
    fieldErrors,
  };
}
