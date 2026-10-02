import axiosClient from './axiosClient';
import type { TeamRankingResponse } from '../types/rankings';

export const rankingApi = {
  getTeams: (params?: { semester?: string; year?: number; classId?: string; checkpointNumber?: number }, signal?: AbortSignal) =>
    axiosClient.get('/rankings', { params, signal }) as Promise<TeamRankingResponse>,
};
