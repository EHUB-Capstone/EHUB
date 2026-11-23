import enAuth from './locales/en/auth.json' with { type: 'json' };
import enCommon from './locales/en/common.json' with { type: 'json' };
import enErrors from './locales/en/errors.json' with { type: 'json' };
import enHome from './locales/en/home.json' with { type: 'json' };
import enNav from './locales/en/nav.json' with { type: 'json' };
import enUtils from './locales/en/utils.json' with { type: 'json' };
import viAuth from './locales/vi/auth.json' with { type: 'json' };
import viCommon from './locales/vi/common.json' with { type: 'json' };
import viErrors from './locales/vi/errors.json' with { type: 'json' };
import viHome from './locales/vi/home.json' with { type: 'json' };
import viNav from './locales/vi/nav.json' with { type: 'json' };
import viUtils from './locales/vi/utils.json' with { type: 'json' };

// Namespaces bundled with the app: shell, auth, shared messages and the utilities that run outside React.
// `en` is the reference shape that every other language must match (tests/i18nParity.test.ts).
export const resources = {
  en: {
    auth: enAuth,
    common: enCommon,
    errors: enErrors,
    home: enHome,
    nav: enNav,
    utils: enUtils,
  },
  vi: {
    auth: viAuth,
    common: viCommon,
    errors: viErrors,
    home: viHome,
    nav: viNav,
    utils: viUtils,
  },
} as const;

// Feature namespaces are split into their own chunks and fetched the first time a screen needs them.
// No JSON import attribute here: Vite's dev server rewrites the URL to a JS module, which browsers reject
// when a JSON attribute is present. Node cannot import these without it, so tests load them from disk.
export const lazyNamespaces = {
  admin: {
    en: () => import('./locales/en/admin.json'),
    vi: () => import('./locales/vi/admin.json'),
  },
  ai: {
    en: () => import('./locales/en/ai.json'),
    vi: () => import('./locales/vi/ai.json'),
  },
  checkpoint: {
    en: () => import('./locales/en/checkpoint.json'),
    vi: () => import('./locales/vi/checkpoint.json'),
  },
  class: {
    en: () => import('./locales/en/class.json'),
    vi: () => import('./locales/vi/class.json'),
  },
  community: {
    en: () => import('./locales/en/community.json'),
    vi: () => import('./locales/vi/community.json'),
  },
  dashboard: {
    en: () => import('./locales/en/dashboard.json'),
    vi: () => import('./locales/vi/dashboard.json'),
  },
  evaluation: {
    en: () => import('./locales/en/evaluation.json'),
    vi: () => import('./locales/vi/evaluation.json'),
  },
  mentoring: {
    en: () => import('./locales/en/mentoring.json'),
    vi: () => import('./locales/vi/mentoring.json'),
  },
  profile: {
    en: () => import('./locales/en/profile.json'),
    vi: () => import('./locales/vi/profile.json'),
  },
  projectData: {
    en: () => import('./locales/en/projectData.json'),
    vi: () => import('./locales/vi/projectData.json'),
  },
  showcase: {
    en: () => import('./locales/en/showcase.json'),
    vi: () => import('./locales/vi/showcase.json'),
  },
  workspace: {
    en: () => import('./locales/en/workspace.json'),
    vi: () => import('./locales/vi/workspace.json'),
  },
} as const;

export const NAMESPACES = [...Object.keys(resources.en), ...Object.keys(lazyNamespaces)] as Array<
  keyof typeof resources.en | keyof typeof lazyNamespaces
>;
