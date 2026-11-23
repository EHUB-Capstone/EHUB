import enAdmin from './locales/en/admin.json' with { type: 'json' };
import enAuth from './locales/en/auth.json' with { type: 'json' };
import enCheckpoint from './locales/en/checkpoint.json' with { type: 'json' };
import enClass from './locales/en/class.json' with { type: 'json' };
import enCommon from './locales/en/common.json' with { type: 'json' };
import enDashboard from './locales/en/dashboard.json' with { type: 'json' };
import enEvaluation from './locales/en/evaluation.json' with { type: 'json' };
import enHome from './locales/en/home.json' with { type: 'json' };
import enMentoring from './locales/en/mentoring.json' with { type: 'json' };
import enNav from './locales/en/nav.json' with { type: 'json' };
import enShowcase from './locales/en/showcase.json' with { type: 'json' };
import enWorkspace from './locales/en/workspace.json' with { type: 'json' };
import viAdmin from './locales/vi/admin.json' with { type: 'json' };
import viAuth from './locales/vi/auth.json' with { type: 'json' };
import viCheckpoint from './locales/vi/checkpoint.json' with { type: 'json' };
import viClass from './locales/vi/class.json' with { type: 'json' };
import viCommon from './locales/vi/common.json' with { type: 'json' };
import viDashboard from './locales/vi/dashboard.json' with { type: 'json' };
import viEvaluation from './locales/vi/evaluation.json' with { type: 'json' };
import viHome from './locales/vi/home.json' with { type: 'json' };
import viMentoring from './locales/vi/mentoring.json' with { type: 'json' };
import viNav from './locales/vi/nav.json' with { type: 'json' };
import viShowcase from './locales/vi/showcase.json' with { type: 'json' };
import viWorkspace from './locales/vi/workspace.json' with { type: 'json' };

// One entry per namespace; `en` is the reference shape that `vi` must match
// (enforced by tests/i18nParity.test.ts).
export const resources = {
  en: {
    admin: enAdmin,
    auth: enAuth,
    checkpoint: enCheckpoint,
    class: enClass,
    common: enCommon,
    dashboard: enDashboard,
    evaluation: enEvaluation,
    home: enHome,
    mentoring: enMentoring,
    nav: enNav,
    showcase: enShowcase,
    workspace: enWorkspace,
  },
  vi: {
    admin: viAdmin,
    auth: viAuth,
    checkpoint: viCheckpoint,
    class: viClass,
    common: viCommon,
    dashboard: viDashboard,
    evaluation: viEvaluation,
    home: viHome,
    mentoring: viMentoring,
    nav: viNav,
    showcase: viShowcase,
    workspace: viWorkspace,
  },
} as const;

export const NAMESPACES = Object.keys(resources.en) as Array<keyof typeof resources.en>;
