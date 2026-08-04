import { useState, useEffect } from 'react';
import { Search, Filter, Plus, Edit, Trash2, Users, ArrowLeft, ArrowRight, Check, X, Shield, Phone, Mail, GraduationCap, UserCheck } from 'lucide-react';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import Modal from '../../components/ui/Modal';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import EmptyState from '../../components/ui/EmptyState';
import toast from 'react-hot-toast';
import { userApi } from '../../api/userApi';
import { parseApiError } from '../../utils/apiError';
import { PROGRAM_GROUPS } from '../../constants/majors';

const roleBadge = { ADMIN: 'Approved', LECTURER: 'Submitted', MENTOR: 'Review', STUDENT: 'Reviewed' };
const roleLabel = { ADMIN: 'Admin', LECTURER: 'Lecturer', MENTOR: 'Mentor', STUDENT: 'Student' };

const statusBadge = { PENDING: 'Review', APPROVED: 'Approved', REJECTED: 'Overdue' };
const statusLabel = { PENDING: 'Pending Approval', APPROVED: 'Approved', REJECTED: 'Rejected' };

export default function UserManagement() {
  const [users, setUsers] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  // Pagination & Filter state
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState('ALL');
  const [statusFilter, setStatusFilter] = useState('ALL');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalItems, setTotalItems] = useState(0);

  // Modal state
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<any | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [editingUser, setEditingUser] = useState<any | null>(null);
  const [rowActionId, setRowActionId] = useState<string | null>(null);
  
  const [formData, setFormData] = useState({
    name: '',
    email: '',
    password: '',
    role: 'STUDENT',
    status: 'APPROVED',
    phone: '',
    studentId: '',
    programGroup: 'BIT',
    major: 'BIT_SE',
  });

  // Debounce search
  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 400);
    return () => clearTimeout(handler);
  }, [search]);

  const fetchUsers = async () => {
    try {
      setLoading(true);
      const params: {
        page: number;
        limit: number;
        search?: string;
        role?: string;
        status?: string;
      } = { page, limit: 10 };
      if (debouncedSearch) params.search = debouncedSearch;
      if (roleFilter !== 'ALL') params.role = roleFilter;
      if (statusFilter !== 'ALL') params.status = statusFilter;

      const res = await userApi.getAll(params);
      const payload = res.data?.data || res.data || res;
      const list = payload.users || [];
      setUsers(Array.isArray(list) ? list : []);
      if (payload.pagination) {
        setTotalPages(payload.pagination.pages);
        setTotalItems(payload.pagination.total);
      } else {
        setTotalItems(list.length);
        setTotalPages(1);
      }
    } catch {
      toast.error('Failed to load users');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchUsers();
  }, [page, debouncedSearch, roleFilter, statusFilter]);

  const handleStatusUpdate = async (userId: string, newStatus: string) => {
    setRowActionId(userId);
    try {
      if (newStatus === 'APPROVED') {
        await userApi.approveUser(userId);
      } else if (newStatus === 'REJECTED') {
        await userApi.rejectUser(userId);
      } else {
        await userApi.update(userId, { status: newStatus });
      }
      toast.success(`User marked as ${newStatus}`);
      fetchUsers();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to update user status').message);
    } finally {
      setRowActionId(null);
    }
  };

  const openAddModal = () => {
    setEditingUser(null);
    setFormData({
      name: '',
      email: '',
      password: '',
      role: 'STUDENT',
      status: 'APPROVED',
      phone: '',
      studentId: '',
      programGroup: 'BIT',
      major: 'BIT_SE',
    });
    setIsModalOpen(true);
  };

  const openEditModal = (user: any) => {
    setEditingUser(user);
    const pGroup = user.programGroup || 'BIT';
    const groupItem = PROGRAM_GROUPS.find(g => g.code === pGroup);
    const defaultMajor = groupItem?.majors[0]?.code || 'BIT_SE';

    setFormData({
      name: user.name || '',
      email: user.email || '',
      password: '',
      role: user.role || 'STUDENT',
      status: user.status || 'APPROVED',
      phone: user.phone || '',
      studentId: user.studentId || '',
      programGroup: pGroup,
      major: user.major || defaultMajor,
    });
    setIsModalOpen(true);
  };

  const handleDelete = (id: string, name: string) => {
    setDeleteTarget({ id, name });
  };

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setIsDeleting(true);
    try {
      await userApi.delete(deleteTarget.id);
      fetchUsers();
      toast.success('User account deleted successfully!');
      setDeleteTarget(null);
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to delete user').message);
    } finally {
      setIsDeleting(false);
    }
  };

  const handleSubmit = async () => {
    if (!formData.name.trim()) {
      toast.error('Full name is required');
      return;
    }
    if (!formData.email.trim() || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email.trim())) {
      toast.error('A valid email address is required');
      return;
    }
    if (!editingUser && (!formData.password || formData.password.length < 6)) {
      toast.error('Temporary password must contain at least 6 characters');
      return;
    }
    if (formData.role === 'STUDENT') {
      if (!formData.studentId.trim()) {
        toast.error('Student ID is required for student accounts');
        return;
      }
      if (!formData.programGroup || !formData.major) {
        toast.error('Program group and major are required for student accounts');
        return;
      }
    }

    setIsSubmitting(true);
    try {
      if (editingUser) {
        await userApi.update(editingUser.id || editingUser._id, formData);
        toast.success('User updated successfully!');
      } else {
        await userApi.create(formData);
        toast.success('User account created successfully!');
      }
      setIsModalOpen(false);
      fetchUsers();
    } catch (err) {
      toast.error(parseApiError(err, 'Failed to save user').message);
    } finally {
      setIsSubmitting(false);
    }
  };

  const currentProgramGroup = PROGRAM_GROUPS.find(g => g.code === formData.programGroup);
  const availableMajors = currentProgramGroup?.majors || [];

  return (
    <div className="space-y-6">
      {/* ── Header ── */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-end gap-4">
        <div>
          <h1 className="text-2xl sm:text-3xl font-bold text-slate-900">User Management</h1>
          <p className="text-slate-500 mt-1">{totalItems} user accounts registered on the platform</p>
        </div>
        <Button variant="primary" icon={Plus} onClick={openAddModal}>Create User</Button>
      </div>

      {/* ── Search & Filters ── */}
      <div className="flex flex-col sm:flex-row gap-3">
        <div className="relative flex-1">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            className="w-full bg-white border border-slate-200 rounded-xl pl-10 pr-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            placeholder="Search users by name, email, student ID..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <div className="relative w-full sm:w-48 shrink-0">
          <Filter className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <select
            className="w-full bg-white border border-slate-200 rounded-xl pl-10 pr-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary appearance-none cursor-pointer"
            value={roleFilter}
            onChange={(e) => { setRoleFilter(e.target.value); setPage(1); }}
          >
            <option value="ALL">All Roles</option>
            <option value="STUDENT">Students</option>
            <option value="LECTURER">Lecturers</option>
            <option value="MENTOR">Mentors</option>
            <option value="ADMIN">Admins</option>
          </select>
        </div>
        <div className="relative w-full sm:w-44 shrink-0">
          <Filter className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <select
            className="w-full bg-white border border-slate-200 rounded-xl pl-10 pr-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary appearance-none cursor-pointer"
            value={statusFilter}
            onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
          >
            <option value="ALL">All Status</option>
            <option value="APPROVED">Approved</option>
            <option value="PENDING">Pending Approval</option>
            <option value="REJECTED">Rejected</option>
          </select>
        </div>
      </div>

      {/* ── Table ── */}
      <div className="bg-white border border-slate-200/60 rounded-2xl shadow-sm overflow-hidden">
        {loading ? (
          <div className="p-6"><LoadingSkeleton lines={6} /></div>
        ) : users.length === 0 ? (
          <div className="p-12"><EmptyState icon={Users} title="No users found" description="Try adjusting your search or filters" /></div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[700px]">
              <thead>
                <tr className="border-b border-slate-100 bg-slate-50/60">
                  <th className="py-3.5 px-6 text-xs text-slate-400 uppercase text-left font-semibold tracking-wider">User</th>
                  <th className="py-3.5 px-6 text-xs text-slate-400 uppercase text-left font-semibold tracking-wider">Role</th>
                  <th className="py-3.5 px-6 text-xs text-slate-400 uppercase text-left font-semibold tracking-wider">Status</th>
                  <th className="py-3.5 px-6 text-xs text-slate-400 uppercase text-left font-semibold tracking-wider">Student ID / Major</th>
                  <th className="py-3.5 px-6 text-xs text-slate-400 uppercase text-left font-semibold tracking-wider">Joined Date</th>
                  <th className="py-3.5 px-6 text-xs text-slate-400 uppercase text-right font-semibold tracking-wider">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {users.map(user => (
                  <tr key={user.id || user._id} className="hover:bg-primary-50/20 transition-colors group">
                    <td className="py-3.5 px-6">
                      <div className="flex items-center gap-3">
                        {user.avatar ? (
                          <img src={user.avatar} alt="" className="w-9 h-9 rounded-xl object-cover shrink-0" />
                        ) : (
                          <div className="w-9 h-9 rounded-xl bg-primary-100 flex items-center justify-center font-bold text-primary shrink-0">
                            {user.name?.charAt(0)?.toUpperCase() || '?'}
                          </div>
                        )}
                        <div className="min-w-0">
                          <div className="font-semibold text-slate-900 truncate">{user.name}</div>
                          <div className="text-xs text-slate-400 truncate flex items-center gap-1">
                            <Mail className="w-3 h-3 inline" /> {user.email}
                          </div>
                        </div>
                      </div>
                    </td>
                    <td className="py-3.5 px-6">
                      <Badge variant={roleBadge[user.role] || 'Default'} size="sm">
                        {roleLabel[user.role] || user.role}
                      </Badge>
                    </td>
                    <td className="py-3.5 px-6">
                      <Badge variant={statusBadge[user.status || 'APPROVED'] || 'Default'} size="sm">
                        {statusLabel[user.status || 'APPROVED'] || user.status}
                      </Badge>
                    </td>
                    <td className="py-3.5 px-6">
                      {user.studentId || user.major ? (
                        <div>
                          <div className="text-sm font-semibold text-slate-800 font-mono">{user.studentId || '—'}</div>
                          <div className="text-xs text-slate-400">{user.major || '—'}</div>
                        </div>
                      ) : (
                        <span className="text-xs text-slate-400">—</span>
                      )}
                    </td>
                    <td className="py-3.5 px-6 text-xs text-slate-400">
                      {user.createdAt ? new Date(user.createdAt).toLocaleDateString() : '—'}
                    </td>
                    <td className="py-3.5 px-6 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        {user.status === 'PENDING' && (
                          <>
                            <button
                              type="button"
                              aria-label={`Approve ${user.name}`}
                              title="Approve User"
                              disabled={rowActionId === (user.id || user._id)}
                              className="p-1.5 rounded-lg text-emerald-600 hover:bg-emerald-50 transition-all border border-emerald-200"
                              onClick={() => handleStatusUpdate(user.id || user._id, 'APPROVED')}
                            >
                              <Check className="w-4 h-4" />
                            </button>
                            <button
                              type="button"
                              aria-label={`Reject ${user.name}`}
                              title="Reject User"
                              disabled={rowActionId === (user.id || user._id)}
                              className="p-1.5 rounded-lg text-red-600 hover:bg-red-50 transition-all border border-red-200"
                              onClick={() => handleStatusUpdate(user.id || user._id, 'REJECTED')}
                            >
                              <X className="w-4 h-4" />
                            </button>
                          </>
                        )}
                        <button
                          type="button"
                          aria-label={`Edit ${user.name}`}
                          title="Edit User"
                          className="p-1.5 rounded-lg text-slate-400 hover:text-primary hover:bg-primary-50 transition-all"
                          onClick={() => openEditModal(user)}
                        >
                          <Edit className="w-4 h-4" />
                        </button>
                        <button
                          type="button"
                          aria-label={`Delete ${user.name}`}
                          title="Delete User"
                          className="p-1.5 rounded-lg text-slate-400 hover:text-red-500 hover:bg-red-50 transition-all"
                          onClick={() => handleDelete(user.id || user._id, `${user.name} (${user.email})`)}
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination Controls */}
        {totalPages > 1 && (
          <div className="border-t border-slate-100 px-6 py-3.5 bg-slate-50/50 flex items-center justify-between">
            <span className="text-xs text-slate-500">
              Page <span className="font-semibold text-slate-900">{page}</span> of <span className="font-semibold text-slate-900">{totalPages}</span> ({totalItems} items)
            </span>
            <div className="flex items-center gap-2">
              <Button variant="outline" size="sm" disabled={page === 1} onClick={() => setPage(p => Math.max(1, p - 1))}>
                <ArrowLeft className="w-4 h-4 mr-1" /> Prev
              </Button>
              <Button variant="outline" size="sm" disabled={page === totalPages} onClick={() => setPage(p => Math.min(totalPages, p + 1))}>
                Next <ArrowRight className="w-4 h-4 ml-1" />
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* ── Add / Edit User Modal ── */}
      <Modal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        title={editingUser ? 'Edit User Profile' : 'Create New User Account'}
        submitText={editingUser ? 'Save Changes' : 'Create User'}
        isSubmitting={isSubmitting}
        onSubmit={handleSubmit}
      >
        <div className="space-y-4 text-sm">
          <div>
            <label className="block font-medium text-slate-700 mb-1">Full Name *</label>
            <input
              type="text"
              placeholder="e.g. Nguyen Van A"
              className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
            />
          </div>

          <div>
            <label className="block font-medium text-slate-700 mb-1">Email Address *</label>
            <input
              type="email"
              placeholder="user@fpt.edu.vn"
              className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              value={formData.email}
              onChange={(e) => setFormData({ ...formData, email: e.target.value })}
            />
          </div>

          {!editingUser && (
            <div>
              <label className="block font-medium text-slate-700 mb-1">Temporary Password *</label>
              <input
                type="password"
                minLength={6}
                placeholder="At least 6 characters"
                className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                value={formData.password}
                onChange={(e) => setFormData({ ...formData, password: e.target.value })}
              />
            </div>
          )}

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700 mb-1">System Role *</label>
              <select
                className="w-full border border-slate-200 rounded-xl px-3 py-2.5 outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary cursor-pointer"
                value={formData.role}
                onChange={(e) => {
                  const role = e.target.value;
                  setFormData({ ...formData, role });
                }}
              >
                <option value="STUDENT">Student</option>
                <option value="LECTURER">Lecturer</option>
                <option value="MENTOR">Mentor</option>
                <option value="ADMIN">Admin</option>
              </select>
            </div>

            <div>
              <label className="block font-medium text-slate-700 mb-1">Account Status *</label>
              <select
                className="w-full border border-slate-200 rounded-xl px-3 py-2.5 outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary cursor-pointer"
                value={formData.status}
                onChange={(e) => setFormData({ ...formData, status: e.target.value })}
              >
                <option value="APPROVED">Approved</option>
                <option value="PENDING">Pending Approval</option>
                <option value="REJECTED">Rejected</option>
              </select>
            </div>
          </div>

          <div>
            <label className="block font-medium text-slate-700 mb-1">Phone Number (optional)</label>
            <input
              type="text"
              placeholder="e.g. 0901234567"
              className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              value={formData.phone}
              onChange={(e) => setFormData({ ...formData, phone: e.target.value })}
            />
          </div>

          {/* Student Profile Section */}
          {formData.role === 'STUDENT' && (
            <div className="pt-2 border-t border-slate-100 space-y-3 bg-slate-50/70 p-3.5 rounded-xl border border-slate-200/60">
              <div className="flex items-center gap-2 text-xs font-semibold text-slate-700">
                <GraduationCap className="w-4 h-4 text-primary" /> Student Details
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-700 mb-1">Student ID (RollNumber) *</label>
                <input
                  type="text"
                  placeholder="e.g. SE170001"
                  className="w-full border border-slate-200 rounded-xl px-3 py-2 bg-white outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary font-mono text-sm uppercase"
                  value={formData.studentId}
                  onChange={(e) => setFormData({ ...formData, studentId: e.target.value.toUpperCase() })}
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-slate-700 mb-1">Program Group *</label>
                  <select
                    className="w-full border border-slate-200 rounded-xl px-3 py-2 bg-white text-sm outline-none focus:ring-2 focus:ring-primary/20 cursor-pointer"
                    value={formData.programGroup}
                    onChange={(e) => {
                      const groupCode = e.target.value;
                      const group = PROGRAM_GROUPS.find(g => g.code === groupCode);
                      setFormData({
                        ...formData,
                        programGroup: groupCode,
                        major: group?.majors[0]?.code || '',
                      });
                    }}
                  >
                    {PROGRAM_GROUPS.filter(g => ['BIT', 'BBA', 'BLA'].includes(g.code)).map(g => (
                      <option key={g.code} value={g.code}>{g.name} ({g.code})</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-medium text-slate-700 mb-1">Major Specialization *</label>
                  <select
                    className="w-full border border-slate-200 rounded-xl px-3 py-2 bg-white text-sm outline-none focus:ring-2 focus:ring-primary/20 cursor-pointer"
                    value={formData.major}
                    onChange={(e) => setFormData({ ...formData, major: e.target.value })}
                  >
                    {availableMajors.map(m => (
                      <option key={m.code} value={m.code}>{m.name} ({m.code})</option>
                    ))}
                  </select>
                </div>
              </div>
            </div>
          )}
        </div>
      </Modal>

      {/* ── Confirm Delete Dialog ── */}
      <ConfirmDialog
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onConfirm={confirmDelete}
        title="Delete User Account"
        description={`Are you sure you want to delete ${deleteTarget?.name}? This action cannot be undone.`}
        isSubmitting={isDeleting}
      />
    </div>
  );
}
