import assert from 'node:assert/strict';
import test from 'node:test';
import axiosClient from '../src/api/axiosClient.ts';
import { enableApiMocks } from '../src/mocks/mockApi.ts';
import { getMockState, resetMockState } from '../src/mocks/mockHelpers.ts';

enableApiMocks();
const hasStatus = (status: number) => (error: unknown) =>
  (error as { response?: { status?: number } }).response?.status === status;
const upload = (endpoint: string, data: FormData, retry = false) =>
  axiosClient.post(endpoint, data, { headers: { 'Content-Type': 'multipart/form-data' }, ...(retry ? { _retry: true } : {}) });
const form = (file: File = new File(['Synthetic CV'], 'mentor.docx'), replaceExisting?: boolean) => {
  const data = new FormData();
  data.append('file', file);
  if (replaceExisting !== undefined) data.append('replaceExisting', String(replaceExisting));
  return data;
};

test('admin CV attachment targets the selected existing mentor and requires explicit replacement', async () => {
  resetMockState();
  await axiosClient.post('/auth/login', { email: 'admin@ehub.local', password: 'Mock123!' });
  const directory = (await axiosClient.get('/mentoring/directory')).data;
  const target = directory[0];
  assert.ok(target);
  const usersBefore = getMockState().users.length;
  const endpoint = `/mentoring/profiles/${target.id}/documents/cv`;
  const uploaded = (await upload(endpoint, form())).data;
  assert.equal(uploaded.id, target.id);
  assert.equal(uploaded.cvFileName, 'mentor.docx');
  assert.equal(uploaded.mentorType, target.mentorType);
  assert.deepEqual(uploaded.technologySkills, target.technologySkills);
  assert.deepEqual(uploaded.experiences, target.experiences);
  assert.equal(getMockState().users.length, usersBefore);
  const replacement = new File(['Synthetic replacement CV'], 'replacement.pdf');
  await assert.rejects(upload(endpoint, form(replacement)), hasStatus(409));
  const unchanged = (await axiosClient.get('/mentoring/directory')).data;
  assert.equal(unchanged.find((profile: { id: string }) => profile.id === target.id).cvFileName, 'mentor.docx');
  for (const original of directory.filter((profile: { id: string }) => profile.id !== target.id)) {
    assert.equal(unchanged.find((profile: { id: string }) => profile.id === original.id).cvFileName, original.cvFileName);
  }
  const replaced = (await upload(endpoint, form(replacement, true))).data;
  assert.equal(replaced.cvFileName, 'replacement.pdf');
  assert.deepEqual(replaced.technologySkills, target.technologySkills);
});

test('admin CV upload rejects unknown mentors and invalid files without creating accounts', async () => {
  resetMockState();
  await axiosClient.post('/auth/login', { email: 'admin@ehub.local', password: 'Mock123!' });
  const target = (await axiosClient.get('/mentoring/directory')).data[0];
  const usersBefore = getMockState().users.length;
  await assert.rejects(upload('/mentoring/profiles/unknown/documents/cv', form()), hasStatus(404));
  for (const invalid of [new File([], 'empty.docx'), new File(['x'], 'invalid.exe')]) {
    await assert.rejects(upload(`/mentoring/profiles/${target.id}/documents/cv`, form(invalid)), hasStatus(400));
  }
  await assert.rejects(upload(`/mentoring/profiles/${target.id}/documents/cv`, new FormData()), hasStatus(400));
  assert.equal(getMockState().users.length, usersBefore);
  assert.equal((await axiosClient.get('/mentoring/directory')).data[0].cvFileName, null);
});

test('CV attachment remains administrator-only for every requested mentor', async () => {
  resetMockState();
  const endpoint = '/mentoring/profiles/unknown/documents/cv';
  await assert.rejects(upload(endpoint, form(), true), hasStatus(401));
  for (const email of ['khoa.mentor@ehub.local', 'giang.lecturer@ehub.local', 'se200001@fpt.edu.vn']) {
    await axiosClient.post('/auth/login', { email, password: 'Mock123!' });
    await assert.rejects(upload(endpoint, form()), hasStatus(403));
  }
});

test('CV email preview matches the imported account email and never saves the document', async () => {
  resetMockState();
  await axiosClient.post('/auth/login', { email: 'admin@ehub.local', password: 'Mock123!' });
  const before = (await axiosClient.get('/mentoring/directory')).data;
  const target = before[0];
  const count = getMockState().users.length;
  const preview = (await upload('/mentoring/profiles/cv-match', form(new File([
    `Personal information\nEmail: <${target.email.toUpperCase()}>\nPRIVATE_SYNTHETIC_MARKER`,
  ], 'unrelated-filename.docx')))).data;
  assert.equal(preview.status, 'Matched');
  assert.equal(preview.mentorProfileId, target.id);
  assert.deepEqual(preview.detectedEmails, [target.email.toLowerCase()]);
  assert.doesNotMatch(JSON.stringify(preview), /PRIVATE_SYNTHETIC_MARKER/);
  assert.equal(getMockState().users.length, count);
  assert.deepEqual((await axiosClient.get('/mentoring/directory')).data, before);
});

test('CV email preview leaves no-email, unknown, and ambiguous CVs for manual review', async () => {
  resetMockState();
  await axiosClient.post('/auth/login', { email: 'admin@ehub.local', password: 'Mock123!' });
  await axiosClient.post('/mentoring/profiles', {
    fullName: 'Synthetic second mentor', email: 'second-cv-mentor@example.test', temporaryPassword: 'Mock123!',
    profile: { mentorType: 'IT', bio: 'Synthetic professional background', expertise: [], startupDomains: [],
      technologySkills: [], tags: [], experiences: [], experience: null, organization: null, linkedInUrl: null, portfolioUrl: null },
  });
  const directory = (await axiosClient.get('/mentoring/directory')).data;
  assert.ok(directory.length >= 2);
  const cases = [
    ['Education\nDemo school', 'NoEmail'],
    ['Email: unknown@example.test', 'NoMatch'],
    [`${directory[0].email}\n${directory[1].email}`, 'Ambiguous'],
    [Array.from({ length: 21 }, (_, index) => `contact${index}@example.test`).join('\n'), 'Ambiguous'],
  ];
  for (const [content, status] of cases) {
    const preview = (await upload('/mentoring/profiles/cv-match', form(new File([content], 'cv.docx')))).data;
    assert.equal(preview.status, status);
    assert.equal(preview.mentorProfileId, null);
    assert.ok(preview.detectedEmails.length <= 20);
  }
  const target = directory[0];
  const singleMatch = (await upload('/mentoring/profiles/cv-match', form(new File([
    `${target.email}\n${target.email.toUpperCase()}\nunknown@example.test`,
  ], 'cv.docx')))).data;
  assert.equal(singleMatch.status, 'Matched');
  assert.equal(singleMatch.mentorProfileId, target.id);
});

test('CV email preview enforces admin authentication and file validation', async () => {
  resetMockState();
  const endpoint = '/mentoring/profiles/cv-match';
  await assert.rejects(upload(endpoint, form(), true), hasStatus(401));
  for (const email of ['khoa.mentor@ehub.local', 'giang.lecturer@ehub.local', 'se200001@fpt.edu.vn']) {
    await axiosClient.post('/auth/login', { email, password: 'Mock123!' });
    await assert.rejects(upload(endpoint, form()), hasStatus(403));
  }
  await axiosClient.post('/auth/login', { email: 'admin@ehub.local', password: 'Mock123!' });
  await assert.rejects(upload(endpoint, new FormData()), hasStatus(400));
  for (const file of [new File([], 'empty.docx'), new File(['x'], 'bad.exe')])
    await assert.rejects(upload(endpoint, form(file)), hasStatus(400));
});
