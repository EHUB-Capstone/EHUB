import assert from 'node:assert/strict';
import test from 'node:test';
import MockAdapter from 'axios-mock-adapter';
import axiosClient from '../src/api/axiosClient.ts';
import { projectDataApi } from '../src/api/projectDataApi.ts';
import {
  DEFAULT_PROJECT_DATA_QUERY,
  activeFilterKeys,
  clearProjectDataFilters,
  parseProjectDataQuery,
  projectDataDistributionKey,
  projectDataScopeKey,
  projectDataSummaryKey,
  toProjectDataSearchParams,
  toProjectDataSummaryParams,
} from '../src/utils/projectData.ts';
import {
  UNSPECIFIED_MAJOR,
  distributionBarWidth,
  distributionPercent,
  limitDistributionBuckets,
  majorLabel,
} from '../src/utils/projectDistribution.ts';

test('distributionPercent rounds to a whole share and tolerates empty totals', () => {
  assert.equal(distributionPercent(1, 3), 33);
  assert.equal(distributionPercent(2, 3), 67);
  assert.equal(distributionPercent(4, 4), 100);
  assert.equal(distributionPercent(0, 4), 0);
  assert.equal(distributionPercent(5, 0), 0);
});

test('distributionBarWidth fills the track for the largest bucket and never hides a non-empty one', () => {
  assert.equal(distributionBarWidth(10, 10), 100);
  assert.equal(distributionBarWidth(5, 10), 50);
  assert.equal(distributionBarWidth(1, 1000), 2);
  assert.equal(distributionBarWidth(0, 10), 0);
  assert.equal(distributionBarWidth(3, 0), 0);
});

test('limitDistributionBuckets keeps the first buckets and sums the rest', () => {
  const buckets = [
    { name: 'A', count: 5 },
    { name: 'B', count: 4 },
    { name: 'C', count: 2 },
    { name: 'D', count: 1 },
  ];
  assert.deepEqual(limitDistributionBuckets(buckets, 4), { items: buckets, otherCount: 0, otherBuckets: 0 });
  assert.deepEqual(limitDistributionBuckets(buckets, 2), {
    items: buckets.slice(0, 2),
    otherCount: 3,
    otherBuckets: 2,
  });
});

test('majorLabel shows a readable name for codes and the unspecified bucket', () => {
  assert.equal(majorLabel('BIT_SE'), 'BIT SE');
  assert.equal(majorLabel(UNSPECIFIED_MAJOR), 'Unspecified');
});

test('distribution query key ignores paging and sorting and sits under the project-data scope', () => {
  const base = { ...DEFAULT_PROJECT_DATA_QUERY, subjectCode: 'EXE201', achievement: 'Potential' as const };
  const paged = { ...base, pageIndex: 4, pageSize: 50 as const, sortBy: 'group' as const, isDescending: true };

  assert.deepEqual(projectDataDistributionKey('u1', base), projectDataDistributionKey('u1', paged));
  assert.notDeepEqual(projectDataDistributionKey('u1', base), projectDataSummaryKey('u1', base));
  assert.deepEqual(projectDataDistributionKey('u1', base).slice(0, 2), projectDataScopeKey('u1'));
});

test('getDistribution sends the same filter params as the summary and no paging', async () => {
  const mock = new MockAdapter(axiosClient);
  try {
    mock.onGet('/project-data/distribution').reply(config => {
      assert.deepEqual(config.params, { search: 'farm', subjectCode: 'EXE201', semester: 'SU', year: '2026' });
      return [200, { success: true, data: { totalGroups: 2 } }];
    });
    const response = await projectDataApi.getDistribution({
      ...DEFAULT_PROJECT_DATA_QUERY,
      search: 'farm',
      subjectCode: 'EXE201',
      semester: 'SU',
      year: '2026',
      pageIndex: 3,
      pageSize: 50,
    });
    assert.equal(response.data.totalGroups, 2);
  } finally {
    mock.restore();
  }
});

test('major is a filter: parsed from the URL, serialised back, and sent with the summary params', () => {
  const query = parseProjectDataQuery(new URLSearchParams('major=bit_se&semester=SU'));
  assert.equal(query.major, 'BIT_SE');
  assert.equal(toProjectDataSearchParams(query).get('major'), 'BIT_SE');
  assert.deepEqual(toProjectDataSummaryParams(query), { semester: 'SU', major: 'BIT_SE' });
  assert.deepEqual(activeFilterKeys(query), ['semester', 'major']);
  assert.equal(clearProjectDataFilters(query).major, '');
});

test('an invalid major in the URL is ignored instead of failing the request', () => {
  assert.equal(parseProjectDataQuery(new URLSearchParams('major=BIT SE!')).major, '');
  assert.equal(parseProjectDataQuery(new URLSearchParams('major=' + 'A'.repeat(40))).major, '');
});
