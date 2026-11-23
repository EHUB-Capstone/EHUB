import i18n from 'i18next';
import type { BackendModule } from 'i18next';
import { initReactI18next } from 'react-i18next';
import { DEFAULT_LANGUAGE, SUPPORTED_LANGUAGES, readStoredLanguage } from './config.ts';
import { NAMESPACES, lazyNamespaces, resources } from './resources.ts';

// Shared namespaces are bundled (see resources.ts) so the shell, login and the utilities that run outside
// React translate synchronously. Feature namespaces are separate chunks; this minimal backend fetches them
// the first time a screen asks for them, and react-i18next suspends that screen (the route-level <Suspense>
// shows the page spinner) until the text has arrived.
const lazyBackend: BackendModule = {
  type: 'backend',
  init() {},
  read(language, namespace, callback) {
    const loaders = (lazyNamespaces as Record<string, Record<string, () => Promise<{ default: object }>>>)[namespace];
    const load = loaders?.[language];
    if (!load) {
      callback(null, {});
      return;
    }
    load().then((module) => callback(null, module.default), (error: Error) => callback(error, null));
  },
};

if (!i18n.isInitialized) {
  void i18n
    .use(lazyBackend)
    .use(initReactI18next)
    .init({
      resources,
      partialBundledLanguages: true,
      lng: readStoredLanguage(),
      fallbackLng: DEFAULT_LANGUAGE,
      supportedLngs: SUPPORTED_LANGUAGES.map((language) => language.code),
      nonExplicitSupportedLngs: true,
      ns: Object.keys(resources.en),
      defaultNS: 'common',
      interpolation: { escapeValue: false }, // React already escapes rendered values.
      returnNull: false,
      initAsync: false,
      react: { useSuspense: true },
    });
}

/** Loads every namespace for a language, so a language switch never shows half-translated screens. */
export function loadAllNamespaces(language: string): Promise<void> {
  return i18n.loadNamespaces([...NAMESPACES]).then(() => i18n.loadLanguages(language)).then(() => undefined);
}

export default i18n;
