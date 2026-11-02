import assert from 'node:assert/strict';
import test from 'node:test';
import {
  MENTOR_CV_MAX_BATCH_BYTES, MENTOR_CV_MAX_BYTES, MENTOR_CV_MAX_FILES,
  addMentorCvFiles, applyCvEmailMatch, failCvEmailRead, mentorCvRowIssue, pendingMentorCvs, validateMentorCvFile,
  type CvFileMetadata, type CvMentor, type MentorCvUploadRow,
} from '../src/utils/mentorCvUpload.ts';

const mentors: CvMentor[] = [
  { id: 'mentor-a', fullName: 'Đặng Minh An', email: 'an@example.test', mentorType: 'Business', cvFileName: null },
  { id: 'mentor-b', fullName: 'Demo Mentor B', email: 'b@example.test', mentorType: 'IT', cvFileName: 'old.docx' },
];
const file = (name = 'CV-Dang Minh An.docx', size = 123, lastModified = 1): CvFileMetadata => ({ name, size, lastModified });
const row = (mentorId = 'mentor-a', status: MentorCvUploadRow<CvFileMetadata>['status'] = 'pending'): MentorCvUploadRow<CvFileMetadata> => ({
  id: 'row-a', file: file(), mentorId, status, error: '', suggested: false, replaceExisting: false,
  emailReadStatus: 'complete', emailMatch: null, emailReadError: '',
});

test('accepts full Word and PDF files, with case-insensitive extensions', () => {
  for (const name of ['cv.pdf', 'cv.DOC', 'cv.docx']) assert.equal(validateMentorCvFile(file(name)), null);
  assert.match(validateMentorCvFile(file('cv.xlsx'))!, /PDF, DOC or DOCX/);
  assert.match(validateMentorCvFile(file('cv.docx.exe'))!, /PDF, DOC or DOCX/);
});

test('rejects empty and oversized CVs while accepting the exact 10 MB boundary', () => {
  assert.match(validateMentorCvFile(file('cv.docx', 0))!, /empty/);
  assert.equal(validateMentorCvFile(file('cv.docx', MENTOR_CV_MAX_BYTES)), null);
  assert.match(validateMentorCvFile(file('cv.docx', MENTOR_CV_MAX_BYTES + 1))!, /10 MB/);
});

test('a matching filename never selects a mentor before reading the CV email', () => {
  const added = addMentorCvFiles([], [file('CV_ĐẶNG_Minh_An.DOCX')]).rows[0];
  assert.equal(added.mentorId, '');
  assert.equal(added.suggested, false);
  assert.equal(added.emailReadStatus, 'pending');
  assert.match(mentorCvRowIssue(added, [added], mentors)!, /Wait until/);
});

test('a unique email match selects the returned mentor despite a different filename', () => {
  const matched = applyCvEmailMatch(row(), { status: 'Matched', mentorProfileId: 'mentor-b', detectedEmails: ['b@example.test'] }, mentors);
  assert.equal(matched.mentorId, 'mentor-b');
  assert.equal(matched.suggested, true);
  assert.equal(matched.emailReadStatus, 'complete');
  assert.match(mentorCvRowIssue(matched, [matched], mentors)!, /Confirm replacement/);
});

test('single and multiple selections append to the queue and skip duplicate files', () => {
  const first = addMentorCvFiles<CvFileMetadata>([], [file()]);
  assert.equal(first.rows.length, 1);
  assert.equal(first.rows[0].mentorId, '');
  assert.equal(first.rows[0].suggested, false);
  const next = addMentorCvFiles(first.rows, [file(), file('Unknown.docx')]);
  assert.equal(next.rows.length, 2);
  assert.match(next.error, /Duplicate files/);
  assert.equal(next.rows[1].mentorId, '');
});

test('duplicates within one file selection are skipped without dropping valid siblings', () => {
  const result = addMentorCvFiles<CvFileMetadata>([], [file(), file(), file('Other.pdf')]);
  assert.equal(result.rows.length, 2);
  assert.match(result.error, /Duplicate files/);
});

test('adding files preserves existing upload states and replacement consent', () => {
  const existing = { ...row('mentor-b', 'success'), replaceExisting: true };
  const result = addMentorCvFiles([existing], [file('New.docx')]);
  assert.deepEqual(result.rows[0], existing);
});

test('an over-limit file count leaves the current queue intact', () => {
  const existing = row();
  const result = addMentorCvFiles([existing], Array.from({ length: MENTOR_CV_MAX_FILES }, (_, i) => file(`File-${i}.docx`)));
  assert.deepEqual(result.rows, [existing]);
  assert.match(result.error, /at most 50/);
});

test('the total-byte limit accounts for files already held in the queue', () => {
  const existing = { ...row(), file: file('Existing.docx', MENTOR_CV_MAX_BATCH_BYTES - 1) };
  const result = addMentorCvFiles([existing], [file('New.docx', 2)]);
  assert.deepEqual(result.rows, [existing]);
  assert.match(result.error, /100 MB/);
});

test('invalid files remain visible for correction or removal and cannot be uploaded', () => {
  const result = addMentorCvFiles<CvFileMetadata>([], [file('bad.exe')]);
  assert.equal(result.rows.length, 1);
  assert.match(mentorCvRowIssue(result.rows[0], result.rows, mentors)!, /PDF, DOC or DOCX/);
});

test('a row must target a mentor that is present in the current directory', () => {
  const unassigned = row('');
  assert.match(mentorCvRowIssue(unassigned, [unassigned], mentors)!, /Choose the mentor/);
  const stale = row('removed-mentor');
  assert.match(mentorCvRowIssue(stale, [stale], mentors)!, /no longer available/);
});

test('multiple CVs cannot silently overwrite the same mentor within one list', () => {
  const first = row();
  const second = { ...row(), id: 'row-b', file: file('Second.docx') };
  assert.match(mentorCvRowIssue(first, [first, second], mentors)!, /Only one CV/);
  assert.match(mentorCvRowIssue(second, [first, second], mentors)!, /Only one CV/);
});

test('the current CV must be explicitly confirmed before replacement', () => {
  const replacing = row('mentor-b');
  assert.match(mentorCvRowIssue(replacing, [replacing], mentors)!, /Confirm replacement/);
  const accepted = { ...replacing, replaceExisting: true };
  assert.equal(mentorCvRowIssue(accepted, [accepted], mentors), null);
});

test('a refreshed directory detects a CV saved by an uncertain previous request', () => {
  const retry = { ...row(), status: 'error' as const, error: 'Request timed out' };
  assert.equal(mentorCvRowIssue(retry, [retry], mentors), null);
  const refreshed = [{ ...mentors[0], cvFileName: 'new-cv.docx' }, mentors[1]];
  assert.match(mentorCvRowIssue(retry, [retry], refreshed)!, /Confirm replacement/);
});

test('retries retain failed rows and skip every previously successful CV', () => {
  const success = row('mentor-a', 'success');
  const failed = { ...row('mentor-b', 'error'), id: 'row-b', error: 'Could not upload' };
  const waiting = { ...row('', 'pending'), id: 'row-c' };
  assert.deepEqual(pendingMentorCvs([success, failed, waiting]), [failed, waiting]);
  assert.equal(mentorCvRowIssue(success, [success, failed], []), null);
});

test('missing, unknown and ambiguous emails clear previous automatic targets and replacement consent', () => {
  for (const status of ['NoEmail', 'NoMatch', 'Ambiguous'] as const) {
    const matched = applyCvEmailMatch({ ...row('mentor-b'), replaceExisting: true, suggested: true },
      { status, mentorProfileId: null, detectedEmails: [] }, mentors);
    assert.equal(matched.mentorId, '');
    assert.equal(matched.replaceExisting, false);
    assert.equal(matched.suggested, false);
    assert.match(mentorCvRowIssue(matched, [matched], mentors)!, /Choose the mentor/);
    const manual = { ...matched, mentorId: 'mentor-a' };
    assert.equal(mentorCvRowIssue(manual, [manual], mentors), null);
  }
});

test('a stale directory match cannot silently select an unavailable mentor', () => {
  const matched = applyCvEmailMatch(row(), { status: 'Matched', mentorProfileId: 'removed', detectedEmails: ['old@example.test'] }, mentors);
  assert.equal(matched.mentorId, '');
  assert.match(matched.emailReadError, /no longer/);
});

test('a read failure allows manual review and successful uploads survive later responses', () => {
  const failed = failCvEmailRead({ ...row('mentor-b'), replaceExisting: true }, 'Could not read the document');
  assert.equal(failed.emailReadStatus, 'error');
  assert.equal(failed.mentorId, '');
  assert.equal(failed.replaceExisting, false);
  const manual = { ...failed, mentorId: 'mentor-a' };
  assert.equal(mentorCvRowIssue(manual, [manual], mentors), null);
  const success = row('mentor-a', 'success');
  assert.deepEqual(failCvEmailRead(success, 'Late response'), success);
  assert.deepEqual(applyCvEmailMatch(success, { status: 'NoMatch', mentorProfileId: null, detectedEmails: [] }, mentors), success);
});

test('upload stays blocked while email reading is in progress even with a selected mentor', () => {
  for (const emailReadStatus of ['pending', 'reading'] as const) {
    const reading = { ...row(), emailReadStatus };
    assert.match(mentorCvRowIssue(reading, [reading], mentors)!, /Wait until/);
  }
});
