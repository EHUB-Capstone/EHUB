// Single source of truth for supported UI languages. Adding a language means adding
// one entry here plus a `locales/<code>/` folder with the same namespaces as `en`.
export const SUPPORTED_LANGUAGES = [
  { code: 'en', label: 'English', short: 'EN', intlLocale: 'en-US' },
  { code: 'vi', label: 'Tiếng Việt', short: 'VI', intlLocale: 'vi-VN' },
] as const;

export type LanguageCode = (typeof SUPPORTED_LANGUAGES)[number]['code'];
export type LanguageOption = (typeof SUPPORTED_LANGUAGES)[number];

export const DEFAULT_LANGUAGE: LanguageCode = 'en';
export const LANGUAGE_STORAGE_KEY = 'ehub-language';

export function isSupportedLanguage(value: unknown): value is LanguageCode {
  return SUPPORTED_LANGUAGES.some((language) => language.code === value);
}

// i18next may report region variants such as "vi-VN"; reduce them to a supported code.
export function normalizeLanguage(value: string | null | undefined): LanguageCode {
  const base = (value ?? '').toLowerCase().split('-')[0];
  return isSupportedLanguage(base) ? base : DEFAULT_LANGUAGE;
}

export function getLanguageOption(code: string | null | undefined): LanguageOption {
  const normalized = normalizeLanguage(code);
  return SUPPORTED_LANGUAGES.find((language) => language.code === normalized) ?? SUPPORTED_LANGUAGES[0];
}

export function readStoredLanguage(): LanguageCode {
  try {
    return normalizeLanguage(localStorage.getItem(LANGUAGE_STORAGE_KEY));
  } catch {
    // Storage can be blocked or absent (tests); fall back to the default language.
    return DEFAULT_LANGUAGE;
  }
}

export function writeStoredLanguage(code: LanguageCode): void {
  try {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, code);
  } catch {
    // The language still changes for this session when storage is unavailable.
  }
}
