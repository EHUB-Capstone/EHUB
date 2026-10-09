import { useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import {
  BriefcaseBusiness,
  Building2,
  CalendarDays,
  Check,
  Clock3,
  Eye,
  Filter,
  GraduationCap,
  Info,
  Mail,
  MessageSquareText,
  Phone,
  Search,
  ShieldCheck,
  UserCheck,
  Users,
  UserX,
  X,
} from 'lucide-react';
import Button from '../ui/Button';
import EmptyState from '../ui/EmptyState';
import LoadingSkeleton from '../ui/LoadingSkeleton';
import type {
  AccountApprovalDecision,
  AccountApprovalRequest,
  AccountApprovalRoleFilter,
  AccountApprovalStatus,
  AccountApprovalStatusFilter,
} from '../../types/accountApproval';
import {
  filterApprovalRequests,
  getApprovalStats,
  validateRejectionReason,
} from '../../utils/accountApproval';

interface AccountApprovalBoardProps {
  requests: AccountApprovalRequest[];
  loading?: boolean;
  loadError?: string | null;
  onDecision: (decision: AccountApprovalDecision) => Promise<void>;
}

const STATUS_STYLES: Record<AccountApprovalStatus, string> = {
  PENDING: 'bg-amber-100 text-amber-700',
  APPROVED: 'bg-green-100 text-green-700',
  REJECTED: 'bg-red-100 text-red-700',
};

const STATUS_LABELS: Record<AccountApprovalStatus, string> = {
  PENDING: 'Pending review',
  APPROVED: 'Approved',
  REJECTED: 'Rejected',
};

const ROLE_STYLES = {
  LECTURER: 'bg-secondary-50 text-secondary',
  MENTOR: 'bg-purple-100 text-purple-700',
};

function formatDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime()) || date.getTime() === 0) return 'Date unavailable';
  return new Intl.DateTimeFormat('en', { day: '2-digit', month: 'short', year: 'numeric' }).format(date);
}

function initials(name: string): string {
  return name.split(/\s+/).filter(Boolean).slice(-2).map((part) => part.charAt(0).toUpperCase()).join('');
}

export default function AccountApprovalBoard({
  requests,
  loading = false,
  loadError = null,
  onDecision,
}: AccountApprovalBoardProps) {
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<AccountApprovalStatusFilter>('PENDING');
  const [roleFilter, setRoleFilter] = useState<AccountApprovalRoleFilter>('ALL');
  const [detailRequest, setDetailRequest] = useState<AccountApprovalRequest | null>(null);
  const [rejectRequest, setRejectRequest] = useState<AccountApprovalRequest | null>(null);
  const [processingRequestId, setProcessingRequestId] = useState<string | null>(null);

  const stats = useMemo(() => getApprovalStats(requests), [requests]);
  const filteredRequests = useMemo(() => filterApprovalRequests(requests, {
    search,
    status: statusFilter,
    role: roleFilter,
  }), [requests, roleFilter, search, statusFilter]);

  const approve = async (request: AccountApprovalRequest) => {
    setProcessingRequestId(request.id);
    try {
      await onDecision({ requestId: request.id, status: 'APPROVED' });
      setDetailRequest(null);
      toast.success(`${request.fullName}'s ${request.role.toLowerCase()} account was approved.`);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'The account could not be approved.');
    } finally {
      setProcessingRequestId(null);
    }
  };

  const reject = async (request: AccountApprovalRequest, reason: string) => {
    setProcessingRequestId(request.id);
    try {
      await onDecision({ requestId: request.id, status: 'REJECTED', reason });
      toast.success(`${request.fullName}'s registration was rejected.`);
      setRejectRequest(null);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'The registration could not be rejected.');
    } finally {
      setProcessingRequestId(null);
    }
  };

  const openReject = (request: AccountApprovalRequest) => {
    setDetailRequest(null);
    setRejectRequest(request);
  };

  const statusTabs: Array<{ value: AccountApprovalStatusFilter; label: string; count: number }> = [
    { value: 'PENDING', label: 'Pending', count: stats.pending },
    { value: 'ALL', label: 'All requests', count: stats.total },
    { value: 'APPROVED', label: 'Approved', count: stats.approved },
    { value: 'REJECTED', label: 'Rejected', count: stats.rejected },
  ];

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.16em] text-primary">
            <ShieldCheck className="h-4 w-4" /> Admin review
          </div>
          <h1 className="text-2xl font-bold text-slate-900 sm:text-3xl">Account approvals</h1>
          <p className="mt-1 text-sm text-slate-500">Review Lecturer and Mentor registration requests before granting access.</p>
        </div>
        <div className="flex items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-2.5 text-sm text-amber-800">
          <Clock3 className="h-4 w-4" />
          <strong>{stats.pending}</strong> awaiting review
        </div>
      </div>

      <div className="flex items-start gap-3 rounded-2xl border border-secondary-100 bg-secondary-50/70 p-4 text-sm text-secondary-dark">
        <Info className="mt-0.5 h-4 w-4 shrink-0" />
        <div>
          <p className="font-semibold">Connected to the account approval service</p>
          <p className="mt-0.5 text-xs text-secondary/80">Pending accounts and approval decisions are synchronized with the server. Rejection notes are shown in this session because the current API stores the decision only.</p>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Pending requests" value={stats.pending} icon={Clock3} tone="amber" />
        <StatCard label="Lecturer accounts" value={stats.lecturers} icon={GraduationCap} tone="blue" />
        <StatCard label="Mentor accounts" value={stats.mentors} icon={BriefcaseBusiness} tone="purple" />
        <StatCard label="Approved" value={stats.approved} icon={UserCheck} tone="green" />
      </div>

      <section className="overflow-hidden rounded-2xl border border-slate-200/70 bg-white shadow-sm">
        <div className="space-y-4 border-b border-slate-100 p-4 sm:p-5">
          <div className="flex flex-col gap-3 xl:flex-row xl:items-center xl:justify-between">
            <div className="flex max-w-full gap-1 overflow-x-auto rounded-xl bg-slate-100 p-1">
              {statusTabs.map((tab) => (
                <button
                  key={tab.value}
                  type="button"
                  onClick={() => setStatusFilter(tab.value)}
                  className={`flex shrink-0 items-center gap-2 rounded-lg px-3 py-2 text-xs font-semibold transition-all ${statusFilter === tab.value ? 'bg-white text-slate-900 shadow-xs' : 'text-slate-500 hover:text-slate-700'}`}
                >
                  {tab.label}
                  <span className={`rounded-full px-1.5 py-0.5 text-[10px] ${statusFilter === tab.value ? 'bg-primary-50 text-primary' : 'bg-slate-200 text-slate-500'}`}>{tab.count}</span>
                </button>
              ))}
            </div>

            <div className="flex flex-col gap-2 sm:flex-row">
              <div className="relative min-w-0 sm:w-72">
                <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                <input
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Search name, email or expertise"
                  className="w-full rounded-xl border border-slate-200 py-2.5 pl-9 pr-3 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
                />
              </div>
              <div className="relative sm:w-44">
                <Filter className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                <select
                  value={roleFilter}
                  onChange={(event) => setRoleFilter(event.target.value as AccountApprovalRoleFilter)}
                  className="w-full appearance-none rounded-xl border border-slate-200 bg-white py-2.5 pl-9 pr-8 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
                >
                  <option value="ALL">All roles</option>
                  <option value="LECTURER">Lecturers</option>
                  <option value="MENTOR">Mentors</option>
                </select>
              </div>
            </div>
          </div>

          {loadError && (
            <div className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">{loadError}</div>
          )}
        </div>

        {loading ? (
          <LoadingSkeleton variant="table" lines={5} className="p-4" />
        ) : filteredRequests.length === 0 ? (
          <EmptyState
            icon={statusFilter === 'PENDING' ? UserCheck : Users}
            title={statusFilter === 'PENDING' ? 'No pending registrations' : 'No matching requests'}
            description={statusFilter === 'PENDING' ? 'All Lecturer and Mentor registration requests have been reviewed.' : 'Try changing the search, role, or status filters.'}
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[860px] text-sm">
              <thead className="bg-slate-50/80">
                <tr className="border-b border-slate-100">
                  <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wider text-slate-400">Applicant</th>
                  <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wider text-slate-400">Role</th>
                  <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wider text-slate-400">Organization / Expertise</th>
                  <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wider text-slate-400">Submitted</th>
                  <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wider text-slate-400">Status</th>
                  <th className="px-5 py-3 text-right text-[11px] font-semibold uppercase tracking-wider text-slate-400">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {filteredRequests.map((request) => (
                  <tr key={request.id} className="transition-colors hover:bg-slate-50/70">
                    <td className="px-5 py-4">
                      <div className="flex items-center gap-3">
                        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-primary-100 to-primary-200 text-xs font-bold text-primary">{initials(request.fullName)}</div>
                        <div className="min-w-0">
                          <p className="truncate font-semibold text-slate-900">{request.fullName}</p>
                          <p className="truncate text-xs text-slate-500">{request.email}</p>
                        </div>
                      </div>
                    </td>
                    <td className="px-5 py-4"><RoleBadge role={request.role} /></td>
                    <td className="max-w-[240px] px-5 py-4">
                      <p className="truncate text-sm font-medium text-slate-700">{request.institution || request.department || 'Not provided'}</p>
                      <p className="mt-0.5 truncate text-xs text-slate-400">{request.expertise || 'No expertise information'}</p>
                    </td>
                    <td className="px-5 py-4 text-xs text-slate-500">{formatDate(request.submittedAt)}</td>
                    <td className="px-5 py-4"><StatusBadge status={request.status} /></td>
                    <td className="px-5 py-4">
                      <div className="flex items-center justify-end gap-2">
                        <Button size="sm" variant="ghost" icon={Eye} onClick={() => setDetailRequest(request)}>Details</Button>
                        {request.status === 'PENDING' && (
                          <>
                            <Button size="sm" variant="outline" className="border-red-200 text-red-600 hover:border-red-300 hover:bg-red-50" icon={X} disabled={processingRequestId !== null} onClick={() => openReject(request)}>Reject</Button>
                            <Button size="sm" className="bg-green-600 hover:bg-green-700" icon={Check} isLoading={processingRequestId === request.id} disabled={processingRequestId !== null} onClick={() => void approve(request)}>Approve</Button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!loading && filteredRequests.length > 0 && (
          <div className="border-t border-slate-100 bg-slate-50/50 px-5 py-3 text-xs text-slate-500">Showing {filteredRequests.length} of {requests.length} registration requests</div>
        )}
      </section>

      {detailRequest && (
        <ApprovalDetailsModal
          request={detailRequest}
          onClose={() => setDetailRequest(null)}
          onApprove={() => void approve(detailRequest)}
          onReject={() => openReject(detailRequest)}
          isProcessing={processingRequestId === detailRequest.id}
        />
      )}

      {rejectRequest && (
        <RejectAccountModal
          request={rejectRequest}
          onClose={() => processingRequestId === null && setRejectRequest(null)}
          onConfirm={(reason) => reject(rejectRequest, reason)}
          isProcessing={processingRequestId === rejectRequest.id}
        />
      )}
    </div>
  );
}

interface StatCardProps {
  label: string;
  value: number;
  icon: typeof Clock3;
  tone: 'amber' | 'blue' | 'purple' | 'green';
}

const STAT_TONES = {
  amber: 'bg-amber-100 text-amber-600',
  blue: 'bg-secondary-100 text-secondary',
  purple: 'bg-purple-100 text-purple-600',
  green: 'bg-green-100 text-green-600',
};

function StatCard({ label, value, icon: Icon, tone }: StatCardProps) {
  return (
    <div className="flex items-center gap-3 rounded-2xl border border-slate-200/70 bg-white p-4 shadow-sm">
      <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${STAT_TONES[tone]}`}><Icon className="h-5 w-5" /></div>
      <div>
        <p className="text-xl font-bold text-slate-900">{value}</p>
        <p className="text-xs text-slate-500">{label}</p>
      </div>
    </div>
  );
}

function RoleBadge({ role }: Pick<AccountApprovalRequest, 'role'>) {
  const Icon = role === 'LECTURER' ? GraduationCap : BriefcaseBusiness;
  return <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${ROLE_STYLES[role]}`}><Icon className="h-3.5 w-3.5" />{role === 'LECTURER' ? 'Lecturer' : 'Mentor'}</span>;
}

function StatusBadge({ status }: Pick<AccountApprovalRequest, 'status'>) {
  return <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${STATUS_STYLES[status]}`}>{STATUS_LABELS[status]}</span>;
}

interface ApprovalDetailsModalProps {
  request: AccountApprovalRequest;
  onClose: () => void;
  onApprove: () => void;
  onReject: () => void;
  isProcessing: boolean;
}

function ApprovalDetailsModal({ request, onClose, onApprove, onReject, isProcessing }: ApprovalDetailsModalProps) {
  return (
    <div className="fixed inset-0 z-[80] flex items-end justify-center p-0 sm:items-center sm:p-6" role="dialog" aria-modal="true" aria-labelledby="approval-detail-title">
      <button type="button" className="absolute inset-0 bg-slate-900/45 backdrop-blur-sm" onClick={onClose} aria-label="Close account details" />
      <div className="relative max-h-[92vh] w-full max-w-2xl overflow-y-auto rounded-t-2xl border border-slate-200 bg-white shadow-float sm:rounded-2xl">
        <header className="flex items-start justify-between border-b border-slate-100 p-5 sm:p-6">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-primary-100 to-primary-200 text-sm font-bold text-primary">{initials(request.fullName)}</div>
            <div>
              <h2 id="approval-detail-title" className="text-lg font-bold text-slate-900">{request.fullName}</h2>
              <div className="mt-1 flex flex-wrap gap-2"><RoleBadge role={request.role} /><StatusBadge status={request.status} /></div>
            </div>
          </div>
          <button type="button" onClick={onClose} className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700" aria-label="Close"><X className="h-5 w-5" /></button>
        </header>

        <div className="space-y-5 p-5 sm:p-6">
          <section>
            <h3 className="text-xs font-bold uppercase tracking-wider text-slate-400">Contact information</h3>
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <DetailItem icon={Mail} label="Email" value={request.email} />
              <DetailItem icon={Phone} label="Phone" value={request.phone || 'Not provided'} />
              <DetailItem icon={Building2} label="Institution" value={request.institution || 'Not provided'} />
              <DetailItem icon={CalendarDays} label="Submitted" value={formatDate(request.submittedAt)} />
            </div>
          </section>

          <section className="rounded-2xl border border-slate-200 bg-slate-50/70 p-4">
            <h3 className="text-xs font-bold uppercase tracking-wider text-slate-400">Professional information</h3>
            <div className="mt-3 space-y-3">
              <DetailRow label="Department" value={request.department || 'Not provided'} />
              <DetailRow label="Expertise" value={request.expertise || 'Not provided'} />
              <DetailRow label="Registration note" value={request.note || 'No additional note'} />
            </div>
          </section>

          {request.status === 'REJECTED' && request.rejectionReason && (
            <div className="flex items-start gap-3 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
              <MessageSquareText className="mt-0.5 h-4 w-4 shrink-0" />
              <div><p className="font-semibold">Rejection reason</p><p className="mt-1 text-xs">{request.rejectionReason}</p></div>
            </div>
          )}
        </div>

        <footer className="flex flex-col-reverse gap-2 border-t border-slate-100 bg-slate-50/70 p-4 sm:flex-row sm:justify-end sm:px-6">
          <Button variant="outline" disabled={isProcessing} onClick={onClose}>Close</Button>
          {request.status === 'PENDING' && (
            <>
              <Button variant="danger" icon={UserX} disabled={isProcessing} onClick={onReject}>Reject request</Button>
              <Button className="bg-green-600 hover:bg-green-700" icon={UserCheck} isLoading={isProcessing} onClick={onApprove}>Approve account</Button>
            </>
          )}
        </footer>
      </div>
    </div>
  );
}

interface DetailItemProps {
  icon: typeof Mail;
  label: string;
  value: string;
}

function DetailItem({ icon: Icon, label, value }: DetailItemProps) {
  return <div className="flex items-start gap-3 rounded-xl border border-slate-200 bg-white p-3"><Icon className="mt-0.5 h-4 w-4 shrink-0 text-primary" /><div className="min-w-0"><p className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">{label}</p><p className="mt-0.5 break-words text-sm font-medium text-slate-700">{value}</p></div></div>;
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return <div><p className="text-xs font-semibold text-slate-500">{label}</p><p className="mt-1 whitespace-pre-wrap text-sm text-slate-700">{value}</p></div>;
}

interface RejectAccountModalProps {
  request: AccountApprovalRequest;
  onClose: () => void;
  onConfirm: (reason: string) => Promise<void>;
  isProcessing: boolean;
}

function RejectAccountModal({ request, onClose, onConfirm, isProcessing }: RejectAccountModalProps) {
  const [reason, setReason] = useState('');
  const [attempted, setAttempted] = useState(false);
  const error = validateRejectionReason(reason);

  const submit = async () => {
    setAttempted(true);
    if (error) return;
    await onConfirm(reason.trim());
  };

  return (
    <div className="fixed inset-0 z-[85] flex items-end justify-center p-0 sm:items-center sm:p-6" role="dialog" aria-modal="true" aria-labelledby="reject-account-title">
      <button type="button" className="absolute inset-0 bg-slate-900/45 backdrop-blur-sm" onClick={onClose} aria-label="Close rejection dialog" />
      <div className="relative w-full max-w-lg rounded-t-2xl border border-slate-200 bg-white shadow-float sm:rounded-2xl">
        <header className="flex items-start justify-between border-b border-slate-100 p-5">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-red-100 text-red-600"><UserX className="h-5 w-5" /></div>
            <div><h2 id="reject-account-title" className="font-bold text-slate-900">Reject registration</h2><p className="mt-0.5 text-xs text-slate-500">{request.fullName} · {request.role === 'LECTURER' ? 'Lecturer' : 'Mentor'}</p></div>
          </div>
          <button type="button" onClick={onClose} className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100" aria-label="Close"><X className="h-5 w-5" /></button>
        </header>
        <div className="p-5">
          <label htmlFor="rejection-reason" className="mb-1.5 block text-sm font-semibold text-slate-700">Reason for rejection <span className="text-red-500">*</span></label>
          <textarea id="rejection-reason" value={reason} onChange={(event) => setReason(event.target.value)} maxLength={500} rows={5} placeholder="Explain why this registration cannot be approved..." className={`w-full resize-none rounded-xl border px-3 py-2.5 text-sm outline-none focus:ring-2 ${attempted && error ? 'border-red-300 bg-red-50 focus:ring-red-100' : 'border-slate-200 focus:border-primary focus:ring-primary/20'}`} />
          <div className="mt-1 flex items-start justify-between gap-3"><p className="text-xs text-red-600">{attempted ? error : ''}</p><p className="shrink-0 text-xs text-slate-400">{reason.length}/500</p></div>
        </div>
        <footer className="flex justify-end gap-2 border-t border-slate-100 bg-slate-50/70 p-4">
          <Button variant="outline" disabled={isProcessing} onClick={onClose}>Cancel</Button>
          <Button variant="danger" icon={UserX} isLoading={isProcessing} onClick={() => void submit()}>Reject registration</Button>
        </footer>
      </div>
    </div>
  );
}
