// @ts-nocheck
import { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ChevronLeft, GraduationCap, Users, Mail, Loader2, LayoutGrid } from 'lucide-react';
import toast from 'react-hot-toast';
import { classApi } from '../../api/classApi';
import TeamList from '../../components/class/TeamList';
import StudentTable from '../../components/class/StudentTable';
import StudentTeamGeneratePanel from '../../components/class/StudentTeamGeneratePanel';
import TeamSuggestionTooltip from '../../components/class/TeamSuggestionTooltip';
import { useAuth } from '../../hooks/useAuth';

const semesterLabel = (sem) => {
  if (sem === 'SP') return 'Spring';
  if (sem === 'SU') return 'Summer';
  if (sem === 'FA') return 'Fall';
  return sem;
};

export default function StudentClassDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('classmates');
  const [selected, setSelected] = useState([]);

  const fetchClassDetail = useCallback(async () => {
    try {
      const res = await classApi.getMyClassDetail(id);
      setData(res?.data || res || null);
    } catch (err) {
      toast.error(err?.message || 'Failed to load class details');
      navigate('/student/classes');
    } finally {
      setLoading(false);
    }
  }, [id, navigate]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchClassDetail();
  }, [fetchClassDetail]);

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh]">
        <Loader2 className="w-8 h-8 text-primary animate-spin" />
        <p className="text-sm text-slate-400 mt-2 font-medium">Loading class details...</p>
      </div>
    );
  }

  const cls      = data?.class;
  const students = Array.isArray(data?.students) ? data.students : [];
  const teams    = Array.isArray(data?.teams) ? data.teams : [];
  const lecturer = cls?.lectureId;
  const currentUserId = (user?._id || user?.id || '').toString();
  const currentStudent = students.find(s => {
    const studentUserId = (s.userId?._id || s.userId || '').toString();
    return (studentUserId && studentUserId === currentUserId)
      || (user?.email && s.email?.toLowerCase() === user.email.toLowerCase());
  });
  const hasTeam = Boolean(currentStudent?.teamId);

  const handleTeamCreated = async () => {
    setSelected([]);
    await fetchClassDetail();
    setActiveTab('classmates');
  };

  return (
    <div className="space-y-6 max-w-5xl mx-auto">
      {/* Back link */}
      <button
        onClick={() => navigate('/student/classes')}
        className="flex items-center gap-1.5 text-slate-500 hover:text-slate-800 transition-colors text-sm font-semibold"
      >
        <ChevronLeft className="w-4 h-4" /> Back to My Classes
      </button>

      {/* Hero Header */}
      <div className="bg-white rounded-2xl border border-slate-200/60 p-6 shadow-sm flex flex-wrap justify-between items-start gap-4">
        <div className="flex items-start gap-4">
          <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-primary-500 to-secondary flex items-center justify-center text-white shrink-0 shadow-sm">
            <GraduationCap className="w-6 h-6" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="text-xs font-semibold px-2 py-0.5 bg-primary-50 text-primary border border-primary-100 rounded-md uppercase">
                {cls?.subjectCode}
              </span>
              <span className="text-xs font-semibold px-2 py-0.5 bg-slate-100 text-slate-500 border border-slate-200/40 rounded-md">
                {semesterLabel(cls?.semester)} {cls?.year}
              </span>
            </div>
            <h1 className="text-2xl font-bold text-slate-900 mt-1.5">{cls?.classCode}</h1>
            <p className="text-sm text-slate-500 mt-0.5">{cls?.description || 'Classroom for Startup Idea Development.'}</p>
          </div>
        </div>

        <div className="flex flex-wrap gap-3 shrink-0">
          {/* Lecturer card */}
          {lecturer && (
            <div className="bg-slate-50 rounded-xl p-4 border border-slate-100 min-w-[220px]">
              <p className="text-[10px] font-semibold text-slate-400 uppercase tracking-wider">Lecturer</p>
              <p className="font-bold text-slate-800 text-sm mt-1">{lecturer.name}</p>
              <p className="text-xs text-slate-400 mt-0.5 flex items-center gap-1">
                <Mail className="w-3.5 h-3.5 text-slate-300" />
                {lecturer.email}
              </p>
            </div>
          )}

          {/* Mentors card */}
          {cls?.mentorIds && cls.mentorIds.length > 0 && (
            <div className="bg-slate-50 rounded-xl p-4 border border-slate-100 min-w-[220px] max-w-[280px]">
              <p className="text-[10px] font-semibold text-slate-400 uppercase tracking-wider">Mentors</p>
              <p className="font-bold text-slate-800 text-sm mt-1 truncate">
                {cls.mentorIds.map(m => m.name || 'Unknown').join(', ')}
              </p>
              <p className="text-xs text-slate-400 mt-0.5">
                {cls.mentorIds.length} assigned to class
              </p>
            </div>
          )}
        </div>
      </div>

      {students.length > 0 && !hasTeam && selected.length > 0 && (
        <div className="sticky top-20 z-30 rounded-2xl bg-white/80 shadow-xl backdrop-blur-md">
          <StudentTeamGeneratePanel
            classId={id}
            selected={selected}
            students={students}
            onTeamCreated={handleTeamCreated}
            currentStudentId={currentStudent?._id}
          />
        </div>
      )}

      {/* Tabs */}
      <div className="flex border-b border-slate-200">
        <button
          onClick={() => setActiveTab('classmates')}
          className={`px-5 py-3 text-sm font-semibold border-b-2 transition-all flex items-center gap-2 ${
            activeTab === 'classmates'
              ? 'border-primary text-primary'
              : 'border-transparent text-slate-400 hover:text-slate-600'
          }`}
        >
          <Users className="w-4 h-4" /> Classmates ({students.length})
        </button>
        <button
          onClick={() => setActiveTab('teams')}
          className={`px-5 py-3 text-sm font-semibold border-b-2 transition-all flex items-center gap-2 ${
            activeTab === 'teams'
              ? 'border-primary text-primary'
              : 'border-transparent text-slate-400 hover:text-slate-600'
          }`}
        >
          <LayoutGrid className="w-4 h-4" /> Class Teams ({teams.length})
        </button>
      </div>

      {/* Tab Contents */}
      {activeTab === 'classmates' ? (
        <StudentTable
          students={students}
          teams={teams}
          cls={cls}
          selected={selected}
          onSelectionChange={setSelected}
          onRefresh={fetchClassDetail}
          maxSelection={7}
          selectionDisabled={hasTeam}
          toolbarAction={!hasTeam && selected.length === 0 ? (
            <TeamSuggestionTooltip label="Xem hướng dẫn tạo nhóm">
              <div className="space-y-2">
                <p className="font-semibold text-white">Hướng dẫn tạo nhóm</p>
                <p className="text-slate-200">
                  Chọn chính bạn và các thành viên trong bảng để bắt đầu. Nhóm cần 4–6 thành viên,
                  có ít nhất một sinh viên nhóm BBA và một sinh viên nhóm BIT.
                </p>
              </div>
            </TeamSuggestionTooltip>
          ) : null}
        />
      ) : (
        <TeamList
          teams={teams}
          onRefresh={fetchClassDetail}
          canDelete={false}
          canManageInfo={false}
          currentStudentId={currentStudent?._id}
        />
      )}
    </div>
  );
}

