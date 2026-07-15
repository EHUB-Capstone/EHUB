// @ts-nocheck
import axiosClient from './axiosClient';

export const rankingApi = {
  getGlobal: () => axiosClient.get('/rankings'),
  getClass: (classId) => axiosClient.get(`/rankings/class/${classId}`),
  getMyClass: () => axiosClient.get('/rankings/my-class'),
  getMyTeams: () => axiosClient.get('/rankings/my-teams'),
};
