// @ts-nocheck
import axiosClient from './axiosClient';

export const aiApi = {
  analyzeStartup: (data) => axiosClient.post('/ai/analyze', data),
  suggestStartup: (data) => axiosClient.post('/ai/suggest', data),
  detectSimilarIdea: (data) => axiosClient.post('/ai/similar', data),
  analyzeSentiment: (data) => axiosClient.post('/ai/sentiment', data),
  generateRubric: (data) => axiosClient.post('/ai/rubric', data),
  // Legacy endpoints
  analyze: (startupIdeaId) => axiosClient.post(`/ai/analyze-startup/${startupIdeaId}`),
  getAnalysis: (startupIdeaId) => axiosClient.get(`/ai/startup/${startupIdeaId}`),
};
