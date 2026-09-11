type FeatureEnvironment = Record<string, string | boolean | undefined>;

const isEnabled = (value: string | boolean | undefined): boolean =>
  value === true || (typeof value === 'string' && value.toLowerCase() === 'true');

const isEnabledByDefault = (value: string | boolean | undefined): boolean =>
  value === undefined ? true : isEnabled(value);

/**
 * Optional pages remain visible in the pilot by default. Each flag is still an
 * emergency release switch, allowing one page to be removed from navigation
 * without changing its source code if production verification finds a blocker.
 * Page visibility does not imply that an unfinished backend flow is release-ready.
 */
export const createReleaseFeatureFlags = (environment: FeatureEnvironment) => Object.freeze({
  ai: isEnabledByDefault(environment.VITE_FEATURE_AI),
  chat: isEnabledByDefault(environment.VITE_FEATURE_CHAT),
  dataBank: isEnabledByDefault(environment.VITE_FEATURE_DATA_BANK),
  evaluations: isEnabledByDefault(environment.VITE_FEATURE_EVALUATIONS),
  mentoring: isEnabledByDefault(environment.VITE_FEATURE_MENTORING),
  rankings: isEnabledByDefault(environment.VITE_FEATURE_RANKINGS),
  roleDashboards: isEnabledByDefault(environment.VITE_FEATURE_ROLE_DASHBOARDS),
  startupIdeas: isEnabledByDefault(environment.VITE_FEATURE_STARTUP_IDEAS),
  workshops: isEnabledByDefault(environment.VITE_FEATURE_WORKSHOPS),
});

const runtimeEnvironment = (import.meta as ImportMeta & { env?: FeatureEnvironment }).env ?? {};
export const releaseFeatureFlags = createReleaseFeatureFlags(runtimeEnvironment);
