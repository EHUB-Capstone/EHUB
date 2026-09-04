// @ts-nocheck
// frontend/src/constants/majors.js

/**
 * Two major groups used for EXE team formation validation:
 *  - Nhóm 1 (BBA-side): BBA_HM, BBA_IB, BBA_MC, BBA_MKT, BEN, BBA_TM
 *  - Nhóm 2 (BIT-side): BIT_AI, BIT_GD, BIT_IA, BIT_SE
 *
 * A team MUST include at least one student from each group.
 * Ratio doesn't matter; total team size must be 4–6.
 */
export const TEAM_MAJOR_GROUPS = [
  {
    key: 'GROUP_1',
    label: 'GROUP 1 (BBA)',
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
    label: 'GROUP 2 (BIT)',
    majors: [
      { code: 'BIT_AI', name: 'Artificial Intelligence' },
      { code: 'BIT_GD', name: 'Graphic Design' },
      { code: 'BIT_IA', name: 'Information Assurance' },
      { code: 'BIT_SE', name: 'Software Engineering' },
    ],
  },
];

/** Flat list of all valid team majors */
export const ALL_TEAM_MAJOR_CODES = TEAM_MAJOR_GROUPS.flatMap(g => g.majors.map(m => m.code));

/**
 * Supported program groups for registration and administrative student forms.
 * Keep this catalog aligned with TEAM_MAJOR_GROUPS and backend MajorCodes.All.
 */
export const PROGRAM_GROUPS = [
  {
    code: 'BBA',
    name: 'Bachelor of Business Administration',
    majors: [
      { code: 'BBA_HM',  name: 'Hospitality Management' },
      { code: 'BBA_IB',  name: 'International Business' },
      { code: 'BBA_MC',  name: 'Marketing & Communication' },
      { code: 'BBA_MKT', name: 'Marketing' },
      { code: 'BBA_TM',  name: 'Tourism Management' },
    ],
  },
  {
    code: 'BEN',
    name: 'Bachelor of Business English',
    majors: [
      { code: 'BEN', name: 'Business English' },
    ],
  },
  {
    code: 'BIT',
    name: 'Bachelor of Information Technology',
    majors: [
      { code: 'BIT_AI', name: 'Artificial Intelligence' },
      { code: 'BIT_GD', name: 'Graphic Design' },
      { code: 'BIT_IA', name: 'Information Assurance' },
      { code: 'BIT_SE', name: 'Software Engineering' },
    ],
  },
];

/** Return display name for a major code */
export function getMajorName(majorCode) {
  if (!majorCode) return null;
  for (const group of PROGRAM_GROUPS) {
    const major = group.majors.find(m => m.code === majorCode);
    if (major) return major.name;
  }
  return null;
}

/** Return which team group key ('GROUP_1' | 'GROUP_2' | null) a major belongs to */
export function getTeamGroupFromMajor(majorCode) {
  if (!majorCode) return null;
  const code = majorCode.trim().toUpperCase();
  for (const g of TEAM_MAJOR_GROUPS) {
    if (g.majors.some(m => m.code === code)) return g.key;
  }
  return null;
}
