// @ts-nocheck
// src/api/teamApi.js — Module 2 Team Management API
import axiosClient from './axiosClient';
import { classFeatureFlags, runClassFeatureRequest } from '../config/classFeatureFlags';

export const teamApi = {
  // ─── Team CRUD ───────────────────────────────────────────────────────────
  getAll:      (params) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.get('/teams', { params })),
  getById:     (id, options = {}) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.get(`/teams/${id}`, { signal: options.signal })),

  // ─── Assignment ──────────────────────────────────────────────────────────
  getMentorAssignments: (teamId) => runClassFeatureRequest(classFeatureFlags.mentorAssignment, 'Class mentor assignment', () => axiosClient.get(`/teams/${teamId}/mentor-assignments`)),
  assignMentor: (teamId, mentorProfileId, note = null) => runClassFeatureRequest(classFeatureFlags.mentorAssignment, 'Class mentor assignment', () => axiosClient.post(`/teams/${teamId}/mentor-assignments`, { mentorProfileId, note })),
  endMentorAssignment: (teamId, reason) => runClassFeatureRequest(classFeatureFlags.mentorAssignment, 'Class mentor assignment', () => axiosClient.post(`/teams/${teamId}/mentor-assignments/end`, { reason })),
  assignLeader: (teamId, studentId, rowVersion) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.put(`/teams/${teamId}/leader`, { studentId, rowVersion })),

  // ─── Proposal Review (Lecturer/Admin) ────────────────────────────────────
  updateProposal: (proposalId, data) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.put(`/team-proposals/${proposalId}`, data)),
  submitProposal: (proposalId, rowVersion) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.post(`/team-proposals/${proposalId}/submit`, { rowVersion })),
  cancelProposal: (proposalId, rowVersion, reason) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.post(`/team-proposals/${proposalId}/cancel`, { rowVersion, reason })),
  reviewProposal: (proposalId, data) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.post(`/team-proposals/${proposalId}/review`, data)),
  getProposalHistory: (proposalId) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.get(`/team-proposals/${proposalId}/history`)),

  // ─── Member Management (Lecturer/Admin) ──────────────────────────────────
  updateMembers: (teamId, data) => runClassFeatureRequest(classFeatureFlags.teamManagement, 'Class team management', () => axiosClient.put(`/teams/${teamId}/members`, data)),

  getProjectDirection: (teamId) => runClassFeatureRequest(classFeatureFlags.projectDirection, 'Project direction', () => axiosClient.get(`/teams/${teamId}/project-direction`)),
  saveProjectDirection: (teamId, data) => runClassFeatureRequest(classFeatureFlags.projectDirection, 'Project direction', () => axiosClient.put(`/teams/${teamId}/project-direction`, data)),
  submitProjectDirection: (teamId, rowVersion) => runClassFeatureRequest(classFeatureFlags.projectDirection, 'Project direction', () => axiosClient.post(`/teams/${teamId}/project-direction/submit`, { rowVersion })),
  reviewProjectDirection: (teamId, data) => runClassFeatureRequest(classFeatureFlags.projectDirection, 'Project direction', () => axiosClient.post(`/teams/${teamId}/project-direction/review`, data)),

  // ─── Chat Group ──────────────────────────────────────────────────────────
};
