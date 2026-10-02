import axiosClient from './axiosClient';
import type { ApiEnvelope } from '../types/workspaceTools';
import type { StudentPreviousScores } from '../types/studentPreviousScores';

export const studentPreviousScoresApi = {
  get: (classId: string, studentId: string, signal?: AbortSignal) =>
    axiosClient.get(`/workspace/checkpoints/classes/${classId}/students/${studentId}/previous-scores`, { signal }) as Promise<ApiEnvelope<StudentPreviousScores>>,
};
