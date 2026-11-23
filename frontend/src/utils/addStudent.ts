import i18n from '../i18n/index.ts';
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

  if (!studentCode) errors.studentCode = i18n.t('utils:addStudent.studentCodeIsRequired');
  else if (studentCode.length > 20) errors.studentCode = i18n.t('utils:addStudent.studentCodeCannotExceed20Characters');

  if (!fullName) errors.fullName = i18n.t('utils:addStudent.fullNameIsRequired');
  else if (fullName.length > 150) errors.fullName = i18n.t('utils:addStudent.fullNameCannotExceed150Characters');

  if (!email) errors.email = i18n.t('utils:addStudent.emailIsRequired');
  else if (email.length > 150 || !emailPattern.test(email)) errors.email = i18n.t('utils:addStudent.enterAValidEmailAddress');

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
