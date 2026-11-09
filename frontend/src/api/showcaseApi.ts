import axiosClient from './axiosClient.ts';
import type { ApiEnvelope } from '../types/workspaceTools.ts';
import type {
  GetShowcasesRequest,
  ShowcaseDiscoveryPageDto,
  ShowcaseFilterOptionsDto,
  ShowcaseFeaturedDto,
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
} from '../types/showcase.ts';

// axiosClient resolves to the response body, so each call yields the ApiEnvelope.
const unwrap = async <T>(request: Promise<unknown>): Promise<T> => (await request as ApiEnvelope<T>).data;

const imageForm = (file: File): FormData => {
  const form = new FormData();
  form.append('file', file);
  return form;
};

const multipart = { headers: { 'Content-Type': 'multipart/form-data' } };

export const showcaseApi = {
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
  setUpvote: (id: string, hasUpvoted: boolean) =>
    unwrap<ShowcaseUpvoteDto>(hasUpvoted
      ? axiosClient.post(`/showcase/${id}/upvote`)
      : axiosClient.delete(`/showcase/${id}/upvote`)),
  feature: (id: string) =>
    unwrap<ShowcaseFeaturedDto>(axiosClient.post(`/showcase/${id}/feature`)),
  unfeature: (id: string) =>
    unwrap<ShowcaseFeaturedDto>(axiosClient.delete(`/showcase/${id}/feature`)),
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
