// @ts-nocheck
import { useState, useEffect, useContext, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { motion } from 'framer-motion';
import {
  ArrowLeft, GraduationCap, Users, BookOpen,
  Upload, Download, UserPlus, Loader2, Calendar, Pencil, ShieldCheck, Lock, Unlock, Trash2, UserRoundCheck
} from 'lucide-react';
import { AuthContext } from '../../context/AuthContext';
import { classApi } from '../../api/classApi';
import { userApi } from '../../api/userApi';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import StudentTable from '../../components/class/StudentTable';
import TeamList from '../../components/class/TeamList';
import TeamManagementModal from '../../components/class/TeamManagementModal';
import StudentAssignmentModal from '../../components/class/StudentAssignmentModal';
import ImportStudentsModal from '../../components/class/ImportStudentsModal';
import StudentTeamGeneratePanel from '../../components/class/StudentTeamGeneratePanel';
import TeamSuggestionTooltip from '../../components/class/TeamSuggestionTooltip';
import ReviewTeamProposalModal from '../../components/class/ReviewTeamProposalModal';
import EditScheduleModal from '../../components/class/EditScheduleModal';
import AssignLectureModal from '../../components/class/AssignLectureModal';
import AssignMentorsModal from '../../components/class/AssignMentorsModal';
import RenameClassModal from '../../components/class/RenameClassModal';
import VerifyMajorModal from '../../components/class/VerifyMajorModal';
import AddStudentModal from '../../components/class/AddStudentModal';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import { entityId, getTeamMemberIds } from '../../utils/teamManagement';
import {
  directoryRecordToStudent,
  mergeAssignmentCandidates,
  normalizeClassStudents,
  studentBelongsToClass,
} from '../../utils/studentAssignment';
import { classFeatureFlags } from '../../config/classFeatureFlags';

export default function ClassDetail() {
  const { id }    = useParams();
  const navigate  = useNavigate();
  const { user }  = useContext(AuthContext);

  const [cls,      setCls]      = useState(null);
  const [students, setStudents] = useState([]);
  const [teams,    setTeams]    = useState([]);
  const [loading,  setLoading]  = useState(true);
  const [tab,      setTab]      = useState('students'); // 'students' | 'teams'

  // Selected students for team generation
  const [selected, setSelected] = useState([]);

  // Modals & Actions
  const [showImport, setShowImport] = useState(false);
  const [showTeamManagement, setShowTeamManagement] = useState(false);
  const [teamToEdit, setTeamToEdit] = useState(null);
  const [teamFormMemberIds, setTeamFormMemberIds] = useState([]);
  const [showStudentAssignment, setShowStudentAssignment] = useState(false);
  const [assignmentMode, setAssignmentMode] = useState('CLASS');
  const [assignmentInitialStudentIds, setAssignmentInitialStudentIds] = useState([]);
  const [assignmentCandidates, setAssignmentCandidates] = useState([]);
  const [loadingAssignmentCandidates, setLoadingAssignmentCandidates] = useState(false);
  const [showAddStudent, setShowAddStudent] = useState(false);
  const [showEditSchedule, setShowEditSchedule] = useState(false);
  const [showAssignLecturer, setShowAssignLecturer] = useState(false);
  const [showAssignMentors, setShowAssignMentors] = useState(false);
  const [showRename, setShowRename] = useState(false);
  const [showVerify, setShowVerify] = useState(false);
  const [reviewTeam, setReviewTeam] = useState(null);
  const [studentToDelete, setStudentToDelete] = useState(null);
  const [backfilling, setBackfilling] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [togglingLock, setTogglingLock] = useState(false);
  const [removingStudent, setRemovingStudent] = useState(false);
  const [showDeleteClass, setShowDeleteClass] = useState(false);
  const [deletingClass, setDeletingClass] = useState(false);

  const fetchData = useCallback(async () => {
    if (!id || id === 'undefined') {
      toast.error('Invalid class ID');
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const [classRes, studentRes] = await Promise.allSettled([
        classApi.getById(id),
        classApi.getStudents(id, { pageSize: 200 }),
      ]);

      const classData = classRes.status === 'fulfilled' ? (classRes.value?.data || classRes.value) : null;
      if (!classData) {
        toast.error('Failed to load class');
        return null;
      }

      const rawClass = classData.class || classData;
      const currentClassId = String(id || rawClass.id || rawClass._id || '');

      let parsedSchedule = rawClass.schedule;
      if (!parsedSchedule && rawClass.scheduleJson) {
        try {
          parsedSchedule = typeof rawClass.scheduleJson === 'string' ? JSON.parse(rawClass.scheduleJson) : rawClass.scheduleJson;
        } catch {
          parsedSchedule = null;
        }
      }

      const normalizedClass = {
        ...rawClass,
        _id: rawClass.id || rawClass._id || currentClassId,
        classCode: rawClass.classCode,
        subjectCode: rawClass.subjectCode,
        subjectName: rawClass.subjectName,
        semester: rawClass.semesterCode || rawClass.semester,
        year: rawClass.year,
        lectureId: rawClass.primaryLecturerId ? {
          _id: rawClass.primaryLecturerId,
          name: rawClass.primaryLecturerName,
          email: rawClass.primaryLecturerEmail
        } : (rawClass.lectureId || null),
        schedule: parsedSchedule
      };
      setCls(normalizedClass);

      let rawStudents = [];
      if (studentRes.status === 'fulfilled') {
        const sData = studentRes.value?.data || studentRes.value;
        rawStudents = sData.items || sData.students || sData.data || (Array.isArray(sData) ? sData : []);
      } else if (rawClass.students) {
        rawStudents = rawClass.students;
      }

      const mappedStudents = rawStudents.map((s, idx) => ({
        _id: s.studentId || s.id || s._id || `student-${idx}`,
        studentCode: s.rollNumber || s.studentCode,
        rollNumber: s.rollNumber || s.studentCode,
        fullName: s.fullName,
        email: s.email,
        major: s.majorCode || s.major,
        majorCode: s.majorCode || s.major,
        enrollmentStatus: s.enrollmentStatus || 'Active',
        classId: currentClassId,
        teamId: s.teamId || null,
        teamName: s.teamName || null,
        isTeamLeader: s.isTeamLeader || false
      }));

      setStudents(mappedStudents);

      let rawTeams = classFeatureFlags.teamManagement ? (rawClass.teams || []) : [];
      if (classFeatureFlags.teamManagement && rawTeams.length === 0) {
        try {
          const teamRes = await classApi.getTeams(id);
          const tData = teamRes?.data || teamRes;
          rawTeams = tData.teams || tData.items || (Array.isArray(tData) ? tData : []);
        } catch {
          // ignore
        }
      }

      setTeams(rawTeams.map(team => ({
        ...team,
        _id: team.id || team._id,
        classId: entityId(team.classId) || currentClassId,
      })));

      return classData;
    } catch (err) {
      toast.error(err?.message || 'Failed to load class');
      return null;
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchData();
  }, [fetchData]);

  const handleTeamCreated = async () => {
    setSelected([]);
    await fetchData();
  };

  const openCreateTeam = (memberIds = []) => {
    setTeamToEdit(null);
    setTeamFormMemberIds(memberIds);
    setShowTeamManagement(true);
  };

  const openEditTeam = (team) => {
    setTeamToEdit(team);
    setTeamFormMemberIds([]);
    setShowTeamManagement(true);
  };

  const closeTeamManagement = () => {
    setShowTeamManagement(false);
    setTeamToEdit(null);
    setTeamFormMemberIds([]);
  };

  const closeStudentAssignment = () => {
    setShowStudentAssignment(false);
    setAssignmentInitialStudentIds([]);
  };

  const openStudentAssignment = async (mode = 'CLASS', studentIds = []) => {
    const currentClassId = String(id || cls?._id || '');
    const currentStudents = normalizeClassStudents(students, currentClassId);
    setAssignmentMode(mode);
    setAssignmentInitialStudentIds(studentIds);
    setAssignmentCandidates(currentStudents);
    setShowStudentAssignment(true);
    setLoadingAssignmentCandidates(true);

    try {
      const response = await userApi.getAll({ page: 1, limit: 200, role: 'STUDENT', status: 'APPROVED' });
      const payload = response?.data || response;
      const records = payload?.users || payload?.data?.users || payload?.data || [];
      const directoryStudents = (Array.isArray(records) ? records : [])
        .map(directoryRecordToStudent)
        .filter(Boolean);
      setAssignmentCandidates(mergeAssignmentCandidates(currentStudents, directoryStudents));
    } catch {
      // The current class roster remains fully usable if the user directory is not accessible.
    } finally {
      setLoadingAssignmentCandidates(false);
    }
  };

  const handleStudentsAssigned = (result) => {
    const currentClassId = String(id || cls?._id || result.classId);
    const nextClassStudents = result.students
      .filter(student => studentBelongsToClass(student, currentClassId))
      .map(student => ({ ...student, source: student.source || 'CLASS_ROSTER' }));
    setStudents(nextClassStudents);
    setTeams(result.teams);
    setAssignmentCandidates(result.students);
    setSelected([]);
    setTab(result.mode === 'TEAM' ? 'teams' : 'students');
    closeStudentAssignment();
  };

  const handleTeamSaved = (savedTeam) => {
    const memberIds = new Set(getTeamMemberIds(savedTeam));
    setTeams(current => (
      current.some(team => team._id === savedTeam._id)
        ? current.map(team => team._id === savedTeam._id ? savedTeam : team)
        : [...current, savedTeam]
    ));
    setStudents(current => current.map(student => {
      if (memberIds.has(student._id)) return { ...student, classId: id, teamId: savedTeam._id };
      if (entityId(student.teamId) === savedTeam._id) return { ...student, teamId: null };
      return student;
    }));
    setSelected([]);
    closeTeamManagement();
  };

  const handleTeamDeleted = (deletedTeam) => {
    const deletedMemberIds = new Set(getTeamMemberIds(deletedTeam));
    setTeams(current => current.filter(team => team._id !== deletedTeam._id));
    setStudents(current => current.map(student => (
      deletedMemberIds.has(student._id) || entityId(student.teamId) === deletedTeam._id
        ? { ...student, teamId: null }
        : student
    )));
    setSelected(current => current.filter(studentId => !deletedMemberIds.has(studentId)));
    toast.success('Team deleted');
  };

  const handleBackfillChats = async () => {
    setBackfilling(true);
    try {
      const res = await classApi.backfillChats(id);
      const summary = res?.data || res;
      toast.success(
        `Backfill complete: Created ${summary.createdCount || 0}, Linked ${summary.attachedExistingCount || 0} chats.`
      );
      await fetchData();
    } catch (e) {
      toast.error(e?.message || 'Failed to backfill chat groups');
    } finally {
      setBackfilling(false);
    }
  };

  const handleToggleMajorLock = async () => {
    setTogglingLock(true);
    try {
      const res = await classApi.toggleMajorLock(id);
      setCls(prev => ({ ...prev, isMajorLocked: res.data.isMajorLocked }));
      toast.success(res.message || 'Đã thay đổi trạng thái cập nhật chuyên ngành');
    } catch (err) {
      toast.error(err.response?.data?.message || 'Lỗi khi thay đổi trạng thái');
    } finally {
      setTogglingLock(false);
    }
  };

  const handleExportExcel = async () => {
    setExporting(true);
    try {
      const response = await classApi.exportClassExcel(id);
      const blob = new Blob([response.data || response], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
      });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${cls.classCode || 'students'}_students.xlsx`;
      link.click();
      window.URL.revokeObjectURL(url);
      toast.success('Export successful');
    } catch (e) {
      console.error(e);
      toast.error('Failed to export students');
    } finally {
      setExporting(false);
    }
  };

  const handleRemoveStudent = (student) => {
    setStudentToDelete(student);
  };
  const confirmRemoveStudent = async () => {
    if (!studentToDelete?._id) return;
    setRemovingStudent(true);
    try {
      await classApi.removeStudent(id, studentToDelete._id);
      toast.success('Xóa sinh viên thành công');
      setStudentToDelete(null);
      await fetchData();
    } catch (err) {
      toast.error(err?.response?.data?.message || 'Xóa sinh viên thất bại');
    } finally {
      setRemovingStudent(false);
    }
  };

  const confirmDeleteClass = async () => {
    setDeletingClass(true);
    try {
      await classApi.delete(id);
      toast.success('Class deleted successfully');
      navigate(user?.role === 'ADMIN' ? '/admin/classes' : '/lecturer/classes');
    } catch (err) {
      toast.error(err?.message || 'Failed to delete class');
    } finally {
      setDeletingClass(false);
      setShowDeleteClass(false);
    }
  };

  if (loading) return <LoadingSkeleton />;
  if (!cls)    return <div className="text-center py-20 text-slate-400">Class not found.</div>;

  const safeStudents = Array.isArray(students) ? students : [];
  const safeTeams    = Array.isArray(teams) ? teams : [];
  const unassignedCount = safeStudents.filter(s => !s.teamId).length;
  
  const createdById = cls.createdBy?._id?.toString() || cls.createdBy?.toString();
  const lecturerId = cls.lectureId?._id?.toString() || cls.lectureId?.toString();
  const isAdminOrLecturer = user?.role === 'ADMIN' || (user?.role === 'LECTURER' && lecturerId === user._id);
  const canDeleteClass = user?.role === 'ADMIN' || (
    user?.role === 'LECTURER' &&
    (createdById === user._id || (!createdById && lecturerId === user._id))
  );

  const getUniqueMentors = () => {
    const classMentors = cls?.mentorIds || [];
    const teamMentors = safeTeams.map(t => t.mentorId).filter(Boolean);
    const seen = new Set();
    const unique = [];

    const addMentor = (m) => {
      const id = m?._id?.toString() || m?.toString();
      if (id && !seen.has(id)) {
        seen.add(id);
        const mObj = typeof m === 'object' ? m : { _id: m, name: 'Unknown' };
        unique.push(mObj);
      }
    };

    classMentors.forEach(addMentor);
    teamMentors.forEach(addMentor);
    return unique;
  };

  const activeMentors = getUniqueMentors();
  const primarySchedule = Array.isArray(cls.schedule) ? cls.schedule[0] : cls.schedule;
  const scheduleDayValue = primarySchedule?.dayOfWeek ?? primarySchedule?.DayOfWeek;
  const scheduleSlotValue = primarySchedule?.slotNumber ?? primarySchedule?.SlotNumber ?? primarySchedule?.slot;
  const scheduleRoomValue = primarySchedule?.room ?? primarySchedule?.Room ?? cls.room;
  const scheduleDayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  const scheduleDayLabel = typeof scheduleDayValue === 'number'
    ? scheduleDayNames[scheduleDayValue]
    : scheduleDayValue;

  return (
    <div className="space-y-6">
      {/* ── Back + Header ── */}
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <div className="flex items-center gap-3">
          <button
            onClick={() => navigate(-1)}
            className="p-2 rounded-xl border border-slate-200 text-slate-400 hover:text-slate-700 hover:border-slate-300 transition-all"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-bold text-slate-900">{cls.classCode}</h1>
              {classFeatureFlags.rename && (user?.role === 'ADMIN' ||
                (user?.role === 'LECTURER' && cls.lectureId?._id?.toString() === user._id)) && (
                <button
                  id="btn-rename-class"
                  onClick={() => setShowRename(true)}
                  title="Đổi tên lớp"
                  className="p-1.5 rounded-lg text-slate-400 hover:text-primary hover:bg-primary-50 transition-all"
                >
                  <Pencil className="w-4 h-4" />
                </button>
              )}
            </div>
            <p className="text-sm text-slate-500">{cls.subjectCode || '—'} · {cls.semester || '—'} {cls.year || ''}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {classFeatureFlags.chatBackfill && (user?.role === 'ADMIN' || user?.role === 'LECTURER') && (
            <button
              onClick={handleBackfillChats}
              disabled={backfilling}
              className="flex items-center gap-2 px-4 py-2 border border-slate-200 text-slate-600 rounded-xl text-sm hover:bg-slate-50 disabled:opacity-50 transition-all font-medium"
            >
              {backfilling ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" /> Backfilling...
                </>
              ) : (
                'Backfill Chats'
              )}
            </button>
          )}
          {(user?.role === 'ADMIN' || user?.role === 'LECTURER') && (
            <button
              onClick={handleExportExcel}
              disabled={exporting}
              className="flex items-center gap-2 px-4 py-2 border border-slate-200 text-slate-700 rounded-xl text-sm hover:bg-slate-50 transition-all font-medium disabled:opacity-50"
            >
              {exporting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Download className="w-4 h-4" />} Export
            </button>
          )}
          {classFeatureFlags.majorVerification && (user?.role === 'ADMIN' || user?.role === 'LECTURER') && (
  <>
    <button
      onClick={() =>
        navigate('/lecturer/data-bank', {
          state: {
            classId: cls._id,
            classCode: cls.classCode,
            subjectCode: cls.subjectCode,
            semester: `${cls.semester || ''}${String(cls.year || '').slice(-2)}`,
          },
        })
      }
      className="flex items-center gap-2 px-4 py-2 border border-slate-200 text-slate-700 rounded-xl text-sm hover:bg-slate-50 transition-all font-medium"
    >
      Open Data Bank
    </button>

    <button
      onClick={() => setShowAddStudent(true)}
      className="flex items-center gap-2 px-4 py-2 border border-primary text-primary rounded-xl text-sm hover:bg-primary-50 transition-all font-medium"
    >
      <UserPlus className="w-4 h-4" /> Thêm 1 SV
    </button>

    {isAdminOrLecturer && (
      <button
        id="btn-assign-students"
        onClick={() => openStudentAssignment('CLASS')}
        className="flex items-center gap-2 rounded-xl bg-gradient-primary px-4 py-2 text-sm font-semibold text-white shadow-sm transition-all hover:shadow-glow-primary"
      >
        <UserRoundCheck className="h-4 w-4" /> Assign students
      </button>
    )}
  </>
)}
          {user?.role === 'ADMIN' && (
            <button
              onClick={() => setShowImport(true)}
              className="flex items-center gap-2 px-4 py-2 border border-primary text-primary rounded-xl text-sm hover:bg-primary-50 transition-all font-medium"
            >
              <Upload className="w-4 h-4" /> Import Excel
            </button>
          )}
          {classFeatureFlags.majorVerification && (user?.role === 'ADMIN' || user?.role === 'LECTURER') && (
            <button
              id="btn-verify-majors"
              onClick={() => setShowVerify(true)}
              className="flex items-center gap-2 px-4 py-2 border border-indigo-300 text-indigo-600 rounded-xl text-sm hover:bg-indigo-50 transition-all font-medium"
            >
              <ShieldCheck className="w-4 h-4" /> Kiểm tra Chuyên ngành
            </button>
          )}
          {(user?.role === 'ADMIN' || user?.role === 'LECTURER') && (
            <button
              onClick={handleToggleMajorLock}
              disabled={togglingLock}
              className={`flex items-center gap-2 px-4 py-2 border rounded-xl text-sm transition-all font-medium disabled:opacity-50 ${
                cls.isMajorLocked 
                  ? 'border-red-300 text-red-600 hover:bg-red-50' 
                  : 'border-green-300 text-green-600 hover:bg-green-50'
              }`}
            >
              {togglingLock ? <Loader2 className="w-4 h-4 animate-spin" /> : (
                cls.isMajorLocked ? <Lock className="w-4 h-4" /> : <Unlock className="w-4 h-4" />
              )}
              {cls.isMajorLocked ? 'Mở khóa cập nhật' : 'Khóa cập nhật CN'}
            </button>
          )}
          {classFeatureFlags.lifecycle && canDeleteClass && (
            <button
              onClick={() => setShowDeleteClass(true)}
              className="flex items-center gap-2 px-4 py-2 border border-red-300 text-red-600 rounded-xl text-sm hover:bg-red-50 transition-all font-medium"
            >
              <Trash2 className="w-4 h-4" /> Delete Class
            </button>
          )}
        </div>
      </div>

      {/* ── Info Cards Grid ── */}
      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4">
        {/* Lecturer Card */}
        <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-4 flex items-center justify-between gap-3 relative group">
          <div className="flex items-center gap-3 min-w-0">
            <div className="w-10 h-10 rounded-xl bg-primary-100 flex items-center justify-center shrink-0">
              <GraduationCap className="w-5 h-5 text-primary" />
            </div>
            <div className="min-w-0">
              <p className="text-[10px] text-slate-400 uppercase font-semibold tracking-wider">Lecturer</p>
              <p className="font-semibold text-slate-800 text-sm truncate">{cls.lectureId?.name || <span className="text-amber-500">Not assigned</span>}</p>
              {cls.lectureId?.email && <p className="text-[11px] text-slate-400 truncate">{cls.lectureId.email}</p>}
            </div>
          </div>
          {user?.role === 'ADMIN' && (
            <button
              onClick={() => setShowAssignLecturer(true)}
              className="text-xs font-semibold text-primary hover:text-primary-700 px-2.5 py-1.5 bg-primary-50 rounded-lg transition-all shrink-0 cursor-pointer"
            >
              Edit
            </button>
          )}
        </div>

        {/* Schedule Card */}
        <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-4 flex items-center justify-between gap-3 relative group">
          <div className="flex items-center gap-3 min-w-0">
            <div className="w-10 h-10 rounded-xl bg-indigo-100 flex items-center justify-center shrink-0">
              <Calendar className="w-5 h-5 text-indigo-500" />
            </div>
            <div className="min-w-0">
              <p className="text-[10px] text-slate-400 uppercase font-semibold tracking-wider">Schedule</p>
              {primarySchedule ? (
                <>
                  <p className="font-semibold text-slate-800 text-sm truncate">{scheduleDayLabel}, Slot {scheduleSlotValue}</p>
                  <p className="text-[11px] text-slate-400 truncate">Room {scheduleRoomValue || 'TBD'}</p>
                </>
              ) : (
                <p className="font-semibold text-slate-800 text-sm truncate">TBD</p>
              )}
            </div>
          </div>
          {(user?.role === 'ADMIN' || user?.role === 'LECTURER') && (
            <button
              onClick={() => setShowEditSchedule(true)}
              className="text-xs font-semibold text-primary hover:text-primary-700 px-2.5 py-1.5 bg-primary-50 rounded-lg transition-all shrink-0 cursor-pointer"
            >
              Edit
            </button>
          )}
        </div>

        {/* Mentors Card */}
        <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-4 flex items-center justify-between gap-3 relative group">
          <div className="flex items-center gap-3 min-w-0">
            <div className="w-10 h-10 rounded-xl bg-amber-100 flex items-center justify-center shrink-0">
              <Users className="w-5 h-5 text-amber-500" />
            </div>
            <div className="min-w-0 flex-1">
              <p className="text-[10px] text-slate-400 uppercase font-semibold tracking-wider">Mentors</p>
              {activeMentors.length > 0 ? (
                <>
                  <p className="font-semibold text-slate-800 text-sm truncate">
                    {activeMentors.map(m => m.name || 'Unknown').join(', ')}
                  </p>
                  <p className="text-[11px] text-slate-400">{activeMentors.length} assigned</p>
                </>
              ) : (
                <p className="text-xs text-slate-400">No mentors assigned</p>
              )}
            </div>
          </div>
          {classFeatureFlags.mentorAssignment && user?.role === 'ADMIN' && (
            <button
              onClick={() => setShowAssignMentors(true)}
              className="text-xs font-semibold text-primary hover:text-primary-700 px-2.5 py-1.5 bg-primary-50 rounded-lg transition-all shrink-0 cursor-pointer"
            >
              Manage
            </button>
          )}
        </div>

        {/* Students Card */}
        <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-secondary-100 flex items-center justify-center shrink-0">
            <Users className="w-5 h-5 text-secondary" />
          </div>
          <div>
            <p className="text-[10px] text-slate-400 uppercase font-semibold tracking-wider">Students</p>
            <p className="font-bold text-2xl text-slate-900 leading-none mt-1">{safeStudents.length}</p>
            <p className="text-[11px] text-slate-400 mt-1">{unassignedCount} unassigned</p>
          </div>
        </div>

        {/* Teams Card */}
        {classFeatureFlags.teamManagement && <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-green-100 flex items-center justify-center shrink-0">
            <BookOpen className="w-5 h-5 text-green-600" />
          </div>
          <div>
            <p className="text-[10px] text-slate-400 uppercase font-semibold tracking-wider">Teams</p>
            <p className="font-bold text-2xl text-slate-900 leading-none mt-1">{safeTeams.length}</p>
          </div>
        </div>}
      </div>

      {/* ── Team Generation Panel (always visible when students exist) ── */}
      {classFeatureFlags.teamManagement && safeStudents.length > 0 && selected.length > 0 && (
        <div className="sticky top-20 z-40 shadow-xl rounded-2xl bg-white/80 backdrop-blur-md">
          {user?.role === 'STUDENT' ? (
            <StudentTeamGeneratePanel
              classId={id}
              selected={selected}
              students={safeStudents}
              onTeamCreated={handleTeamCreated}
              currentStudentId={safeStudents.find(s => s.userId === user._id)?._id}
            />
          ) : (
            <div className="flex flex-col gap-3 rounded-2xl border border-primary-100 bg-white p-4 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-center gap-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary-50 text-primary"><Users className="h-5 w-5" /></div>
                <div>
                  <p className="font-semibold text-slate-800">{selected.length} student{selected.length === 1 ? '' : 's'} selected</p>
                  <p className="text-xs text-slate-500">Continue to enter the team name and review members.</p>
                </div>
              </div>
              <div className="flex flex-col gap-2 sm:flex-row">
                <button onClick={() => openStudentAssignment('TEAM', selected)} className="flex items-center justify-center gap-2 rounded-xl border border-primary px-4 py-2.5 text-sm font-semibold text-primary hover:bg-primary-50">
                  <UserRoundCheck className="h-4 w-4" /> Assign to team
                </button>
                <button onClick={() => openCreateTeam(selected)} className="flex items-center justify-center gap-2 rounded-xl bg-gradient-primary px-4 py-2.5 text-sm font-semibold text-white shadow-sm hover:shadow-glow-primary">
                  <UserPlus className="h-4 w-4" /> Create team with selected
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* ── Tabs ── */}
      <div className="flex gap-1 p-1 bg-slate-100 rounded-xl w-fit">
        {(classFeatureFlags.teamManagement ? ['students', 'teams'] : ['students']).map(t => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-5 py-2 rounded-lg text-sm font-medium transition-all capitalize cursor-pointer ${
              tab === t ? 'bg-white shadow-sm text-slate-900' : 'text-slate-500 hover:text-slate-700'
            }`}
          >
            {t === 'students' ? `Students (${safeStudents.length})` : `Teams (${safeTeams.length})`}
          </button>
        ))}
      </div>

      {/* ── Tab Content ── */}
      <motion.div key={tab} initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ duration: 0.2 }}>
        {tab === 'students' ? (
          <StudentTable
            students={safeStudents}
            teams={classFeatureFlags.teamManagement ? safeTeams : []}
            cls={cls}
            selected={classFeatureFlags.teamManagement ? selected : []}
            onSelectionChange={classFeatureFlags.teamManagement ? setSelected : undefined}
            onRefresh={fetchData}
            onDeleteStudent={isAdminOrLecturer ? handleRemoveStudent : undefined}
            toolbarAction={classFeatureFlags.teamManagement && selected.length === 0 ? (
              user?.role === 'STUDENT' ? (
                <TeamSuggestionTooltip label="Xem hướng dẫn tạo nhóm">
                  <div className="space-y-2">
                    <p className="font-semibold text-white">
                      {unassignedCount} sinh viên chưa có nhóm
                    </p>
                    <p className="text-slate-200">
                      Chọn chính bạn và các thành viên trong bảng để bắt đầu tạo nhóm.
                      Nhóm cần 4–6 thành viên, có ít nhất một sinh viên nhóm BBA và một sinh viên nhóm BIT.
                    </p>
                  </div>
                </TeamSuggestionTooltip>
              ) : (
                <button onClick={() => openCreateTeam()} className="flex items-center gap-2 rounded-xl border border-primary px-3 py-2 text-sm font-semibold text-primary hover:bg-primary-50">
                  <UserPlus className="h-4 w-4" /> Create team
                </button>
              )
            ) : null}
          />
        ) : (
          <TeamList
            teams={safeTeams}
            onReview={(team) => setReviewTeam(team)}
            canDelete={isAdminOrLecturer}
            canManageInfo={isAdminOrLecturer}
            classStudents={safeStudents}
            onCreate={isAdminOrLecturer ? () => openCreateTeam() : undefined}
            onAssign={isAdminOrLecturer ? () => openStudentAssignment('TEAM') : undefined}
            onEdit={isAdminOrLecturer ? openEditTeam : undefined}
            onDelete={isAdminOrLecturer ? handleTeamDeleted : undefined}
          />
        )}
      </motion.div>

      {/* ── Modals ── */}
      {showImport && (
        <ImportStudentsModal
          classId={id || cls?._id}
          onClose={() => setShowImport(false)}
          onImported={() => {
            fetchData();
          }}
        />
      )}

      {classFeatureFlags.teamManagement && showTeamManagement && isAdminOrLecturer && (
        <TeamManagementModal
          classInfo={{
            id: id || cls._id,
            code: cls.classCode || 'Class',
            name: cls.subjectName || cls.subjectCode || '',
          }}
          students={safeStudents}
          teams={safeTeams}
          team={teamToEdit}
          initialMemberIds={teamFormMemberIds}
          onClose={closeTeamManagement}
          onSave={handleTeamSaved}
        />
      )}

      {showStudentAssignment && isAdminOrLecturer && (
        <StudentAssignmentModal
          classInfo={{
            id: id || cls._id,
            code: cls.classCode || 'Class',
            name: cls.subjectName || cls.subjectCode || '',
          }}
          students={assignmentCandidates}
          teams={safeTeams}
          initialMode={assignmentMode}
          initialStudentIds={assignmentInitialStudentIds}
          loadingCandidates={loadingAssignmentCandidates}
          onClose={closeStudentAssignment}
          onSave={handleStudentsAssigned}
        />
      )}

      {showEditSchedule && (
        <EditScheduleModal
          classId={id}
          currentSchedule={cls.schedule}
          rowVersion={cls.rowVersion}
          onClose={() => setShowEditSchedule(false)}
          onUpdated={async () => {
            setShowEditSchedule(false);
            await fetchData();
          }}
        />
      )}

      {showAssignLecturer && user?.role === 'ADMIN' && (
        <AssignLectureModal
          classId={id}
          currentLecture={cls.lectureId}
          rowVersion={cls.rowVersion}
          onClose={() => setShowAssignLecturer(false)}
          onAssigned={async () => {
            setShowAssignLecturer(false);
            await fetchData();
          }}
        />
      )}

      {classFeatureFlags.mentorAssignment && showAssignMentors && (
        <AssignMentorsModal
          classId={id}
          currentMentors={activeMentors}
          onClose={() => setShowAssignMentors(false)}
          onAssigned={async () => {
            setShowAssignMentors(false);
            await fetchData();
          }}
        />
      )}

      {/* ── Rename Class Modal ── */}
      {classFeatureFlags.rename && showRename && (
        <RenameClassModal
          classId={id}
          currentCode={cls.classCode}
          onClose={() => setShowRename(false)}
          onRenamed={(updated) => {
            if (updated) setCls(prev => ({ ...prev, classCode: updated.classCode }));
            setShowRename(false);
          }}
        />
      )}

      {/* ── Verify Majors Modal ── */}
      {classFeatureFlags.majorVerification && showVerify && (
        <VerifyMajorModal
          classId={id}
          onClose={() => setShowVerify(false)}
        />
      )}

      {/* ── Add Student Modal ── */}
      {showAddStudent && (
        <AddStudentModal
          classId={id}
          onClose={() => setShowAddStudent(false)}
          onAdded={() => {
            setShowAddStudent(false);
            fetchData();
          }}
        />
      )}

      {classFeatureFlags.teamManagement && reviewTeam && (
        <ReviewTeamProposalModal
          team={reviewTeam}
          classStudents={safeStudents}
          onClose={() => setReviewTeam(null)}
          onRefresh={handleTeamCreated}
        />
      )}

      <ConfirmDialog
        isOpen={!!studentToDelete}
        onClose={() => setStudentToDelete(null)}
        onConfirm={confirmRemoveStudent}
        isSubmitting={removingStudent}
        title="Xóa sinh viên khỏi lớp?"
        description={
          studentToDelete
            ? `Sinh viên "${studentToDelete.fullName}" sẽ bị xóa khỏi lớp và gỡ khỏi nhóm hiện tại nếu có.`
            : ''
        }
        confirmText="Xóa sinh viên"
        cancelText="Hủy"
      />

      {classFeatureFlags.lifecycle && <ConfirmDialog
        isOpen={showDeleteClass}
        onClose={() => setShowDeleteClass(false)}
        onConfirm={confirmDeleteClass}
        isSubmitting={deletingClass}
        title="Delete this class?"
        description={`Class "${cls.classCode}" will be removed from active class lists. Student and team data will remain in the system.`}
        confirmText="Delete class"
        cancelText="Cancel"
      />}
    </div>
  );
}

