import assert from 'node:assert/strict';
import test from 'node:test';
import {
  DEFAULT_PROJECT_DATA_QUERY,
  parseProjectDataQuery,
  parseProjectDataTab,
  projectDataDistributionKey,
  projectDataSummaryKey,
  toProjectDataSearchParams,
} from '../src/utils/projectData.ts';

test('the Overview tab is the default and stays out of the URL', () => {
  assert.equal(parseProjectDataTab(new URLSearchParams()), 'overview');
  assert.equal(toProjectDataSearchParams(DEFAULT_PROJECT_DATA_QUERY).toString(), '');
  assert.equal(toProjectDataSearchParams(DEFAULT_PROJECT_DATA_QUERY, 'overview').toString(), '');
});

test('an unknown tab value falls back to Overview', () => {
  assert.equal(parseProjectDataTab(new URLSearchParams('tab=charts')), 'overview');
  assert.equal(parseProjectDataTab(new URLSearchParams('tab=ANALYTICS')), 'overview');
});

test('the Analytics tab round-trips through the URL together with the filters', () => {
  const query = { ...DEFAULT_PROJECT_DATA_QUERY, subjectCode: 'EXE201', achievement: 'Potential' as const, pageIndex: 3 };
  const params = toProjectDataSearchParams(query, 'analytics');

  assert.equal(params.get('tab'), 'analytics');
  assert.equal(parseProjectDataTab(params), 'analytics');
  assert.deepEqual(parseProjectDataQuery(params), query);
});

test('switching tab does not change the filter query or the request keys', () => {
  const query = { ...DEFAULT_PROJECT_DATA_QUERY, search: 'farm', semester: 'SU' as const, year: '2026' };
  const overview = parseProjectDataQuery(toProjectDataSearchParams(query, 'overview'));
  const analytics = parseProjectDataQuery(toProjectDataSearchParams(query, 'analytics'));

  assert.deepEqual(overview, analytics);
  assert.deepEqual(projectDataSummaryKey('u1', overview), projectDataSummaryKey('u1', analytics));
  assert.deepEqual(projectDataDistributionKey('u1', overview), projectDataDistributionKey('u1', analytics));
});
