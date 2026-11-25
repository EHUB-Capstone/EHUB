import type { ShowcaseModerationRuleDto, ShowcaseViolationCategory, ShowcaseViolationCategoryDto } from '../types/showcase.ts';

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

/** Literal selection preserves Vietnamese accents and whole words, matching the proposal API. */
export function canonicalShowcasePhrase(value: string): string {
  return (value.normalize('NFC').toLowerCase().replace(/\p{Cf}/gu, '').match(/[\p{L}\p{M}\p{Nd}]+/gu) ?? []).join(' ');
}
export function isShowcasePhraseInComment(comment: string, phrase: string): boolean {
  const candidate = canonicalShowcasePhrase(phrase);
  return /\p{L}/u.test(candidate)
    && (' ' + canonicalShowcasePhrase(comment) + ' ').includes(' ' + candidate + ' ');
}

export function showcaseCategoryLabel(category: ShowcaseViolationCategoryDto, language: string): string {
  return language.startsWith('vi') ? category.nameVi : category.nameEn;
}
