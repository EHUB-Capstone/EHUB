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
export const lazyNamespaces = {
  admin: {
    en: () => import('./locales/en/admin.json', { with: { type: 'json' } }),
    vi: () => import('./locales/vi/admin.json', { with: { type: 'json' } }),
  },
  ai: {
    en: () => import('./locales/en/ai.json', { with: { type: 'json' } }),
    vi: () => import('./locales/vi/ai.json', { with: { type: 'json' } }),
  },
  checkpoint: {
    en: () => import('./locales/en/checkpoint.json', { with: { type: 'json' } }),
    vi: () => import('./locales/vi/checkpoint.json', { with: { type: 'json' } }),
  },
  class: {
    en: () => import('./locales/en/class.json', { with: { type: 'json' } }),
    vi: () => import('./locales/vi/class.json', { with: { type: 'json' } }),
  },
  community: {
    en: () => import('./locales/en/community.json', { with: { type: 'json' } }),
    vi: () => import('./locales/vi/community.json', { with: { type: 'json' } }),
  },
  dashboard: {
    en: () => import('./locales/en/dashboard.json', { with: { type: 'json' } }),
    vi: () => import('./locales/vi/dashboard.json', { with: { type: 'json' } }),
  },
  evaluation: {
    en: () => import('./locales/en/evaluation.json', { with: { type: 'json' } }),
    vi: () => import('./locales/vi/evaluation.json', { with: { type: 'json' } }),
  },
  mentoring: {
    en: () => import('./locales/en/mentoring.json', { with: { type: 'json' } }),
    vi: () => import('./locales/vi/mentoring.json', { with: { type: 'json' } }),
  },
  profile: {
    en: () => import('./locales/en/profile.json', { with: { type: 'json' } }),
    vi: () => import('./locales/vi/profile.json', { with: { type: 'json' } }),
  },
  projectData: {
    en: () => import('./locales/en/projectData.json', { with: { type: 'json' } }),
    vi: () => import('./locales/vi/projectData.json', { with: { type: 'json' } }),
  },
  showcase: {
    en: () => import('./locales/en/showcase.json', { with: { type: 'json' } }),
    vi: () => import('./locales/vi/showcase.json', { with: { type: 'json' } }),
  },
  workspace: {
    en: () => import('./locales/en/workspace.json', { with: { type: 'json' } }),
    vi: () => import('./locales/vi/workspace.json', { with: { type: 'json' } }),
  },
} as const;

export const NAMESPACES = [...Object.keys(resources.en), ...Object.keys(lazyNamespaces)] as Array<
  keyof typeof resources.en | keyof typeof lazyNamespaces
>;
