// @ts-nocheck
import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { motion } from 'framer-motion';
import { Plus, Search, Calendar, Filter } from 'lucide-react';
import { workshopApi } from '../../api/workshopApi';
import { useAuth } from '../../hooks/useAuth';
import WorkshopList from '../../components/workspace/WorkshopList';
import WorkshopForm from '../../components/workspace/WorkshopForm';
import WorkshopCheckInModal from '../../components/workspace/WorkshopCheckInModal';
import WorkshopAttendanceManager from '../../components/workspace/WorkshopAttendanceManager';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';

const Workshops = () => {
  const { user } = useAuth();
  const [workshops, setWorkshops] = useState([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [filter, setFilter] = useState('ALL'); // ALL, UPCOMING, PAST
  const [filterSem, setFilterSem] = useState('');
  const [filterYear, setFilterYear] = useState('');

  const SEMESTERS = ['SP', 'SU', 'FA'];
  const CURRENT_YEAR = new Date().getFullYear();
  const YEARS = Array.from({ length: 5 }, (_, i) => CURRENT_YEAR - 1 + i);

  const getSemesterFromDate = (date) => {
    const d = new Date(date);
    const month = d.getMonth() + 1;
    if (month >= 1 && month <= 4) return 'SP';
    if (month >= 5 && month <= 8) return 'SU';
    return 'FA';
  };
  const getYearFromDate = (date) => {
    return new Date(date).getFullYear().toString();
  };

  // Modal State
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [editingWorkshop, setEditingWorkshop] = useState(null);

  // Check-in and Attendance State
  const [isCheckInOpen, setIsCheckInOpen] = useState(false);
  const [isAttendanceManagerOpen, setIsAttendanceManagerOpen] = useState(false);
  const [selectedWorkshop, setSelectedWorkshop] = useState(null);

  const canCreate = user?.role === 'ADMIN' || user?.role === 'LECTURER';

  const fetchWorkshops = async () => {
    setLoading(true);
    try {
      const res = await workshopApi.getAll();
      const list = res.data?.workshops || res.workshops || res.data || [];
      setWorkshops(Array.isArray(list) ? list : []);
    } catch {
      toast.error('Failed to load workshops');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchWorkshops();
  }, []);

  const handleDelete = async (id) => {
    if (!window.confirm('Are you sure you want to delete this event? This action cannot be undone.')) return;
    try {
      await workshopApi.delete(id);
      toast.success('Event deleted successfully');
      setWorkshops(prev => prev.filter(w => w._id !== id));
    } catch (err) {
      toast.error(err.response?.data?.message || 'Failed to delete event');
    }
  };

  const handleEdit = (workshop) => {
    setEditingWorkshop(workshop);
    setIsFormOpen(true);
  };

  const handleCreate = () => {
    setEditingWorkshop(null);
    setIsFormOpen(true);
  };

  const handleCheckIn = (workshop) => {
    setSelectedWorkshop(workshop);
    setIsCheckInOpen(true);
  };

  const handleManageAttendance = (workshop) => {
    setSelectedWorkshop(workshop);
    setIsAttendanceManagerOpen(true);
  };

  // Filter and Search Logic
  const now = new Date();
  const filteredWorkshops = workshops.filter(ws => {
    const matchesSearch = ws.title?.toLowerCase().includes(searchQuery.toLowerCase()) ||
      ws.description?.toLowerCase().includes(searchQuery.toLowerCase());
    if (!matchesSearch) return false;

    if (filterSem && getSemesterFromDate(ws.startDate) !== filterSem) return false;
    if (filterYear && getYearFromDate(ws.startDate) !== filterYear) return false;

    if (filter === 'UPCOMING') {
      return new Date(ws.startDate) >= now.setHours(0, 0, 0, 0);
    }
    if (filter === 'PAST') {
      return new Date(ws.startDate) < now.setHours(0, 0, 0, 0);
    }
    return true;
  });

  if (loading && workshops.length === 0) return <LoadingSkeleton />;

  return (
    <div className="space-y-6">
      {/* Header */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div>
          <h1 className="text-3xl font-bold text-slate-900 flex items-center gap-2">
            <Calendar className="w-8 h-8 text-primary" />
            <span>Workshops & Seminars</span>
          </h1>
          <p className="text-slate-500 mt-1">
            Discover and manage upcoming knowledge-sharing sessions.
          </p>
        </div>
        {canCreate && (
          <button
            onClick={handleCreate}
            className="flex items-center gap-2 bg-primary text-white px-5 py-2.5 rounded-xl font-semibold hover:shadow-glow-primary active:scale-[0.98] transition-all shrink-0"
          >
            <Plus className="w-5 h-5" />
            Schedule Event
          </button>
        )}
      </motion.div>

      {/* Toolbar */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.05 }} className="flex flex-col sm:flex-row gap-4 bg-white p-2 rounded-2xl shadow-sm border border-slate-200/60 flex-wrap">
        <div className="flex-1 relative group min-w-[200px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400 group-focus-within:text-primary transition-colors" />
          <input
            type="text"
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            className="w-full bg-slate-50 border-none rounded-xl pl-9 pr-4 py-2.5 text-sm text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:bg-white transition-all"
            placeholder="Search events by title or description..."
          />
        </div>

        <select
          value={filterSem}
          onChange={(e) => setFilterSem(e.target.value)}
          className="bg-slate-50 border-none rounded-xl px-4 py-2.5 text-sm text-slate-700 outline-none focus:ring-2 focus:ring-primary/20 focus:bg-white transition-all shrink-0"
        >
          <option value="">All Semesters</option>
          {SEMESTERS.map(s => <option key={s} value={s}>{s}</option>)}
        </select>

        <select
          value={filterYear}
          onChange={(e) => setFilterYear(e.target.value)}
          className="bg-slate-50 border-none rounded-xl px-4 py-2.5 text-sm text-slate-700 outline-none focus:ring-2 focus:ring-primary/20 focus:bg-white transition-all shrink-0"
        >
          <option value="">All Years</option>
          {YEARS.map(y => <option key={y} value={y}>{y}</option>)}
        </select>
        <div className="flex gap-2 p-1 bg-slate-50 border border-slate-100 rounded-xl overflow-x-auto hide-scrollbar">
          {['ALL', 'UPCOMING', 'PAST'].map(f => (
            <button
              key={f}
              onClick={() => setFilter(f)}
              className={`px-4 py-1.5 rounded-lg text-sm font-semibold transition-all whitespace-nowrap ${filter === f
                  ? 'bg-white text-primary shadow-sm border border-slate-200/50'
                  : 'text-slate-500 hover:text-slate-700 hover:bg-slate-100'
                }`}
            >
              {f === 'ALL' ? 'All Events' : f === 'UPCOMING' ? 'Upcoming' : 'Past'}
            </button>
          ))}
        </div>
      </motion.div>

      {/* List */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }}>
        {filteredWorkshops.length === 0 ? (
          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-12 text-center text-slate-400">
            <Filter className="w-12 h-12 mx-auto mb-3 text-slate-200" />
            <h3 className="font-semibold text-slate-700 text-sm">No Events Found</h3>
            <p className="text-xs text-slate-400 mt-1">Try adjusting your filters or search query.</p>
          </div>
        ) : (
          <WorkshopList
            workshops={filteredWorkshops}
            onEdit={handleEdit}
            onDelete={handleDelete}
            onCheckIn={handleCheckIn}
            onManageAttendance={handleManageAttendance}
          />
        )}
      </motion.div>

      {/* Form Modal */}
      <WorkshopForm
        isOpen={isFormOpen}
        onClose={() => setIsFormOpen(false)}
        workshop={editingWorkshop}
        onSave={fetchWorkshops}
      />

      {/* Check-In Modal */}
      <WorkshopCheckInModal
        isOpen={isCheckInOpen}
        onClose={() => {
          setIsCheckInOpen(false);
          setSelectedWorkshop(null);
        }}
        workshop={selectedWorkshop}
        onCheckInSuccess={fetchWorkshops}
      />

      {/* Attendance Manager Modal */}
      <WorkshopAttendanceManager
        isOpen={isAttendanceManagerOpen}
        onClose={() => {
          setIsAttendanceManagerOpen(false);
          setSelectedWorkshop(null);
        }}
        workshop={selectedWorkshop}
      />
    </div>
  );
};

export default Workshops;
