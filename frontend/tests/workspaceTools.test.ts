import test from 'node:test';
import assert from 'node:assert/strict';
import { weeklyTaskSavePayload } from '../src/utils/weeklyTaskPayload.ts';
import { resolveWorkspaceTab, workspaceTabSearch } from '../src/utils/workspaceNavigation.ts';
import type { WeeklyTask, SaveWeeklyTaskPayload } from '../src/types/workspaceTools.ts';

const draft: SaveWeeklyTaskPayload = { title: 'Edited title', taskType: 'TEAM_TASK', weekNumber: 1, courseCode: 'EXE101' };

for (const status of ['TODO', 'IN_PROGRESS', 'REVIEW', 'COMPLETED', 'CANCELLED', 'OVERDUE'] as const) {
  test(`editing details preserves ${status}`, () => {
    const existing = { status, attachments: [{ name: 'Brief', url: 'https://example.com' }], visibleToStudents: false } as WeeklyTask;
    const result = weeklyTaskSavePayload(existing, draft);
    assert.equal(result.status, status);
    assert.equal(result.title, draft.title);
    assert.deepEqual(result.attachments, existing.attachments);
    assert.equal(result.visibleToStudents, false);
  });
}
test('new tasks default to To Do', () => {
  assert.equal(weeklyTaskSavePayload(null, draft).status, 'TODO');
});
for (const tab of ['overview', 'roadmap', 'shortcut'] as const) {
  test(`URL restores ${tab} after reload`, () => {
    const search = workspaceTabSearch('?tab=roadmap&teamId=team-1', tab);
    assert.equal(resolveWorkspaceTab(search), tab);
    assert.equal(new URLSearchParams(search).get('teamId'), 'team-1');
  });
}
test('invalid tab falls back to overview', () => {
  assert.equal(resolveWorkspaceTab('?tab=invalid'), 'overview');
});
