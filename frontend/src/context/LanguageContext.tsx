import { createContext, useCallback, useContext, useEffect, useMemo } from 'react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import {
  SUPPORTED_LANGUAGES,
  normalizeLanguage,
  writeStoredLanguage,
  type LanguageCode,
  type LanguageOption,
} from '../i18n/config.ts';
import '../i18n';

interface LanguageContextType {
  language: LanguageCode;
  languages: readonly LanguageOption[];
  setLanguage: (code: LanguageCode) => void;
}

const LanguageContext = createContext<LanguageContextType>({
  language: 'en',
  languages: SUPPORTED_LANGUAGES,
  setLanguage: () => {},
});

// Owns the language preference: i18next holds the active language, localStorage
// remembers the choice, and <html lang> / the document title follow it. It is
// independent from ThemeContext and AuthContext, so switching never touches them.
export const LanguageProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const { i18n, t } = useTranslation();
  const language = normalizeLanguage(i18n.resolvedLanguage ?? i18n.language);

  const setLanguage = useCallback((code: LanguageCode) => {
    writeStoredLanguage(code);
    void i18n.changeLanguage(code);
  }, [i18n]);

  useEffect(() => {
    document.documentElement.lang = language;
    document.title = t('meta.title');
    document.querySelector('meta[name="description"]')?.setAttribute('content', t('meta.description'));
  }, [language, t]);

  const value = useMemo(
    () => ({ language, languages: SUPPORTED_LANGUAGES, setLanguage }),
    [language, setLanguage],
  );

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
};

export const useLanguage = () => useContext(LanguageContext);
