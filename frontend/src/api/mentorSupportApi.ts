import axiosClient from './axiosClient';
import type { MentorProfile, MentorRecommendation, MentoringActionItem, MentoringFeedback, MentoringSession, SaveMentoringSession } from '../types/mentoring';

interface Envelope<T> { data: T }
const data = <T>(value: Envelope<T>): T => value.data;

export const mentorSupportApi = {
  getProfile: async (): Promise<MentorProfile> => data(await axiosClient.get('/mentoring/profile')),
  updateProfile: async (profile: Pick<MentorProfile, 'mentorType' | 'expertise' | 'bio' | 'experience' | 'organization' | 'linkedInUrl' | 'portfolioUrl'>): Promise<MentorProfile> =>
    data(await axiosClient.put('/mentoring/profile', profile)),
  uploadDocument: async (kind: 'cv' | 'portfolio', file: File): Promise<MentorProfile> => {
    const form = new FormData();
    form.append('file', file);
    return data(await axiosClient.post(`/mentoring/profile/documents/${kind}`, form, { headers: { 'Content-Type': 'multipart/form-data' } }));
  },
  getDocument: async (mentorId: string, kind: 'cv' | 'portfolio'): Promise<Blob> =>
    axiosClient.get(`/mentoring/profiles/${mentorId}/documents/${kind}`, { responseType: 'blob' }),
  getDirectory: async (): Promise<MentorProfile[]> => data(await axiosClient.get('/mentoring/directory')),
  getRecommendations: async (teamId: string, signal?: AbortSignal): Promise<MentorRecommendation[]> =>
    data(await axiosClient.get(`/mentoring/teams/${teamId}/recommendations`, { timeout: 55_000, signal })),
  getSessions: async (teamId?: string): Promise<MentoringSession[]> =>
    data(await axiosClient.get('/mentoring/sessions', { params: teamId ? { teamId } : {} })),
  createSession: async (session: SaveMentoringSession): Promise<MentoringSession> =>
    data(await axiosClient.post('/mentoring/sessions', session)),
  updateSession: async (id: string, session: SaveMentoringSession): Promise<MentoringSession> =>
    data(await axiosClient.put(`/mentoring/sessions/${id}`, session)),
  completeSession: async (id: string, notes: string): Promise<MentoringSession> =>
    data(await axiosClient.post(`/mentoring/sessions/${id}/complete`, { notes })),
  cancelSession: async (id: string): Promise<MentoringSession> =>
    data(await axiosClient.post(`/mentoring/sessions/${id}/cancel`)),
  addActionItem: async (id: string, content: string): Promise<MentoringActionItem> =>
    data(await axiosClient.post(`/mentoring/sessions/${id}/action-items`, { content })),
  saveFeedback: async (id: string, rating: number, comment: string): Promise<MentoringFeedback> =>
    data(await axiosClient.put(`/mentoring/sessions/${id}/feedback`, { rating, comment })),
  getFeedback: async (id: string): Promise<MentoringFeedback[]> =>
    data(await axiosClient.get(`/mentoring/sessions/${id}/feedback`)),
};
