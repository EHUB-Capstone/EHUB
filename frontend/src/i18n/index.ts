import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { DEFAULT_LANGUAGE, SUPPORTED_LANGUAGES, readStoredLanguage } from './config.ts';
import { NAMESPACES, resources } from './resources.ts';

// Resources are bundled and init is synchronous, so the first render already has
// translations and there is no flash of untranslated keys.
if (!i18n.isInitialized) {
  void i18n.use(initReactI18next).init({
    resources,
    lng: readStoredLanguage(),
    fallbackLng: DEFAULT_LANGUAGE,
    supportedLngs: SUPPORTED_LANGUAGES.map((language) => language.code),
    nonExplicitSupportedLngs: true,
    ns: NAMESPACES,
    defaultNS: 'common',
    interpolation: { escapeValue: false }, // React already escapes rendered values.
    returnNull: false,
    initAsync: false,
    react: { useSuspense: false },
  });
}

export default i18n;
