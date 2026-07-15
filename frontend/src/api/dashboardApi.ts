// @ts-nocheck
import axiosClient from './axiosClient';

export const dashboardApi = {
  getAdmin: () => axiosClient.get('/dashboard/admin'),
  getLecturer: () => axiosClient.get('/dashboard/lecturer'),
  getMentor: () => axiosClient.get('/dashboard/mentor'),
  /**
   * Fetch student dashboard stats.
   * @param {number} [weekNumber] - Roadmap week (1-10). Falls back to 1 on the server.
   */
  getStudent: (weekNumber, options = {}) => {
    const wn = Number(weekNumber) || 1;
    return axiosClient.get(`/dashboard/student?weekNumber=${wn}`, {
      signal: options.signal,
    });
  },
};
