import axiosClient from './axiosClient';
import type { TeamRankingResponse } from '../types/rankings';

export const rankingApi = {
  getTeams: (params?: { semester?: string; year?: number }) =>
    axiosClient.get('/rankings', { params }) as Promise<TeamRankingResponse>,
};
