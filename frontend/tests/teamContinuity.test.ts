import assert from 'node:assert/strict';
import test from 'node:test';
import type { TeamContinuationItem, TeamContinuationSummary } from '../src/types/classes.ts';
import {
  continuationHeadline,
  continuationNeedsAttention,
  continuationOutcomeLabel,
  hasContinuationActivity,
} from '../src/utils/teamContinuity.ts';
import { normalizeManagedTeam } from '../src/utils/teamManagement.ts';
import { resolveWorkspaceTab } from '../src/utils/workspaceNavigation.ts';

const item = (overrides: Partial<TeamContinuationItem> = {}): TeamContinuationItem => ({
  teamId: 'team-new',
  sourceTeamId: 'team-old',
  teamName: 'Alpha',
  sourceTeamName: 'Alpha',
  sourceClassCode: 'EXE101-SP26-01',
  outcome: 'Created',
  memberCount: 5,
  reasons: [],
  ...overrides,
});

const summary = (overrides: Partial<TeamContinuationSummary> = {}): TeamContinuationSummary => ({
  sourceSemesterCode: 'SP26',
  createdCount: 0,
  membersAddedCount: 0,
  notEligibleCount: 0,
  items: [],
  ...overrides,
});

test('labels every continuation outcome and flags the ones needing attention', () => {
  assert.equal(continuationOutcomeLabel('Created'), 'Continued');
  assert.equal(continuationOutcomeLabel('NotEligible'), 'Not eligible');
  assert.equal(continuationNeedsAttention(item({ outcome: 'NotEligible' })), true);
  assert.equal(continuationNeedsAttention(item({ outcome: 'Dissolved' })), true);
  assert.equal(continuationNeedsAttention(item({ outcome: 'Created' })), false);
  assert.equal(continuationNeedsAttention(item({ outcome: 'MembersAdded' })), false);
});

test('shows the continuation panel only when there is something to report', () => {
  assert.equal(hasContinuationActivity(null), false);
  assert.equal(hasContinuationActivity(undefined), false);
  assert.equal(hasContinuationActivity(summary()), false);
  assert.equal(hasContinuationActivity(summary({ items: [item()] })), true);
});

test('words the headline differently for preview and committed imports', () => {
  const data = summary({ createdCount: 2, membersAddedCount: 1, notEligibleCount: 1, items: [item()] });

  assert.equal(
    continuationHeadline(data, false),
    '2 teams from SP26 will be continued, 1 team will receive late members, 1 not eligible yet',
  );
  assert.equal(
    continuationHeadline(data, true),
    '2 teams from SP26 continued, 1 team received late members, 1 not eligible yet',
  );
  assert.equal(continuationHeadline(summary(), true), 'No team from SP26 can be continued');
  assert.equal(
    continuationHeadline(summary({ createdCount: 1 }), true),
    '1 team from SP26 continued',
  );
});

test('keeps the continuity fields when normalizing a team from the API', () => {
  const team = normalizeManagedTeam({
    id: 'team-new',
    teamName: 'Alpha',
    teamLineageId: 'lineage-1',
    isContinued: true,
    continuedFromSemesterCode: 'SP26',
    continuedFromClassCode: 'EXE101-SP26-01',
    members: [],
  });

  assert.equal(team.isContinued, true);
  assert.equal(team.teamLineageId, 'lineage-1');
  assert.equal(team.continuedFromSemesterCode, 'SP26');
  assert.equal(team.continuedFromClassCode, 'EXE101-SP26-01');
});

test('opens the semester history workspace tab from the query string', () => {
  assert.equal(resolveWorkspaceTab('?tab=history'), 'history');
  assert.equal(resolveWorkspaceTab('?tab=unknown'), 'overview');
});
