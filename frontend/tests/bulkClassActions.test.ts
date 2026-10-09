import assert from 'node:assert/strict';
import test from 'node:test';
import { executeBulkClassAction } from '../src/utils/bulkClassActions.ts';

const targets = [
  { _id: 'class-1', classCode: 'EXE101_1' },
  { _id: 'class-2', classCode: 'EXE101_2' },
  { _id: 'class-3', classCode: 'EXE101_3' },
];

test('executes bulk class actions sequentially and preserves the selected order', async () => {
  const executionOrder: string[] = [];
  const result = await executeBulkClassAction(targets, async (target) => {
    executionOrder.push(target.classCode);
  }, 'Bulk operation failed.');

  assert.deepEqual(executionOrder, ['EXE101_1', 'EXE101_2', 'EXE101_3']);
  assert.deepEqual(result.succeeded, executionOrder);
  assert.deepEqual(result.failed, []);
});

test('continues after one class fails and exposes its backend error', async () => {
  const result = await executeBulkClassAction(targets, async (target) => {
    if (target._id === 'class-2') {
      throw {
        response: {
          data: {
            code: 'SCHEDULE_CONFLICT',
            message: 'The lecturer already teaches another class in this slot.',
          },
        },
      };
    }
  }, 'Bulk operation failed.');

  assert.deepEqual(result.succeeded, ['EXE101_1', 'EXE101_3']);
  assert.deepEqual(result.failed, [{
    classId: 'class-2',
    classCode: 'EXE101_2',
    code: 'SCHEDULE_CONFLICT',
    message: 'The lecturer already teaches another class in this slot.',
  }]);
});
