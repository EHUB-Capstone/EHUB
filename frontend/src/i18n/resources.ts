import enCommon from './locales/en/common.json' with { type: 'json' };
import viCommon from './locales/vi/common.json' with { type: 'json' };

// One entry per namespace; `en` is the reference shape that `vi` must match
// (enforced by tests/i18nParity.test.ts).
export const resources = {
  en: {
    common: enCommon,
  },
  vi: {
    common: viCommon,
  },
} as const;

export const NAMESPACES = Object.keys(resources.en) as Array<keyof typeof resources.en>;
