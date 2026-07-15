// @ts-nocheck
import axiosClient from './axiosClient';

export const mentoringApi = {
  createSession: (sessionData) => axiosClient.post('/mentoring-sessions', sessionData),
  getAllSessions: () => axiosClient.get('/mentoring-sessions'),
  getMyLecturerSessions: () => axiosClient.get('/mentoring-sessions/lecturer'),
  getSessionsByTeam: (teamId) => axiosClient.get(`/mentoring-sessions/team/${teamId}`),
  getPastSessions: () => axiosClient.get('/mentoring-sessions/past'),
  updateSession: (sessionId, sessionData) => axiosClient.put(`/mentoring-sessions/${sessionId}`, sessionData),
  cancelSession: (sessionId) => axiosClient.patch(`/mentoring-sessions/${sessionId}/cancel`, {}),
  deleteSession: (sessionId) => axiosClient.delete(`/mentoring-sessions/${sessionId}`),
  addSessionNote: (sessionId, notes) => axiosClient.post(`/mentoring-sessions/${sessionId}/notes`, { notes }),
  addActionItem: (sessionId, actionItemData) => axiosClient.post(`/mentoring-sessions/${sessionId}/action-items`, actionItemData),
  updateActionItem: (sessionId, itemId, actionItemData) => axiosClient.patch(`/mentoring-sessions/${sessionId}/action-items/${itemId}`, actionItemData)
};

export const {
  createSession,
  getAllSessions,
  getMyLecturerSessions,
  getSessionsByTeam,
  getPastSessions,
  updateSession,
  cancelSession,
  deleteSession,
  addSessionNote,
  addActionItem,
  updateActionItem
} = mentoringApi;
