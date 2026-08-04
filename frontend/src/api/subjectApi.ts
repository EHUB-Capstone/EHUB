// @ts-nocheck
// src/api/subjectApi.js
import axiosClient from './axiosClient';

export const subjectApi = {
  getAll: (params) => axiosClient.get('/subjects', { params }),
  getActive: () => axiosClient.get('/subjects/active'),
  create: (data) => axiosClient.post('/subjects', data),
  update: (id, data) => axiosClient.put(`/subjects/${id}`, data),
  delete: (id) => axiosClient.delete(`/subjects/${id}`),
  getCurrentSemester: () => axiosClient.get('/subjects/current-semester'),
  updateCurrentSemester: (semester, year) => axiosClient.post('/subjects/current-semester', { semester, year }),
  getTeachingStaff: (params) => axiosClient.get('/subjects/teaching-staff', { params }),
  getCurriculum: (subjectCode) => axiosClient.get(`/subjects/${subjectCode}/curriculum`),
  synchronizeCheckpoints: (subjectCode, data) => axiosClient.put(`/subjects/${subjectCode}/checkpoints`, data),
  createRoadmapItem: (subjectCode, data) => axiosClient.post(`/subjects/${subjectCode}/roadmap`, data),
  updateRoadmapItem: (subjectCode, id, data) => axiosClient.put(`/subjects/${subjectCode}/roadmap/${id}`, data),
  deleteRoadmapItem: (subjectCode, id) => axiosClient.delete(`/subjects/${subjectCode}/roadmap/${id}`),
  createRubric: (subjectCode, data) => axiosClient.post(`/subjects/${subjectCode}/rubrics`, data),
  updateRubric: (subjectCode, id, data) => axiosClient.put(`/subjects/${subjectCode}/rubrics/${id}`, data),
  deleteRubric: (subjectCode, id) => axiosClient.delete(`/subjects/${subjectCode}/rubrics/${id}`),
  createCriterion: (subjectCode, rubricId, data) => axiosClient.post(`/subjects/${subjectCode}/rubrics/${rubricId}/criteria`, data),
  updateCriterion: (subjectCode, rubricId, id, data) => axiosClient.put(`/subjects/${subjectCode}/rubrics/${rubricId}/criteria/${id}`, data),
  deleteCriterion: (subjectCode, rubricId, id) => axiosClient.delete(`/subjects/${subjectCode}/rubrics/${rubricId}/criteria/${id}`),
};
