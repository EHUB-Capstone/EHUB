import assert from 'node:assert/strict';
import test from 'node:test';
import {
  academicOverviewMetricEntries,
  percentage,
  resolveAcademicOverviewState,
} from '../src/utils/academicDashboard.ts';

test('resolves loading, error, assignment, filtered empty, and ready states', () => {
  assert.equal(resolveAcademicOverviewState({ loading: true, hasError: false }), 'loading');
  assert.equal(resolveAcademicOverviewState({ loading: false, hasError: true }), 'error');
  assert.equal(resolveAcademicOverviewState({ loading: false, hasError: false, hasAssignedClasses: false }), 'unassigned');
  assert.equal(resolveAcademicOverviewState({ loading: false, hasError: false, hasAssignedClasses: true, hasMatchingClasses: false }), 'filtered-empty');
  assert.equal(resolveAcademicOverviewState({ loading: false, hasError: false, hasAssignedClasses: true, hasMatchingClasses: true }), 'ready');
});

test('calculates bounded dashboard percentages', () => {
  assert.equal(percentage(3, 4), 75);
  assert.equal(percentage(0, 0), 0);
  assert.equal(percentage(6, 4), 100);
});

test('builds all six metrics and preserves zero values', () => {
  const entries = academicOverviewMetricEntries({
    totalClasses: 2,
    totalTeams: 0,
    totalProjects: 0,
    totalSubmissions: 0,
    totalEvaluations: 0,
    totalPotentialProjects: 0,
  });

  assert.equal(entries.length, 6);
  assert.equal(entries[0].value, 2);
  assert.equal(entries.slice(1).every((entry) => entry.value === 0), true);
});
