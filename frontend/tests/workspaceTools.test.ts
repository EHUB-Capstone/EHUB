import test from 'node:test';
import assert from 'node:assert/strict';
import { weeklyTaskSavePayload } from '../src/utils/weeklyTaskPayload.ts';
import { resolveWorkspaceTab, workspaceTabSearch } from '../src/utils/workspaceNavigation.ts';
import type { WeeklyTask, SaveWeeklyTaskPayload } from '../src/types/workspaceTools.ts';
import { normalizeBoardResponse, moveTaskStatusInBoard, normalizeFilters } from '../src/features/execution-board/boardUtils.ts';
import { taskProgress } from '../src/utils/taskProgress.ts';

test('table and board summary use API progress instead of status estimates', () => {
  const tasks = [{ _id: 'review', status: 'REVIEW', completionPercentage: 0 }, { _id: 'working', status: 'IN_PROGRESS', completionPercentage: 30 }];
  const board = normalizeBoardResponse({ data: { teamTasks: tasks } });
  assert.equal(taskProgress(tasks[0]), 0);
  assert.equal(taskProgress(tasks[1]), 30);
  assert.equal(board.summary.completionPercentage, 15);
});

test('reopening completed task resets optimistic progress to unchanged checklist progress', () => {
  const checklist = [{ text: 'First', isCompleted: true }, { text: 'Second', isCompleted: false }];
  const board = normalizeBoardResponse({ tasks: [{ _id: 'task', status: 'COMPLETED', completionPercentage: 100, checklist }] });
  const reopened = moveTaskStatusInBoard(board, 'task', 'REVIEW');
  assert.equal(taskProgress(reopened.tasks[0]), 50);
  assert.deepEqual(reopened.tasks[0].checklist, checklist);
  const completed = moveTaskStatusInBoard(reopened, 'task', 'COMPLETED');
  assert.equal(taskProgress(completed.tasks[0]), 100);
  assert.deepEqual(completed.tasks[0].checklist, checklist);
});

test('progress safely handles missing or invalid API values', () => {
  for (const value of [undefined, null, NaN, Infinity, -1]) assert.equal(taskProgress({ completionPercentage: value }), 0);
  assert.equal(taskProgress({ completionPercentage: 150 }), 100);
});

test('execution board includes all three roadmap sources and preserves status', () => {
  const board = normalizeBoardResponse({ data: {
    courseTasks: [{ _id: 'course', status: 'TODO' }],
    classTasks: [{ _id: 'class', status: 'IN_PROGRESS' }],
    teamTasks: [{ _id: 'team', status: 'COMPLETED' }],
  } });
  assert.equal(board.tasks.length, 3);
  assert.equal(board.grouped.TODO[0]._id, 'course');
  assert.equal(board.grouped.IN_PROGRESS[0]._id, 'class');
  assert.equal(board.grouped.COMPLETED[0]._id, 'team');
  const moved = moveTaskStatusInBoard(board, 'team', 'REVIEW');
  assert.equal(moved.grouped.REVIEW[0]._id, 'team');
  assert.equal(moved.tasks.length, 3);
});

test('all weeks omits week restriction while forwarding board filters', () => {
  assert.deepEqual(normalizeFilters({ week: 'ALL', assignee: 'ALL', priority: 'HIGH', search: '  test  ' }), { priority: 'HIGH', search: 'test' });
});

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
