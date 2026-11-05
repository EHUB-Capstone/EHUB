import { isSupportedShowcaseVideo } from './showcaseVideo.ts';
import { PROJECT_PROFILE_LABELS } from '../constants/projectProfile.ts';
import type {
  SaveShowcaseRequest,
  ShowcaseDto,
  ShowcaseLinkType,
  ShowcaseStage,
  ShowcaseProfileDto,
  ShowcaseStatus,
} from '../types/showcase.ts';

/** Server limits (SaveShowcaseRequestValidator, ShowcaseManageHandler). The server stays the authority. */
export const SHOWCASE_LIMITS = {
  highlights: 2000,
  linkLabel: 100,
  linkUrl: 1000,
  links: 8,
  images: 8,
  imageBytes: 5 * 1024 * 1024,
  note: 1000,
} as const;

export const SHOWCASE_ERROR_CODES = {
  concurrency: 'SHOWCASE_CONCURRENCY_CONFLICT',
  alreadyExists: 'SHOWCASE_ALREADY_EXISTS',
  invalidState: 'SHOWCASE_INVALID_STATE',
  notFound: 'SHOWCASE_NOT_FOUND',
} as const;

export type ShowcaseTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral' | 'brand';

export const SHOWCASE_STATUS_META: Record<ShowcaseStatus, { label: string; tone: ShowcaseTone; description: string }> = {
  Draft: { label: 'Draft', tone: 'neutral', description: 'Only your team can see this page.' },
  PendingReview: { label: 'Pending review', tone: 'info', description: 'Waiting for a lecturer or administrator to review it.' },
  ChangesRequested: { label: 'Changes requested', tone: 'warning', description: 'A reviewer asked for changes before it can be published.' },
  Published: { label: 'Published', tone: 'success', description: 'Visible to signed-in E-HUB users.' },
  Hidden: { label: 'Hidden', tone: 'neutral', description: 'Not visible to the community.' },
};

export const SHOWCASE_STAGES: ReadonlyArray<{ value: ShowcaseStage; label: string; hint: string }> = [
  { value: 'Idea', label: 'Idea', hint: 'A concept being validated' },
  { value: 'Mvp', label: 'MVP', hint: 'A first working version' },
  { value: 'Beta', label: 'Beta', hint: 'Early users are testing it' },
  { value: 'Launched', label: 'Launched', hint: 'Available to its customers' },
];

export const SHOWCASE_LINK_TYPES: ReadonlyArray<{ value: ShowcaseLinkType; label: string }> = [
  { value: 'Website', label: 'Website' },
  { value: 'Demo', label: 'Demo' },
  { value: 'Video', label: 'Video' },
  { value: 'Social', label: 'Social' },
  { value: 'Other', label: 'Other' },
];

export const stageLabel = (stage: ShowcaseStage): string =>
  SHOWCASE_STAGES.find((item) => item.value === stage)?.label ?? stage;

export interface ShowcaseLinkForm {
  /** Local key so rows keep their identity while being edited or removed. */
  key: string;
  linkType: ShowcaseLinkType;
  label: string;
  url: string;
}

/** Showcase-only content. Project name, Description, Problem, Solution and Target users come from the Project Profile. */
export interface ShowcaseFormValues {
  highlights: string;
  developmentStage: ShowcaseStage;
  showTeamMembers: boolean;
  links: ShowcaseLinkForm[];
}

let linkKeyCounter = 0;
export const newLinkKey = (): string => `link-${Date.now().toString(36)}-${linkKeyCounter++}`;

export const emptyLink = (): ShowcaseLinkForm => ({ key: newLinkKey(), linkType: 'Website', label: '', url: '' });

export const emptyForm = (): ShowcaseFormValues => ({
  highlights: '',
  developmentStage: 'Idea',
  showTeamMembers: true,
  links: [],
});

export const formFromShowcase = (showcase: ShowcaseDto): ShowcaseFormValues => ({
  highlights: showcase.highlights ?? '',
  developmentStage: showcase.developmentStage,
  showTeamMembers: showcase.showTeamMembers,
  links: showcase.links.map((link) => ({ key: link.id, linkType: link.linkType, label: link.label, url: link.url })),
});

const blankToNull = (value: string): string | null => (value.trim() === '' ? null : value.trim());

export const toSaveRequest = (
  values: ShowcaseFormValues,
  options: { projectLineageId?: string; rowVersion?: number } = {},
): SaveShowcaseRequest => ({
  ...(options.projectLineageId ? { projectLineageId: options.projectLineageId } : {}),
  ...(options.rowVersion !== undefined ? { rowVersion: options.rowVersion } : {}),
  highlights: blankToNull(values.highlights),
  developmentStage: values.developmentStage,
  showTeamMembers: values.showTeamMembers,
  links: values.links.map((link) => ({ linkType: link.linkType, label: link.label.trim(), url: link.url.trim() })),
});

/** Plain http(s) URL without embedded credentials, matching SaveShowcaseRequestValidator.IsPublicWebUrl. */
export const isPublicWebUrl = (value: string): boolean => {
  try {
    const url = new URL(value.trim());
    return (url.protocol === 'https:' || url.protocol === 'http:') && url.username === '' && url.password === '' && url.hostname !== '';
  } catch {
    return false;
  }
};

export type ShowcaseFormErrors = Partial<Record<'highlights' | 'links', string>> & {
  linkRows: Record<string, { label?: string; url?: string }>;
};

export const validateShowcaseForm = (values: ShowcaseFormValues): ShowcaseFormErrors => {
  const errors: ShowcaseFormErrors = { linkRows: {} };
  if (values.highlights.trim().length > SHOWCASE_LIMITS.highlights) errors.highlights = `Keep this within ${SHOWCASE_LIMITS.highlights} characters.`;

  if (values.links.length > SHOWCASE_LIMITS.links) errors.links = `Add at most ${SHOWCASE_LIMITS.links} links.`;
  for (const link of values.links) {
    const row: { label?: string; url?: string } = {};
    if (link.label.trim() === '') row.label = 'Enter a label.';
    else if (link.label.trim().length > SHOWCASE_LIMITS.linkLabel) row.label = `Keep the label within ${SHOWCASE_LIMITS.linkLabel} characters.`;
    if (link.url.trim().length > SHOWCASE_LIMITS.linkUrl) row.url = `Keep the link within ${SHOWCASE_LIMITS.linkUrl} characters.`;
    else if (!isPublicWebUrl(link.url)) row.url = 'Enter a full link starting with https:// or http://.';
    else if (link.linkType === 'Video' && !isSupportedShowcaseVideo(link.url)) row.url = 'Use an https link from YouTube or Vimeo for a video.';
    if (row.label || row.url) errors.linkRows[link.key] = row;
  }
  return errors;
};

export const hasFormErrors = (errors: ShowcaseFormErrors): boolean =>
  Object.entries(errors).some(([key, value]) => (key === 'linkRows' ? Object.keys(value as object).length > 0 : Boolean(value)));

const sameLinks = (a: ShowcaseLinkForm[], b: ShowcaseLinkForm[]): boolean =>
  a.length === b.length && a.every((link, index) =>
    link.linkType === b[index].linkType && link.label === b[index].label && link.url === b[index].url);

export const isFormDirty = (current: ShowcaseFormValues, saved: ShowcaseFormValues): boolean =>
  current.highlights !== saved.highlights || current.developmentStage !== saved.developmentStage
  || current.showTeamMembers !== saved.showTeamMembers || !sameLinks(current.links, saved.links);

/**
 * Project Profile fields a page needs before it can be sent for review (mirrors ShowcaseModerationHandler.SubmitAsync).
 * Returns their display names, which are the labels of the Project Profile page.
 */
export const missingForReview = (profile: Pick<ShowcaseProfileDto, 'description' | 'problem' | 'solution'>): string[] => {
  const missing: string[] = [];
  if (!profile.description?.trim()) missing.push(PROJECT_PROFILE_LABELS.description);
  if (!profile.problem?.trim()) missing.push(PROJECT_PROFILE_LABELS.problem);
  if (!profile.solution?.trim()) missing.push(PROJECT_PROFILE_LABELS.solution);
  return missing;
};

/** Route of the Project Profile page, where the shared fields are edited. */
export const projectProfilePath = (teamId: string): string => `/student/workspace/project-profile/${teamId}`;

const SUBMITTABLE: ReadonlySet<ShowcaseStatus> = new Set(['Draft', 'ChangesRequested', 'Hidden']);
const WITHDRAWABLE: ReadonlySet<ShowcaseStatus> = new Set(['Published', 'PendingReview']);

export const canSubmitForReview = (status: ShowcaseStatus): boolean => SUBMITTABLE.has(status);
export const canWithdraw = (status: ShowcaseStatus): boolean => WITHDRAWABLE.has(status);

/** Moderation actions available for a page in the given status (mirrors ShowcaseModerationHandler rules). */
export const moderationActionsFor = (status: ShowcaseStatus, wasEverPublished: boolean): Array<'approve' | 'request-changes' | 'reject' | 'hide' | 'unhide'> => {
  switch (status) {
    case 'PendingReview': return ['approve', 'request-changes', 'reject'];
    case 'Published': return ['hide'];
    case 'Hidden': return wasEverPublished ? ['unhide'] : [];
    default: return [];
  }
};

export const IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.webp'] as const;

/** Returns an error message, or null when the file may be uploaded. */
export const validateImageFile = (file: { name: string; size: number }): string | null => {
  const lower = file.name.toLowerCase();
  if (!IMAGE_EXTENSIONS.some((extension) => lower.endsWith(extension))) return 'Only PNG, JPG and WEBP images are accepted.';
  if (file.size <= 0) return 'This image is empty.';
  if (file.size > SHOWCASE_LIMITS.imageBytes) return 'Images must be 5 MB or smaller.';
  return null;
};
