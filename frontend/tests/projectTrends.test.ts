import assert from 'node:assert/strict';
import test from 'node:test';
import MockAdapter from 'axios-mock-adapter';
import axiosClient from '../src/api/axiosClient.ts';
import { projectDataApi } from '../src/api/projectDataApi.ts';
import type { ProjectDataTrendRow, ProjectDataTrends } from '../src/types/projectData.ts';
import {
  DEFAULT_PROJECT_DATA_QUERY,
  projectDataScopeKey,
  projectDataTrendsKey,
  toProjectDataTrendParams,
} from '../src/utils/projectData.ts';
import {
  MAX_CHART_SERIES,
  latestSemesterSummary,
  subjectColorSlots,
  toOutcomeChartData,
  toScoreChartData,
  formatCountChange,
  formatScore,
  formatScoreChange,
  scoreBarWidth,
  trendDirection,
  trendRowKey,
  trendRows,
} from '../src/utils/projectTrends.ts';

const row = (patch: Partial<ProjectDataTrendRow> = {}): ProjectDataTrendRow => ({
  semesterCode: 'FA2026', year: 2026, term: 'FA', subjectCode: null, classId: null, classCode: null,
  totalGroups: 2, gradedGroups: 1, averageScore: 9, potentialGroups: 1, internallyFundedGroups: 0, externallyFundedGroups: 0, awardedGroups: 0,
  averageScoreChange: null, potentialChange: null, internallyFundedChange: null, externallyFundedChange: null, awardedChange: null,
  ...patch,
});

test('score and change are formatted with an explicit sign and a placeholder when missing', () => {
  assert.equal(formatScore(7.4), '7.40');
  assert.equal(formatScore(null), '-');
  assert.equal(formatScoreChange(2.2), '+2.20');
  assert.equal(formatScoreChange(-0.3), '-0.30');
  assert.equal(formatScoreChange(0), '0.00');
  assert.equal(formatScoreChange(null), '-');
  assert.equal(formatCountChange(2), '+2');
  assert.equal(formatCountChange(-1), '-1');
  assert.equal(formatCountChange(0), '0');
  assert.equal(formatCountChange(null), '-');
});

test('direction tells improvement, decline, no change and no earlier semester apart', () => {
  assert.equal(trendDirection(0.1), 'up');
  assert.equal(trendDirection(-0.1), 'down');
  assert.equal(trendDirection(0), 'flat');
  assert.equal(trendDirection(null), 'none');
});

test('score bar is proportional to a ten-point scale and never hides a real score', () => {
  assert.equal(scoreBarWidth(10), 100);
  assert.equal(scoreBarWidth(5), 50);
  assert.equal(scoreBarWidth(0.1), 2);
  assert.equal(scoreBarWidth(null), 0);
  assert.equal(scoreBarWidth(12), 100);
});

test('rows are picked by level and keyed per semester, subject and class', () => {
  const trends: ProjectDataTrends = {
    semesters: [row()],
    subjects: [row({ subjectCode: 'EXE201' })],
    classes: [row({ subjectCode: 'EXE201', classId: 'c1', classCode: 'EXE201_1' })],
  };
  assert.equal(trendRows(trends, 'semester'), trends.semesters);
  assert.equal(trendRows(trends, 'subject'), trends.subjects);
  assert.equal(trendRows(trends, 'class'), trends.classes);
  assert.equal(new Set([...trends.semesters, ...trends.subjects, ...trends.classes].map(trendRowKey)).size, 3);
});

test('trend params leave out semester and year but keep the other filters', () => {
  const query = {
    ...DEFAULT_PROJECT_DATA_QUERY, search: 'farm', subjectCode: 'EXE201', semester: 'SU' as const, year: '2026',
    major: 'BIT_SE', pageIndex: 4,
  };
  assert.deepEqual(toProjectDataTrendParams(query), { search: 'farm', subjectCode: 'EXE201', major: 'BIT_SE' });
});

test('trend key ignores semester and year changes and sits under the project-data scope', () => {
  const base = { ...DEFAULT_PROJECT_DATA_QUERY, subjectCode: 'EXE201' };
  const other = { ...base, semester: 'FA' as const, year: '2025', pageIndex: 3 };
  assert.deepEqual(projectDataTrendsKey('u1', base), projectDataTrendsKey('u1', other));
  assert.deepEqual(projectDataTrendsKey('u1', base).slice(0, 2), projectDataScopeKey('u1'));
});

test('getTrends sends the filters without semester, year or paging', async () => {
  const mock = new MockAdapter(axiosClient);
  try {
    mock.onGet('/project-data/trends').reply(config => {
      assert.deepEqual(config.params, { subjectCode: 'EXE201' });
      return [200, { success: true, data: { semesters: [], subjects: [], classes: [] } }];
    });
    const response = await projectDataApi.getTrends({
      ...DEFAULT_PROJECT_DATA_QUERY, subjectCode: 'EXE201', semester: 'SU', year: '2026', pageIndex: 3,
    });
    assert.deepEqual(response.data.semesters, []);
  } finally {
    mock.restore();
  }
});

const trends = (): ProjectDataTrends => ({
  semesters: [
    row({ semesterCode: 'SU2026', year: 2026, term: 'SU', totalGroups: 3, gradedGroups: 3, averageScore: 7, potentialGroups: 1, internallyFundedGroups: 0 }),
    row({ semesterCode: 'FA2026', totalGroups: 2, gradedGroups: 0, averageScore: null, averageScoreChange: null, potentialGroups: 2, potentialChange: 1, internallyFundedGroups: 1, internallyFundedChange: 1, externallyFundedGroups: 1, externallyFundedChange: 1 }),
  ],
  subjects: [
    row({ semesterCode: 'SU2026', subjectCode: 'EXE201', totalGroups: 2, gradedGroups: 2, averageScore: 7.5 }),
    row({ semesterCode: 'SU2026', subjectCode: 'EXE301', totalGroups: 1, gradedGroups: 1, averageScore: 6 }),
    row({ semesterCode: 'FA2026', subjectCode: 'EXE201', totalGroups: 2, gradedGroups: 1, averageScore: 9, averageScoreChange: 1.5 }),
  ],
  classes: [],
});

test('single-series chart data keeps semester order and leaves a gap where nothing is graded', () => {
  const model = toScoreChartData(trends(), 'semester');
  assert.deepEqual(model.series, [{ key: 'score', label: 'Average score', slot: null }]);
  assert.deepEqual(model.data.map(item => [item.semester, item.score]), [['SU2026', 7], ['FA2026', null]]);
  assert.equal(model.data[0]['score:graded'], 3);
  assert.equal(model.hiddenSubjects, 0);
});

test('subject chart data has one series per subject and a null where a subject has no row in a semester', () => {
  const model = toScoreChartData(trends(), 'subject');
  assert.deepEqual(model.series.map(item => item.key), ['EXE201', 'EXE301']);
  assert.deepEqual(model.data.map(item => [item.semester, item.EXE201, item.EXE301]), [
    ['SU2026', 7.5, 6],
    ['FA2026', 9, undefined],
  ]);
});

test('only the busiest subjects get a line, the rest are counted as hidden', () => {
  const many: ProjectDataTrends = {
    semesters: [],
    classes: [],
    subjects: Array.from({ length: 8 }, (_, index) =>
      row({ semesterCode: 'SU2026', subjectCode: `S${index}`, totalGroups: 10 - index, averageScore: 5 })),
  };
  const model = toScoreChartData(many, 'subject');
  assert.equal(model.series.length, MAX_CHART_SERIES);
  assert.equal(model.hiddenSubjects, 2);
  assert.deepEqual(model.series.map(item => item.key), ['S0', 'S1', 'S2', 'S3', 'S4', 'S5']);
  assert.equal(new Set(model.series.map(item => item.slot)).size, MAX_CHART_SERIES);
});

test('a subject keeps its color when a filter removes another subject of the scope', () => {
  const scope = ['EXE101', 'EXE201', 'EXE301'];
  assert.equal(subjectColorSlots(['EXE201', 'EXE301'], scope).get('EXE301'), 2);
  assert.equal(subjectColorSlots(['EXE301'], scope).get('EXE301'), 2);
  assert.equal(subjectColorSlots(['EXE201'], scope).get('EXE201'), 1);
});

test('subjects outside the first slots take the free ones and never share a slot', () => {
  const scope = ['A', 'B', 'C', 'D', 'E', 'F', 'G'];
  const slots = subjectColorSlots(['A', 'G'], scope);
  assert.equal(slots.get('A'), 0);
  assert.equal(slots.get('G'), 1);
  assert.equal(new Set(slots.values()).size, 2);
});

test('outcome chart data follows the semester rows', () => {
  assert.deepEqual(toOutcomeChartData(trends()), [
    { semester: 'SU2026', Potential: 1, 'Internally Funded': 0, 'Externally Funded': 0, Awarded: 0 },
    { semester: 'FA2026', Potential: 2, 'Internally Funded': 1, 'Externally Funded': 1, Awarded: 0 },
  ]);
});

test('the KPI summary describes the newest semester even when nothing in it is graded', () => {
  const summary = latestSemesterSummary(trends());
  assert.equal(summary?.semesterCode, 'FA2026');
  assert.equal(summary?.averageScore, null);
  assert.equal(summary?.coveragePercent, 0);
  assert.equal(summary?.potentialChange, 1);
  assert.deepEqual(summary?.scoreHistory.map(item => item.score), [7, null]);
  assert.equal(latestSemesterSummary({ semesters: [], subjects: [], classes: [] }), null);
});
