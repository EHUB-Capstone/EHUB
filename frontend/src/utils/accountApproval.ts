import type {
  AccountApprovalDecision,
  AccountApprovalFilters,
  AccountApprovalRequest,
  AccountApprovalRole,
  AccountApprovalStats,
  AccountApprovalStatus,
  RawAccountRegistration,
} from '../types/accountApproval';

export function normalizeApprovalRole(value?: string | null): AccountApprovalRole | null {
  const normalized = String(value || '').trim().toUpperCase();
  if (normalized === 'LECTURER' || normalized === 'MENTOR') return normalized;
  return null;
}

export function normalizeApprovalStatus(value?: string | null): AccountApprovalStatus {
  const normalized = String(value || '').trim().toUpperCase().replaceAll(' ', '_');
  if (['APPROVED', 'ACTIVE', 'ACTIVATED'].includes(normalized)) return 'APPROVED';
  if (['REJECTED', 'DECLINED', 'DENIED'].includes(normalized)) return 'REJECTED';
  return 'PENDING';
}

export function registrationToApprovalRequest(
  registration: RawAccountRegistration,
): AccountApprovalRequest | null {
  const id = String(registration._id || registration.id || '').trim();
  const fullName = String(registration.fullName || registration.name || '').trim();
  const email = String(registration.email || '').trim();
  const role = normalizeApprovalRole(registration.role || registration.roles?.[0]);
  if (!id || !fullName || !email || !role) return null;

  const expertise = Array.isArray(registration.expertise)
    ? registration.expertise.join(', ')
    : registration.expertise || registration.specialization || null;

  return {
    id,
    fullName,
    email,
    role,
    status: normalizeApprovalStatus(registration.status),
    submittedAt: registration.submittedAt || registration.registeredAt || registration.createdAt || new Date(0).toISOString(),
    department: registration.department || null,
    institution: registration.institution || registration.organization || null,
    expertise,
    phone: registration.phone || registration.phoneNumber || null,
    note: registration.note || registration.bio || null,
    reviewedAt: null,
    rejectionReason: null,
  };
}

export function validateRejectionReason(reason: string): string | null {
  const normalized = reason.trim();
  if (normalized.length < 10) return 'Please provide a reason of at least 10 characters.';
  if (normalized.length > 500) return 'The rejection reason cannot exceed 500 characters.';
  return null;
}

export function applyApprovalDecision(
  requests: AccountApprovalRequest[],
  decision: AccountApprovalDecision,
): AccountApprovalRequest[] {
  const reviewedAt = decision.reviewedAt || new Date().toISOString();
  const reason = decision.reason?.trim() || null;

  return requests.map((request) => request.id === decision.requestId
    ? {
        ...request,
        status: decision.status,
        reviewedAt,
        rejectionReason: decision.status === 'REJECTED' ? reason : null,
      }
    : request);
}

export function filterApprovalRequests(
  requests: AccountApprovalRequest[],
  filters: AccountApprovalFilters,
): AccountApprovalRequest[] {
  const query = filters.search.trim().toLowerCase();

  return requests
    .filter((request) => filters.status === 'ALL' || request.status === filters.status)
    .filter((request) => filters.role === 'ALL' || request.role === filters.role)
    .filter((request) => !query || [
      request.fullName,
      request.email,
      request.department,
      request.institution,
      request.expertise,
    ].some((value) => value?.toLowerCase().includes(query)))
    .sort((left, right) => {
      if (left.status === 'PENDING' && right.status !== 'PENDING') return -1;
      if (right.status === 'PENDING' && left.status !== 'PENDING') return 1;
      return new Date(right.submittedAt).getTime() - new Date(left.submittedAt).getTime();
    });
}

export function getApprovalStats(requests: AccountApprovalRequest[]): AccountApprovalStats {
  return requests.reduce<AccountApprovalStats>((stats, request) => {
    stats.total += 1;
    stats[request.status.toLowerCase() as 'pending' | 'approved' | 'rejected'] += 1;
    if (request.role === 'LECTURER') stats.lecturers += 1;
    if (request.role === 'MENTOR') stats.mentors += 1;
    return stats;
  }, {
    total: 0,
    pending: 0,
    lecturers: 0,
    mentors: 0,
    approved: 0,
    rejected: 0,
  });
}
