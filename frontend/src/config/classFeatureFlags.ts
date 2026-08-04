type FeatureEnvironment = Record<string, string | boolean | undefined>;

const isEnabled = (value: string | boolean | undefined): boolean =>
  value === true || (typeof value === 'string' && value.toLowerCase() === 'true');

export const createClassFeatureFlags = (environment: FeatureEnvironment) => Object.freeze({
  rename: isEnabled(environment.VITE_FEATURE_CLASS_RENAME),
  lifecycle: isEnabled(environment.VITE_FEATURE_CLASS_LIFECYCLE),
  majorVerification: isEnabled(environment.VITE_FEATURE_CLASS_MAJOR_VERIFICATION),
  mentorAssignment: isEnabled(environment.VITE_FEATURE_CLASS_MENTOR_ASSIGNMENT),
  teamManagement: isEnabled(environment.VITE_FEATURE_CLASS_TEAM_MANAGEMENT),
  chatBackfill: isEnabled(environment.VITE_FEATURE_CLASS_CHAT_BACKFILL),
  codeConflictReport: isEnabled(environment.VITE_FEATURE_CLASS_CODE_CONFLICT_REPORT),
  projectDirection: isEnabled(environment.VITE_FEATURE_CLASS_PROJECT_DIRECTION),
  studentSelfService: isEnabled(environment.VITE_FEATURE_CLASS_STUDENT_SELF_SERVICE),
  lecturerStudentImport: isEnabled(environment.VITE_FEATURE_CLASS_LECTURER_STUDENT_IMPORT),
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
