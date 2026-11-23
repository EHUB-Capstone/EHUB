import i18n from './index.ts';

// Localizes messages that frontend utilities build in English. The utilities keep returning an English
// `message` plus a stable `code`, so existing callers and tests do not change; the UI layer translates by
// field + code and falls back to the original message when no translation exists.
export interface LocalizableValidationError {
  field: string;
  message: string;
  code: string;
}

export function localizeValidationMessage(namespace: string, error: LocalizableValidationError): string {
  const key = `${namespace}:validation.${error.field}.${error.code}`;
  return i18n.exists(key) ? i18n.t(key) : error.message;
}

export function localizeValidationErrors<T extends LocalizableValidationError>(namespace: string, errors: readonly T[]): T[] {
  return errors.map((error) => ({ ...error, message: localizeValidationMessage(namespace, error) }));
}

// Backend errors carry a stable `code`. In English the server's own (often more specific) message wins; in other
// languages a translated message for the code wins, and the server message is the fallback for unknown codes.
export function localizeApiMessage(code: string | undefined | null, serverMessage: string | undefined | null, fallback: string): string {
  const key = code ? `errors:codes.${code}` : '';
  const localized = key && i18n.exists(key) ? i18n.t(key) : '';
  const isEnglish = (i18n.resolvedLanguage ?? i18n.language ?? 'en').toLowerCase().startsWith('en');
  return isEnglish
    ? (serverMessage || localized || fallback)
    : (localized || serverMessage || fallback);
}
