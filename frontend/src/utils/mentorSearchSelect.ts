import { matchesSearchQuery } from './searchText.ts';

export interface SearchSelectOption {
  id: string;
  label: string;
  /** Extra text that is searched and shown in a muted style, such as the email or the current load. */
  detail?: string;
}

/** Options whose name or detail contain every typed word, ignoring accents and case. An empty query keeps everything. */
export function filterSearchOptions(options: readonly SearchSelectOption[], query: string): SearchSelectOption[] {
  return options.filter(option => matchesSearchQuery(query, [option.label, option.detail]));
}

/** Moves the highlighted option up or down the list and wraps around at both ends. */
export function moveActiveIndex(current: number, step: 1 | -1, count: number): number {
  if (count <= 0) return -1;
  if (current < 0) return step === 1 ? 0 : count - 1;
  return (current + step + count) % count;
}
