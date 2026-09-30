import axiosClient from './axiosClient';
import type { ApiResponse } from '../types/auth';
import type { AcademicOverviewFilters, AcademicOverviewResponse } from '../types/dashboard';

interface DashboardRequestOptions {
  signal?: AbortSignal;
  filters?: AcademicOverviewFilters;
}

export const dashboardApi = {
  getAdmin: (): Promise<ApiResponse<unknown>> => axiosClient.get('/dashboard/admin'),
  getAcademicOverview: (options: DashboardRequestOptions = {}): Promise<ApiResponse<AcademicOverviewResponse>> =>
    axiosClient.get('/dashboard/academic-overview', {
      signal: options.signal,
      params: options.filters,
    }),
  getLecturer: (): Promise<ApiResponse<unknown>> => axiosClient.get('/dashboard/lecturer'),
  getMentor: (): Promise<ApiResponse<unknown>> => axiosClient.get('/dashboard/mentor'),
  /**
   * Fetch student dashboard stats.
   * @param {number} [weekNumber] - Roadmap week (1-10). Falls back to 1 on the server.
   */
  getStudent: (weekNumber?: number, options: DashboardRequestOptions = {}): Promise<ApiResponse<unknown>> => {
    const wn = Number(weekNumber) || 1;
    return axiosClient.get(`/dashboard/student?weekNumber=${wn}`, {
      signal: options.signal,
    });
  },
};
