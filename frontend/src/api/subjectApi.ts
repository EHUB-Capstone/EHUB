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
};
