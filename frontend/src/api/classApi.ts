// src/api/classApi.js — Module 2 Class Management API
import axiosClient from './axiosClient';
import { classFeatureFlags, runClassFeatureRequest } from '../config/classFeatureFlags';
import type {
  CreateBulkClassesRequest,
  GetClassesParams,
  GetClassRosterParams,
  ExportClassRosterParams,
} from '../types/classes';

export const classApi = {
  // ─── Class CRUD ───────────────────────────────────────────────────────────
  getAll:      (params: GetClassesParams = {}) => axiosClient.get('/classes', { params }),
  getById:     (id: string) => axiosClient.get(`/classes/${id}`),
  create:            (data: unknown) => axiosClient.post('/classes', data),
  previewBulkCreate: (data: CreateBulkClassesRequest) => axiosClient.post('/classes/bulk/preview', data),
  commitBulkCreate:  (data: CreateBulkClassesRequest) => axiosClient.post('/classes/bulk/commit', data),
  update:      (id: string, data: unknown) => axiosClient.put(`/classes/${id}`, data),
  rename:      (id, classCode) => runClassFeatureRequest(classFeatureFlags.rename, 'Class rename', () =>
    axiosClient.put(`/classes/${id}/rename`, { classCode })),
  archive: (id: string, data: { rowVersion: string; reason: string }) =>
    runClassFeatureRequest(classFeatureFlags.lifecycle, 'Class lifecycle management', () =>
      axiosClient.post(`/classes/${id}/archive`, data)),
  restore: (id: string, data: { rowVersion: string; reason: string }) =>
    runClassFeatureRequest(classFeatureFlags.lifecycle, 'Class lifecycle management', () =>
      axiosClient.post(`/classes/${id}/restore`, data)),
  getAudit: (id: string, params: { page?: number; pageSize?: number } = {}) =>
    axiosClient.get(`/classes/${id}/audit`, { params }),

  // ─── Lecturer Assignment & Schedule ──────────────────────────────────────────
  getClassMentors: (id: string) => runClassFeatureRequest(classFeatureFlags.mentorAssignment, 'Class mentor assignment', () =>
    axiosClient.get(`/classes/${id}/mentors`)),
  getMentorCandidates: (id: string) => runClassFeatureRequest(classFeatureFlags.mentorAssignment, 'Class mentor assignment', () =>
    axiosClient.get(`/classes/${id}/mentor-candidates`)),
  updateSchedule: (id: string, schedule: unknown) => axiosClient.put(`/classes/${id}/schedule`, schedule),
  updateTeachingAssignment: (id: string, data: unknown) => axiosClient.put(`/classes/${id}/teaching-assignment`, data),
  repairChatMemberships: (id: string) => runClassFeatureRequest(classFeatureFlags.chatBackfill, 'Class chat repair', () =>
    axiosClient.post(`/classes/${id}/repair-chat-memberships`)),

  // ─── Students ────────────────────────────────────────────────────────────
  getStudents: (classId: string, params: GetClassRosterParams) => axiosClient.get(`/classes/${classId}/students`, { params }),
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
  exportClassExcel: (classId: string, params: ExportClassRosterParams) =>
    axiosClient.get(`/classes/${classId}/export-excel`, { params, responseType: 'blob' }),

  // Verify student majors against lecturer's Excel file
  verifyMajors: (classId, formData) =>
    runClassFeatureRequest(classFeatureFlags.majorVerification, 'Class major verification', () =>
      axiosClient.post(`/classes/${classId}/major-verification`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })),
  getMajorVerificationTemplate: () =>
    axiosClient.get('/classes/major-verification-template', { responseType: 'blob' }),
  // Manually update one enrollment's major snapshot
  updateStudentMajor: (classId, studentId, majorCode, reason) =>
    runClassFeatureRequest(classFeatureFlags.majorVerification, 'Class major verification', () =>
      axiosClient.put(`/classes/${classId}/students/${studentId}/major`, { majorCode, reason })),
  // Explicit, idempotent lock and unlock operations
  lockMajors: (classId) =>
    runClassFeatureRequest(classFeatureFlags.majorVerification, 'Class major verification', () =>
      axiosClient.post(`/classes/${classId}/major-lock`)),
  unlockMajors: (classId) =>
    runClassFeatureRequest(classFeatureFlags.majorVerification, 'Class major verification', () =>
      axiosClient.delete(`/classes/${classId}/major-lock`)),
  addStudent: (classId, data) =>
    axiosClient.post(`/classes/${classId}/students`, data),
  dropStudent: (classId, studentId) =>
    axiosClient.post(`/classes/${classId}/students/${studentId}/drop`),
  reEnrollStudent: (classId, studentId) =>
    axiosClient.post(`/classes/${classId}/students/${studentId}/re-enroll`),

  // ─── Teams ───────────────────────────────────────────────────────────────
  getTeams:      (classId) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () =>
    axiosClient.get(`/classes/${classId}/teams`)),
  createTeam:  (classId, data) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () =>
    axiosClient.post(`/classes/${classId}/teams`, data)),
  generateTeam: (classId, data) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () =>
    axiosClient.post(`/classes/${classId}/teams/generate`, data)),
  getTeamProposals: (classId) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () =>
    axiosClient.get(`/classes/${classId}/team-proposals`)),
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
