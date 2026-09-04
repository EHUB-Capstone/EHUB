import axiosClient from './axiosClient';
import type { ApiEnvelope, SaveWeeklyTaskPayload, WeeklyTask, WeeklyTaskBoard, WeeklyTaskQuery, WeeklyTaskStatus } from '../types/workspaceTools';

/**
 * GET /api/weekly-tasks
 * @param {Object} params - { courseCode, weekNumber, classId, teamId, status, assigneeStudentId }
 */
export const getWeeklyTasks = (params: WeeklyTaskQuery = {}): Promise<ApiEnvelope<WeeklyTaskBoard>> =>
  axiosClient.get('/weekly-tasks', { params });

/**
 * GET /api/weekly-tasks/team/:teamId/board
 * @param {string} teamId
 * @param {Object} params - { weekNumber, assigneeStudentId, priority, status, search }
 */
export const getTeamTaskBoard = (teamId: string, params: WeeklyTaskQuery = {}, options: { signal?: AbortSignal } = {}): Promise<ApiEnvelope<WeeklyTaskBoard>> =>
  axiosClient.get(`/weekly-tasks/team/${teamId}/board`, {
    params,
    signal: options.signal,
  });

/**
 * POST /api/weekly-tasks
 * @param {Object} payload - task fields
 */
export const createWeeklyTask = (payload: SaveWeeklyTaskPayload): Promise<ApiEnvelope<WeeklyTask>> =>
  axiosClient.post('/weekly-tasks', payload);

/**
 * PUT /api/weekly-tasks/:id
 * @param {string} taskId
 * @param {Object} payload - updated task fields
 */
export const updateWeeklyTask = (taskId: string, payload: SaveWeeklyTaskPayload): Promise<ApiEnvelope<WeeklyTask>> =>
  axiosClient.put(`/weekly-tasks/${taskId}`, payload);

/**
 * DELETE /api/weekly-tasks/:id
 * @param {string} taskId
 */
export const deleteWeeklyTask = (taskId: string): Promise<ApiEnvelope<null>> =>
  axiosClient.delete(`/weekly-tasks/${taskId}`);

/**
 * PATCH /api/weekly-tasks/:id/status
 * @param {string} taskId
 * @param {Object} payload - { status, checklist? }
 */
export const updateWeeklyTaskStatus = (taskId: string, payload: { status: WeeklyTaskStatus; checklist?: WeeklyTask['checklist'] }): Promise<ApiEnvelope<WeeklyTask>> =>
  axiosClient.patch(`/weekly-tasks/${taskId}/status`, payload);
