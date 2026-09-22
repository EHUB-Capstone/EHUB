import axiosClient from './axiosClient';
import type { ApiResponse } from '../types/auth';
import type { FeatureAvailability } from '../types/featureAvailability';

export const featureApi = {
  getAvailability: (signal?: AbortSignal): Promise<ApiResponse<FeatureAvailability>> =>
    axiosClient.get('/features', { signal }),
};
