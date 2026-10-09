// @ts-nocheck
import axiosClient from './axiosClient';

export const workshopApi = {
  getAll: () => axiosClient.get('/workshops'),
  create: (data) => axiosClient.post('/workshops', data),
  update: (id, data) => axiosClient.put(`/workshops/${id}`, data),
  delete: (id) => axiosClient.delete(`/workshops/${id}`),

  // Attendance
  checkIn: (id, data) => axiosClient.post(`/workshops/${id}/check-in`, data),
  getAttendance: (id, classId) => axiosClient.get(`/workshops/${id}/attendance`, { params: { classId } }),
  verifyAttendance: (id, studentId, status, rejectReason) => axiosClient.put(`/workshops/${id}/attendance/${studentId}/verify`, { status, rejectReason }),
  verifyAllAttendance: (id, classId) => axiosClient.put(`/workshops/${id}/attendance/verify-all`, { classId }),
};
