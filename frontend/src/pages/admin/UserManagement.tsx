// @ts-nocheck
import { useState, useEffect } from 'react';
import { Search, Filter, Plus, Edit, Trash2, Users, ArrowLeft, ArrowRight, Check, X } from 'lucide-react';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import Modal from '../../components/ui/Modal';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import EmptyState from '../../components/ui/EmptyState';
import toast from 'react-hot-toast';
import { userApi } from '../../api/userApi';

const roleBadge = { ADMIN: 'Approved', LECTURER: 'Submitted', MENTOR: 'Review', STUDENT: 'Reviewed' };
const roleLabel = { ADMIN: 'Admin', LECTURER: 'Lecturer', MENTOR: 'Mentor', STUDENT: 'Student' };

const statusBadge = { PENDING: 'Review', APPROVED: 'Approved', REJECTED: 'Overdue' };
const statusLabel = { PENDING: 'Pending', APPROVED: 'Approved', REJECTED: 'Rejected' };

const UserManagement = () => {
  const [users, setUsers] = useState([]);
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
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [editingUser, setEditingUser] = useState(null);
  const [formData, setFormData] = useState({ name: '', email: '', role: 'STUDENT', status: 'APPROVED' });

  // Debounce search
  useEffect(() => {
    const handler = setTimeout(() => { setDebouncedSearch(search); setPage(1); }, 500);
    return () => clearTimeout(handler);
  }, [search]);

  const fetchUsers = async () => {
    try {
      setLoading(true);
      const params = { page, limit: 10 };
      if (debouncedSearch) params.search = debouncedSearch;
      if (roleFilter !== 'ALL') params.role = roleFilter;
      if (statusFilter !== 'ALL') params.status = statusFilter;
      
      const res = await userApi.getAll(params);
      const list = res.data?.users || res.users || res.data || [];
      setUsers(Array.isArray(list) ? list : []);
      if (res.data?.pagination) {
        setTotalPages(res.data.pagination.pages);
        setTotalItems(res.data.pagination.total);
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

  // eslint-disable-next-line react-hooks/set-state-in-effect, react-hooks/exhaustive-deps
  useEffect(() => { fetchUsers(); }, [page, debouncedSearch, roleFilter, statusFilter]);

  const handleStatusUpdate = async (userId, newStatus) => {
    try {
      await userApi.update(userId, { status: newStatus });
      toast.success(`User marked as ${newStatus}`);
      fetchUsers();
    } catch {
      toast.error('Failed to update status');
    }
  };

  const openAddModal = () => {
    setEditingUser(null);
    setFormData({ name: '', email: '', role: 'STUDENT' });
    setIsModalOpen(true);
  };

  const openEditModal = (user) => {
    setEditingUser(user);
    setFormData({ name: user.name, email: user.email, role: user.role, status: user.status || 'APPROVED' });
    setIsModalOpen(true);
  };

  const handleDelete = (id, name) => {
    setDeleteTarget({ id, name });
  };

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setIsDeleting(true);
    try {
      await userApi.delete(deleteTarget.id);
      fetchUsers();
      toast.success('User deleted!');
      setDeleteTarget(null);
    } catch {
      toast.error('Failed to delete user');
    } finally {
      setIsDeleting(false);
    }
  };

  const handleSubmit = async () => {
    if (!formData.name || !formData.email) { toast.error('Fill in all required fields'); return; }
    setIsSubmitting(true);
    try {
      if (editingUser) {
        await userApi.update(editingUser._id, formData);
        toast.success('User updated!');
      } else {
        toast.error('Use Register flow to add new users');
      }
      setIsModalOpen(false);
      fetchUsers();
    } catch (err) {
      toast.error(err.message || 'Failed to save');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-end gap-4">
        <div>
          <h1 className="text-2xl sm:text-3xl font-bold text-slate-900">User Management</h1>
          <p className="text-slate-500 mt-1">{totalItems} users registered on the platform</p>
        </div>
        <Button variant="primary" icon={Plus} onClick={openAddModal}>Invite User</Button>
      </div>

      <div className="flex flex-col sm:flex-row gap-3">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            className="w-full bg-white border border-slate-200 rounded-xl pl-10 pr-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            placeholder="Search users by name, email..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <div className="relative w-full sm:w-48 shrink-0">
          <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <select
            className="w-full bg-white border border-slate-200 rounded-xl pl-10 pr-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary appearance-none"
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
        <div className="relative w-full sm:w-40 shrink-0">
          <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <select
            className="w-full bg-white border border-slate-200 rounded-xl pl-10 pr-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary appearance-none"
            value={statusFilter}
            onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
          >
            <option value="ALL">All Status</option>
            <option value="PENDING">Pending</option>
            <option value="APPROVED">Approved</option>
            <option value="REJECTED">Rejected</option>
          </select>
        </div>
      </div>

      <div className="bg-white border border-slate-200/60 rounded-2xl shadow-sm overflow-hidden">
        {loading ? (
          <div className="p-6"><LoadingSkeleton lines={5} /></div>
        ) : users.length === 0 ? (
          <div className="p-12"><EmptyState icon={Users} title="No users found" description="Try adjusting your search or filters" /></div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[600px]">
              <thead>
                <tr className="border-b border-slate-100 bg-slate-50/60">
                  <th className="py-3 px-6 text-xs text-slate-400 uppercase text-left font-semibold tracking-wider">User</th>
                  <th className="py-3 px-6 text-xs text-slate-400 uppercase text-left font-semibold tracking-wider">Role</th>
                  <th className="py-3 px-6 text-xs text-slate-400 uppercase text-left font-semibold tracking-wider">Status</th>
                  <th className="py-3 px-6 text-xs text-slate-400 uppercase text-left font-semibold tracking-wider">Student ID</th>
                  <th className="py-3 px-6 text-xs text-slate-400 uppercase text-left font-semibold tracking-wider">Joined</th>
                  <th className="py-3 px-6 text-xs text-slate-400 uppercase text-right font-semibold tracking-wider">Actions</th>
                </tr>
              </thead>
              <tbody>
                {users.map(user => (
                  <tr key={user._id} className="border-b border-slate-50 hover:bg-primary-50/20 transition-colors group">
                    <td className="py-3.5 px-6">
                      <div className="flex items-center gap-3">
                        <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-primary-100 to-primary-200 flex items-center justify-center font-bold text-primary shrink-0">
                          {user.name?.charAt(0)?.toUpperCase()}
                        </div>
                        <div className="min-w-0">
                          <div className="font-semibold text-slate-900 truncate">{user.name}</div>
                          <div className="text-sm text-slate-400 truncate">{user.email}</div>
                        </div>
                      </div>
                    </td>
                    <td className="py-3.5 px-6"><Badge variant={roleBadge[user.role]} size="sm">{roleLabel[user.role]}</Badge></td>
                    <td className="py-3.5 px-6"><Badge variant={statusBadge[user.status || 'APPROVED']} size="sm">{statusLabel[user.status || 'APPROVED']}</Badge></td>
                    <td className="py-3.5 px-6 text-sm text-slate-500 font-mono">{user.studentId || '—'}</td>
                    <td className="py-3.5 px-6 text-sm text-slate-400">{user.createdAt ? new Date(user.createdAt).toLocaleDateString() : '—'}</td>
                    <td className="py-3.5 px-6 text-right">
                      <div className="flex items-center justify-end gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                        {user.status === 'PENDING' && (
                          <>
                            <button className="p-1.5 rounded-lg text-success-dark hover:bg-success-50 transition-all" onClick={() => handleStatusUpdate(user._id, 'APPROVED')} title="Approve"><Check className="w-4 h-4" /></button>
                            <button className="p-1.5 rounded-lg text-danger hover:bg-danger-50 transition-all" onClick={() => handleStatusUpdate(user._id, 'REJECTED')} title="Reject"><X className="w-4 h-4" /></button>
                          </>
                        )}
                        <button className="p-1.5 rounded-lg text-slate-400 hover:text-primary hover:bg-primary-50 transition-all" onClick={() => openEditModal(user)}><Edit className="w-4 h-4" /></button>
                        <button className="p-1.5 rounded-lg text-slate-400 hover:text-red-500 hover:bg-red-50 transition-all" onClick={() => handleDelete(user._id, user.name)}><Trash2 className="w-4 h-4" /></button>
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
            <span className="text-sm text-slate-500">
              Showing page <span className="font-medium text-slate-900">{page}</span> of <span className="font-medium text-slate-900">{totalPages}</span>
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

      <Modal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)} title={editingUser ? 'Edit User' : 'Invite User'} submitText="Save Changes" isSubmitting={isSubmitting} onSubmit={handleSubmit}>
        <div className="space-y-4">
          <div><label className="block text-sm font-medium text-slate-700 mb-1">Full Name *</label><input type="text" className="w-full border border-slate-200 rounded-xl px-3 py-2.5 outline-none text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary" value={formData.name} onChange={(e) => setFormData({ ...formData, name: e.target.value })} /></div>
          <div><label className="block text-sm font-medium text-slate-700 mb-1">Email *</label><input type="email" className="w-full border border-slate-200 rounded-xl px-3 py-2.5 outline-none text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary" value={formData.email} onChange={(e) => setFormData({ ...formData, email: e.target.value })} /></div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Role</label>
            <select className="w-full border border-slate-200 rounded-xl px-3 py-2.5 outline-none bg-white text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary" value={formData.role} onChange={(e) => setFormData({ ...formData, role: e.target.value })}>
              <option value="STUDENT">Student</option>
              <option value="LECTURER">Lecturer</option>
              <option value="MENTOR">Mentor</option>
              <option value="ADMIN">Admin</option>
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Status</label>
            <select className="w-full border border-slate-200 rounded-xl px-3 py-2.5 outline-none bg-white text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary" value={formData.status} onChange={(e) => setFormData({ ...formData, status: e.target.value })}>
              <option value="APPROVED">Approved</option>
              <option value="PENDING">Pending</option>
              <option value="REJECTED">Rejected</option>
            </select>
          </div>
        </div>
      </Modal>

      <ConfirmDialog isOpen={!!deleteTarget} onClose={() => setDeleteTarget(null)} onConfirm={confirmDelete} title={`Delete User`} description={`Are you sure you want to delete ${deleteTarget?.name}? This action cannot be undone.`} isSubmitting={isDeleting} />
    </div>
  );
};
export default UserManagement;
