import assert from 'node:assert/strict';
import test from 'node:test';
import { buildEvaluationReportExportScope } from '../src/utils/evaluationGrading.ts';
import type { EvaluationGradingRecord } from '../src/types/evaluationGrading.ts';

function record(teamId: string, checkpointNumber: number): EvaluationGradingRecord {
  return {
    key: `${teamId}-${checkpointNumber}`,
    team: {
      teamId,
      teamName: teamId,
      classId: 'class-1',
      classCode: 'EXE201_1',
      courseCode: 'EXE201',
      semester: 'FA 2026',
      hasWorkspace: true,
      accessMode: 'EDIT',
      isCurrent: true,
    },
    checkpoint: {
      number: checkpointNumber,
      title: `Checkpoint ${checkpointNumber}`,
      courseWeight: 10,
    },
    evaluation: null,
    status: 'NOT_GRADED',
  };
}

test('evaluation export scope contains only visible teams and checkpoints', () => {
  const scope = buildEvaluationReportExportScope([
    record('team-1', 2),
    record('team-1', 1),
    record('team-1', 2),
    record('team-2', 3),
  ]);

  assert.deepEqual(scope, [
    { teamId: 'team-1', checkpointNumbers: [1, 2] },
    { teamId: 'team-2', checkpointNumbers: [3] },
  ]);
});

test('evaluation export scope is empty when filters leave no records', () => {
  assert.deepEqual(buildEvaluationReportExportScope([]), []);
});
