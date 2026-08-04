import { useState, useEffect, useContext, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { motion, AnimatePresence } from 'framer-motion';
import {
  Plus, RefreshCw, Search, Filter, GraduationCap,
  Users, BookOpen, ChevronRight, Upload, Eye, Calendar, LayoutGrid, ClipboardCheck,
} from 'lucide-react';
import { AuthContext } from '../../context/AuthContext';
import { classApi } from '../../api/classApi';
import { userApi } from '../../api/userApi';
import { subjectApi } from '../../api/subjectApi';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import EmptyState from '../../components/ui/EmptyState';
import BulkCreateModal from '../../components/class/BulkCreateModal';
import ImportStudentsModal from '../../components/class/ImportStudentsModal';
import ClassDirectionOverview from '../../components/class/ClassDirectionOverview';
import { classFeatureFlags } from '../../config/classFeatureFlags';

const SEMESTERS = ['SP', 'SU', 'FA'];
const CURRENT_YEAR = new Date().getFullYear();
const YEARS = Array.from({ length: 5 }, (_, i) => CURRENT_YEAR - 1 + i);

const semesterColor = { SP: 'bg-green-100 text-green-700', SU: 'bg-amber-100 text-amber-700', FA: 'bg-blue-100 text-blue-700' };
const semesterOrder = { SP: 1, SU: 2, FA: 3 };
const classCodeCollator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });

const sortClasses = (list) => [...list].sort((a, b) => {
  const subjectCompare = (a.subjectCode || '').localeCompare(b.subjectCode || '');
  if (subjectCompare !== 0) return subjectCompare;

  if ((b.year || 0) !== (a.year || 0)) return (b.year || 0) - (a.year || 0);

  const semesterCompare = (semesterOrder[a.semester] || 99) - (semesterOrder[b.semester] || 99);
  if (semesterCompare !== 0) return semesterCompare;

  if ((a.classIndex || 0) !== (b.classIndex || 0)) return (a.classIndex || 0) - (b.classIndex || 0);

  return classCodeCollator.compare(a.classCode || '', b.classCode || '');
});

const groupClassesBySubject = (list) => sortClasses(list).reduce((groups, cls) => {
  const subjectCode = cls.subjectCode || 'Unknown Subject';
  const group = groups.find(item => item.subjectCode === subjectCode);
  if (group) {
    group.classes.push(cls);
  } else {
    groups.push({ subjectCode, classes: [cls] });
  }
  return groups;
}, []);

export default function ClassManagement() {
  const { user } = useContext(AuthContext);
  const navigate  = useNavigate();
  const isAdmin    = user?.role === 'ADMIN';
  const isLecturer = user?.role === 'LECTURER';

  const [classes,    setClasses]    = useState([]);
  const [loading,    setLoading]    = useState(true);
  const [lecturers,  setLecturers]  = useState([]);
  const [subjects,   setSubjects]   = useState([]);

  // Filters
  const [search,     setSearch]     = useState('');
  const [filterSem,  setFilterSem]  = useState('');
  const [filterYear, setFilterYear] = useState('');
  const [filterSubj, setFilterSubj] = useState('');
  const [viewMode, setViewMode] = useState('classes');

  // Modals
  const [showBulk,   setShowBulk]   = useState(false);
  const [importTarget, setImportTarget] = useState(null); // classId for import

  const fetchAll = async () => {
    setLoading(true);
    try {
      const params: {
        semester?: string;
        year?: string;
        subjectCode?: string;
        search?: string;
      } = {};
      if (filterSem)  params.semester    = filterSem;
      if (filterYear) params.year        = filterYear;
      if (filterSubj) params.subjectCode = filterSubj;
      if (search)     params.search      = search;

      const [clsRes, usrRes] = await Promise.all([
        classApi.getAll(params),
        isAdmin ? userApi.getAll({ role: 'LECTURER' }) : Promise.resolve({ users: [] }),
      ]);

      const rawClasses = clsRes?.data?.items || clsRes?.data?.classes || clsRes?.items || clsRes?.classes || [];
      const normalizedClasses = rawClasses.map(c => ({
        ...c,
        _id: c.id || c._id,
        semester: c.semesterCode || c.semester,
        lectureId: c.primaryLecturerName ? {
          _id: c.primaryLecturerId,
          name: c.primaryLecturerName,
          email: c.primaryLecturerEmail
        } : (c.lectureId || null),
        schedule: c.scheduleJson ? (typeof c.scheduleJson === 'string' ? JSON.parse(c.scheduleJson) : c.scheduleJson) : c.schedule
      }));
      setClasses(normalizedClasses);
      const lects = usrRes?.data?.users || usrRes?.users || [];
      setLecturers(lects
        .filter(u => u.role === 'LECTURER')
        .map(lecturer => ({
          ...lecturer,
          _id: lecturer._id || lecturer.id,
          name: lecturer.name || lecturer.fullName,
        })));
    } catch {
      toast.error('Failed to load classes');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const loadInitialConfig = async () => {
      try {
        const [subjRes, semRes] = await Promise.all([
          subjectApi.getActive(),
          subjectApi.getCurrentSemester(),
        ]);
        const list = subjRes.data?.subjects || subjRes.subjects || [];
        setSubjects(list.map(s => s.subjectCode));

        const activeSem = semRes.data?.currentSemester || semRes.currentSemester;
        if (activeSem) {
          setFilterSem(activeSem.semester);
          setFilterYear(String(activeSem.year));
        }
      } catch (err) {
        console.error('Failed to load initial config', err);
      }
    };
    loadInitialConfig();
  }, []);

  useEffect(() => { fetchAll(); }, [filterSem, filterYear, filterSubj]); // eslint-disable-line

  const handleSearch = (e) => {
    e.preventDefault();
    fetchAll();
  };

  const handleBulkCreated = () => {
    setShowBulk(false);
    fetchAll();
    toast.success('Classes created successfully!');
  };

  const handleImported = () => {
    setImportTarget(null);
    fetchAll();
  };

  const sortedClasses = useMemo(() => sortClasses(classes), [classes]);
  const subjectGroups = useMemo(() => groupClassesBySubject(classes), [classes]);
  const showSubjectGroups = !filterSubj;

  if (loading) return <LoadingSkeleton />;

  return (
    <div className="space-y-6">
      {/* ── Header ── */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-end gap-4">
        <div>
          <h1 className="text-2xl sm:text-3xl font-bold text-slate-900">{isLecturer ? 'My Classes' : 'Class Management'}</h1>
          <p className="text-slate-500 mt-1">{classes.length} class{classes.length !== 1 ? 'es' : ''} found</p>
        </div>
        <div className="flex gap-2 flex-wrap">
          <button
            onClick={fetchAll}
            className="flex items-center gap-2 px-3 py-2 text-sm border border-slate-200 rounded-xl text-slate-600 hover:bg-slate-50 transition-all"
          >
            <RefreshCw className="w-4 h-4" /> Refresh
          </button>
          {(isAdmin || isLecturer) && (
            <button
              id="btn-bulk-create"
              onClick={() => setShowBulk(true)}
              className="flex items-center gap-2 px-4 py-2 text-sm bg-primary text-white rounded-xl hover:bg-primary-700 transition-all shadow-sm hover:shadow-md"
            >
              <Plus className="w-4 h-4" /> {isLecturer ? 'Tạo lớp' : 'Bulk Create'}
            </button>
          )}
        </div>
      </div>

      {isLecturer && classFeatureFlags.projectDirection && (
        <div className="inline-flex rounded-lg border border-slate-200 bg-slate-50 p-1">
          <button
            type="button"
            onClick={() => setViewMode('classes')}
            className={`inline-flex items-center gap-2 rounded-md px-4 py-2 text-sm font-semibold transition ${viewMode === 'classes' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}
          >
            <LayoutGrid className="h-4 w-4" /> Classes
          </button>
          <button
            type="button"
            onClick={() => setViewMode('overview')}
            className={`inline-flex items-center gap-2 rounded-md px-4 py-2 text-sm font-semibold transition ${viewMode === 'overview' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}
          >
            <ClipboardCheck className="h-4 w-4" /> Overview
          </button>
        </div>
      )}

      {/* ── Filters ── */}
      <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-4">
        <form onSubmit={handleSearch} className="flex flex-wrap gap-3 items-center">
          <div className="relative flex-1 min-w-[200px]">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <input
              type="text"
              placeholder="Search by class code..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full pl-9 pr-4 py-2 border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>
          <select
            value={filterSubj}
            onChange={(e) => setFilterSubj(e.target.value)}
            className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
          >
            <option value="">All Subjects</option>
            {subjects.map(s => <option key={s}>{s}</option>)}
          </select>
          <select
            value={filterSem}
            onChange={(e) => setFilterSem(e.target.value)}
            className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
          >
            <option value="">All Semesters</option>
            {SEMESTERS.map(s => <option key={s}>{s}</option>)}
          </select>
          <select
            value={filterYear}
            onChange={(e) => setFilterYear(e.target.value)}
            className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
          >
            <option value="">All Years</option>
            {YEARS.map(y => <option key={y}>{y}</option>)}
          </select>
          <button type="submit" className="flex items-center gap-2 px-4 py-2 bg-secondary text-white rounded-xl text-sm hover:bg-secondary-700 transition-all">
            <Filter className="w-4 h-4" /> Filter
          </button>
          <button type="button" onClick={() => { setSearch(''); setFilterSem(''); setFilterYear(''); setFilterSubj(''); setTimeout(() => fetchAll(), 0); }} className="text-sm text-slate-400 hover:text-slate-600 px-2">
            Reset
          </button>
        </form>
      </div>

      {/* ── Class Grid ── */}
      {isLecturer && classFeatureFlags.projectDirection && viewMode === 'overview' ? (
        <ClassDirectionOverview semester={filterSem} year={filterYear} />
      ) : classes.length === 0 ? (
        <EmptyState
          icon={GraduationCap}
          title="No classes found"
          description={isAdmin ? 'Use Bulk Create to generate classes by subject code' : 'No classes assigned to you yet'}
          action={isAdmin ? { label: 'Bulk Create', onClick: () => setShowBulk(true) } : undefined}
        />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          <AnimatePresence>
            {sortedClasses.map((cls, i) => (
              <div key={cls._id} className="contents">
                {showSubjectGroups && (i === 0 || sortedClasses[i - 1]?.subjectCode !== cls.subjectCode) && (
                  <div className="col-span-full flex items-center gap-3 pt-2 first:pt-0">
                    <h2 className="text-lg font-bold text-slate-900">{cls.subjectCode || 'Unknown Subject'}</h2>
                    <span className="h-px flex-1 bg-slate-200" />
                    <span className="text-xs font-semibold text-slate-500 bg-slate-100 px-2 py-1 rounded-full">
                      {subjectGroups.find(group => group.subjectCode === (cls.subjectCode || 'Unknown Subject'))?.classes.length || 0} classes
                    </span>
                  </div>
                )}
              <motion.div
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: i * 0.04 }}
                className="bg-white rounded-2xl border border-slate-200/60 shadow-sm hover:shadow-elevated hover:-translate-y-1 transition-all group cursor-pointer"
                onClick={() => navigate(`/classes/${cls._id || cls.id}`)}
              >
                <div className="p-5">
                  {/* Top row */}
                  <div className="flex items-start justify-between mb-3">
                    <div>
                      <div className="flex items-center gap-2 mb-1">
                        <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${semesterColor[cls.semester] || 'bg-slate-100 text-slate-600'}`}>
                          {cls.semester} {cls.year}
                        </span>
                        <span className="text-xs font-semibold px-2 py-0.5 rounded-full bg-primary-50 text-primary">
                          {cls.subjectCode}
                        </span>
                        {cls.status === 'disabled' && (
                          <span className="text-xs font-semibold px-2 py-0.5 rounded-full bg-red-50 text-red-500">Disabled</span>
                        )}
                      </div>
                      <h3 className="text-xl font-bold text-slate-900">{cls.classCode}</h3>
                    </div>
                    <ChevronRight className="w-5 h-5 text-slate-300 group-hover:text-primary transition-colors mt-1" />
                  </div>

                  {/* Lecturer */}
                   {cls.lectureId ? (
                    <div className="flex items-center gap-2 mb-2 p-2 bg-slate-50 rounded-xl">
                      <div className="w-7 h-7 rounded-lg bg-primary-100 flex items-center justify-center shrink-0">
                        <GraduationCap className="w-4 h-4 text-primary" />
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs text-slate-400">Lecturer</p>
                        <p className="text-sm font-medium text-slate-800 truncate">{cls.lectureId?.name || 'Unknown'}</p>
                      </div>
                    </div>
                  ) : (
                    <div className="mb-2 p-2 bg-amber-50 rounded-xl border border-amber-100">
                      <p className="text-xs text-amber-600 font-medium">⚠ No lecturer assigned</p>
                    </div>
                  )}

                  {/* Mentors */}
                  {cls.mentorIds && cls.mentorIds.length > 0 ? (
                    <div className="flex items-center gap-2 mb-2 p-2 bg-slate-50 rounded-xl">
                      <div className="w-7 h-7 rounded-lg bg-amber-100 flex items-center justify-center shrink-0">
                        <Users className="w-4 h-4 text-amber-500" />
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-xs text-slate-400">Mentors ({cls.mentorIds.length})</p>
                        <p className="text-sm font-medium text-slate-800 truncate">
                          {cls.mentorIds.map(m => m.name || 'Unknown').join(', ')}
                        </p>
                      </div>
                    </div>
                  ) : (
                    <div className="mb-2 p-2 bg-slate-50/50 rounded-xl border border-dashed border-slate-200">
                      <p className="text-xs text-slate-400">No mentors assigned</p>
                    </div>
                  )}

                  {/* Schedule */}
                  {cls.schedule && cls.schedule.dayOfWeek ? (
                    <div className="flex items-center gap-2 mb-4 p-2 bg-slate-50 rounded-xl">
                      <div className="w-7 h-7 rounded-lg bg-indigo-100 flex items-center justify-center shrink-0">
                        <Calendar className="w-4 h-4 text-indigo-500" />
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-xs text-slate-400">Schedule (Room: {cls.schedule.room})</p>
                        <p className="text-sm font-medium text-slate-800 truncate">
                          {cls.schedule.dayOfWeek}, Slot {cls.schedule.slot} ({cls.schedule.startTime} - {cls.schedule.endTime})
                        </p>
                      </div>
                    </div>
                  ) : (
                    <div className="mb-4 p-2 bg-slate-50/50 rounded-xl border border-dashed border-slate-200">
                      <p className="text-xs text-slate-400">Schedule TBD</p>
                    </div>
                  )}

                  {/* Stats */}
                  <div className="grid grid-cols-2 gap-2">
                    <div className="flex items-center gap-2 bg-slate-50 rounded-xl p-2.5">
                      <Users className="w-4 h-4 text-secondary" />
                      <div>
                        <p className="text-xs text-slate-400">Students</p>
                        <p className="text-base font-bold text-slate-900">{cls.studentCount ?? 0}</p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2 bg-slate-50 rounded-xl p-2.5">
                      <BookOpen className="w-4 h-4 text-fpt-green" />
                      <div>
                        <p className="text-xs text-slate-400">Teams</p>
                        <p className="text-base font-bold text-slate-900">{cls.teamCount ?? 0}</p>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Footer actions */}
                <div className="border-t border-slate-100 px-5 py-3 flex gap-2">
                  <button
                    onClick={(e) => { e.stopPropagation(); navigate(`/classes/${cls._id || cls.id}`); }}
                    className="flex-1 flex items-center justify-center gap-1.5 text-xs text-secondary hover:text-secondary-dark font-medium transition-colors"
                  >
                    <Eye className="w-3.5 h-3.5" /> View Detail
                  </button>
                  {isAdmin && (
                    <button
                      onClick={(e) => { e.stopPropagation(); setImportTarget(cls._id); }}
                      className="flex-1 flex items-center justify-center gap-1.5 text-xs text-primary hover:text-primary-dark font-medium transition-colors border-l border-slate-100"
                    >
                      <Upload className="w-3.5 h-3.5" /> Import Students
                    </button>
                  )}
                </div>
              </motion.div>
              </div>
            ))}
          </AnimatePresence>
        </div>
      )}

      {/* ── Modals ── */}
      {showBulk && (
        <BulkCreateModal
          lecturers={lecturers}
          isLecturer={isLecturer}
          onClose={() => setShowBulk(false)}
          onCreated={handleBulkCreated}
        />
      )}
      {isAdmin && importTarget && (
        <ImportStudentsModal
          classId={importTarget}
          onClose={() => setImportTarget(null)}
          onImported={handleImported}
        />
      )}
    </div>
  );
}
