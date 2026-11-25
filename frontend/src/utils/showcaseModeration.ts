import type { ShowcaseModerationRuleDto, ShowcaseViolationCategory } from '../types/showcase.ts';

export const SHOWCASE_VIOLATION_CATEGORIES: readonly ShowcaseViolationCategory[] = [
  'Profanity', 'Insult', 'Threat', 'Defamation', 'Racism', 'AdultContent', 'Spam',
];

export function showcaseCategoryFromReason(reason: ShowcaseModerationRuleDto['reason']): ShowcaseViolationCategory {
  return reason === 'Spam' ? 'Spam' : reason === 'InappropriateContent' ? 'AdultContent' : 'Insult';
}

export function showcaseReasonFromCategory(category: ShowcaseViolationCategory): ShowcaseModerationRuleDto['reason'] {
  return category === 'Spam' ? 'Spam' : category === 'AdultContent' ? 'InappropriateContent' : 'Harassment';
}

// Only this Showcase error prefix carries a bounded list of categories. Unknown codes keep the existing fallback.
export function showcaseLanguageErrorCodes(code: string | null | undefined): string[] {
  const prefix = 'SHOWCASE_LANGUAGE_BLOCKED_';
  if (!code?.startsWith(prefix)) return [];
  const parts = code.slice(prefix.length).split('_');
  if (parts.some(part => !SHOWCASE_VIOLATION_CATEGORIES.some(category => category.toUpperCase() === part))) return [];
  return [...new Set(parts)].map(part => prefix + part);
}
