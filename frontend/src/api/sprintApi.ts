// @ts-nocheck
// frontend/src/api/sprintApi.js
import axiosClient from './axiosClient';

// ── Milestones ───────────────────────────────────────────

export const getTeamMilestones = async (teamId) => {
  return axiosClient.get(`/milestones/team/${teamId}`);
};

export const createMilestone = async (teamId, data) => {
  return axiosClient.post(`/milestones/team/${teamId}`, data);
};

export const updateMilestone = async (milestoneId, data) => {
  return axiosClient.put(`/milestones/${milestoneId}`, data);
};

export const deleteMilestone = async (milestoneId) => {
  return axiosClient.delete(`/milestones/${milestoneId}`);
};

// ── Tasks ────────────────────────────────────────────────

export const getTeamTasks = async (teamId, params = {}) => {
  return axiosClient.get(`/sprint-tasks/team/${teamId}`, { params });
};

export const createTask = async (teamId, data) => {
  return axiosClient.post(`/sprint-tasks/team/${teamId}`, data);
};

export const updateTask = async (taskId, data) => {
  return axiosClient.put(`/sprint-tasks/${taskId}`, data);
};

export const updateTaskStatus = async (taskId, data) => {
  return axiosClient.put(`/sprint-tasks/${taskId}/status`, data);
};

export const deleteTask = async (taskId) => {
  return axiosClient.delete(`/sprint-tasks/${taskId}`);
};

// ── Progress ─────────────────────────────────────────────

export const getTeamProgress = async (teamId) => {
  return axiosClient.get(`/sprint-tasks/team/${teamId}/progress`);
};
