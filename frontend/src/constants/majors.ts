// frontend/src/constants/majors.ts

export interface Major {
  code: string;
  name: string;
}

export interface MajorGroup {
  key: string;
  label: string;
  majors: Major[];
}

/**
 * Two major groups used for EXE team formation validation:
 *  - Nhóm 1 (BBA-side): BBA_HM, BBA_IB, BBA_MC, BBA_MKT, BEN, BBA_TM
 *  - Nhóm 2 (BIT-side): BIT_AI, BIT_GD, BIT_IA, BIT_SE
 *
 * A team MUST include at least one student from each group.
 * Ratio doesn't matter; total team size must be 4–6.
 */
export const TEAM_MAJOR_GROUPS: MajorGroup[] = [
  {
    key: 'GROUP_1',
    label: 'Nhóm 1 (BBA)',
    majors: [
      { code: 'BBA_HM',  name: 'Hospitality Management' },
      { code: 'BBA_IB',  name: 'International Business' },
      { code: 'BBA_MC',  name: 'Marketing & Communication' },
      { code: 'BBA_MKT', name: 'Marketing' },
      { code: 'BEN',     name: 'Business English' },
      { code: 'BBA_TM',  name: 'Tourism Management' },
    ],
  },
  {
    key: 'GROUP_2',
    label: 'Nhóm 2 (BIT)',
    majors: [
      { code: 'BIT_AI', name: 'Artificial Intelligence' },
      { code: 'BIT_GD', name: 'Graphic Design' },
      { code: 'BIT_IA', name: 'Information Assurance' },
      { code: 'BIT_SE', name: 'Software Engineering' },
    ],
  },
];

/** Flat list of all valid team majors */
export const ALL_TEAM_MAJOR_CODES: string[] = TEAM_MAJOR_GROUPS.flatMap(g => g.majors.map(m => m.code));

/**
 * Full program groups (for registration / profile selects).
 * Includes every major a student may hold.
 */
export const PROGRAM_GROUPS: MajorGroup[] = [
  {
    key: 'BIT',
    label: 'Bachelor of Information Technology',
    majors: [
      { code: 'BIT_SE', name: 'Software Engineering' },
      { code: 'BIT_IA', name: 'Information Assurance' },
      { code: 'BIT_GD', name: 'Graphic Design' },
      { code: 'BIT_AI', name: 'Artificial Intelligence' },
      { code: 'BIT_IS', name: 'Information Systems' },
      { code: 'BIT_CS', name: 'Computer Science' },
      { code: 'BIT_CY', name: 'Cyber Security' },
      { code: 'BIT_DS', name: 'Data Science' },
    ],
  },
  {
    key: 'BBA',
    label: 'Bachelor of Business Administration',
    majors: [
      { code: 'BBA_IB',  name: 'International Business' },
      { code: 'BBA_MKT', name: 'Marketing' },
      { code: 'BBA_HM',  name: 'Hospitality Management' },
      { code: 'BBA_MC',  name: 'Marketing & Communication' },
      { code: 'BBA_TM',  name: 'Tourism Management' },
      { code: 'BBA_FIN', name: 'Finance' },
      { code: 'BBA_HRM', name: 'Human Resource Management' },
      { code: 'BBA_DM',  name: 'Digital Marketing' },
      { code: 'BBA_BA',  name: 'Business Administration' },
      { code: 'BBA_LOG', name: 'Logistics' },
    ],
  },
  {
    key: 'BEN',
    label: 'Business English / Other',
    majors: [
      { code: 'BEN', name: 'Business English' },
    ],
  },
  {
    key: 'BLA',
    label: 'Bachelor of Language Arts',
    majors: [
      { code: 'BLA_ELT', name: 'English Language Teaching' },
      { code: 'BLA_BC',  name: 'Business Communication' },
      { code: 'BLA_JP',  name: 'Japanese Language' },
      { code: 'BLA_KR',  name: 'Korean Language' },
      { code: 'BLA_CN',  name: 'Chinese Language' },
    ],
  },
];

/** Return display name for a major code */
export function getMajorName(majorCode: string | null | undefined): string | null {
  if (!majorCode) return null;
  for (const group of PROGRAM_GROUPS) {
    const major = group.majors.find(m => m.code === majorCode);
    if (major) return major.name;
  }
  return null;
}

/** Return which team group key ('GROUP_1' | 'GROUP_2' | null) a major belongs to */
export function getTeamGroupFromMajor(majorCode: string | null | undefined): string | null {
  if (!majorCode) return null;
  const code = majorCode.trim().toUpperCase();
  for (const g of TEAM_MAJOR_GROUPS) {
    if (g.majors.some(m => m.code === code)) return g.key;
  }
  return null;
}
