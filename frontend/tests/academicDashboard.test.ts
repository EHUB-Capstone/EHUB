import assert from 'node:assert/strict';
import test from 'node:test';
import {
  academicOverviewMetricEntries,
  ACADEMIC_OVERVIEW_ERROR_FALLBACK,
  percentage,
  resolveAcademicOverviewState,
  resolveOverviewErrorMessage,
  semesterScopeNotice,
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

test('shows the reason from the server for business errors and a generic message otherwise', () => {
  const businessError = { response: { data: { code: 'SEMESTER_NOT_FOUND', message: 'No semester is configured for the academic overview.' } } };
  const messageWithoutCode = { response: { data: { message: 'Internal detail that must not reach the user' } } };

  assert.equal(resolveOverviewErrorMessage(businessError), 'No semester is configured for the academic overview.');
  assert.equal(resolveOverviewErrorMessage(messageWithoutCode), ACADEMIC_OVERVIEW_ERROR_FALLBACK);
  assert.equal(resolveOverviewErrorMessage(new Error('Network Error')), ACADEMIC_OVERVIEW_ERROR_FALLBACK);
  assert.equal(resolveOverviewErrorMessage(undefined), ACADEMIC_OVERVIEW_ERROR_FALLBACK);
});

test('explains when the overview is not about the active semester', () => {
  assert.equal(semesterScopeNotice({ isActiveSemester: true, semesterName: 'FA 2026', semesterCode: 'FA2026' }), null);
  assert.equal(semesterScopeNotice({ semesterName: 'FA 2026', semesterCode: 'FA2026' }), null, 'older servers do not send the flag');
  assert.equal(
    semesterScopeNotice({ isActiveSemester: false, semesterName: 'SP 2027', semesterCode: 'SP2027' }),
    'No semester is currently active. Showing SP 2027.',
  );
  assert.equal(
    semesterScopeNotice({ isActiveSemester: false, semesterName: '', semesterCode: 'SP2027' }),
    'No semester is currently active. Showing SP2027.',
  );
});
