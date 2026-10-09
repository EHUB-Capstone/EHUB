import assert from 'node:assert/strict';
import test from 'node:test';
import { mentorSearchText, parseMentorTags, validateMentorMetadata } from '../src/utils/mentorProfiles.ts';
import type { MentorProfileDraft } from '../src/types/mentoring.ts';

const profile: MentorProfileDraft = { mentorType: 'Business', expertise: ['Product'], startupDomains: ['Education'],
  technologySkills: ['Cloud'], tags: ['Founder'], bio: 'Advisor', experience: null, organization: null,
  linkedInUrl: null, portfolioUrl: null, experiences: [{ kind: 'Technology', area: 'AI', years: 3.5, level: 'Advanced', notes: 'Platform development' }] };

test('mentor metadata accepts structured experience and rejects duplicate and blank tags', () => {
  assert.equal(validateMentorMetadata(profile), null);
  assert.ok(validateMentorMetadata({ ...profile, tags: parseMentorTags('AI, ai') }));
  assert.ok(validateMentorMetadata({ ...profile, tags: parseMentorTags('AI,') }));
  assert.ok(validateMentorMetadata({ ...profile, expertise: [] }));
});
test('mentor experience rejects duplicates, invalid years and missing area', () => {
  const entry = profile.experiences[0];
  assert.ok(validateMentorMetadata({ ...profile, experiences: [entry, { ...entry, area: ' ai ' }] }));
  for (const years of [-1, 81, NaN, 2.23]) assert.ok(validateMentorMetadata({ ...profile, experiences: [{ ...entry, years }] }));
  assert.ok(validateMentorMetadata({ ...profile, experiences: [{ ...entry, area: ' ' }] }));
});
test('mentor search includes domains, technology, tags and structured experience', () => {
  const text = mentorSearchText(profile);
  for (const term of ['education', 'cloud', 'founder', 'ai', 'advanced', 'platform']) assert.ok(text.includes(term));
});
