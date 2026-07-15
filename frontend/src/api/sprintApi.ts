// @ts-nocheck
// frontend/src/api/sprintApi.js
import axios from 'axios';

const getBaseUrl = () => {
  const envUrl = import.meta.env.VITE_API_URL;
  if (!envUrl) return 'http://localhost:5000/api';
  return envUrl.endsWith('/api') ? envUrl : `${envUrl}/api`;
};
const API_URL = getBaseUrl();

const auth = () => ({
  headers: { Authorization: `Bearer ${localStorage.getItem('token')}` },
});

// ── Milestones ───────────────────────────────────────────

export const getTeamMilestones = async (teamId) => {
  const res = await axios.get(`${API_URL}/milestones/team/${teamId}`, auth());
  return res.data;
};

export const createMilestone = async (teamId, data) => {
  const res = await axios.post(`${API_URL}/milestones/team/${teamId}`, data, auth());
  return res.data;
};

export const updateMilestone = async (milestoneId, data) => {
  const res = await axios.put(`${API_URL}/milestones/${milestoneId}`, data, auth());
  return res.data;
};

export const deleteMilestone = async (milestoneId) => {
  const res = await axios.delete(`${API_URL}/milestones/${milestoneId}`, auth());
  return res.data;
};

// ── Tasks ────────────────────────────────────────────────

export const getTeamTasks = async (teamId, params = {}) => {
  const res = await axios.get(`${API_URL}/sprint-tasks/team/${teamId}`, { ...auth(), params });
  return res.data;
};

export const createTask = async (teamId, data) => {
  const res = await axios.post(`${API_URL}/sprint-tasks/team/${teamId}`, data, auth());
  return res.data;
};

export const updateTask = async (taskId, data) => {
  const res = await axios.put(`${API_URL}/sprint-tasks/${taskId}`, data, auth());
  return res.data;
};

export const updateTaskStatus = async (taskId, data) => {
  const res = await axios.put(`${API_URL}/sprint-tasks/${taskId}/status`, data, auth());
  return res.data;
};

export const deleteTask = async (taskId) => {
  const res = await axios.delete(`${API_URL}/sprint-tasks/${taskId}`, auth());
  return res.data;
};

// ── Progress ─────────────────────────────────────────────

export const getTeamProgress = async (teamId) => {
  const res = await axios.get(`${API_URL}/sprint-tasks/team/${teamId}/progress`, auth());
  return res.data;
};
