// @ts-nocheck
// src/api/teamApi.js — Module 2 Team Management API
import axiosClient from './axiosClient';
import { classFeatureFlags, runClassFeatureRequest } from '../config/classFeatureFlags';

export const teamApi = {
  // ─── Team CRUD ───────────────────────────────────────────────────────────
  getAll:      (params) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.get('/teams', { params })),
  getById:     (id, options = {}) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.get(`/teams/${id}`, { signal: options.signal })),
  update:      (teamId, data) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.put(`/teams/${teamId}`, data)),
  delete:      (teamId) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.delete(`/teams/${teamId}`)),

  // ─── Assignment ──────────────────────────────────────────────────────────
  assignMentor: (teamId, mentorId) => runClassFeatureRequest(classFeatureFlags.mentorAssignment, 'Class mentor assignment', () => axiosClient.put(`/teams/${teamId}/assign-mentor`, { mentorId })),
  assignLeader: (teamId, leaderStudentId) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.put(`/teams/${teamId}/assign-leader`, { leaderStudentId })),

  // ─── Proposal Review (Lecturer/Admin) ────────────────────────────────────
  reviewProposal: (teamId, data) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.put(`/teams/${teamId}/review`, data)),

  // ─── Member Management (Lecturer/Admin) ──────────────────────────────────
  updateMembers: (teamId, data) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.put(`/teams/${teamId}/members`, data)),

  // ─── Chat Group ──────────────────────────────────────────────────────────
  getChatGroup: (teamId) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.get(`/teams/${teamId}/chat-group`)),
};
