import axiosClient from './axiosClient.ts';

export interface PendingApprovalUserDto {
  id: string;
  name?: string;
  fullName?: string;
  email: string;
  role?: string;
  roles?: string[];
  status: string;
  createdAt: string;
  phone?: string | null;
}

interface ManagedApprovalUserListDto {
  users: PendingApprovalUserDto[];
  pagination: {
    total: number;
    page: number;
    limit: number;
    pages: number;
  };
}

interface ApiResponse<T> {
  success: boolean;
  message: string;
  code?: string | null;
  data: T;
}

const APPROVAL_ROLES = ['LECTURER', 'MENTOR'] as const;
const APPROVAL_STATUSES = ['PENDING', 'APPROVED', 'REJECTED'] as const;
const APPROVAL_PAGE_SIZE = 100;

async function getApprovalGroup(
  role: typeof APPROVAL_ROLES[number],
  status: typeof APPROVAL_STATUSES[number],
): Promise<PendingApprovalUserDto[]> {
  const getPage = (page: number): Promise<ApiResponse<ManagedApprovalUserListDto>> =>
    axiosClient.get('/users', {
      params: { page, limit: APPROVAL_PAGE_SIZE, role, status },
    });

  const firstPage = await getPage(1);
  const remainingPages = Array.from(
    { length: Math.max(0, firstPage.data.pagination.pages - 1) },
    (_, index) => index + 2,
  );
  const remainingResponses = await Promise.all(remainingPages.map(getPage));

  return [firstPage, ...remainingResponses]
    .flatMap((response) => response.data.users || []);
}

export const adminApprovalApi = {
  getPending: (): Promise<ApiResponse<PendingApprovalUserDto[]>> =>
    axiosClient.get('/admin/users/pending-approval'),

  getAll: async (): Promise<ApiResponse<PendingApprovalUserDto[]>> => {
    const groups = await Promise.all(
      APPROVAL_ROLES.flatMap((role) =>
        APPROVAL_STATUSES.map((status) => getApprovalGroup(role, status))),
    );

    return {
      success: true,
      message: 'Account approval users retrieved successfully.',
      code: null,
      data: groups.flat(),
    };
  },

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
