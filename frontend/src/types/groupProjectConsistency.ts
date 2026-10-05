export type GroupProjectWarningType = 'GROUP_HAS_MULTIPLE_PROJECTS' | 'PROJECT_HAS_MULTIPLE_GROUPS';

/** One violation of the 1-1 Group/Project rule, evaluated by the backend. */
export interface GroupProjectWarning {
  type: GroupProjectWarningType;
  /** The Group (or Project) that is assigned to several Projects (or Groups). */
  subject: string;
  related: string[];
  message: string;
}

export interface GroupProjectConsistency {
  isConsistent: boolean;
  warnings: GroupProjectWarning[];
}
