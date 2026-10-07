export interface BatchTeam {
  id: string;
  name: string;
}

export interface BatchAssignmentResult {
  teamId: string;
  teamName: string;
  ok: boolean;
  error?: string;
}

export interface BatchSummary {
  succeeded: number;
  failed: BatchAssignmentResult[];
  tone: 'success' | 'partial' | 'error';
  message: string;
}

/**
 * Assigns one mentor to several teams, one team at a time. Every team keeps its own permission and rule checks
 * on the server, so a team that cannot take the mentor is reported and the remaining teams are still processed.
 * Calls are sequential on purpose: each assignment is its own serializable transaction.
 */
export async function assignMentorToTeams(
  teams: BatchTeam[],
  assign: (teamId: string) => Promise<unknown>,
  describeError: (error: unknown) => string,
): Promise<BatchAssignmentResult[]> {
  const results: BatchAssignmentResult[] = [];
  for (const team of teams) {
    try {
      await assign(team.id);
      results.push({ teamId: team.id, teamName: team.name, ok: true });
    } catch (error) {
      results.push({ teamId: team.id, teamName: team.name, ok: false, error: describeError(error) });
    }
  }
  return results;
}

export function summarizeBatch(results: BatchAssignmentResult[]): BatchSummary {
  const failed = results.filter(item => !item.ok);
  const succeeded = results.length - failed.length;
  const plural = (count: number) => `${count} team${count === 1 ? '' : 's'}`;

  if (failed.length === 0) {
    return { succeeded, failed, tone: 'success', message: `Assigned to ${plural(succeeded)}.` };
  }
  if (succeeded === 0) {
    return { succeeded, failed, tone: 'error', message: `Could not assign ${plural(failed.length)}.` };
  }
  return {
    succeeded,
    failed,
    tone: 'partial',
    message: `Assigned to ${plural(succeeded)}, ${failed.length} failed.`,
  };
}

export type SelectionState = 'none' | 'some' | 'all';

export function toggleSelection(selected: string[], id: string): string[] {
  return selected.includes(id) ? selected.filter(item => item !== id) : [...selected, id];
}

/** Ticks or unticks every id in `visibleIds` while leaving selections outside the current filter untouched. */
export function setVisibleSelection(selected: string[], visibleIds: string[], checked: boolean): string[] {
  const visible = new Set(visibleIds);
  const outsideFilter = selected.filter(id => !visible.has(id));
  return checked ? [...outsideFilter, ...visibleIds] : outsideFilter;
}

export function getSelectionState(selected: string[], visibleIds: string[]): SelectionState {
  if (visibleIds.length === 0) return 'none';
  const chosen = visibleIds.filter(id => selected.includes(id)).length;
  if (chosen === 0) return 'none';
  return chosen === visibleIds.length ? 'all' : 'some';
}

/** Keeps only selections that are still eligible, so a stale tick never reaches a team that can no longer take the mentor. */
export function keepEligibleSelection(selected: string[], eligibleIds: string[]): string[] {
  const eligible = new Set(eligibleIds);
  return selected.filter(id => eligible.has(id));
}
