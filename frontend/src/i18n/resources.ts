import enCommon from './locales/en/common.json' with { type: 'json' };
import enNav from './locales/en/nav.json' with { type: 'json' };
import viCommon from './locales/vi/common.json' with { type: 'json' };
import viNav from './locales/vi/nav.json' with { type: 'json' };

// One entry per namespace; `en` is the reference shape that `vi` must match
// (enforced by tests/i18nParity.test.ts).
export const resources = {
  en: {
    common: enCommon,
    nav: enNav,
  },
  vi: {
    common: viCommon,
    nav: viNav,
  },
} as const;

export const NAMESPACES = Object.keys(resources.en) as Array<keyof typeof resources.en>;
