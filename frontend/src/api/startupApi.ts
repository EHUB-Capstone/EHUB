// @ts-nocheck
import axiosClient from './axiosClient';

export const startupApi = {
  getAll: (params) => axiosClient.get('/startup-ideas', { params }),
  getById: (id) => axiosClient.get(`/startup-ideas/${id}`),
  create: (data) => axiosClient.post('/startup-ideas', data),
  update: (id, data) => axiosClient.put(`/startup-ideas/${id}`, data),
  delete: (id) => axiosClient.delete(`/startup-ideas/${id}`),
};
