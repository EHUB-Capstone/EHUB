// @ts-nocheck
import axiosClient from './axiosClient';

const parseNotificationData = (notification) => {
  if (notification?.data) {
    return notification.data;
  }

  if (!notification?.dataJson) {
    return null;
  }

  try {
    const parsed = JSON.parse(notification.dataJson);
    return parsed?.data || parsed;
  } catch {
    return null;
  }
};

export const getNotificationId = (notification) => notification?.id || notification?._id;

export const normalizeNotification = (notification) => ({
  ...notification,
  _id: getNotificationId(notification),
  id: getNotificationId(notification),
  message: notification?.message || notification?.body || '',
  data: parseNotificationData(notification),
});

export const notificationApi = {
  getAll: () => axiosClient.get('/notifications'),
  getUnreadCount: () => axiosClient.get('/notifications/unread-count'),
  markRead: (id) => axiosClient.put(`/notifications/${id}/read`),
  markAllRead: () => axiosClient.put('/notifications/mark-all-read'),
};
