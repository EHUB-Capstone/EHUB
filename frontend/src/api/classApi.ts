// @ts-nocheck
// src/api/classApi.js — Module 2 Class Management API
import axiosClient from './axiosClient';

export const classApi = {
  // ─── Class CRUD ───────────────────────────────────────────────────────────
  getAll:      (params) => axiosClient.get('/classes', { params }),
  getById:     (id)     => axiosClient.get(`/classes/${id}`),
  create:            (data)   => axiosClient.post('/classes', data),
  previewBulkCreate: (data)   => axiosClient.post('/classes/bulk/preview', data),
  commitBulkCreate:  (data)   => axiosClient.post('/classes/bulk/commit', data),
  bulkCreate:        (data)   => axiosClient.post('/classes/bulk/commit', data),
  reportCodeConflict: (data) => axiosClient.post('/classes/report-code-conflict', data),
  update:      (id, data) => axiosClient.put(`/classes/${id}`, data),
  rename:      (id, classCode) => axiosClient.put(`/classes/${id}/rename`, { classCode }),
  delete:      (id)     => axiosClient.delete(`/classes/${id}`),

  // ─── Lecturer Assignment & Schedule ──────────────────────────────────────────
  assignLecture: (id, lectureId) => axiosClient.put(`/classes/${id}/assign-lecture`, { lectureId }),
  assignMentors: (id, mentorIds) => axiosClient.put(`/classes/${id}/assign-mentors`, { mentorIds }),
  updateSchedule: (id, schedule) => axiosClient.put(`/classes/${id}/schedule`, schedule),
  updateTeachingAssignment: (id, data) => axiosClient.put(`/classes/${id}/teaching-assignment`, data),
  backfillChats: (id)            => axiosClient.post(`/classes/${id}/backfill-chats`),

  // ─── Students ────────────────────────────────────────────────────────────
  getStudents: (classId, params) => axiosClient.get(`/classes/${classId}/students`, { params }),
  importStudents: (classId, formData) =>
    axiosClient.post(`/classes/${classId}/import-students`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }),
  exportClassExcel: (classId) => 
    axiosClient.get(`/classes/${classId}/export-excel`, { responseType: 'blob' }),

  // Verify student majors against lecturer's Excel file
  verifyMajors: (classId, formData) =>
    axiosClient.post(`/classes/${classId}/verify-majors`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }),
  // Manually update one student's major
  updateStudentMajor: (classId, studentId, newMajor) =>
    axiosClient.patch(`/classes/${classId}/students/${studentId}/major`, { newMajor }),
  // Lock/Unlock major changes for a class
  toggleMajorLock: (classId) =>
    axiosClient.patch(`/classes/${classId}/toggle-major-lock`),
  addStudent: (classId, data) =>
    axiosClient.post(`/classes/${classId}/students`, data),
  removeStudent: (classId, studentId) =>
    axiosClient.delete(`/classes/${classId}/students/${studentId}`),

  // ─── Teams ───────────────────────────────────────────────────────────────
  getTeams:      (classId)       => axiosClient.get(`/classes/${classId}/teams`),
  generateTeam:  (classId, data) => axiosClient.post(`/classes/${classId}/teams/generate`, data),
  studentProposeTeam: (classId, payload) =>
    axiosClient.post(`/classes/${classId}/teams/student-proposal`, payload),

  // ─── Student/User side ───────────────────────────────────────────────────
  getMyClasses:     () => axiosClient.get('/classes/my-classes'),
  getMyTeam:        () => axiosClient.get('/classes/my-team'),
  getMyClassDetail: (classId) => axiosClient.get(`/classes/my-class-detail/${classId}`),
};
