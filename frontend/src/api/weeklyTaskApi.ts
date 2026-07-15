// @ts-nocheck
// frontend/src/api/weeklyTaskApi.js
import axiosClient from './axiosClient';

/**
 * GET /api/weekly-tasks
 * @param {Object} params - { courseCode, weekNumber, classId, teamId, status, assigneeStudentId }
 */
export const getWeeklyTasks = (params = {}) =>
  axiosClient.get('/weekly-tasks', { params });

/**
 * GET /api/weekly-tasks/team/:teamId/board
 * @param {string} teamId
 * @param {Object} params - { weekNumber, assigneeStudentId, priority, status, search }
 */
export const getTeamTaskBoard = (teamId, params = {}, options = {}) =>
  axiosClient.get(`/weekly-tasks/team/${teamId}/board`, {
    params,
    signal: options.signal,
  });

/**
 * POST /api/weekly-tasks
 * @param {Object} payload - task fields
 */
export const createWeeklyTask = (payload) =>
  axiosClient.post('/weekly-tasks', payload);

/**
 * PUT /api/weekly-tasks/:id
 * @param {string} taskId
 * @param {Object} payload - updated task fields
 */
export const updateWeeklyTask = (taskId, payload) =>
  axiosClient.put(`/weekly-tasks/${taskId}`, payload);

/**
 * DELETE /api/weekly-tasks/:id
 * @param {string} taskId
 */
export const deleteWeeklyTask = (taskId) =>
  axiosClient.delete(`/weekly-tasks/${taskId}`);

/**
 * PATCH /api/weekly-tasks/:id/status
 * @param {string} taskId
 * @param {Object} payload - { status, checklist? }
 */
export const updateWeeklyTaskStatus = (taskId, payload) =>
  axiosClient.patch(`/weekly-tasks/${taskId}/status`, payload);
