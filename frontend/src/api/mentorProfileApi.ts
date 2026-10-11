import axiosClient from './axiosClient';
import type { MentorProfile, MentorTagSuggestions, UpdateMentorProfilePayload } from '../types/mentorProfile';

interface ApiEnvelope<T> {
  success: boolean;
  data: T;
  message: string;
}

export const mentorProfileApi = {
  get: (mentorProfileId: string, signal?: AbortSignal): Promise<ApiEnvelope<MentorProfile>> =>
    axiosClient.get(`/admin/mentor-profiles/${mentorProfileId}`, { signal }),

  getTagSuggestions: (): Promise<ApiEnvelope<MentorTagSuggestions>> =>
    axiosClient.get('/admin/mentor-profiles/tag-suggestions'),

  update: (mentorProfileId: string, payload: UpdateMentorProfilePayload): Promise<ApiEnvelope<MentorProfile>> =>
    axiosClient.put(`/admin/mentor-profiles/${mentorProfileId}`, payload),
};
