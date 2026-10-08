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
