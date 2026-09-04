export interface AddStudentFormValues {
  studentCode: string;
  fullName: string;
  email: string;
  majorCode: string;
}

export type AddStudentField = keyof AddStudentFormValues;
export type AddStudentFormErrors = Partial<Record<AddStudentField, string>>;

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function validateAddStudentForm(values: AddStudentFormValues): AddStudentFormErrors {
  const errors: AddStudentFormErrors = {};
  const studentCode = values.studentCode.trim();
  const fullName = values.fullName.trim();
  const email = values.email.trim();

  if (!studentCode) errors.studentCode = 'Student code is required.';
  else if (studentCode.length > 20) errors.studentCode = 'Student code cannot exceed 20 characters.';

  if (!fullName) errors.fullName = 'Full name is required.';
  else if (fullName.length > 150) errors.fullName = 'Full name cannot exceed 150 characters.';

  if (!email) errors.email = 'Email is required.';
  else if (email.length > 150 || !emailPattern.test(email)) errors.email = 'Enter a valid email address.';

  return errors;
}

export function buildAddStudentPayload(values: AddStudentFormValues) {
  return {
    studentCode: values.studentCode.trim().toUpperCase(),
    fullName: values.fullName.trim(),
    email: values.email.trim().toLowerCase(),
    majorCode: values.majorCode.trim() || null,
  };
}
