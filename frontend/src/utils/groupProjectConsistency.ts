import type { GroupProjectConsistency, GroupProjectWarning } from '../types/groupProjectConsistency';

const collator = new Intl.Collator('en', { numeric: true, sensitivity: 'base' });

function collect(pairs: Array<{ key: string; value: string }>): Array<{ key: string; values: string[] }> {
  const byKey = new Map<string, { key: string; values: Map<string, string> }>();
  for (const { key, value } of pairs) {
    const entry = byKey.get(key.toLowerCase()) ?? { key, values: new Map<string, string>() };
    if (!byKey.has(key.toLowerCase())) byKey.set(key.toLowerCase(), entry);
    if (!entry.values.has(value.toLowerCase())) entry.values.set(value.toLowerCase(), value);
  }
  return [...byKey.values()]
    .map((entry) => ({ key: entry.key, values: [...entry.values.values()].sort(collator.compare) }))
    .filter((entry) => entry.values.length > 1)
    .sort((left, right) => collator.compare(left.key, right.key));
}

const quote = (values: string[]) => values.map((value) => `\`${value}\``).join(', ');

/**
 * Mirrors the backend GroupProjectConsistencyRules. The backend is the source of truth
 * (GET /classes/:id/group-project-consistency); this is used by the mock API only.
 */
export function evaluateGroupProjectConsistency(
  students: Array<{ group?: string | null; project?: string | null }>,
): GroupProjectConsistency {
  const pairs = students
    .map((student) => ({ group: student.group?.trim() ?? '', project: student.project?.trim() ?? '' }))
    .filter((pair) => pair.group && pair.project);

  const warnings: GroupProjectWarning[] = [
    ...collect(pairs.map((pair) => ({ key: pair.group, value: pair.project }))).map((item) => ({
      type: 'GROUP_HAS_MULTIPLE_PROJECTS' as const,
      subject: item.key,
      related: item.values,
      message: `Group \`${item.key}\` is assigned to multiple projects: ${quote(item.values)}.`,
    })),
    ...collect(pairs.map((pair) => ({ key: pair.project, value: pair.group }))).map((item) => ({
      type: 'PROJECT_HAS_MULTIPLE_GROUPS' as const,
      subject: item.key,
      related: item.values,
      message: `Project \`${item.key}\` is assigned to multiple groups: ${quote(item.values)}.`,
    })),
  ];
  return { isConsistent: warnings.length === 0, warnings };
}
