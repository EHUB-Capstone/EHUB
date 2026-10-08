import type { MentorProfile, MentorProfileStatus, UpdateMentorProfilePayload } from '../types/mentorProfile';

/** Same limits as the server, so most mistakes are shown before the request is sent. */
export const MENTOR_PROFILE_LIMITS = {
  expertiseItems: 20,
  expertiseMin: 2,
  expertiseMax: 50,
  bio: 2000,
  availabilityNote: 500,
  organization: 200,
  department: 200,
  jobTitle: 200,
  contractType: 100,
  educationLevel: 200,
  currentAddress: 500,
  linkedInUrl: 500,
  fptEmail: 320,
} as const;

export const MENTOR_PROFILE_STATUSES: ReadonlyArray<{ value: MentorProfileStatus; label: string; hint: string }> = [
  { value: 'Active', label: 'Active', hint: 'Can be assigned to teams.' },
  { value: 'Inactive', label: 'Inactive', hint: 'Not used for new assignments. Existing assignments stay.' },
  { value: 'Unavailable', label: 'Unavailable', hint: 'Temporarily cannot take teams. Existing assignments stay.' },
];

export interface MentorProfileFormValues {
  status: MentorProfileStatus;
  expertise: string[];
  startupDomains: string[];
  technologySkills: string[];
  mentorTags: string[];
  bio: string;
  availabilityNote: string;
  organization: string;
  department: string;
  jobTitle: string;
  contractType: string;
  educationLevel: string;
  currentAddress: string;
  linkedInUrl: string;
  fptEmail: string;
  /** yyyy-MM-dd, or empty. */
  dateOfBirth: string;
}

export type MentorProfileFormErrors = Partial<Record<keyof MentorProfileFormValues, string>>;

export function normalizeExpertiseTag(raw: string): string {
  return raw.trim().replace(/\s+/g, ' ');
}

/** Adds a tag when it is valid; otherwise returns the reason and leaves the list unchanged. */
export function addExpertiseTag(current: readonly string[], raw: string): { tags: string[]; error: string | null } {
  return addTag('expertise', 'tags', current, raw);
}

/** The same rule for every kind of tag; `noun` names the kind in messages. */
export function addTagOfKind(noun: string) {
  return (current: readonly string[], raw: string) => addTag(noun, 'tags', current, raw);
}

function addTag(noun: string, plural: string, current: readonly string[], raw: string): { tags: string[]; error: string | null } {
  const value = normalizeExpertiseTag(raw);
  if (value === '') return { tags: [...current], error: null };
  if (value.length < MENTOR_PROFILE_LIMITS.expertiseMin || value.length > MENTOR_PROFILE_LIMITS.expertiseMax) {
    return { tags: [...current], error: `Each ${noun} must be ${MENTOR_PROFILE_LIMITS.expertiseMin} to ${MENTOR_PROFILE_LIMITS.expertiseMax} characters.` };
  }
  if (current.some(tag => tag.toLowerCase() === value.toLowerCase())) {
    return { tags: [...current], error: `"${value}" is already listed.` };
  }
  if (current.length >= MENTOR_PROFILE_LIMITS.expertiseItems) {
    return { tags: [...current], error: `A mentor can have at most ${MENTOR_PROFILE_LIMITS.expertiseItems} ${noun} ${plural}.` };
  }
  return { tags: [...current, value], error: null };
}

export function profileToFormValues(profile: MentorProfile): MentorProfileFormValues {
  return {
    status: profile.status,
    expertise: [...profile.expertise],
    startupDomains: [...profile.startupDomains],
    technologySkills: [...profile.technologySkills],
    mentorTags: [...profile.mentorTags],
    bio: profile.bio ?? '',
    availabilityNote: profile.availabilityNote ?? '',
    organization: profile.organization ?? '',
    department: profile.department ?? '',
    jobTitle: profile.jobTitle ?? '',
    contractType: profile.contractType ?? '',
    educationLevel: profile.educationLevel ?? '',
    currentAddress: profile.currentAddress ?? '',
    linkedInUrl: profile.linkedInUrl ?? '',
    fptEmail: profile.fptEmail ?? '',
    dateOfBirth: profile.dateOfBirth ?? '',
  };
}

const isHttpUrl = (value: string) => {
  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
};

export function validateMentorProfileForm(values: MentorProfileFormValues, today: Date = new Date()): MentorProfileFormErrors {
  const errors: MentorProfileFormErrors = {};
  const tooLong = (key: keyof typeof MENTOR_PROFILE_LIMITS & keyof MentorProfileFormValues, label: string) => {
    if (values[key].toString().trim().length > MENTOR_PROFILE_LIMITS[key]) errors[key] = `${label} may contain at most ${MENTOR_PROFILE_LIMITS[key]} characters.`;
  };
  tooLong('bio', 'Background');
  tooLong('availabilityNote', 'Availability note');
  tooLong('organization', 'Organization');
  tooLong('department', 'Department');
  tooLong('jobTitle', 'Job title');
  tooLong('contractType', 'Contract type');
  tooLong('educationLevel', 'Education level');
  tooLong('currentAddress', 'Address');

  const linkedIn = values.linkedInUrl.trim();
  if (linkedIn && (linkedIn.length > MENTOR_PROFILE_LIMITS.linkedInUrl || !isHttpUrl(linkedIn))) errors.linkedInUrl = 'LinkedIn URL must be a valid http or https address.';

  const fptEmail = values.fptEmail.trim();
  if (fptEmail && (fptEmail.length > MENTOR_PROFILE_LIMITS.fptEmail || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(fptEmail))) errors.fptEmail = 'FPT email must be a valid email address.';

  if (values.dateOfBirth) {
    const date = new Date(`${values.dateOfBirth}T00:00:00`);
    if (Number.isNaN(date.getTime()) || date.getFullYear() < 1900 || date > today) errors.dateOfBirth = 'Date of birth is outside the allowed range.';
  }
  return errors;
}

export function toUpdatePayload(values: MentorProfileFormValues, rowVersion: string): UpdateMentorProfilePayload {
  const text = (value: string) => value.trim() || null;
  return {
    rowVersion,
    status: values.status,
    expertise: values.expertise,
    startupDomains: values.startupDomains,
    technologySkills: values.technologySkills,
    mentorTags: values.mentorTags,
    bio: text(values.bio),
    availabilityNote: text(values.availabilityNote),
    organization: text(values.organization),
    department: text(values.department),
    jobTitle: text(values.jobTitle),
    contractType: text(values.contractType),
    educationLevel: text(values.educationLevel),
    currentAddress: text(values.currentAddress),
    linkedInUrl: text(values.linkedInUrl),
    fptEmail: text(values.fptEmail),
    dateOfBirth: values.dateOfBirth || null,
  };
}

/** True when the form differs from the stored profile, so an untouched form does not light up Save. */
export function hasProfileChanges(profile: MentorProfile, values: MentorProfileFormValues): boolean {
  return JSON.stringify(toUpdatePayload(profileToFormValues(profile), profile.rowVersion))
    !== JSON.stringify(toUpdatePayload(values, profile.rowVersion));
}
