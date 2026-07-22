export type AccountApprovalRole = 'LECTURER' | 'MENTOR';
export type AccountApprovalStatus = 'PENDING' | 'APPROVED' | 'REJECTED';
export type AccountApprovalStatusFilter = 'ALL' | AccountApprovalStatus;
export type AccountApprovalRoleFilter = 'ALL' | AccountApprovalRole;

export interface AccountApprovalRequest {
  id: string;
  fullName: string;
  email: string;
  role: AccountApprovalRole;
  status: AccountApprovalStatus;
  submittedAt: string;
  department?: string | null;
  institution?: string | null;
  expertise?: string | null;
  phone?: string | null;
  note?: string | null;
  reviewedAt?: string | null;
  rejectionReason?: string | null;
}

export interface AccountApprovalDecision {
  requestId: string;
  status: Exclude<AccountApprovalStatus, 'PENDING'>;
  reason?: string;
  reviewedAt?: string;
}

export interface AccountApprovalFilters {
  search: string;
  status: AccountApprovalStatusFilter;
  role: AccountApprovalRoleFilter;
}

export interface AccountApprovalStats {
  total: number;
  pending: number;
  lecturers: number;
  mentors: number;
  approved: number;
  rejected: number;
}

export interface RawAccountRegistration {
  _id?: string;
  id?: string;
  name?: string | null;
  fullName?: string | null;
  email?: string | null;
  role?: string | null;
  roles?: string[] | null;
  status?: string | null;
  createdAt?: string | null;
  registeredAt?: string | null;
  submittedAt?: string | null;
  department?: string | null;
  institution?: string | null;
  organization?: string | null;
  expertise?: string | string[] | null;
  specialization?: string | null;
  phone?: string | null;
  phoneNumber?: string | null;
  note?: string | null;
  bio?: string | null;
}
