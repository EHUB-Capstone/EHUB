// Progress comes from the API, not an estimate based on workflow status.
export function taskProgress(task: { completionPercentage?: number | null }): number {
  const value = task.completionPercentage;
  return typeof value === 'number' && Number.isFinite(value)
    ? Math.min(100, Math.max(0, value))
    : 0;
}
