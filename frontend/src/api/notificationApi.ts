// @ts-nocheck
import axiosClient from './axiosClient';

export const notificationApi = {
  getAll: () => axiosClient.get('/notifications'),
  getUnreadCount: () => axiosClient.get('/notifications/unread-count'),
  markRead: (id) => axiosClient.put(`/notifications/${id}/read`),
  markAllRead: () => axiosClient.put('/notifications/mark-all-read'),
};
