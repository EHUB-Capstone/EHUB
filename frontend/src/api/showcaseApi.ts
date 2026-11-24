import axiosClient from './axiosClient.ts';
import { ASSISTANT_KEY_HEADER } from '../utils/showcaseAssistant.ts';
import type { ApiEnvelope } from '../types/workspaceTools.ts';
import type {
  GetShowcasesRequest,
  ShowcaseDiscoveryPageDto,
  ShowcaseFilterOptionsDto,
  ShowcaseFeaturedDto,
  BulkFeatureShowcasesResultDto,
  FeaturedCandidateDto,
  MyShowcaseEntryDto,
  SaveShowcaseRequest,
  ShowcaseDecision,
  ShowcaseDetailDto,
  ShowcaseDto,
  ShowcaseMediaDto,
  ShowcaseModerationPageDto,
  ShowcaseStatus,
  ShowcaseUpvoteDto,
  ShowcaseViewDto,
  ShowcaseInsightsDto,
  ShowcaseCommentDto,
  ShowcaseCommentPageDto,
  CreateShowcaseCommentRequest,
  CreateShowcaseReportRequest,
  ResolveShowcaseReportRequest,
  ShowcaseReportPageDto,
  ShowcaseReportReceiptDto,
  ShowcaseReportDto,
  ShowcaseReportStatus,
  ShowcaseModerationRuleDto,
  SaveShowcaseModerationRuleRequest,
  ShowcaseAssistantChatRequest,
  ShowcaseAssistantKeyStatusDto,
  ShowcaseAssistantReplyDto,
  SaveShowcaseAssistantKeyRequest,
} from '../types/showcase.ts';

// axiosClient resolves to the response body, so each call yields the ApiEnvelope.
const unwrap = async <T>(request: Promise<unknown>): Promise<T> => (await request as ApiEnvelope<T>).data;

const imageForm = (file: File): FormData => {
  const form = new FormData();
  form.append('file', file);
  return form;
};

const multipart = { headers: { 'Content-Type': 'multipart/form-data' } };
// The backend can try three models for 25 seconds each; leave time for catalog queries and transport.
const assistantRequestTimeout = 90_000;

export const showcaseApi = {
  getModerationRules: (signal?: AbortSignal) =>
    unwrap<ShowcaseModerationRuleDto[]>(axiosClient.get('/showcase/moderation-rules', { signal })),
  saveModerationRule: (id: string | null, body: SaveShowcaseModerationRuleRequest) =>
    unwrap<ShowcaseModerationRuleDto>(id
      ? axiosClient.put(`/showcase/moderation-rules/${id}`, body)
      : axiosClient.post('/showcase/moderation-rules', body)),
  // A key given here is the user's own Gemini key. It travels in a header for this one request, never in the URL or the body, and is
  // not kept. Without one (null) the server uses the key this user saved to their account, and answers that a key is needed if none.
  askAssistant: (body: ShowcaseAssistantChatRequest, apiKey: string | null, signal?: AbortSignal) =>
    unwrap<ShowcaseAssistantReplyDto>(axiosClient.post('/showcase/assistant/chat', body, {
      signal, timeout: assistantRequestTimeout, ...(apiKey ? { headers: { [ASSISTANT_KEY_HEADER]: apiKey } } : {}),
    })),
  // The key a user chose to keep on the server, encrypted there. It is only ever sent in the body of a save; no call returns it,
  // only whether one is saved and its last four characters.
  getAssistantKey: (signal?: AbortSignal) =>
    unwrap<ShowcaseAssistantKeyStatusDto>(axiosClient.get('/showcase/assistant/key', { signal })),
  saveAssistantKey: (apiKey: string) =>
    unwrap<ShowcaseAssistantKeyStatusDto>(axiosClient.put('/showcase/assistant/key', { apiKey } satisfies SaveShowcaseAssistantKeyRequest)),
  deleteAssistantKey: () =>
    unwrap<ShowcaseAssistantKeyStatusDto>(axiosClient.delete('/showcase/assistant/key')),
  recordView: (id: string, signal?: AbortSignal) =>
    unwrap<ShowcaseViewDto>(axiosClient.post(`/showcase/${id}/view`, undefined, { signal })),
  getInsights: (id: string, signal?: AbortSignal) =>
    unwrap<ShowcaseInsightsDto>(axiosClient.get(`/showcase/${id}/insights`, { signal })),
  getComments: (id: string, page: number, pageSize: number, signal?: AbortSignal) =>
    unwrap<ShowcaseCommentPageDto>(axiosClient.get(`/showcase/${id}/comments`, { params: { page, pageSize }, signal })),
  getCommentReplies: (id: string, commentId: string, page: number, pageSize: number, signal?: AbortSignal) =>
    unwrap<ShowcaseCommentPageDto>(axiosClient.get(`/showcase/${id}/comments/${commentId}/replies`, { params: { page, pageSize }, signal })),
  createComment: (id: string, body: CreateShowcaseCommentRequest) =>
    unwrap<ShowcaseCommentDto>(axiosClient.post(`/showcase/${id}/comments`, body)),
  deleteComment: (id: string, commentId: string) =>
    unwrap<null>(axiosClient.delete(`/showcase/${id}/comments/${commentId}`)),
  createReport: (id: string, body: CreateShowcaseReportRequest) =>
    unwrap<ShowcaseReportReceiptDto>(axiosClient.post(`/showcase/${id}/reports`, body)),
  getReports: (params: { status?: ShowcaseReportStatus; page?: number; pageSize?: number }, signal?: AbortSignal) =>
    unwrap<ShowcaseReportPageDto>(axiosClient.get('/showcase/reports', { params, signal })),
  resolveReport: (reportId: string, body: ResolveShowcaseReportRequest) =>
    unwrap<ShowcaseReportDto>(axiosClient.post(`/showcase/reports/${reportId}/resolve`, body)),
  deleteReportHistory: (reportId: string) =>
    unwrap<null>(axiosClient.delete(`/showcase/reports/${reportId}`)),
  deleteAllReportHistory: (status: Extract<ShowcaseReportStatus, 'Dismissed' | 'ActionTaken'>) =>
    unwrap<null>(axiosClient.delete('/showcase/reports/history', { params: { status } })),
  setUpvote: (id: string, hasUpvoted: boolean) =>
    unwrap<ShowcaseUpvoteDto>(hasUpvoted
      ? axiosClient.post(`/showcase/${id}/upvote`)
      : axiosClient.delete(`/showcase/${id}/upvote`)),
  feature: (id: string) =>
    unwrap<ShowcaseFeaturedDto>(axiosClient.post(`/showcase/${id}/feature`)),
  unfeature: (id: string) =>
    unwrap<ShowcaseFeaturedDto>(axiosClient.delete(`/showcase/${id}/feature`)),
  featureMany: (ids: string[], featured: boolean) =>
    unwrap<BulkFeatureShowcasesResultDto>(axiosClient.post('/showcase/feature/bulk', { ids, featured })),
  getFeatureCandidates: (signal?: AbortSignal) =>
    unwrap<FeaturedCandidateDto[]>(axiosClient.get('/showcase/feature/candidates', { signal })),
  getFilterOptions: (signal?: AbortSignal) =>
    unwrap<ShowcaseFilterOptionsDto>(axiosClient.get('/showcase/filter-options', { signal })),
  list: (params: GetShowcasesRequest = {}, signal?: AbortSignal) =>
    unwrap<ShowcaseDiscoveryPageDto>(axiosClient.get('/showcase', { params, signal })),
  getMine: (signal?: AbortSignal) =>
    unwrap<MyShowcaseEntryDto[]>(axiosClient.get('/showcase/mine', { signal })),
  get: (id: string, signal?: AbortSignal) =>
    unwrap<ShowcaseDetailDto>(axiosClient.get(`/showcase/${id}`, { signal })),
  create: (data: SaveShowcaseRequest) =>
    unwrap<ShowcaseDto>(axiosClient.post('/showcase', data)),
  update: (id: string, data: SaveShowcaseRequest) =>
    unwrap<ShowcaseDto>(axiosClient.put(`/showcase/${id}`, data)),
  uploadLogo: (id: string, file: File) =>
    unwrap<ShowcaseDto>(axiosClient.post(`/showcase/${id}/logo`, imageForm(file), multipart)),
  uploadMedia: (id: string, file: File) =>
    unwrap<ShowcaseMediaDto>(axiosClient.post(`/showcase/${id}/media`, imageForm(file), multipart)),
  deleteMedia: (id: string, mediaId: string) =>
    unwrap<null>(axiosClient.delete(`/showcase/${id}/media/${mediaId}`)),
  submit: (id: string, acknowledgedPublishWarning: boolean) =>
    unwrap<ShowcaseDto>(axiosClient.post(`/showcase/${id}/submit`, { acknowledgedPublishWarning })),
  withdraw: (id: string) =>
    unwrap<ShowcaseDto>(axiosClient.post(`/showcase/${id}/withdraw`)),
  getModerationQueue: (params: { status?: ShowcaseStatus; page?: number; pageSize?: number }, signal?: AbortSignal) =>
    unwrap<ShowcaseModerationPageDto>(axiosClient.get('/showcase/moderation', { params, signal })),
  decide: (id: string, decision: ShowcaseDecision, note?: string) =>
    unwrap<ShowcaseDto>(axiosClient.post(`/showcase/${id}/${decision}`, { note: note?.trim() || null })),
};
