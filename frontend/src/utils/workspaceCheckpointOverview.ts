import type {
  WorkspaceCheckpointConfig,
  WorkspaceCheckpointStats,
  WorkspaceCheckpointSubmission,
} from '../types/workspaceCheckpoints.ts';

const CHECKPOINT_ICONS = ['Users', 'BarChart2', 'Layers', 'TrendingUp'] as const;

export function buildWorkspaceCheckpointOverview(
  checkpoints: readonly WorkspaceCheckpointConfig[],
  submissions: readonly WorkspaceCheckpointSubmission[],
) {
  const presentationCheckpoints = checkpoints.map((checkpoint, index) => ({
    ...checkpoint,
    icon: CHECKPOINT_ICONS[index % CHECKPOINT_ICONS.length],
  }));
  const checkpointByNumber = new Map(
    checkpoints.map((checkpoint) => [Number(checkpoint.number), checkpoint]),
  );
  const stats: Record<number, WorkspaceCheckpointStats> = {};

  for (const submission of submissions) {
    const checkpointNumber = Number(submission.checkpointNumber);
    const checkpoint = checkpointByNumber.get(checkpointNumber);
    if (!checkpoint) continue;

    const files = [...(submission.files || [])].sort(
      (left, right) => Date.parse(right.uploadedAt) - Date.parse(left.uploadedAt),
    );
    const reqFilled = (submission.requirementContents || []).filter(
      (requirement) => String(requirement.content || '').trim().length > 0,
    ).length;
    stats[checkpointNumber] = {
      count: files.length,
      latest: files[0] || null,
      reqFilled,
      reqTotal: checkpoint.requirements.length,
    };
  }

  for (const checkpoint of checkpoints) {
    const checkpointNumber = Number(checkpoint.number);
    stats[checkpointNumber] ??= {
      count: 0,
      latest: null,
      reqFilled: 0,
      reqTotal: checkpoint.requirements.length,
    };
  }

  return { checkpoints: presentationCheckpoints, stats };
}
