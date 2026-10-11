import assert from 'node:assert/strict';
import test from 'node:test';
import { collectTagOptions, keepKnownTags, matchesAnyTag, mentorTagList, tagKey, tagSearchText } from '../src/utils/mentorTags.ts';

const alice = { tags: { expertise: ['Marketing'], startupDomains: ['FinTech'], technologySkills: ['React', '.NET'], mentorTags: [] } };
const bob = { tags: { expertise: ['marketing'], startupDomains: [], technologySkills: ['React'], mentorTags: ['Alumni'] } };
const nobody = { tags: null };

test('mentorTagList and tagSearchText flatten every kind of tag, and tolerate a mentor without tags', () => {
  assert.deepEqual(mentorTagList(alice.tags).map(tag => tag.label), ['Marketing', 'FinTech', 'React', '.NET']);
  assert.deepEqual(tagSearchText(bob.tags), ['marketing', 'React', 'Alumni']);
  assert.deepEqual(mentorTagList(nobody.tags), []);
  assert.deepEqual(mentorTagList(undefined), []);
});

test('collectTagOptions merges spellings that differ by case and counts mentors, not repeats', () => {
  const options = collectTagOptions([alice, bob, nobody, { tags: { technologySkills: ['React', 'react'] } }]);
  const marketing = options.find(option => option.key === tagKey('expertise', 'Marketing'));
  const react = options.find(option => option.key === tagKey('technologySkills', 'React'));
  assert.equal(marketing?.count, 2);
  assert.equal(react?.count, 3, 'one count per mentor even when a tag is repeated');
  assert.deepEqual(options.map(option => option.category), ['expertise', 'startupDomains', 'technologySkills', 'technologySkills', 'mentorTags']);
  assert.equal(options.find(option => option.category === 'technologySkills')?.label, 'React', 'the most used tag comes first inside a category');
});

test('matchesAnyTag keeps everyone without a filter and otherwise needs one of the selected tags', () => {
  assert.equal(matchesAnyTag(nobody.tags, []), true);
  assert.equal(matchesAnyTag(nobody.tags, [tagKey('expertise', 'Marketing')]), false);
  assert.equal(matchesAnyTag(alice.tags, [tagKey('startupDomains', 'fintech')]), true, 'ignores case');
  assert.equal(matchesAnyTag(alice.tags, [tagKey('mentorTags', 'Alumni'), tagKey('technologySkills', '.net')]), true);
  assert.equal(matchesAnyTag(alice.tags, [tagKey('mentorTags', 'Alumni')]), false);
  assert.equal(matchesAnyTag(alice.tags, [tagKey('startupDomains', 'Marketing')]), false, 'the category matters');
});

test('keepKnownTags removes selections no mentor carries any more', () => {
  const options = collectTagOptions([alice]);
  assert.deepEqual(keepKnownTags([tagKey('expertise', 'Marketing'), tagKey('expertise', 'Gone')], options), [tagKey('expertise', 'Marketing')]);
});
