import type { IncompleteMentor, UpdateIncompleteMentorPayload } from '../types/mentorAdmin';

/** The edit form keeps every value as text; empty text means "not known yet". */
export interface IncompleteMentorFormValues {
  fullName: string;
  /** Login email. Leave empty to keep the mentor without an account; fill it in to create the account now. */
  email: string;
  fptEmail: string;
  phone: string;
  dateOfBirth: string;
  contractType: string;
  educationLevel: string;
  currentAddress: string;
  organization: string;
  department: string;
  jobTitle: string;
}

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const LIMITS = { fullName: 100, phone: 30, contractType: 100, educationLevel: 200, currentAddress: 500, organization: 200, department: 200, jobTitle: 200 };

export function toFormValues(mentor: IncompleteMentor): IncompleteMentorFormValues {
  return {
    fullName: mentor.fullName,
    email: '',
    fptEmail: mentor.fptEmail ?? '',
    phone: mentor.phone ?? '',
    dateOfBirth: mentor.dateOfBirth ?? '',
    contractType: mentor.contractType ?? '',
    educationLevel: mentor.educationLevel ?? '',
    currentAddress: mentor.currentAddress ?? '',
    organization: mentor.organization ?? '',
    department: mentor.department ?? '',
    jobTitle: mentor.jobTitle ?? '',
  };
}

/** Returns the first problem as one sentence, or null when the form can be saved. */
export function validateFormValues(values: IncompleteMentorFormValues, mentorType: IncompleteMentor['mentorType']): string | null {
  const name = values.fullName.trim();
  if (name.length < 2) return 'Mentor name is required (at least 2 characters).';
  if (name.length > LIMITS.fullName) return `Mentor name may contain at most ${LIMITS.fullName} characters.`;
  const checks: [string, keyof typeof LIMITS, string][] = mentorType === 'Enterprise'
    ? [[values.phone, 'phone', 'Phone number'], [values.contractType, 'contractType', 'Contract type'], [values.educationLevel, 'educationLevel', 'Education level'],
       [values.currentAddress, 'currentAddress', 'Current address'], [values.organization, 'organization', 'Company'], [values.jobTitle, 'jobTitle', 'Job title']]
    : [[values.department, 'department', 'Department'], [values.jobTitle, 'jobTitle', 'Job title']];
  for (const [value, key, label] of checks) {
    if (value.trim().length > LIMITS[key]) return `${label} may contain at most ${LIMITS[key]} characters.`;
  }
  const loginEmail = values.email.trim();
  if (loginEmail && !EMAIL_PATTERN.test(loginEmail)) return 'Login email must be a valid email address.';
  const fpt = values.fptEmail.trim();
  if (mentorType === 'Enterprise' && fpt && !EMAIL_PATTERN.test(fpt)) return 'Fpt Email must be a valid email address when provided.';
  if (mentorType === 'Enterprise' && values.dateOfBirth) {
    const born = new Date(`${values.dateOfBirth}T00:00:00`);
    if (Number.isNaN(born.getTime()) || born > new Date() || born.getFullYear() < 1900) return 'Date of birth is outside the allowed range.';
  }
  return null;
}

/** Only the fields of the mentor's own kind are sent; blank text becomes null so the field counts as missing again. */
export function toPayload(values: IncompleteMentorFormValues, mentorType: IncompleteMentor['mentorType']): UpdateIncompleteMentorPayload {
  const text = (value: string) => (value.trim() ? value.trim() : null);
  const common = { fullName: values.fullName.trim(), email: text(values.email), jobTitle: text(values.jobTitle) };
  if (mentorType === 'Academic') return { ...common, department: text(values.department) };
  return {
    ...common,
    fptEmail: text(values.fptEmail),
    phone: text(values.phone),
    dateOfBirth: values.dateOfBirth || null,
    contractType: text(values.contractType),
    educationLevel: text(values.educationLevel),
    currentAddress: text(values.currentAddress),
    organization: text(values.organization),
  };
}
