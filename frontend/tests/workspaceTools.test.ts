import test from 'node:test';
import assert from 'node:assert/strict';
import { weeklyTaskSavePayload } from '../src/utils/weeklyTaskPayload.ts';
import { resolveWorkspaceTab, workspaceTabSearch } from '../src/utils/workspaceNavigation.ts';
import type { WeeklyTask, SaveWeeklyTaskPayload } from '../src/types/workspaceTools.ts';
import {
  getDropStatus,
  getTaskDropIndex,
  isTaskStatusMutableType,
  moveTaskStatusInBoard,
  normalizeBoardResponse,
  normalizeFilters,
} from '../src/features/execution-board/boardUtils.ts';
import { taskProgress } from '../src/utils/taskProgress.ts';
import { groupWorkspacesByClass, normalizeAccessibleWorkspaces } from '../src/utils/workspaceHub.ts';

test('workspace hub reads the accessible workspace array from the API envelope', () => {
  const workspaces = [{
    teamId: 'team-1',
    teamName: 'Phoenix Founders',
    classId: 'class-1',
    classCode: 'SE1818',
    courseCode: 'EXE101',
    semester: 'FA26',
    accessMode: 'READ_WRITE' as const,
    isArchived: false,
    isCurrent: true,
    hasWorkspace: true,
  }];

  assert.deepEqual(normalizeAccessibleWorkspaces({
    success: true,
    message: 'Accessible workspaces retrieved.',
    data: workspaces,
  }), workspaces);
  assert.deepEqual(normalizeAccessibleWorkspaces({
    success: false,
    message: 'Forbidden',
    data: workspaces,
  }), []);
});

test('workspace hub groups teams by class code and sorts class and team names naturally', () => {
  const base = {
    courseCode: 'EXE101',
    semester: 'SU26',
    accessMode: 'READ_WRITE' as const,
    isArchived: false,
    isCurrent: true,
    hasWorkspace: true,
  };
  const groups = groupWorkspacesByClass([
    { ...base, teamId: 'team-3', teamName: 'Team 10', classId: 'class-10', classCode: 'SE10' },
    { ...base, teamId: 'team-2', teamName: 'Team 2', classId: 'class-2', classCode: 'SE2' },
    { ...base, teamId: 'team-1', teamName: 'Team 1', classId: 'class-10', classCode: 'SE10' },
  ]);

  assert.deepEqual(groups.map(group => group.classCode), ['SE2', 'SE10']);
  assert.deepEqual(groups[1].workspaces.map(workspace => workspace.teamName), ['Team 1', 'Team 10']);
});

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

test('execution board resolves drag targets from empty columns and task cards', () => {
  const taskStatuses = new Map([
    ['task-in-review', 'REVIEW'],
  ]);

  assert.equal(getDropStatus({ id: 'column-IN_PROGRESS' }, taskStatuses), 'IN_PROGRESS');
  assert.equal(getDropStatus({ id: 'column-tab-COMPLETED' }, taskStatuses), 'COMPLETED');
  assert.equal(getDropStatus({ id: 'task-in-review' }, taskStatuses), 'REVIEW');
  assert.equal(getDropStatus(null, taskStatuses), null);
});

test('execution board prioritizes live droppable data after a card changes columns', () => {
  const staleTaskStatuses = new Map([
    ['moving-task', 'TODO'],
  ]);

  assert.equal(getDropStatus({
    id: 'moving-task',
    data: { current: { type: 'task', status: 'REVIEW' } },
  }, staleTaskStatuses), 'REVIEW');
});

test('execution board inserts a dragged task beside the card under the pointer', () => {
  const tasks = [
    { _id: 'review-a' },
    { _id: 'moving-task' },
    { _id: 'review-b' },
  ];

  assert.equal(getTaskDropIndex({
    tasks,
    activeTaskId: 'moving-task',
    overTaskId: 'review-b',
    insertAfter: false,
  }), 1);
  assert.equal(getTaskDropIndex({
    tasks,
    activeTaskId: 'moving-task',
    overTaskId: 'review-b',
    insertAfter: true,
  }), 2);
});

test('execution board can cycle a task through every status without duplicating or losing it', () => {
  const statuses = ['IN_PROGRESS', 'REVIEW', 'COMPLETED', 'OVERDUE', 'TODO'];
  let board = normalizeBoardResponse({
    tasks: [{ _id: 'moving-task', status: 'TODO' }],
  });

  for (let cycle = 0; cycle < 3; cycle += 1) {
    for (const status of statuses) {
      board = moveTaskStatusInBoard(board, 'moving-task', status);
      assert.equal(board.tasks[0].status, status);
      assert.equal(board.grouped[status].filter((task) => task._id === 'moving-task').length, 1);
      assert.equal(Object.values(board.grouped).flat().length, 1);
    }
  }
});

test('all execution board task sources allow team-scoped status updates', () => {
  assert.equal(isTaskStatusMutableType({ taskType: 'COURSE_TEMPLATE' }), true);
  assert.equal(isTaskStatusMutableType({ taskType: 'CLASS_TASK' }), true);
  assert.equal(isTaskStatusMutableType({ taskType: 'TEAM_TASK' }), true);
  assert.equal(isTaskStatusMutableType({ taskType: 'UNKNOWN' }), false);
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
