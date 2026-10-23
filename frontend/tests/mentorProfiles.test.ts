import assert from 'node:assert/strict';
import test from 'node:test';
import { mentorSearchText, needsMentorProfileSetup, parseMentorTags, validateMentorMetadata } from '../src/utils/mentorProfiles.ts';
import type { MentorProfile, MentorProfileDraft } from '../src/types/mentoring.ts';
import { resolveProfileTab } from '../src/utils/profileNavigation.ts';
import { formatMentorDirectoryBio, isMentorCvFormLabel, parseMentorCvFormBio, parseMentorProfessionalExperience } from '../src/utils/mentorDirectoryBio.ts';

const profile: MentorProfileDraft = { mentorType: 'Business', expertise: ['Product'], startupDomains: ['Education'],
  technologySkills: ['Cloud'], tags: ['Founder'], bio: 'Advisor', experience: null, organization: null,
  linkedInUrl: null, portfolioUrl: null, experiences: [{ kind: 'Technology', area: 'AI', years: 3.5, level: 'Advanced', notes: 'Platform development' }] };

test('mentor metadata accepts structured experience and rejects duplicate and blank tags', () => {
  assert.equal(validateMentorMetadata(profile), null);
  assert.ok(validateMentorMetadata({ ...profile, tags: parseMentorTags('AI, ai') }));
  assert.ok(validateMentorMetadata({ ...profile, tags: parseMentorTags('AI,') }));
  assert.equal(validateMentorMetadata({ ...profile, expertise: [] }), null);
});

test('manual skills accept trimmed values and can be cleared', () => {
  assert.deepEqual(parseMentorTags(' Leadership, Python '), ['Leadership', 'Python']);
  assert.equal(validateMentorMetadata({ ...profile, technologySkills: parseMentorTags(' Leadership, Python ') }), null);
  assert.deepEqual(parseMentorTags('   '), []);
  assert.equal(validateMentorMetadata({ ...profile, technologySkills: [] }), null);
});

test('manual skills reject duplicates, blank entries and values outside the profile limits', () => {
  for (const skills of [
    parseMentorTags('Python, python'), parseMentorTags('Python,'), [' '], ['x'.repeat(81)],
    Array.from({ length: 21 }, (_, index) => `Skill ${index}`),
  ]) assert.match(validateMentorMetadata({ ...profile, technologySkills: skills }) ?? '', /Technology skills/);
  assert.equal(validateMentorMetadata({ ...profile, technologySkills: ['x'.repeat(80)] }), null);
  assert.equal(validateMentorMetadata({ ...profile, technologySkills: Array.from({ length: 20 }, (_, index) => `Skill ${index}`) }), null);
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

test('mentor directory removes CV form instructions but preserves profile details', () => {
  const formatted = formatMentorDirectoryBio('Thời gian (Từ tháng/năm đến tháng/năm) 2012-2015 Tên trường/cơ sở đào tạo, quốc gia Đại học Demo Chuyên ngành đào tạo Thiết kế UX/UI Xếp loại Giỏi Các ngoại ngữ có thể sử dụng Tiếng Anh');
  assert.equal(formatted, '2012-2015 Đại học Demo Thiết kế UX/UI Giỏi Tiếng Anh');
  assert.equal(formatMentorDirectoryBio('Advisor in software product design.'), 'Advisor in software product design.');
  assert.equal(formatMentorDirectoryBio(null), '');
  assert.equal(isMentorCvFormLabel('Thời gian'), true);
  assert.equal(isMentorCvFormLabel('AI'), false);
});

test('mentor profile separates education rows from extra details in the standard CV form', () => {
  const parsed = parseMentorCvFormBio(`Thời gian\n(Từ tháng/năm\nđến tháng/năm)\nTên trường/cơ sở đào tạo, quốc gia\nChuyên ngành đào tạo\nLoại hình đào tạo\nXếp loại\n2009-2012\nTHPT Tri Thức Demo\nPhổ thông\nTHPT\nKhá\n2012-2016\nĐại học Hạ tầng Số Demo\nMạng máy tính\nKỹ sư\nGiỏi\n* Tên chuyên đề luận án tốt nghiệp bậc cao nhất: Giám sát hạ tầng\nCác ngoại ngữ có thể sử dụng\nTiếng Anh\nTOEFL iBT\n100\nBằng/\n(nếu có)\nXếp loại/\nĐiểm\nNgày cấp`);
  assert.equal(parsed?.education.length, 2);
  assert.equal(parsed?.education[1].institution, 'Đại học Hạ tầng Số Demo');
  assert.deepEqual(parsed?.education[1].details, ['Mạng máy tính', 'Kỹ sư', 'Giỏi']);
  assert.deepEqual(parsed?.thesis, ['Giám sát hạ tầng']);
  assert.deepEqual(parsed?.languagesAndCertificates, ['Tiếng Anh', 'TOEFL iBT', '100']);
  assert.equal(parseMentorCvFormBio('Experienced software architect.'), null);
});

test('work experience hides blank date prompts while preserving entered details', () => {
  assert.deepEqual(parseMentorProfessionalExperience('Thời gian\n(Từ tháng/năm\nđến tháng/năm)\n2019-2023\nProduct Manager\nCông ty Demo'),
    ['2019-2023', 'Product Manager', 'Công ty Demo']);
  assert.deepEqual(parseMentorProfessionalExperience('Thời gian (Từ tháng/năm đến tháng/năm)'), []);
});

const emptyMentor: MentorProfile = {
  id: 'profile', userId: 'mentor', fullName: 'Synthetic mentor', email: 'mentor@example.test', mentorType: 'Business',
  expertise: [], startupDomains: [], technologySkills: [], tags: [], experiences: [],
  bio: null, experience: null, organization: 'Organization', linkedInUrl: null, portfolioUrl: null,
  cvFileName: null, portfolioFileName: null, maxTeams: null, status: 'Active', activeTeamCount: 0,
  totalAssignments: 0, totalSessions: 0, averageFeedbackRating: null,
};

test('new mentors need profile setup even when the administrator already created their empty record', () => {
  assert.equal(needsMentorProfileSetup(null), true);
  assert.equal(needsMentorProfileSetup(emptyMentor), true);
  assert.equal(needsMentorProfileSetup({ ...emptyMentor, bio: ' ', expertise: [' '], cvFileName: ' ' }), true);
  assert.equal(needsMentorProfileSetup({ ...emptyMentor, portfolioFileName: 'portfolio.pdf' }), true);
});

test('a CV or existing professional information does not require profile setup', () => {
  for (const information of [
    { cvFileName: 'cv.pdf' }, { bio: 'Advisor' }, { experience: 'Marketing director' },
    { expertise: ['Marketing'] }, { technologySkills: ['Python'] }, { startupDomains: ['Education'] },
    { experiences: profile.experiences },
  ]) assert.equal(needsMentorProfileSetup({ ...emptyMentor, ...information }), false);
});

test('shared profile opens mentor expertise by default and keeps personal and security tabs accessible', () => {
  assert.equal(resolveProfileTab('/profile', '', 'MENTOR'), 'mentor');
  assert.equal(resolveProfileTab('/profile', '?tab=personal', 'mentor'), 'profile');
  assert.equal(resolveProfileTab('/settings', '?tab=mentor', 'MENTOR'), 'password');
});

test('other roles cannot select the mentor profile through query parameters', () => {
  for (const role of ['ADMIN', 'LECTURER', 'STUDENT', 'USER', '']) {
    assert.equal(resolveProfileTab('/profile', '', role), 'profile');
    assert.equal(resolveProfileTab('/profile', '?tab=mentor', role), 'profile');
    assert.equal(resolveProfileTab('/settings', '', role), 'password');
  }
});
