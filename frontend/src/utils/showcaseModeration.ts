import type { ShowcaseModerationRuleDto, ShowcaseModerationRuleStatus, ShowcaseReportReason, ShowcaseViolationCategory, ShowcaseViolationCategoryDto } from '../types/showcase.ts';

export const SHOWCASE_VIOLATION_CATEGORIES: readonly ShowcaseViolationCategory[] = [
  'Profanity', 'Insult', 'Threat', 'Defamation', 'Racism', 'AdultContent', 'Spam',
  'ProhibitedContent', 'Misinformation', 'OffTopic',
];

const PHRASE_BLOCKING_CATEGORIES: readonly ShowcaseViolationCategory[] = [
  'Profanity', 'Insult', 'Threat', 'Defamation', 'Racism', 'AdultContent', 'Spam',
];

export const showcaseViolationManagementRoles = ['ADMIN'] as const;

export function canManageShowcaseViolations(role: string | null | undefined): boolean {
  return role?.trim().toUpperCase() === 'ADMIN';
}

export function supportsShowcasePhraseBlocking(category: ShowcaseViolationCategoryDto): boolean {
  return PHRASE_BLOCKING_CATEGORIES.includes(category.code) && category.supportsPhraseBlocking !== false;
}

export function showcaseReportReasonFromCategory(category: ShowcaseViolationCategory): ShowcaseReportReason {
  if (category === 'Misinformation' || category === 'OffTopic') return 'Other';
  return category === 'ProhibitedContent' ? 'InappropriateContent' : showcaseReasonFromCategory(category);
}

export function showcaseRuleStatus(rule: { status?: string; isEnabled?: boolean }): ShowcaseModerationRuleStatus {
  if (rule.status === 'Active' || rule.status === 'Inactive') return rule.status;
  if (rule.status !== undefined) throw new Error('Invalid moderation rule status');
  return rule.isEnabled === false ? 'Inactive' : 'Active';
}

export function normalizeShowcaseModerationRule(rule: Omit<ShowcaseModerationRuleDto, 'status'> & { status?: string }): ShowcaseModerationRuleDto {
  const status = showcaseRuleStatus(rule);
  return { ...rule, status, isEnabled: status === 'Active' };
}

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
  if (parts.some(part => !PHRASE_BLOCKING_CATEGORIES.some(category => category.toUpperCase() === part))) return [];
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

/** A page snapshot is server-captured JSON; comments keep their original literal text. */
export function showcaseReportSnapshotFields(snapshot: string | null | undefined, isComment: boolean): Array<{ key: string; value: string }> {
  if (!snapshot) return [];
  if (isComment) return [{ key: 'Comment', value: snapshot }];
  try {
    const page: unknown = JSON.parse(snapshot);
    if (page && typeof page === 'object' && !Array.isArray(page)) {
      const data = page as Record<string, unknown>;
      const fields = ['ProjectName', 'Description', 'Problem', 'Solution', 'TargetUsers', 'Highlights'].flatMap(key =>
        typeof data[key] === 'string' && data[key] ? [{ key, value: data[key] as string }] : []);
      if (Array.isArray(data.Links)) {
        const links = data.Links.flatMap((item: unknown) => {
          if (!item || typeof item !== 'object') return [];
          const link = item as Record<string, unknown>;
          return typeof link.Url === 'string' ? [(typeof link.Label === 'string' ? link.Label + ': ' : '') + link.Url] : [];
        });
        if (links.length) fields.push({ key: 'Links', value: links.join('\n') });
      }
      if (fields.length) return fields;
    }
  } catch { /* Older snapshots may be plain text. Display them as text, never HTML. */ }
  return [{ key: 'Content', value: snapshot }];
}
