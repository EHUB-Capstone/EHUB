// @ts-nocheck
import axiosClient from './axiosClient';

export const userApi = {
  getAll: (params, config = {}) => axiosClient.get('/users', { ...config, params }),
  getById: (id) => axiosClient.get(`/users/${id}`),
  create: (data) => axiosClient.post('/users', data),
  update: (id, data) => axiosClient.put(`/users/${id}`, data),
  delete: (id) => axiosClient.delete(`/users/${id}`),
  approveUser: (userId) => axiosClient.post(`/admin/users/${userId}/approve`),
  rejectUser: (userId) => axiosClient.post(`/admin/users/${userId}/reject`),
};
