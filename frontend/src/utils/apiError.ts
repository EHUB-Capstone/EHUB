import type { ApiResponse } from '../types/auth';

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

  return {
    code: response?.code
      ?? (response as { errorCode?: string | null } | undefined)?.errorCode
      ?? '',
    message: response?.message || fallbackMessage,
    fieldErrors,
  };
}
