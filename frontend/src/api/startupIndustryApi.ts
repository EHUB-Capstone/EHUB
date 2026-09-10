import axiosClient from './axiosClient';
import type {
  SaveStartupIndustryPayload,
  StartupIndustrySort,
  StartupIndustryStatus,
} from '../types/startupIndustries';

interface GetStartupIndustriesParams {
  search?: string;
  status?: StartupIndustryStatus;
  sort?: StartupIndustrySort;
}

export const startupIndustryApi = {
  getActiveOptions: (signal?: AbortSignal) =>
    axiosClient.get('/startup-industries/options', { signal }),
  getAll: (params: GetStartupIndustriesParams = {}, signal?: AbortSignal) =>
    axiosClient.get('/startup-industries', { params, signal }),
  create: (data: SaveStartupIndustryPayload) =>
    axiosClient.post('/startup-industries', data),
  update: (id: string, data: SaveStartupIndustryPayload) =>
    axiosClient.put(`/startup-industries/${id}`, data),
  changeStatus: (id: string, status: StartupIndustryStatus) =>
    axiosClient.put(`/startup-industries/${id}/status`, { status }),
};
