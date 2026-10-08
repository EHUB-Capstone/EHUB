import assert from 'node:assert/strict';
import test from 'node:test';
import {
  addExpertiseTag,
  hasProfileChanges,
  profileToFormValues,
  toUpdatePayload,
  validateMentorProfileForm,
} from '../src/utils/mentorProfileForm.ts';
import type { MentorProfile } from '../src/types/mentorProfile.ts';

const profile: MentorProfile = {
  id: 'p1', userId: 'u1', fullName: 'An', email: 'an@x.vn', mentorType: 'Academic', status: 'Active',
  expertise: ['AI'], bio: 'Bio', availabilityNote: null, organization: 'Acme', department: null, jobTitle: null,
  contractType: null, educationLevel: null, currentAddress: null, linkedInUrl: null, fptEmail: null, dateOfBirth: '1990-01-31',
  activeTeamCount: 2, rowVersion: '7',
};

test('addExpertiseTag trims, collapses spaces and keeps the list when the tag is not valid', () => {
  assert.deepEqual(addExpertiseTag(['AI'], '  Product   design '), { tags: ['AI', 'Product design'], error: null });
  assert.deepEqual(addExpertiseTag(['AI'], '   '), { tags: ['AI'], error: null });
  assert.match(addExpertiseTag(['AI'], 'ai').error!, /already listed/);
  assert.match(addExpertiseTag([], 'A').error!, /2 to 50/);
  assert.match(addExpertiseTag([], 'x'.repeat(51)).error!, /2 to 50/);
  const full = Array.from({ length: 20 }, (_, index) => `Skill ${index}`);
  assert.match(addExpertiseTag(full, 'One more').error!, /at most 20/);
});

test('validateMentorProfileForm accepts an untouched profile and explains each mistake', () => {
  const values = profileToFormValues(profile);
  assert.deepEqual(validateMentorProfileForm(values), {});

  const errors = validateMentorProfileForm({
    ...values,
    bio: 'b'.repeat(2001),
    availabilityNote: 'a'.repeat(501),
    linkedInUrl: 'javascript:alert(1)',
    fptEmail: 'not-an-email',
    dateOfBirth: '1800-01-01',
  });
  assert.deepEqual(Object.keys(errors).sort(), ['availabilityNote', 'bio', 'dateOfBirth', 'fptEmail', 'linkedInUrl']);
  assert.match(errors.linkedInUrl!, /http or https/);
  assert.equal(validateMentorProfileForm({ ...values, dateOfBirth: '2999-01-01' }, new Date('2026-10-08')).dateOfBirth !== undefined, true);
  assert.deepEqual(validateMentorProfileForm({ ...values, linkedInUrl: 'https://www.linkedin.com/in/someone', fptEmail: 'a@fpt.edu.vn' }), {});
});

test('toUpdatePayload turns blank text into null and carries the row version', () => {
  const payload = toUpdatePayload({ ...profileToFormValues(profile), bio: '   ', organization: ' Acme ' }, '7');
  assert.equal(payload.rowVersion, '7');
  assert.equal(payload.bio, null);
  assert.equal(payload.organization, 'Acme');
  assert.equal(payload.dateOfBirth, '1990-01-31');
  assert.deepEqual(payload.expertise, ['AI']);
});

test('hasProfileChanges ignores untouched fields and detects a real edit', () => {
  const values = profileToFormValues(profile);
  assert.equal(hasProfileChanges(profile, values), false);
  assert.equal(hasProfileChanges(profile, { ...values, bio: 'Bio ' }), false, 'trailing spaces are not a change');
  assert.equal(hasProfileChanges(profile, { ...values, status: 'Unavailable' }), true);
  assert.equal(hasProfileChanges(profile, { ...values, expertise: ['AI', 'UX'] }), true);
});
