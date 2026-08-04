// @ts-nocheck
// src/api/classApi.js — Module 2 Class Management API
import axiosClient from './axiosClient';
import { classFeatureFlags, runClassFeatureRequest } from '../config/classFeatureFlags';

export const classApi = {
  // ─── Class CRUD ───────────────────────────────────────────────────────────
  getAll:      (params) => axiosClient.get('/classes', { params }),
  getById:     (id)     => axiosClient.get(`/classes/${id}`),
  create:            (data)   => axiosClient.post('/classes', data),
  previewBulkCreate: (data)   => axiosClient.post('/classes/bulk/preview', data),
  commitBulkCreate:  (data)   => axiosClient.post('/classes/bulk/commit', data),
  bulkCreate:        (data)   => axiosClient.post('/classes/bulk/commit', data),
  reportCodeConflict: (data) => runClassFeatureRequest(classFeatureFlags.codeConflictReport, 'Class code conflict reporting', () =>
    axiosClient.post('/classes/report-code-conflict', data)),
  update:      (id, data) => axiosClient.put(`/classes/${id}`, data),
  rename:      (id, classCode) => runClassFeatureRequest(classFeatureFlags.rename, 'Class rename', () =>
    axiosClient.put(`/classes/${id}/rename`, { classCode })),
  delete:      (id) => runClassFeatureRequest(classFeatureFlags.lifecycle, 'Class lifecycle management', () =>
    axiosClient.delete(`/classes/${id}`)),

  // ─── Lecturer Assignment & Schedule ──────────────────────────────────────────
  assignMentors: (id, mentorIds) => runClassFeatureRequest(classFeatureFlags.mentorAssignment, 'Class mentor assignment', () =>
    axiosClient.put(`/classes/${id}/assign-mentors`, { mentorIds })),
  updateSchedule: (id, schedule) => axiosClient.put(`/classes/${id}/schedule`, schedule),
  updateTeachingAssignment: (id, data) => axiosClient.put(`/classes/${id}/teaching-assignment`, data),
  backfillChats: (id) => runClassFeatureRequest(classFeatureFlags.chatBackfill, 'Class chat backfill', () =>
    axiosClient.post(`/classes/${id}/backfill-chats`)),

  // ─── Students ────────────────────────────────────────────────────────────
  getStudents: (classId, params) => axiosClient.get(`/classes/${classId}/students`, { params }),
  previewImportStudents: (classId, formData) =>
    axiosClient.post(`/classes/${classId}/import-students/preview`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }),
  commitImportStudents: (classId, payload) =>
    axiosClient.post(`/classes/${classId}/import-students/commit`, payload),
  importStudents: (classId, formData) =>
    axiosClient.post(`/classes/${classId}/import-students/preview`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }),
  getImportTemplate: () =>
    axiosClient.get('/classes/import-template', { responseType: 'blob' }),
  exportClassExcel: (classId) => 
    axiosClient.get(`/classes/${classId}/export-excel`, { responseType: 'blob' }),

  // Verify student majors against lecturer's Excel file
  verifyMajors: (classId, formData) =>
    runClassFeatureRequest(classFeatureFlags.majorVerification, 'Class major verification', () =>
      axiosClient.post(`/classes/${classId}/verify-majors`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })),
  // Manually update one student's major
  updateStudentMajor: (classId, studentId, newMajor) =>
    runClassFeatureRequest(classFeatureFlags.majorVerification, 'Class major verification', () =>
      axiosClient.patch(`/classes/${classId}/students/${studentId}/major`, { newMajor })),
  // Lock/Unlock major changes for a class
  toggleMajorLock: (classId) =>
    runClassFeatureRequest(classFeatureFlags.majorVerification, 'Class major verification', () =>
      axiosClient.patch(`/classes/${classId}/toggle-major-lock`)),
  addStudent: (classId, data) =>
    axiosClient.post(`/classes/${classId}/students`, data),
  removeStudent: (classId, studentId) =>
    axiosClient.delete(`/classes/${classId}/students/${studentId}`),

  // ─── Teams ───────────────────────────────────────────────────────────────
  getTeams:      (classId) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () =>
    axiosClient.get(`/classes/${classId}/teams`)),
  generateTeam:  (classId, data) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () =>
    axiosClient.post(`/classes/${classId}/teams/generate`, data)),
  studentProposeTeam: (classId, payload) =>
    runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () =>
      axiosClient.post(`/classes/${classId}/teams/student-proposal`, payload)),

  // ─── Student/User side ───────────────────────────────────────────────────
  getMyClasses: () => runClassFeatureRequest(classFeatureFlags.studentSelfService, 'Student class self-service', () =>
    axiosClient.get('/classes/my-classes')),
  getMyTeam: () => runClassFeatureRequest(classFeatureFlags.studentSelfService, 'Student class self-service', () =>
    axiosClient.get('/classes/my-team')),
  getMyClassDetail: (classId) => runClassFeatureRequest(classFeatureFlags.studentSelfService, 'Student class self-service', () =>
    axiosClient.get(`/classes/my-class-detail/${classId}`)),
};
