import type { MentorTagSet } from '../types/mentorProfile';

export type TagCategory = keyof MentorTagSet;

export const TAG_CATEGORIES: ReadonlyArray<{ key: TagCategory; label: string }> = [
  { key: 'expertise', label: 'Expertise' },
  { key: 'startupDomains', label: 'Startup domain' },
  { key: 'technologySkills', label: 'Technology' },
  { key: 'mentorTags', label: 'Tag' },
];

export interface TagOption {
  /** Stable id of a tag inside the filter: the category and the lower-cased label. */
  key: string;
  category: TagCategory;
  label: string;
  /** How many of the listed mentors carry it. */
  count: number;
}

type MaybeTags = Partial<Record<TagCategory, readonly string[] | null | undefined>> | null | undefined;

export const tagKey = (category: TagCategory, label: string) => `${category}:${label.trim().toLowerCase()}`;

/** Every tag of one mentor as a flat list. */
export function mentorTagList(tags: MaybeTags): { category: TagCategory; label: string }[] {
  return TAG_CATEGORIES.flatMap(({ key }) => (tags?.[key] ?? []).map(label => ({ category: key, label })));
}

/** The words of a mentor's tags, for the text search box. */
export function tagSearchText(tags: MaybeTags): string[] {
  return mentorTagList(tags).map(tag => tag.label);
}

/** The distinct tags of a set of mentors, grouped by category and ordered by how many mentors have them. */
export function collectTagOptions(items: ReadonlyArray<{ tags?: MaybeTags }>): TagOption[] {
  const counts = new Map<string, TagOption>();
  for (const item of items) {
    const seenForItem = new Set<string>();
    for (const { category, label } of mentorTagList(item.tags)) {
      const key = tagKey(category, label);
      if (seenForItem.has(key)) continue;
      seenForItem.add(key);
      const existing = counts.get(key);
      if (existing) existing.count += 1;
      else counts.set(key, { key, category, label: label.trim(), count: 1 });
    }
  }
  const order = (category: TagCategory) => TAG_CATEGORIES.findIndex(item => item.key === category);
  return [...counts.values()].sort((left, right) =>
    order(left.category) - order(right.category) || right.count - left.count || left.label.localeCompare(right.label, undefined, { sensitivity: 'base' }));
}

/** True when no tag is selected, or the mentor carries at least one of the selected tags. */
export function matchesAnyTag(tags: MaybeTags, selectedKeys: readonly string[]): boolean {
  if (selectedKeys.length === 0) return true;
  const own = new Set(mentorTagList(tags).map(tag => tagKey(tag.category, tag.label)));
  return selectedKeys.some(key => own.has(key));
}

/** Drops selected tags that no listed mentor has any more (for example after the list refreshed). */
export function keepKnownTags(selectedKeys: readonly string[], options: readonly TagOption[]): string[] {
  const known = new Set(options.map(option => option.key));
  return selectedKeys.filter(key => known.has(key));
}
