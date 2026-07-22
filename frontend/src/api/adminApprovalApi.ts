import axiosClient from './axiosClient';

export interface PendingApprovalUserDto {
  id: string;
  fullName: string;
  email: string;
  roles: string[];
  status: string;
  createdAt: string;
}

interface ApiResponse<T> {
  success: boolean;
  message: string;
  code?: string | null;
  data: T;
}

export const adminApprovalApi = {
  getPending: (): Promise<ApiResponse<PendingApprovalUserDto[]>> =>
    axiosClient.get('/admin/users/pending-approval'),

  approve: (userId: string): Promise<ApiResponse<null>> =>
    axiosClient.post(`/admin/users/${userId}/approve`),

  reject: (userId: string): Promise<ApiResponse<null>> =>
    axiosClient.post(`/admin/users/${userId}/reject`),
};

export function getAdminApprovalErrorMessage(error: unknown): string {
  const requestError = error as {
    message?: string;
    response?: { data?: { message?: string } };
  };

  return requestError.response?.data?.message
    || requestError.message
    || 'The account approval request could not be completed.';
}
