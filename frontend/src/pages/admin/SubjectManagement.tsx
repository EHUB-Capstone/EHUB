// @ts-nocheck
// src/pages/admin/SubjectManagement.jsx
import { useState, useEffect } from 'react';
import { Search, Filter, Plus, Edit, Trash2, BookOpen, RefreshCw, Calendar, Sparkles } from 'lucide-react';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import Modal from '../../components/ui/Modal';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import EmptyState from '../../components/ui/EmptyState';
import toast from 'react-hot-toast';
import { subjectApi } from '../../api/subjectApi';

const statusBadge = { active: 'Approved', disabled: 'Overdue' };
const statusLabel = { active: 'Active', disabled: 'Disabled' };
const termColor = { SP: 'bg-green-100 text-green-700', SU: 'bg-amber-100 text-amber-700', FA: 'bg-blue-100 text-blue-700' };

const SubjectManagement = () => {
  const [subjects, setSubjects] = useState([]);
  const [loading, setLoading] = useState(true);

  // Filter state
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('ALL');

  // Semester Setting State
  const [currentSemester, setCurrentSemester] = useState({ semester: 'SP', year: new Date().getFullYear() });
  const [selectedSemester, setSelectedSemester] = useState('SP');
  const [selectedYear, setSelectedYear] = useState(new Date().getFullYear());
  const [availableYears, setAvailableYears] = useState([new Date().getFullYear()]);
  const [isDecember, setIsDecember] = useState(false);
  const [savingSemester, setSavingSemester] = useState(false);

  // Modal states
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [editingSubject, setEditingSubject] = useState(null);
  const [disableTarget, setDisableTarget] = useState(null);
  const [isDisabling, setIsDisabling] = useState(false);

  // Form state
  const [formData, setFormData] = useState({
    subjectCode: '',
    subjectName: '',
    status: 'active',
  });

  // Debounce search
  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedSearch(search);
    }, 500);
    return () => clearTimeout(handler);
  }, [search]);

  const fetchSubjects = async () => {
    try {
      setLoading(true);
      const params = {};
      if (debouncedSearch) params.search = debouncedSearch;
      if (statusFilter !== 'ALL') params.status = statusFilter;

      const res = await subjectApi.getAll(params);
      const list = res.data?.subjects || res.subjects || [];
      setSubjects(list);
    } catch (err) {
      toast.error('Failed to load subjects');
    } finally {
      setLoading(false);
    }
  };

  const fetchSemester = async () => {
    try {
      const res = await subjectApi.getCurrentSemester();
      const config = res.data?.currentSemester || res.currentSemester || { semester: 'SP', year: new Date().getFullYear() };
      const years  = res.data?.availableYears   || [new Date().getFullYear()];
      const isDec  = res.data?.isDecember       || false;
      setCurrentSemester(config);
      setSelectedSemester(config.semester);
      setSelectedYear(config.year);
      setAvailableYears(years);
      setIsDecember(isDec);
    } catch (err) {
      console.error('Failed to load active semester configuration');
    }
  };

  useEffect(() => {
    fetchSubjects();
  }, [debouncedSearch, statusFilter]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    fetchSemester();
  }, []);

  const openAddModal = () => {
    setEditingSubject(null);
    setFormData({ subjectCode: '', subjectName: '', status: 'active' });
    setIsModalOpen(true);
  };

  const openEditModal = (subj) => {
    setEditingSubject(subj);
    setFormData({
      subjectCode: subj.subjectCode,
      subjectName: subj.subjectName,
      status: subj.status || 'active',
    });
    setIsModalOpen(true);
  };

  const handleDisableRequest = (subj) => {
    setDisableTarget(subj);
  };

  const confirmDisable = async () => {
    if (!disableTarget) return;
    setIsDisabling(true);
    try {
      await subjectApi.delete(disableTarget._id);
      toast.success(`Subject ${disableTarget.subjectCode} disabled successfully!`);
      setDisableTarget(null);
      fetchSubjects();
    } catch (err) {
      toast.error('Failed to disable subject');
    } finally {
      setIsDisabling(false);
    }
  };

  const handleSaveSemester = async () => {
    setSavingSemester(true);
    try {
      const res = await subjectApi.updateCurrentSemester(selectedSemester, selectedYear);
      const config = res.data?.currentSemester || res.currentSemester;
      setCurrentSemester(config);
      toast.success(`Active semester set to ${config.semester} ${config.year}!`);
    } catch (err) {
      const msg = err.response?.data?.message || 'Failed to update active semester';
      toast.error(msg);
    } finally {
      setSavingSemester(false);
    }
  };

  const handleSubmit = async (e) => {
    if (e && e.preventDefault) e.preventDefault();

    if (!formData.subjectCode || !formData.subjectCode.trim()) {
      toast.error('Subject Code is required');
      return;
    }
    if (!formData.subjectName || !formData.subjectName.trim()) {
      toast.error('Subject Name is required');
      return;
    }

    setIsSubmitting(true);
    try {
      if (editingSubject) {
        await subjectApi.update(editingSubject._id, {
          subjectName: formData.subjectName,
          status: formData.status,
        });
        toast.success('Subject updated successfully!');
      } else {
        await subjectApi.create(formData);
        toast.success('Subject created successfully!');
      }
      setIsModalOpen(false);
      fetchSubjects();
    } catch (err) {
      const errorMsg = err.response?.data?.message || err.message || 'Failed to save subject';
      toast.error(errorMsg);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-end gap-4">
        <div>
          <h1 className="text-2xl sm:text-3xl font-bold text-slate-900">Subject & Semester</h1>
          <p className="text-slate-500 mt-1">Manage academic subjects and active semesters</p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => { fetchSubjects(); fetchSemester(); }}
            className="flex items-center gap-2 px-3 py-2 text-sm border border-slate-200 rounded-xl text-slate-600 hover:bg-slate-50 transition-all bg-white"
          >
            <RefreshCw className="w-4 h-4" /> Refresh
          </button>
          <Button variant="primary" icon={Plus} onClick={openAddModal}>
            Add Subject
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        {/* Left Column: Subjects CRUD */}
        <div className="lg:col-span-3 space-y-6">
          {/* Filter and Search Bar */}
          <div className="flex flex-col sm:flex-row gap-3">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
              <input
                className="w-full bg-white border border-slate-200 rounded-xl pl-10 pr-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                placeholder="Search subjects by code or name..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
            <div className="relative w-full sm:w-48 shrink-0">
              <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
              <select
                className="w-full bg-white border border-slate-200 rounded-xl pl-10 pr-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary appearance-none"
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
              >
                <option value="ALL">All Status</option>
                <option value="active">Active</option>
                <option value="disabled">Disabled</option>
              </select>
            </div>
          </div>

          {/* Subjects Table */}
          <div className="bg-white border border-slate-200/60 rounded-2xl shadow-sm overflow-hidden">
            {loading ? (
              <div className="p-6">
                <LoadingSkeleton lines={5} />
              </div>
            ) : subjects.length === 0 ? (
              <div className="p-12">
                <EmptyState
                  icon={BookOpen}
                  title="No subjects found"
                  description="Try adjusting your search query or filters, or add a new subject."
                  action={{ label: 'Add Subject', onClick: openAddModal }}
                />
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[600px]">
                  <thead>
                    <tr className="border-b border-slate-100 bg-slate-50/60">
                      <th className="py-3 px-6 text-xs text-slate-400 uppercase text-left font-semibold tracking-wider w-1/4">
                        Subject Code
                      </th>
                      <th className="py-3 px-6 text-xs text-slate-400 uppercase text-left font-semibold tracking-wider">
                        Subject Name
                      </th>
                      <th className="py-3 px-6 text-xs text-slate-400 uppercase text-left font-semibold tracking-wider w-40">
                        Status
                      </th>
                      <th className="py-3 px-6 text-xs text-slate-400 uppercase text-right font-semibold tracking-wider w-32">
                        Actions
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {subjects.map((subj) => (
                      <tr
                        key={subj._id}
                        className="border-b border-slate-50 hover:bg-primary-50/20 transition-colors group"
                      >
                        <td className="py-3.5 px-6">
                          <div className="flex items-center gap-3">
                            <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-primary-50 to-primary-100 flex items-center justify-center font-mono font-bold text-primary shrink-0">
                              {subj.subjectCode?.substring(0, 3)}
                            </div>
                            <span className="font-mono font-bold text-slate-900">
                              {subj.subjectCode}
                            </span>
                          </div>
                        </td>
                        <td className="py-3.5 px-6 text-sm text-slate-700 font-medium">
                          {subj.subjectName}
                        </td>
                        <td className="py-3.5 px-6">
                          <Badge variant={statusBadge[subj.status || 'active']} size="sm">
                            {statusLabel[subj.status || 'active']}
                          </Badge>
                        </td>
                        <td className="py-3.5 px-6 text-right">
                          <div className="flex items-center justify-end gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                            <button
                              className="p-1.5 rounded-lg text-slate-400 hover:text-primary hover:bg-primary-50 transition-all"
                              onClick={() => openEditModal(subj)}
                              title="Edit Subject"
                            >
                              <Edit className="w-4 h-4" />
                            </button>
                            {subj.status !== 'disabled' && (
                              <button
                                className="p-1.5 rounded-lg text-slate-400 hover:text-red-500 hover:bg-red-50 transition-all"
                                onClick={() => handleDisableRequest(subj)}
                                  title="Disable Subject"
                              >
                                <Trash2 className="w-4 h-4" />
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>

        {/* Right Column: Current Semester Management */}
        <div className="space-y-6">
          <div className="bg-white border border-slate-200/60 rounded-2xl shadow-sm p-5 space-y-4">
            <div className="flex items-center gap-2 pb-3 border-b border-slate-100">
              <Calendar className="w-5 h-5 text-primary" />
              <h2 className="font-bold text-slate-800 text-base">Active Semester</h2>
            </div>
            
            <div className="space-y-1.5">
              <span className="text-xs text-slate-400 font-medium">Current Setting</span>
              <div className="flex items-center gap-2.5 p-3.5 bg-slate-50 border border-slate-100 rounded-xl">
                <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-primary-500 to-secondary-500 flex items-center justify-center text-white shrink-0 font-bold text-sm">
                  {currentSemester.semester}
                </div>
                <div>
                  <div className="font-bold text-slate-800">
                    {currentSemester.semester} {currentSemester.year}
                  </div>
                  <div className="text-[11px] text-slate-400 font-medium leading-none">
                    Locked configuration active
                  </div>
                </div>
              </div>
            </div>

            <div className="space-y-3 pt-2">
              <div>
                <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">
                  Change Term
                </label>
                <select
                  className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
                  value={selectedSemester}
                  onChange={(e) => setSelectedSemester(e.target.value)}
                >
                  <option value="SP">SP (Spring)</option>
                  <option value="SU">SU (Summer)</option>
                  <option value="FA">FA (Fall)</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">
                  Year
                </label>
                {availableYears.length > 1 ? (
                  <select
                    className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
                    value={selectedYear}
                    onChange={(e) => setSelectedYear(Number(e.target.value))}
                  >
                    {availableYears.map(y => (
                      <option key={y} value={y}>
                        {y}{y === new Date().getFullYear() ? ' (Current)' : ' (Next Year)'}
                      </option>
                    ))}
                  </select>
                ) : (
                  <input
                    type="text"
                    disabled
                    className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-slate-50 text-slate-400 font-medium"
                    value={`${availableYears[0] ?? new Date().getFullYear()} (Current Year)`}
                  />
                )}
                {isDecember && (
                  <p className="text-[11px] text-green-600 font-medium mt-1">
                    ✓ December — you may plan for next year ({new Date().getFullYear() + 1})
                  </p>
                )}
              </div>

              <button
                onClick={handleSaveSemester}
                disabled={savingSemester}
                className="w-full bg-gradient-to-r from-primary to-secondary hover:shadow-glow-primary text-white font-semibold py-2.5 rounded-xl text-sm transition-all active:scale-[0.98] flex items-center justify-center gap-2 mt-2"
              >
                {savingSemester ? 'Saving...' : 'Set Active Semester'}
              </button>
            </div>

            <div className="flex items-start gap-2 p-3 bg-amber-50/50 border border-amber-100/50 rounded-xl text-[11px] text-amber-700 leading-normal">
              <Sparkles className="w-4 h-4 shrink-0 text-amber-500 mt-0.5" />
              <span>
                Setting this will lock class creation to this active semester. Users will not be able to choose other semesters.
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Add / Edit Modal */}
      <Modal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        title={editingSubject ? 'Edit Subject' : 'Add New Subject'}
        submitText={isSubmitting ? 'Saving...' : 'Save Subject'}
        isSubmitting={isSubmitting}
        onSubmit={handleSubmit}
      >
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">
              Subject Code *
            </label>
            <input
              type="text"
              disabled={!!editingSubject}
              placeholder="e.g. EXE301"
              className="w-full border border-slate-200 rounded-xl px-3 py-2.5 outline-none text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary disabled:bg-slate-50 disabled:text-slate-400 font-mono"
              value={formData.subjectCode}
              onChange={(e) => setFormData({ ...formData, subjectCode: e.target.value })}
            />
            {editingSubject && (
              <p className="text-[11px] text-slate-400 mt-1">
                Subject code cannot be changed after creation.
              </p>
            )}
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">
              Subject Name *
            </label>
            <input
              type="text"
              placeholder="e.g. Experiential Entrepreneurship 3"
              className="w-full border border-slate-200 rounded-xl px-3 py-2.5 outline-none text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary"
              value={formData.subjectName}
              onChange={(e) => setFormData({ ...formData, subjectName: e.target.value })}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">
              Status
            </label>
            <select
              className="w-full border border-slate-200 rounded-xl px-3 py-2.5 outline-none bg-white text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary"
              value={formData.status}
              onChange={(e) => setFormData({ ...formData, status: e.target.value })}
            >
              <option value="active">Active</option>
              <option value="disabled">Disabled</option>
            </select>
          </div>
        </div>
      </Modal>

      {/* Soft Disable Confirmation Dialog */}
      <ConfirmDialog
        isOpen={!!disableTarget}
        onClose={() => setDisableTarget(null)}
        onConfirm={confirmDisable}
        title="Disable Subject"
        description={`Are you sure you want to disable the subject ${disableTarget?.subjectCode}? Existing class data will remain, but this subject can no longer be used to create new classes.`}
        isSubmitting={isDisabling}
        confirmText="Disable"
        confirmVariant="danger"
      />
    </div>
  );
};

export default SubjectManagement;
