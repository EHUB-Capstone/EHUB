import assert from 'node:assert/strict';
import test from 'node:test';
import MockAdapter from 'axios-mock-adapter';
import axiosClient from '../src/api/axiosClient.ts';
import { showcaseApi } from '../src/api/showcaseApi.ts';
import { discoveryRequest, discoveryPages, isNewlyPublished } from '../src/utils/showcaseDiscovery.ts';

test('showcase view client returns the current counter and accepts an abort signal', async () => {
  const mock = new MockAdapter(axiosClient);
  const controller = new AbortController();
  mock.onPost('/showcase/s1/view').reply(200, { data: { viewCount: 8 } });
  try {
    assert.deepEqual(await showcaseApi.recordView('s1', controller.signal), { viewCount: 8 });
    assert.equal(mock.history.post[0].signal, controller.signal);
    controller.abort();
    await assert.rejects(showcaseApi.recordView('s1', controller.signal));
  } finally {
    mock.restore();
  }
});

test('showcase insights client gets the owner-only fourteen-day snapshot', async () => {
  const mock = new MockAdapter(axiosClient);
  const controller = new AbortController();
  const insights = { viewCount: 12, upvoteCount: 3, commentCount: 2, daily: [] };
  mock.onGet('/showcase/s1/insights').reply(200, { data: insights });
  try {
    assert.deepEqual(await showcaseApi.getInsights('s1', controller.signal), insights);
    assert.equal(mock.history.get[0].signal, controller.signal);
  } finally { mock.restore(); }
});

test('report client submits content, lists the staff queue, and resolves a report', async () => {
  const mock = new MockAdapter(axiosClient);
  const receipt = { id: 'r1', showcaseProjectId: 's1', commentId: null, status: 'Open', createdAt: '2026-11-11T00:00:00Z' };
  const queue = { items: [{ ...receipt, reason: 'Spam', note: null, resolvedByUserId: null, resolvedAt: null, resolutionNote: null }], totalItems: 1, pageIndex: 1, pageSize: 20, totalPages: 1 };
  mock.onPost('/showcase/s1/reports').reply(200, { data: receipt });
  mock.onGet('/showcase/reports').reply(200, { data: queue });
  mock.onPost('/showcase/reports/r1/resolve').reply(200, { data: { ...queue.items[0], status: 'ActionTaken' } });
  mock.onDelete('/showcase/reports/r1').reply(200, { data: null });
  mock.onDelete('/showcase/reports/history', { params: { status: 'Dismissed' } }).reply(200, { data: null });
  try {
    assert.deepEqual(await showcaseApi.createReport('s1', { reason: 'Spam', note: 'Spam link' }), receipt);
    assert.deepEqual(mock.history.post[0].data && JSON.parse(mock.history.post[0].data), { reason: 'Spam', note: 'Spam link' });
    assert.deepEqual(await showcaseApi.getReports({ status: 'Open', page: 1, pageSize: 20 }), queue);
    assert.equal(mock.history.get[0].params.status, 'Open');
    assert.equal((await showcaseApi.resolveReport('r1', { status: 'ActionTaken' })).status, 'ActionTaken');
    await showcaseApi.deleteReportHistory('r1');
    assert.equal(mock.history.delete[0].url, '/showcase/reports/r1');
    await showcaseApi.deleteAllReportHistory('Dismissed');
    assert.equal(mock.history.delete[1].params.status, 'Dismissed');
  } finally { mock.restore(); }
});

test('newly published rail includes only projects published in the preceding seven days', () => {
  const now = Date.parse('2026-11-05T12:00:00Z');
  assert.equal(isNewlyPublished('2026-10-29T12:00:00Z', now), true);
  assert.equal(isNewlyPublished('2026-10-29T11:59:59Z', now), false);
  assert.equal(isNewlyPublished('2026-11-06T12:00:00Z', now), false);
  assert.equal(isNewlyPublished(null, now), false);
});

test('featured client calls POST/DELETE, unwraps selection and preserves discovery filters', async () => {
  const mock = new MockAdapter(axiosClient);
  const selection = { id: 's1', isFeatured: true, featuredAt: '2026-11-05T02:00:00Z', rowVersion: 9 };
  const cleared = { ...selection, isFeatured: false, featuredAt: null, rowVersion: 10 };
  const params = discoveryRequest(new URLSearchParams('featured=true&industry=HealthTech&stage=Mvp&page=2'));
  mock.onPost('/showcase/s1/feature').reply(200, { data: selection });
  mock.onDelete('/showcase/s1/feature').reply(200, { data: cleared });
  mock.onGet('/showcase', { params }).reply(200, { data: { items: [], totalItems: 0, pageIndex: 2, pageSize: 8, totalPages: 0 } });
  mock.onPost('/showcase/denied/feature').reply(403, { message: 'Forbidden' });
  try {
    assert.deepEqual(await showcaseApi.feature('s1'), selection);
    assert.deepEqual(await showcaseApi.unfeature('s1'), cleared);
    await showcaseApi.list(params);
    assert.equal(mock.history.get[0].params.featured, true);
    assert.equal(mock.history.get[0].params.industry, 'HealthTech');
    await assert.rejects(showcaseApi.feature('denied'));
  } finally { mock.restore(); }
});

test('discovery URL maps to server filters and rejects obsolete stages, sorts and invalid paging', () => {
  const semesterId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
  const request = discoveryRequest(new URLSearchParams({ q: ' garden ', stage: 'MVP', industry: ' HealthTech ', semesterId, sort: 'popular', page: '2' }));
  assert.deepEqual(request, { search: 'garden', stage: 'Mvp', industry: 'HealthTech', semesterId, sort: 'trending', page: 2, pageSize: 8 });
  const invalid = discoveryRequest(new URLSearchParams('stage=Prototype&sort=name&semesterId=FA2026&page=-1'));
  assert.equal(invalid.stage, undefined);
  assert.equal(invalid.semesterId, undefined);
  assert.equal(invalid.sort, 'newest');
  assert.equal(invalid.page, 1);
  assert.equal(discoveryRequest(new URLSearchParams('sort=popular&tab=new')).sort, 'newest');
  assert.equal(discoveryRequest(new URLSearchParams('sort=trending')).sort, 'trending');
  assert.equal(discoveryRequest(new URLSearchParams('page=999999999999')).page, 1);
  assert.equal(discoveryRequest(new URLSearchParams({ q: 'x'.repeat(101) })).search?.length, 100);
  assert.equal(discoveryRequest(new URLSearchParams('featured=true')).featured, true);
  assert.equal(discoveryRequest(new URLSearchParams('featured=false')).featured, false);
  assert.equal(discoveryRequest(new URLSearchParams('featured=invalid')).featured, undefined);
});

test('discovery pagination stays bounded for large server result sets', () => {
  assert.deepEqual(discoveryPages(1, 0), []);
  assert.deepEqual(discoveryPages(1, 2), [1, 2]);
  assert.deepEqual(discoveryPages(1000, 1000), [996, 997, 998, 999, 1000]);
});

test('discovery client forwards filters and unwraps list, options and detail; errors remain failures', async () => {
  const mock = new MockAdapter(axiosClient);
  const params = discoveryRequest(new URLSearchParams('q=garden&sort=trending&page=2'));
  const page = { items: [], pageIndex: 2, pageSize: 8, totalItems: 0, totalPages: 0 };
  const options = { industries: ['HealthTech'], semesters: [{ id: 's1', code: 'FA26', name: 'Fall 2026' }] };
  mock.onGet('/showcase', { params }).reply(200, { data: page });
  mock.onGet('/showcase/filter-options').reply(200, { data: options });
  mock.onGet('/showcase/published-id').reply(200, { data: { teamName: 'Real team', members: [] } });
  mock.onGet('/showcase/missing').reply(404, { message: 'Not found' });
  try {
    assert.deepEqual(await showcaseApi.list(params), page);
    assert.equal(mock.history.get[0].params.sort, 'trending');
    assert.deepEqual(await showcaseApi.getFilterOptions(), options);
    assert.equal((await showcaseApi.get('published-id')).teamName, 'Real team');
    await assert.rejects(showcaseApi.get('missing'));
    const abort = new AbortController();
    abort.abort();
    await assert.rejects(showcaseApi.list(params, abort.signal));
  } finally { mock.restore(); }
});
import {
  PROJECT_DESCRIPTION_MAX_LENGTH,
  PROJECT_NAME_MAX_LENGTH,
  PROJECT_PROFILE_LABELS,
} from '../src/constants/projectProfile.ts';
import {
  SHOWCASE_LIMITS,
  canSubmitForReview,
  canWithdraw,
  emptyForm,
  emptyLink,
  formFromShowcase,
  hasFormErrors,
  isFormDirty,
  isPublicWebUrl,
  missingForReview,
  moderationActionsFor,
  optimisticShowcaseUpvote,
  projectProfilePath,
  toSaveRequest,
  validateImageFile,
  validateShowcaseForm,
  insightsDelta,
  insightsTotal,
  timeAgo,
} from '../src/utils/showcase.ts';
import { validateProjectProfile, validateProjectWorkspace } from '../src/utils/projectWorkspace.ts';
import { createReleaseFeatureFlags } from '../src/config/releaseFeatureFlags.ts';
import type { ShowcaseDto } from '../src/types/showcase.ts';

test('optimistic showcase upvote adds and removes one without mutating the confirmed vote', () => {
  const confirmed = { hasUpvoted: false, likeCount: 5 };
  const added = optimisticShowcaseUpvote(confirmed);
  assert.deepEqual(added, { hasUpvoted: true, likeCount: 6 });
  assert.deepEqual(optimisticShowcaseUpvote(added), confirmed);
  assert.deepEqual(confirmed, { hasUpvoted: false, likeCount: 5 });
  assert.deepEqual(optimisticShowcaseUpvote({ hasUpvoted: true, likeCount: 0 }), { hasUpvoted: false, likeCount: 0 });
});

test('showcase upvote client sends POST to vote and DELETE to remove', async () => {
  const mock = new MockAdapter(axiosClient);
  const voted = { hasUpvoted: true, likeCount: 6 };
  const cleared = { hasUpvoted: false, likeCount: 5 };
  mock.onPost('/showcase/s1/upvote').reply(200, { data: voted });
  mock.onDelete('/showcase/s1/upvote').reply(200, { data: cleared });
  mock.onPost('/showcase/owner/upvote').reply(403, { message: 'Forbidden' });
  try {
    assert.deepEqual(await showcaseApi.setUpvote('s1', true), voted);
    assert.deepEqual(await showcaseApi.setUpvote('s1', false), cleared);
    await assert.rejects(showcaseApi.setUpvote('owner', true));
  } finally {
    mock.restore();
  }
});

const showcase = (overrides: Partial<ShowcaseDto> = {}): ShowcaseDto => ({
  id: 's1',
  projectLineageId: 'l1',
  profile: {
    projectName: 'Smart Garden',
    description: 'Water plants automatically',
    problem: 'Plants die',
    solution: 'A pump',
    targetUsers: null,
  },
  logoUrl: null,
  highlights: null,
  developmentStage: 'Mvp',
  status: 'Draft',
  showTeamMembers: true,
  reviewNote: null,
  publishedAt: null,
  isFeatured: false,
  featuredAt: null,
  viewCount: 0,
  likeCount: 0,
  commentCount: 0,
  rowVersion: 7,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: null,
  canEdit: true,
  links: [{ id: 'k1', linkType: 'Demo', label: 'Demo', url: 'https://demo.example.test' }],
  media: [],
  ...overrides,
});

test('shares one set of field names and limits with the Project Profile page', () => {
  assert.deepEqual(PROJECT_PROFILE_LABELS, {
    projectName: 'Project name',
    description: 'Description',
    problem: 'Problem',
    solution: 'Solution',
    targetUsers: 'Target users',
  });
  assert.equal(PROJECT_NAME_MAX_LENGTH, 100);
  assert.equal(PROJECT_DESCRIPTION_MAX_LENGTH, 300);
});

test('applies the new Project name and Description limits on the Project Profile and create forms', () => {
  const profile = { projectName: 'x'.repeat(100), description: 'd'.repeat(300), problem: '', solution: '', targetUsers: '', zaloGroupUrl: 'https://zalo.me/g/abc' };

  assert.equal(validateProjectProfile(profile).projectName, undefined);
  assert.equal(validateProjectProfile(profile).description, undefined);
  assert.match(validateProjectProfile({ ...profile, projectName: 'x'.repeat(101) }).projectName ?? '', /3–100/);
  assert.match(validateProjectProfile({ ...profile, description: 'd'.repeat(301) }).description ?? '', /20–300/);

  const create = { teamName: 'Team', projectName: 'x'.repeat(101), description: 'd'.repeat(301), zaloGroupUrl: 'https://zalo.me/g/abc', startupIndustryIds: ['i1'] };
  assert.match(validateProjectWorkspace(create).projectName ?? '', /3–100/);
  assert.match(validateProjectWorkspace(create).description ?? '', /20–300/);
});

test('starts a new page with only showcase-specific defaults', () => {
  const form = emptyForm();

  assert.equal(form.highlights, '');
  assert.equal(form.developmentStage, 'Idea');
  assert.equal(form.showTeamMembers, true);
  assert.deepEqual(form.links, []);
  assert.equal('title' in form, false);
});

test('builds a save request without any Project Profile field', () => {
  const form = { ...formFromShowcase(showcase()), highlights: '  Works offline  ' };

  const request = toSaveRequest(form, { rowVersion: 7 });

  assert.equal(request.highlights, 'Works offline');
  assert.equal(request.rowVersion, 7);
  assert.equal('projectLineageId' in request, false);
  for (const profileField of ['title', 'tagline', 'problem', 'solution', 'targetCustomers', 'projectName', 'description']) {
    assert.equal(profileField in request, false, profileField);
  }
  assert.deepEqual(request.links, [{ linkType: 'Demo', label: 'Demo', url: 'https://demo.example.test' }]);
});

test('sends the lineage only when creating, and nulls blank highlights', () => {
  const request = toSaveRequest(emptyForm(), { projectLineageId: 'l1' });

  assert.equal(request.projectLineageId, 'l1');
  assert.equal(request.highlights, null);
  assert.equal('rowVersion' in request, false);
});

test('accepts only plain http(s) links without credentials', () => {
  assert.equal(isPublicWebUrl('https://garden.example.test/a?b=1'), true);
  assert.equal(isPublicWebUrl(' http://garden.example.test '), true);
  for (const bad of ['javascript:alert(1)', 'data:text/html;base64,AAAA', 'ftp://files.example.test', 'https://user:secret@example.test', 'example.test', '']) {
    assert.equal(isPublicWebUrl(bad), false, bad);
  }
});

test('validates highlights length and link rows', () => {
  const base = formFromShowcase(showcase());
  assert.equal(hasFormErrors(validateShowcaseForm(base)), false);

  const bad = {
    ...base,
    highlights: 'x'.repeat(SHOWCASE_LIMITS.highlights + 1),
    links: [{ ...emptyLink(), label: '', url: 'javascript:alert(1)' }, { ...emptyLink(), label: 'Site', url: 'https://ok.example.test' }],
  };
  const errors = validateShowcaseForm(bad);

  assert.ok(errors.highlights);
  assert.equal(Object.keys(errors.linkRows).length, 1);
  assert.ok(errors.linkRows[bad.links[0].key].label);
  assert.ok(errors.linkRows[bad.links[0].key].url);
  assert.equal(hasFormErrors(errors), true);
});

test('rejects more links than the server accepts', () => {
  const links = Array.from({ length: SHOWCASE_LIMITS.links + 1 }, () => ({ ...emptyLink(), label: 'L', url: 'https://ok.example.test' }));

  assert.ok(validateShowcaseForm({ ...formFromShowcase(showcase()), links }).links);
});

test('detects unsaved changes, including edits to links', () => {
  const saved = formFromShowcase(showcase());

  assert.equal(isFormDirty(saved, formFromShowcase(showcase())), false);
  assert.equal(isFormDirty({ ...saved, highlights: 'Other' }, saved), true);
  assert.equal(isFormDirty({ ...saved, links: [{ ...saved.links[0], url: 'https://other.example.test' }] }, saved), true);
  assert.equal(isFormDirty({ ...saved, links: [] }, saved), true);
});

test('names the Project Profile fields still missing before a page can be submitted', () => {
  assert.deepEqual(missingForReview(showcase().profile), []);
  assert.deepEqual(
    missingForReview({ description: ' ', problem: null, solution: 'Pump' }),
    ['Description', 'Problem'],
  );
});

test('links to the Project Profile page of the team', () => {
  assert.equal(projectProfilePath('team-1'), '/student/workspace/project-profile/team-1');
});

test('mirrors the status rules for owners', () => {
  assert.deepEqual(['Draft', 'Hidden'].map((status) => canSubmitForReview(status as never)), [true, true]);
  assert.deepEqual(['PendingReview', 'Published'].map((status) => canSubmitForReview(status as never)), [false, false]);
  assert.deepEqual(['Published', 'PendingReview'].map((status) => canWithdraw(status as never)), [true, true]);
  assert.deepEqual(['Draft', 'Hidden'].map((status) => canWithdraw(status as never)), [false, false]);
});

test('mirrors the decisions a reviewer may take', () => {
  assert.deepEqual(moderationActionsFor('PendingReview', false), ['approve', 'reject']);
  assert.deepEqual(moderationActionsFor('Published', true), ['hide']);
  assert.deepEqual(moderationActionsFor('Hidden', true), ['unhide']);
  assert.deepEqual(moderationActionsFor('Hidden', false), []);
  assert.deepEqual(moderationActionsFor('Draft', false), []);
});

test('checks image type and size before uploading', () => {
  assert.equal(validateImageFile({ name: 'shot.PNG', size: 1024 }), null);
  assert.equal(validateImageFile({ name: 'shot.webp', size: SHOWCASE_LIMITS.imageBytes }), null);
  assert.ok(validateImageFile({ name: 'shot.gif', size: 1024 }));
  assert.ok(validateImageFile({ name: 'run.exe', size: 1024 }));
  assert.ok(validateImageFile({ name: 'empty.png', size: 0 }));
  assert.ok(validateImageFile({ name: 'big.png', size: SHOWCASE_LIMITS.imageBytes + 1 }));
});

test('can be switched off with the release feature flag', () => {
  assert.equal(createReleaseFeatureFlags({}).showcase, true);
  assert.equal(createReleaseFeatureFlags({ VITE_FEATURE_SHOWCASE: 'false' }).showcase, false);
});

test('shows how long ago a comment was written, and its date after a week', () => {
  const now = Date.parse('2026-11-09T12:00:00Z');
  assert.equal(timeAgo('2026-11-09T11:59:40Z', now), 'just now');
  assert.equal(timeAgo('2026-11-09T11:59:00Z', now), '1 minute ago');
  assert.equal(timeAgo('2026-11-09T11:15:00Z', now), '45 minutes ago');
  assert.equal(timeAgo('2026-11-09T10:00:00Z', now), '2 hours ago');
  assert.equal(timeAgo('2026-11-06T12:00:00Z', now), '3 days ago');
  assert.equal(timeAgo('2026-11-09T12:05:00Z', now), 'just now', 'a clock that runs ahead never shows a negative time');
  assert.equal(timeAgo('not a date', now), '');
  assert.match(timeAgo('2026-10-01T12:00:00Z', now), /2026/);
});

test('summarises fourteen days of insights and the trend against the fourteen before', () => {
  const daily = [
    { views: 4, upvotes: 1, comments: 0 },
    { views: 6, upvotes: 0, comments: 2 },
    { views: 0, upvotes: 2, comments: 0 },
  ];
  assert.equal(insightsTotal(daily, 'views'), 10);
  assert.equal(insightsTotal(daily, 'upvotes'), 3);
  assert.equal(insightsTotal(daily, 'comments'), 2);
  assert.equal(insightsTotal([], 'views'), 0);

  assert.deepEqual(insightsDelta(34, 10), { label: '+24', direction: 'up' });
  assert.deepEqual(insightsDelta(2, 5), { label: '-3', direction: 'down' });
  assert.deepEqual(insightsDelta(7, 7), { label: 'no change', direction: 'flat' });
  assert.deepEqual(insightsDelta(1500, 0), { label: '+' + (1500).toLocaleString(), direction: 'up' });
});

test('comment client pages threads and replies, posts and removes a comment', async () => {
  const mock = new MockAdapter(axiosClient);
  const thread = { id: 'c1', parentCommentId: null, content: 'Nice', authorName: 'Chi', authorAvatarUrl: null, isTeamReply: false, createdAt: '2026-11-09T10:00:00Z', canDelete: true, replyCount: 5, replies: [] };
  const page = (items: unknown[]) => ({ data: { items, pageIndex: 1, pageSize: 10, totalItems: items.length, totalPages: 1 } });
  mock.onGet('/showcase/s1/comments').reply(200, page([thread]));
  mock.onGet('/showcase/s1/comments/c1/replies').reply(200, page([]));
  mock.onPost('/showcase/s1/comments').reply(200, { data: { ...thread, id: 'c2' } });
  mock.onDelete('/showcase/s1/comments/c1').reply(200, { data: null });
  try {
    assert.equal((await showcaseApi.getComments('s1', 2, 10)).items[0].replyCount, 5);
    assert.deepEqual(mock.history.get[0].params, { page: 2, pageSize: 10 });
    await showcaseApi.getCommentReplies('s1', 'c1', 1, 20);
    assert.deepEqual(mock.history.get[1].params, { page: 1, pageSize: 20 });
    assert.equal((await showcaseApi.createComment('s1', { content: 'Hi', parentCommentId: 'c1' })).id, 'c2');
    assert.deepEqual(JSON.parse(mock.history.post[0].data), { content: 'Hi', parentCommentId: 'c1' });
    assert.equal(await showcaseApi.deleteComment('s1', 'c1'), null);
  } finally { mock.restore(); }
});

test('showcase detail client passes reviewer reference data through and keeps it null for other viewers', async () => {
  const mock = new MockAdapter(axiosClient);
  const reference = {
    classCode: 'C1',
    semester: { id: 'sem1', code: 'FA26', name: 'Fall 2026' },
    evaluations: [{ evaluatorRole: 'Lecturer', status: 'Published', totalScore: 85, maxTotalScore: 100, strengths: null, weaknesses: null, suggestions: null, publishedAt: null }],
  };
  mock.onGet('/showcase/reviewed').reply(200, { data: { industries: ['HealthTech'], referenceData: reference } });
  mock.onGet('/showcase/public').reply(200, { data: { industries: ['HealthTech'], referenceData: null } });
  try {
    const reviewed = await showcaseApi.get('reviewed');
    assert.deepEqual(reviewed.referenceData, reference);
    const publicView = await showcaseApi.get('public');
    assert.equal(publicView.referenceData, null);
    assert.deepEqual(publicView.industries, ['HealthTech']);
  } finally { mock.restore(); }
});
