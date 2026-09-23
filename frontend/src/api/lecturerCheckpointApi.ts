import axiosClient from './axiosClient';
import type {
  BulkSaveClassCheckpointSchedulePayload,
  GetLecturerCheckpointsParams,
  SaveClassCheckpointSchedulePayload,
} from '../types/lecturerCheckpoints';

export const lecturerCheckpointApi = {
  getOverview: (params: GetLecturerCheckpointsParams) =>
    axiosClient.get('/lecturer/checkpoints', { params }),

  saveSchedule: (
    classId: string,
    checkpointId: string,
    payload: SaveClassCheckpointSchedulePayload,
  ) => axiosClient.put(
    `/lecturer/checkpoints/classes/${classId}/definitions/${checkpointId}/schedule`,
    payload,
  ),

  saveBulkSchedule: (payload: BulkSaveClassCheckpointSchedulePayload) =>
    axiosClient.put('/lecturer/checkpoints/schedules/bulk', payload),
};
