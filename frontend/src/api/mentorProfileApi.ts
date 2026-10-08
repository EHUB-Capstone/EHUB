import axiosClient from './axiosClient';
import type { MentorProfile, UpdateMentorProfilePayload } from '../types/mentorProfile';

interface ApiEnvelope<T> {
  success: boolean;
  data: T;
  message: string;
}

export const mentorProfileApi = {
  get: (mentorProfileId: string, signal?: AbortSignal): Promise<ApiEnvelope<MentorProfile>> =>
    axiosClient.get(`/admin/mentor-profiles/${mentorProfileId}`, { signal }),

  update: (mentorProfileId: string, payload: UpdateMentorProfilePayload): Promise<ApiEnvelope<MentorProfile>> =>
    axiosClient.put(`/admin/mentor-profiles/${mentorProfileId}`, payload),
};
