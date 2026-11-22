import enAuth from './locales/en/auth.json' with { type: 'json' };
import enCommon from './locales/en/common.json' with { type: 'json' };
import enDashboard from './locales/en/dashboard.json' with { type: 'json' };
import enHome from './locales/en/home.json' with { type: 'json' };
import enNav from './locales/en/nav.json' with { type: 'json' };
import viAuth from './locales/vi/auth.json' with { type: 'json' };
import viCommon from './locales/vi/common.json' with { type: 'json' };
import viDashboard from './locales/vi/dashboard.json' with { type: 'json' };
import viHome from './locales/vi/home.json' with { type: 'json' };
import viNav from './locales/vi/nav.json' with { type: 'json' };

// One entry per namespace; `en` is the reference shape that `vi` must match
// (enforced by tests/i18nParity.test.ts).
export const resources = {
  en: {
    auth: enAuth,
    common: enCommon,
    dashboard: enDashboard,
    home: enHome,
    nav: enNav,
  },
  vi: {
    auth: viAuth,
    common: viCommon,
    dashboard: viDashboard,
    home: viHome,
    nav: viNav,
  },
} as const;

export const NAMESPACES = Object.keys(resources.en) as Array<keyof typeof resources.en>;
