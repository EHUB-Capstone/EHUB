// Mirrors EHub.Contracts/Showcase/ShowcaseContracts.cs. Keep both in sync.

export type ShowcaseStatus = 'Draft' | 'PendingReview' | 'ChangesRequested' | 'Published' | 'Hidden';
export type ShowcaseStage = 'Idea' | 'Mvp' | 'Beta' | 'Launched';
export type ShowcaseLinkType = 'Website' | 'Demo' | 'Video' | 'Social' | 'Other';
export type ShowcaseDecision = 'approve' | 'request-changes' | 'reject' | 'hide' | 'unhide';

export interface ShowcaseFeaturedDto {
  id: string;
  isFeatured: boolean;
  featuredAt: string | null;
  rowVersion: number;
}

export interface GetShowcasesRequest {
  search?: string;
  stage?: ShowcaseStage;
  industry?: string;
  featured?: boolean;
  semesterId?: string;
  sort?: 'newest' | 'popular';
  page?: number;
  pageSize?: number;
}

export interface ShowcaseCardDto {
  id: string;
  projectName: string;
  description: string;
  logoUrl: string | null;
  developmentStage: ShowcaseStage;
  publishedAt: string | null;
  isFeatured: boolean;
  featuredAt: string | null;
  teamName: string;
  industries: string[];
  likeCount: number;
  commentCount: number;
  /** Canonical YouTube or Vimeo watch URL of the page's first Video link, when it has a supported one. */
  videoUrl?: string | null;
  /** The page's first uploaded image, used as the preview of a feed post. */
  coverUrl?: string | null;
}

export interface ShowcaseDiscoveryPageDto {
  items: ShowcaseCardDto[];
  pageIndex: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface ShowcaseFilterOptionsDto {
  industries: string[];
  semesters: { id: string; code: string; name: string }[];
}

export interface ShowcaseLinkDto {
  id: string;
  linkType: ShowcaseLinkType;
  label: string;
  url: string;
}

export interface ShowcaseMediaDto {
  id: string;
  mediaType: 'Image' | 'Video';
  url: string;
  caption: string | null;
  sortOrder: number;
}

/**
 * Project Profile fields shown on a showcase, read live from the project. The names and limits are the ones of the
 * Project Profile page (Project name, Description, Problem, Solution, Target users); edit them there.
 */
export interface ShowcaseProfileDto {
  projectName: string;
  description: string;
  problem: string | null;
  solution: string | null;
  targetUsers: string | null;
}

export interface ShowcaseDto {
  id: string;
  projectLineageId: string;
  profile: ShowcaseProfileDto;
  logoUrl: string | null;
  highlights: string | null;
  developmentStage: ShowcaseStage;
  status: ShowcaseStatus;
  showTeamMembers: boolean;
  reviewNote: string | null;
  publishedAt: string | null;
  isFeatured: boolean;
  featuredAt: string | null;
  viewCount: number;
  likeCount: number;
  commentCount: number;
  rowVersion: number;
  createdAt: string;
  updatedAt: string | null;
  canEdit: boolean;
  links: ShowcaseLinkDto[];
  media: ShowcaseMediaDto[];
}

export interface MyShowcaseEntryDto {
  projectLineageId: string;
  projectId: string;
  /** The Project Profile page is addressed by team id. */
  teamId: string;
  teamName: string;
  profile: ShowcaseProfileDto;
  showcase: ShowcaseDto | null;
}

export interface ShowcaseTeamMemberDto {
  fullName: string;
  avatarUrl: string | null;
  role: string;
}

export interface ShowcaseHistoryDto {
  fromStatus: ShowcaseStatus;
  toStatus: ShowcaseStatus;
  note: string | null;
  actorName: string;
  createdAt: string;
}

export interface ShowcaseDetailDto {
  showcase: ShowcaseDto;
  teamName: string;
  members: ShowcaseTeamMemberDto[];
  history: ShowcaseHistoryDto[];
  isOwner: boolean;
  canModerate: boolean;
}

export interface ShowcaseModerationItemDto {
  id: string;
  projectName: string;
  description: string;
  logoUrl: string | null;
  status: ShowcaseStatus;
  developmentStage: ShowcaseStage;
  teamName: string;
  classCode: string;
  reviewNote: string | null;
  publishedAt: string | null;
  isFeatured: boolean;
  featuredAt: string | null;
  submittedOrUpdatedAt: string;
}

export interface ShowcaseModerationPageDto {
  items: ShowcaseModerationItemDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ShowcaseLinkRequest {
  linkType: ShowcaseLinkType;
  label: string;
  url: string;
}

export interface SaveShowcaseRequest {
  projectLineageId?: string;
  highlights: string | null;
  developmentStage: ShowcaseStage;
  showTeamMembers: boolean;
  links: ShowcaseLinkRequest[];
  rowVersion?: number;
}
