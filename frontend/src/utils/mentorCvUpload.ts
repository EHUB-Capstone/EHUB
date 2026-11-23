import type { MentorCvMatchResponse, MentorProfile } from '../types/mentoring.ts';
import i18n from '../i18n/index.ts';

export const MENTOR_CV_MAX_BYTES = 10 * 1024 * 1024;
export const MENTOR_CV_MAX_FILES = 50;
export const MENTOR_CV_MAX_BATCH_BYTES = 100 * 1024 * 1024;

export interface CvFileMetadata { name: string; size: number; lastModified: number }
export type CvMentor = Pick<MentorProfile, 'id' | 'fullName' | 'email' | 'mentorType' | 'cvFileName'>;
export type CvUploadStatus = 'pending' | 'uploading' | 'success' | 'error';
export type CvEmailReadStatus = 'pending' | 'reading' | 'complete' | 'error';
export interface MentorCvUploadRow<T extends CvFileMetadata = File> {
  id: string;
  file: T;
  mentorId: string;
  replaceExisting: boolean;
  status: CvUploadStatus;
  error: string;
  suggested: boolean;
  emailReadStatus: CvEmailReadStatus;
  emailMatch: MentorCvMatchResponse | null;
  emailReadError: string;
}

export function validateMentorCvFile(file: Pick<CvFileMetadata, 'name' | 'size'>): string | null {
  if (!/\.(pdf|doc|docx)$/i.test(file.name)) return i18n.t('utils:mentorCvUpload.chooseAPdfDocOrDocx');
  if (!Number.isFinite(file.size) || file.size <= 0) return i18n.t('utils:mentorCvUpload.thisFileIsEmpty');
  if (file.size > MENTOR_CV_MAX_BYTES) return i18n.t('utils:mentorCvUpload.eachCvMustBe10Mb');
  return null;
}

export function cvFileKey(file: CvFileMetadata): string {
  return `${file.name.toLocaleLowerCase()}\u0000${file.size}\u0000${file.lastModified}`;
}

/** Adds files without discarding the current queue, including previous successful uploads. */
export function addMentorCvFiles<T extends CvFileMetadata>(rows: readonly MentorCvUploadRow<T>[], incoming: readonly T[],
): { rows: MentorCvUploadRow<T>[]; error: string } {
  const seen = new Set(rows.map(row => cvFileKey(row.file)));
  const unique = incoming.filter(file => {
    const key = cvFileKey(file);
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
  if (rows.length + unique.length > MENTOR_CV_MAX_FILES)
    return { rows: [...rows], error: `Select at most ${MENTOR_CV_MAX_FILES} CVs in one list.` };
  if (rows.reduce((total, row) => total + row.file.size, 0) + unique.reduce((total, file) => total + file.size, 0) > MENTOR_CV_MAX_BATCH_BYTES)
    return { rows: [...rows], error: 'The selected CVs exceed the 100 MB list limit.' };
  return { rows: [...rows, ...unique.map(file => {
    const invalid = validateMentorCvFile(file);
    return { id: cvFileKey(file), file, mentorId: '', replaceExisting: false,
      status: 'pending' as const, error: '', suggested: false,
      emailReadStatus: invalid ? 'error' as const : 'pending' as const,
      emailMatch: null, emailReadError: invalid ?? '' };
  })], error: unique.length < incoming.length ? 'Duplicate files were skipped.' : '' };
}

/** Only a unique server email match can select a mentor automatically. */
export function applyCvEmailMatch<T extends CvFileMetadata>(row: MentorCvUploadRow<T>,
  response: MentorCvMatchResponse, mentors: readonly CvMentor[]): MentorCvUploadRow<T> {
  if (row.status === 'success') return row;
  const matched = response.status === 'Matched' && mentors.some(mentor => mentor.id === response.mentorProfileId);
  return { ...row, mentorId: matched ? response.mentorProfileId! : '', suggested: matched,
    replaceExisting: false, status: 'pending', error: '', emailReadStatus: 'complete', emailMatch: response,
    emailReadError: response.status === 'Matched' && !matched ? 'The matched mentor is no longer in this list. Refresh the mentor list or choose manually.' : '' };
}

export function failCvEmailRead<T extends CvFileMetadata>(row: MentorCvUploadRow<T>, message: string): MentorCvUploadRow<T> {
  if (row.status === 'success') return row;
  return { ...row, mentorId: '', suggested: false, replaceExisting: false, status: 'pending', error: '',
    emailReadStatus: 'error', emailMatch: null, emailReadError: message };
}

export function describeCvEmailMatch(row: Pick<MentorCvUploadRow, 'emailReadStatus' | 'emailMatch' | 'emailReadError' | 'suggested'>): string {
  if (row.emailReadStatus === 'pending') return i18n.t('utils:mentorCvUpload.waitingToReadTheEmailIn');
  if (row.emailReadStatus === 'reading') return i18n.t('utils:mentorCvUpload.readingTheEmailInThisCv');
  if (row.emailReadError) return row.emailReadError;
  switch (row.emailMatch?.status) {
    case 'Matched': return row.suggested ? i18n.t('utils:mentorCvUpload.matchedByEmailFromCvVerify') : i18n.t('utils:mentorCvUpload.mentorSelectedManuallyAfterReadingThe');
    case 'NoEmail': return i18n.t('utils:mentorCvUpload.noEmailWasFoundInThis');
    case 'NoMatch': return i18n.t('utils:mentorCvUpload.noImportedMentorHasAMatching');
    case 'Ambiguous': return i18n.t('utils:mentorCvUpload.theCvEmailDoesNotIdentify');
    default: return '';
  }
}

export function mentorCvRowIssue<T extends CvFileMetadata>(row: MentorCvUploadRow<T>,
  rows: readonly MentorCvUploadRow<T>[], mentors: readonly CvMentor[]): string | null {
  if (row.status === 'success') return null;
  const fileIssue = validateMentorCvFile(row.file);
  if (fileIssue) return fileIssue;
  if (row.emailReadStatus === 'pending' || row.emailReadStatus === 'reading') return i18n.t('utils:mentorCvUpload.waitUntilTheCvEmailHas');
  if (!row.mentorId) return i18n.t('utils:mentorCvUpload.chooseTheMentorWhoOwnsThis');
  const mentor = mentors.find(item => item.id === row.mentorId);
  if (!mentor) return i18n.t('utils:mentorCvUpload.thisMentorIsNoLongerAvailable');
  if (rows.some(item => item.id !== row.id && item.mentorId === row.mentorId))
    return i18n.t('utils:mentorCvUpload.onlyOneCvCanBeUploaded');
  if (mentor.cvFileName && !row.replaceExisting) return i18n.t('utils:mentorCvUpload.confirmReplacementOfTheExistingCv');
  return null;
}

/** Retry only unfinished rows; successful requests must never be resubmitted. */
export function pendingMentorCvs<T extends CvFileMetadata>(rows: readonly MentorCvUploadRow<T>[]): MentorCvUploadRow<T>[] {
  return rows.filter(row => row.status !== 'success');
}
