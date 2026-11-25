// Mirrors EHub.Contracts/Showcase/ShowcaseContracts.cs. Keep both in sync.

export type ShowcaseStatus = 'Draft' | 'PendingReview' | 'Published' | 'Hidden';
export type ShowcaseReportStatus = 'Open' | 'Dismissed' | 'ActionTaken';
export type ShowcaseReportReason = 'Spam' | 'Harassment' | 'InappropriateContent' | 'CopyrightViolation' | 'Other';
export type ShowcaseStage = 'Idea' | 'Mvp' | 'Beta' | 'Launched';
export type ShowcaseLinkType = 'Website' | 'Demo' | 'Video' | 'Social' | 'Other';
export type ShowcaseDecision = 'approve' | 'reject' | 'hide' | 'unhide';
export type ShowcaseLaunchPeriod = 'topThisWeek' | 'thisWeek' | 'lastWeek' | 'earlier' | 'trending';

// Mirrors EHub.Contracts/Showcase/ShowcaseAssistantContracts.cs. Keep both in sync.
export type ShowcaseAssistantRole = 'user' | 'assistant';

export interface ShowcaseAssistantTurn {
  role: ShowcaseAssistantRole;
  content: string;
}

export interface ShowcaseAssistantChatRequest {
  message: string;
  history: ShowcaseAssistantTurn[];
}

export interface ShowcaseAssistantProjectDto {
  id: string;
  projectName: string;
  description: string;
  logoUrl: string | null;
  developmentStage: ShowcaseStage;
  teamName: string;
  industries: string[];
  likeCount: number;
  viewCount: number;
  /** One sentence from the assistant explaining why the project matches the question. */
  reason: string;
}

export interface ShowcaseAssistantReplyDto {
  answer: string;
  projects: ShowcaseAssistantProjectDto[];
  suggestions: string[];
}

// Mirrors EHub.Contracts/Showcase/ShowcaseAssistantKeyContracts.cs. Keep both in sync.
/** What the server says about the Gemini key the signed-in user saved to their account. The key itself is never returned. */
export interface ShowcaseAssistantKeyStatusDto {
  /** False when the server has no encryption key configured, so nothing can be saved there. */
  storageAvailable: boolean;
  hasKey: boolean;
  /** The last four characters, enough to recognise which key it is. */
  last4: string | null;
  updatedAt: string | null;
}

export interface SaveShowcaseAssistantKeyRequest {
  apiKey: string;
}

export interface ShowcaseFeaturedDto {
  id: string;
  isFeatured: boolean;
  featuredAt: string | null;
  rowVersion: number;
}

export interface BulkFeatureShowcasesResultDto {
  updated: number;
  unchanged: number;
  /** Ids that are missing, unpublished or outside the caller's classes. */
  skipped: number;
}

/** A published showcase the caller may feature; the server returns only the caller's own classes (all for an admin). */
export interface FeaturedCandidateDto {
  id: string;
  projectName: string;
  logoUrl: string | null;
  developmentStage: ShowcaseStage;
  teamName: string;
  classCode: string;
  lecturerName: string;
  isFeatured: boolean;
}

export interface ShowcaseUpvoteDto {
  likeCount: number;
  hasUpvoted: boolean;
}

export interface ShowcaseViewDto {
  viewCount: number;
}

export interface ShowcaseInsightsDayDto {
  date: string;
  views: number;
  upvotes: number;
  comments: number;
}

export interface ShowcaseInsightsPeriodDto {
  views: number;
  upvotes: number;
  comments: number;
}

export interface ShowcaseInsightsDto {
  viewCount: number;
  upvoteCount: number;
  commentCount: number;
  /** The last fourteen UTC days, oldest first. */
  daily: ShowcaseInsightsDayDto[];
  /** Totals of the fourteen UTC days before `daily`, to show the trend. */
  previous: ShowcaseInsightsPeriodDto;
}

export interface CreateShowcaseCommentRequest {
  content: string;
  parentCommentId?: string | null;
}

export interface CreateShowcaseReportRequest {
  commentId?: string | null;
  reason: ShowcaseReportReason;
  category?: ShowcaseViolationCategory | null;
  note?: string | null;
}

export interface ResolveShowcaseReportRequest {
  status: Extract<ShowcaseReportStatus, 'Dismissed' | 'ActionTaken'>;
  resolutionNote?: string | null;
}

export interface ShowcaseReportReceiptDto {
  id: string;
  showcaseProjectId: string;
  commentId: string | null;
  status: ShowcaseReportStatus;
  createdAt: string;
}

export interface ShowcaseReportDto extends ShowcaseReportReceiptDto {
  sources: Array<'User' | 'System'>;
  systemNote: string | null;
  userReportCount: number;
  systemReportCount: number;
  showcaseStatus: ShowcaseStatus | null;
  commentContent?: string | null;
  category?: ShowcaseViolationCategory | null;
  reason: ShowcaseReportReason;
  note: string | null;
  resolvedByUserId: string | null;
  resolvedAt: string | null;
  resolutionNote: string | null;
  reportCount: number;
}

export type ShowcaseViolationCategory = 'Profanity' | 'Insult' | 'Threat' | 'Defamation' | 'Racism' | 'AdultContent' | 'Spam';

export interface ShowcaseViolationCategoryDto {
  code: ShowcaseViolationCategory;
  nameVi: string;
  nameEn: string;
  sortOrder: number;
}

export interface ShowcaseModerationRuleDto {
  id: string;
  phrase: string;
  description: string;
  reason: 'Spam' | 'Harassment' | 'InappropriateContent';
  category: ShowcaseViolationCategory;
  isEnabled: boolean;
  rowVersion: number;
}

export type SaveShowcaseModerationRuleRequest = Omit<ShowcaseModerationRuleDto, 'id' | 'rowVersion' | 'category'> & { category?: ShowcaseViolationCategory; rowVersion?: number };

export interface ShowcaseReportPageDto {
  items: ShowcaseReportDto[];
  totalItems: number;
  pageIndex: number;
  pageSize: number;
  totalPages: number;
}

export interface ShowcaseCommentDto {
  id: string;
  parentCommentId: string | null;
  content: string;
  authorName: string;
  authorAvatarUrl: string | null;
  isTeamReply: boolean;
  createdAt: string;
  /** True for the author, administrators and the lecturers of the class. */
  canDelete: boolean;
  /** All replies of this comment; `replies` holds only the oldest few. */
  replyCount: number;
  replies: ShowcaseCommentDto[];
}

export interface ShowcaseCommentPageDto {
  items: ShowcaseCommentDto[];
  pageIndex: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface GetShowcasesRequest {
  search?: string;
  stage?: ShowcaseStage;
  industry?: string;
  featured?: boolean;
  semesterId?: string;
  sort?: 'newest' | 'popular' | 'trending';
  period?: ShowcaseLaunchPeriod;
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
  viewCount: number;
  likeCount: number;
  hasUpvoted: boolean;
  isOwner: boolean;
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
  /** Only semesters with at least one published showcase, with how many they hold. */
  semesters: { id: string; code: string; name: string; projectCount: number }[];
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
  /** Status changes of the page, newest first, with the reviewer's feedback. */
  history: ShowcaseHistoryDto[];
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

export type ShowcaseEvaluatorRole = 'Lecturer' | 'Mentor' | 'Admin';

/** A reviewer's final (Published/Locked) evaluation, shown as reference only. */
export interface ShowcaseReferenceEvaluationDto {
  evaluatorRole: ShowcaseEvaluatorRole;
  status: string;
  totalScore: number;
  maxTotalScore: number;
  strengths: string | null;
  weaknesses: string | null;
  suggestions: string | null;
  publishedAt: string | null;
}

/** Module 1-3 data; the server sends it only to an Admin or an in-scope Lecturer. */
export interface ShowcaseReferenceDataDto {
  classCode: string;
  semester: { id: string; code: string; name: string } | null;
  evaluations: ShowcaseReferenceEvaluationDto[];
}

export interface ShowcaseDetailDto {
  showcase: ShowcaseDto;
  teamName: string;
  members: ShowcaseTeamMemberDto[];
  /** Public startup-industry tags; never accompanied by scores. */
  industries: string[];
  /** Reviewer-only; null for everyone else. */
  referenceData: ShowcaseReferenceDataDto | null;
  history: ShowcaseHistoryDto[];
  isOwner: boolean;
  hasUpvoted: boolean;
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

export type ShowcaseRuleProposalStatus = 'Pending' | 'Approved' | 'Rejected';
export interface CreateShowcaseRuleProposalRequest { phrase: string; category: ShowcaseViolationCategory; }
export interface ReviewShowcaseRuleProposalRequest { status: 'Approved' | 'Rejected'; rowVersion: number; }
export interface ShowcaseRuleProposalDto {
  id: string;
  showcaseProjectId: string;
  sourceReportId: string | null;
  commentId: string | null;
  commentContent: string | null;
  phrase: string;
  category: ShowcaseViolationCategory;
  status: ShowcaseRuleProposalStatus;
  proposedAtUtc: string;
  reviewedAtUtc: string | null;
  createdRuleId: string | null;
  rowVersion: number;
}
export interface ShowcaseRuleProposalPageDto {
  items: ShowcaseRuleProposalDto[];
  totalItems: number;
  pageIndex: number;
  pageSize: number;
  totalPages: number;
}
