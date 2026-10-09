import type { MentorProfileDraft } from '../types/mentoring.ts';

export const parseMentorTags = (text: string): string[] => text.trim() ? text.split(',').map(x => x.trim()) : [];

export function validateMentorMetadata(profile: MentorProfileDraft): string | null {
  if (!['Business', 'IT'].includes(profile.mentorType)) return 'Choose a mentor type.';
  for (const [label, values, required] of [
    ['Expertise', profile.expertise, true], ['Startup domains', profile.startupDomains, false],
    ['Technology skills', profile.technologySkills, false], ['Mentor tags', profile.tags, false],
  ] as const) {
    if (!Array.isArray(values) || values.length > 20 || (required && values.length === 0) ||
      values.some(x => !x.trim() || x.trim().length > 80) || new Set(values.map(x => x.trim().toLowerCase())).size !== values.length)
      return `${label}: enter ${required ? '1–20' : 'up to 20'} distinct values, each at most 80 characters. Remove blank or duplicate entries.`;
  }
  const experiences = profile.experiences;
  if (experiences.length > 40) return 'Enter at most 40 experience entries.';
  const keys = experiences.map(x => `${x.kind}:${x.area.trim().toLowerCase()}`);
  if (new Set(keys).size !== keys.length) return 'Duplicate experience entries are not allowed.';
  if (experiences.some(x => !['Startup', 'Technology'].includes(x.kind) || !x.area.trim() || x.area.trim().length > 80 ||
    (x.years !== null && (!Number.isFinite(x.years) || x.years < 0 || x.years > 80 || Math.abs(x.years * 10 - Math.round(x.years * 10)) > 1e-8)) ||
    (x.level?.length ?? 0) > 40 || (x.notes?.length ?? 0) > 1000)) return 'Experience requires an area and valid years (0–80, one decimal), level and notes.';
  return null;
}

export function mentorSearchText(profile: MentorProfileDraft): string {
  return [...profile.expertise, ...profile.startupDomains, ...profile.technologySkills, ...profile.tags,
    ...profile.experiences.map(x => `${x.area} ${x.level ?? ''} ${x.notes ?? ''}`)].join(' ').toLowerCase();
}
