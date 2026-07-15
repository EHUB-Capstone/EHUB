// @ts-nocheck
// src/api/trackingApi.js
import axiosClient from './axiosClient';

export const trackingApi = {
  /**
   * Lấy thống kê Auth Analytics cho Admin Dashboard.
   * @param {number} [days=7] - Số ngày muốn thống kê (7 hoặc 30)
   */
  getAuthStats: (days = 7) =>
    axiosClient.get(`/tracking/auth-stats?days=${days}`),

  /**
   * Lấy danh sách user đang online và recently active.
   */
  getOnlineUsers: () =>
    axiosClient.get('/tracking/online-users'),
};
