type FeatureEnvironment = Record<string, string | boolean | undefined>;

const isEnabled = (value: string | boolean | undefined): boolean =>
  value === true || (typeof value === 'string' && value.toLowerCase() === 'true');

export const createClassFeatureFlags = (environment: FeatureEnvironment) => Object.freeze({
  showDevelopmentControls: isEnabled(environment.DEV) ||
    isEnabled(environment.VITE_SHOW_UNAVAILABLE_CLASS_FEATURES),
  rename: isEnabled(environment.VITE_FEATURE_CLASS_RENAME),
  lifecycle: isEnabled(environment.VITE_FEATURE_CLASS_LIFECYCLE),
  majorVerification: environment.VITE_FEATURE_CLASS_MAJOR_VERIFICATION === undefined
    ? true
    : isEnabled(environment.VITE_FEATURE_CLASS_MAJOR_VERIFICATION),
  mentorAssignment: environment.VITE_FEATURE_CLASS_MENTOR_ASSIGNMENT === undefined
    ? true
    : isEnabled(environment.VITE_FEATURE_CLASS_MENTOR_ASSIGNMENT),
  teamManagement: environment.VITE_FEATURE_CLASS_TEAM_MANAGEMENT === undefined
    ? true
    : isEnabled(environment.VITE_FEATURE_CLASS_TEAM_MANAGEMENT),
  chatBackfill: isEnabled(environment.VITE_FEATURE_CLASS_CHAT_BACKFILL),
  codeConflictReport: isEnabled(environment.VITE_FEATURE_CLASS_CODE_CONFLICT_REPORT),
  projectDirection: environment.VITE_FEATURE_CLASS_PROJECT_DIRECTION === undefined
    ? true
    : isEnabled(environment.VITE_FEATURE_CLASS_PROJECT_DIRECTION),
  studentSelfService: environment.VITE_FEATURE_CLASS_STUDENT_SELF_SERVICE === undefined
    ? true
    : isEnabled(environment.VITE_FEATURE_CLASS_STUDENT_SELF_SERVICE),
  lecturerStudentImport: environment.VITE_FEATURE_CLASS_LECTURER_STUDENT_IMPORT === undefined
    ? true
    : isEnabled(environment.VITE_FEATURE_CLASS_LECTURER_STUDENT_IMPORT),
});

const runtimeEnvironment = (import.meta as ImportMeta & { env?: FeatureEnvironment }).env ?? {};
export const classFeatureFlags = createClassFeatureFlags(runtimeEnvironment);

export class ClassFeatureDisabledError extends Error {
  readonly code = 'CLASS_FEATURE_DISABLED';

  constructor(featureName: string) {
    super(`${featureName} is temporarily unavailable while safety hardening is in progress.`);
    this.name = 'ClassFeatureDisabledError';
  }
}

export const runClassFeatureRequest = <T>(
  enabled: boolean,
  featureName: string,
  request: () => Promise<T>,
): Promise<T> => {
  if (!enabled) {
    return Promise.reject(new ClassFeatureDisabledError(featureName));
  }

  return request();
};
