// @ts-nocheck
import axiosClient from './axiosClient';

export const milestoneApi = {
  getByTeam: (teamId) => axiosClient.get(`/milestones/team/${teamId}`),
  create: (data) => axiosClient.post(`/milestones/team/${data.teamId}`, data),
  update: (id, data) => axiosClient.put(`/milestones/${id}`, data),
  delete: (id) => axiosClient.delete(`/milestones/${id}`),
};
