import { useState, useEffect, useContext, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { motion } from 'framer-motion';
import {
  ArrowLeft, GraduationCap, Users, BookOpen,
  Upload, Download, UserPlus, Loader2, Calendar, Pencil, ShieldCheck, Lock, Unlock, AlertTriangle,
  Database, MessagesSquare, Archive, RotateCcw, CircleCheck, Play
} from 'lucide-react';
import { AuthContext } from '../../context/AuthContext';
import { classApi } from '../../api/classApi';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import StudentTable from '../../components/class/StudentTable';
import TeamList from '../../components/class/TeamList';
import TeamManagementModal from '../../components/class/TeamManagementModal';
import ImportStudentsModal from '../../components/class/ImportStudentsModal';
import StudentTeamGeneratePanel from '../../components/class/StudentTeamGeneratePanel';
import TeamSuggestionTooltip from '../../components/class/TeamSuggestionTooltip';
import ReviewTeamProposalModal from '../../components/class/ReviewTeamProposalModal';
import ProjectDirectionModal from '../../components/class/ProjectDirectionModal';
import EditScheduleModal from '../../components/class/EditScheduleModal';
import AssignLectureModal from '../../components/class/AssignLectureModal';
import AssignMentorsModal from '../../components/class/AssignMentorsModal';
import RenameClassModal from '../../components/class/RenameClassModal';
import VerifyMajorModal from '../../components/class/VerifyMajorModal';
import AddStudentModal from '../../components/class/AddStudentModal';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import { entityId, getTeamMemberIds, normalizeManagedTeam, normalizeTeamProposal } from '../../utils/teamManagement';
import { classFeatureFlags } from '../../config/classFeatureFlags';
import { parseApiError } from '../../utils/apiError';
import { toClassViewModel, unwrapApiData } from '../../utils/classMappers';
import { getClassLifecyclePresentation, isArchivedClass, isClassReadOnly } from '../../utils/classComponentPolicy';
import { canManageClass as canManageClassPermission, hasClassRole } from '../../utils/classPermissions';

const classActionTone = {
  neutral: 'border-slate-200 bg-white text-slate-600 hover:border-slate-300 hover:bg-slate-50 hover:text-slate-800',
  primary: 'border-primary-200 bg-primary-50 text-primary hover:border-primary-300 hover:bg-primary-100',
  secondary: 'border-secondary-200 bg-secondary-50 text-secondary hover:border-secondary-300 hover:bg-secondary-100 dark:hover:bg-secondary-900/30',
  indigo: 'border-indigo-200 bg-indigo-50/70 text-indigo-600 hover:border-indigo-300 hover:bg-indigo-50 dark:border-indigo-800 dark:bg-indigo-950/30 dark:text-indigo-300',
  success: 'border-green-200 bg-green-50 text-green-700 hover:border-green-300 hover:bg-green-100 dark:hover:bg-green-950/30',
  danger: 'border-red-200 bg-red-50 text-red-600 hover:border-red-300 hover:bg-red-100 dark:hover:bg-red-950/30',
};

function ClassActionButton({ icon: Icon, tone = 'neutral', loading = false, children, className = '', ...props }) {
  return (
    <button
      {...props}
      aria-busy={loading || undefined}
      className={`inline-flex min-h-8 items-center justify-center gap-1.5 whitespace-nowrap rounded-lg border px-2.5 py-1.5 text-xs font-semibold shadow-xs transition-all duration-150 hover:-translate-y-px hover:shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/25 disabled:pointer-events-none disabled:opacity-50 ${classActionTone[tone]} ${className}`}
    >
      {loading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : Icon ? <Icon className="h-3.5 w-3.5" /> : null}
      <span>{children}</span>
    </button>
  );
}

export default function ClassDetail() {
  const { id }    = useParams();
  const navigate  = useNavigate();
  const { user }  = useContext(AuthContext);

  const [cls,      setCls]      = useState(null);
  const [students, setStudents] = useState([]);
  const [teams,    setTeams]    = useState([]);
  const [teamProposals, setTeamProposals] = useState([]);
  const [loading,  setLoading]  = useState(true);
  const [rosterLoadError, setRosterLoadError] = useState('');
  const [rosterPage, setRosterPage] = useState(1);
  const [rosterPageSize] = useState(50);
  const [rosterSearch, setRosterSearch] = useState('');
  const [rosterMajor, setRosterMajor] = useState('');
  const [rosterStatus, setRosterStatus] = useState('Active');
  const [rosterMeta, setRosterMeta] = useState({ totalCount: 0, page: 1, pageSize: 50, totalPages: 1 });
  const [tab,      setTab]      = useState('students'); // 'students' | 'teams'

  // Selected students for team generation
  const [selected, setSelected] = useState([]);

  // Modals & Actions
  const [showImport, setShowImport] = useState(false);
  const [showTeamManagement, setShowTeamManagement] = useState(false);
  const [teamToEdit, setTeamToEdit] = useState(null);
  const [teamFormMemberIds, setTeamFormMemberIds] = useState([]);
  const [showAddStudent, setShowAddStudent] = useState(false);
  const [showEditSchedule, setShowEditSchedule] = useState(false);
  const [showAssignLecturer, setShowAssignLecturer] = useState(false);
  const [showAssignMentors, setShowAssignMentors] = useState(false);
  const [showRename, setShowRename] = useState(false);
  const [showVerify, setShowVerify] = useState(false);
  const [reviewTeam, setReviewTeam] = useState(null);
  const [directionTeam, setDirectionTeam] = useState(null);
  const [studentToDelete, setStudentToDelete] = useState(null);
  const [studentToReEnroll, setStudentToReEnroll] = useState(null);
  const [backfilling, setBackfilling] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [togglingLock, setTogglingLock] = useState(false);
  const [removingStudent, setRemovingStudent] = useState(false);
  const [reEnrollingStudent, setReEnrollingStudent] = useState(false);
  const [showDeleteClass, setShowDeleteClass] = useState(false);
  const [deletingClass, setDeletingClass] = useState(false);
  const [lifecycleReason, setLifecycleReason] = useState('');
  const [showCompletion, setShowCompletion] = useState(false);
  const [completionReason, setCompletionReason] = useState('');
  const [completionPreview, setCompletionPreview] = useState(null);
  const [completionLoading, setCompletionLoading] = useState(false);

  const fetchData = useCallback(async () => {
    if (!id || id === 'undefined') {
      toast.error('Invalid class ID');
      setLoading(false);
      return;
    }
    setLoading(true);
    setRosterLoadError('');
    try {
      const [classRes, studentRes] = await Promise.allSettled([
        classApi.getById(id),
        classApi.getStudents(id, {
          page: rosterPage,
          pageSize: rosterPageSize,
          search: rosterSearch || undefined,
          majorCode: rosterMajor || undefined,
          status: rosterStatus || undefined,
        }),
      ]);

      const classData = classRes.status === 'fulfilled' ? unwrapApiData(classRes.value) : null;
      if (!classData) {
        toast.error('Failed to load class');
        return null;
      }

      const rawClass = classData.class || classData;
      const currentClassId = String(id || rawClass.id || rawClass._id || '');
      const classViewModel = toClassViewModel(rawClass);
      const normalizedClass = {
        ...classViewModel,
        _id: classViewModel._id || currentClassId,
        schedule: classViewModel.schedules,
        isMajorLocked: rawClass.isEnrollmentMajorLocked ?? rawClass.isMajorLocked ?? false,
      };
      setCls(normalizedClass);
      const usesCompletedRoster = normalizedClass.status === 'Completed' ||
        (normalizedClass.status === 'Archived' && normalizedClass.statusBeforeArchive === 'Completed');
      if (usesCompletedRoster && rosterStatus !== 'Completed') {
        setRosterStatus('Completed');
      }

      let rawStudents = [];
      if (studentRes.status === 'fulfilled') {
        const sData = unwrapApiData(studentRes.value);
        rawStudents = sData.items || sData.students || sData.data || (Array.isArray(sData) ? sData : []);
        setRosterMeta({
          totalCount: sData.totalCount ?? rawStudents.length,
          page: sData.page ?? rosterPage,
          pageSize: sData.pageSize ?? rosterPageSize,
          totalPages: Math.max(1, sData.totalPages ?? 1),
        });
      } else if (rawClass.students) {
        rawStudents = rawClass.students;
      } else {
        setRosterLoadError(parseApiError(studentRes.reason, 'Failed to load the class roster.').message);
      }

      const mappedStudents = rawStudents.map((s, idx) => ({
        _id: s.studentId || s.id || s._id || `student-${idx}`,
        studentCode: s.rollNumber || s.studentCode,
        rollNumber: s.rollNumber || s.studentCode,
        fullName: s.fullName,
        email: s.email,
        major: s.majorCode || s.major,
        majorCode: s.majorCode || s.major,
        majorVerificationStatus: s.majorVerificationStatus || 'Unverified',
        enrollmentStatus: s.enrollmentStatus || 'Active',
        classId: currentClassId,
        teamId: s.teamId || null,
        teamName: s.teamName || null,
        isTeamLeader: s.isTeamLeader || false
      }));

      setStudents(mappedStudents);

      let rawTeams = [];
      let rawProposals = [];
      if (classFeatureFlags.teamManagement) {
        try {
          const [teamRes, proposalRes] = await Promise.all([
            classApi.getTeams(id),
            classApi.getTeamProposals(id),
          ]);
          const tData = unwrapApiData(teamRes);
          const pData = unwrapApiData(proposalRes);
          rawTeams = Array.isArray(tData) ? tData : [];
          rawProposals = Array.isArray(pData) ? pData : [];
        } catch {
          // Class and roster remain usable if team data cannot be loaded.
        }
      }

      setTeams(rawTeams.map(normalizeManagedTeam));
      setTeamProposals(rawProposals.map(normalizeTeamProposal));

      return classData;
    } catch (err) {
      toast.error(err?.message || 'Failed to load class');
      return null;
    } finally {
      setLoading(false);
    }
  }, [id, rosterMajor, rosterPage, rosterPageSize, rosterSearch, rosterStatus]);

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

  const handleBackfillChats = async () => {
    setBackfilling(true);
    try {
      const res: any = await classApi.repairChatMemberships(id);
      const summary = res?.data || res;
      toast.success(
        `Repair complete: ${summary.groupsCreated || 0} groups created, ${summary.membershipsAdded || 0} members added, ${summary.membershipsEnded || 0} stale memberships ended.`
      );
      await fetchData();
    } catch (e) {
      toast.error(parseApiError(e, 'Failed to repair chat memberships').message);
    } finally {
      setBackfilling(false);
    }
  };

  const handleToggleMajorLock = async () => {
    setTogglingLock(true);
    try {
      const res: any = cls.isMajorLocked
        ? await classApi.unlockMajors(id)
        : await classApi.lockMajors(id);
      setCls(prev => ({ ...prev, isMajorLocked: res.data.isLocked }));
      toast.success(res.message || 'Đã thay đổi trạng thái cập nhật chuyên ngành');
    } catch (err) {
      toast.error(parseApiError(err, 'Lỗi khi thay đổi trạng thái').message);
    } finally {
      setTogglingLock(false);
    }
  };

  const handleExportExcel = async () => {
    setExporting(true);
    try {
      const response = await classApi.exportClassExcel(id, {
        scope: rosterStatus === 'Active' ? 'Active' : 'History',
        search: rosterSearch || undefined,
        majorCode: rosterMajor || undefined,
        status: (rosterStatus || '') as '' | 'Active' | 'Dropped' | 'Completed',
      });
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
      toast.error(parseApiError(e, 'Failed to export students').message);
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
      await classApi.dropStudent(id, studentToDelete._id);
      toast.success('Enrollment dropped successfully');
      setStudentToDelete(null);
      await fetchData();
    } catch (err) {
      toast.error(parseApiError(err, 'Failed to drop enrollment').message);
    } finally {
      setRemovingStudent(false);
    }
  };

  const confirmDeleteClass = async () => {
    setDeletingClass(true);
    try {
      const isCurrentlyArchived = cls.status === 'Archived';
      if (isCurrentlyArchived) {
        await classApi.restore(id, { rowVersion: cls.rowVersion, reason: lifecycleReason.trim() });
        toast.success('Class restored successfully');
        await fetchData();
      } else {
        await classApi.archive(id, { rowVersion: cls.rowVersion, reason: lifecycleReason.trim() });
        toast.success('Class archived successfully');
        const targetRoute = user?.role === 'LECTURER' ? '/lecturer/classes' : '/admin/classes';
        navigate(targetRoute);
      }
    } catch (err) {
      toast.error(parseApiError(err, 'Failed to change class lifecycle').message);
    } finally {
      setDeletingClass(false);
      setShowDeleteClass(false);
      setLifecycleReason('');
    }
  };

  const openCompletionDialog = async () => {
    setCompletionLoading(true);
    try {
      if (cls.status === 'Completed') {
        setCompletionPreview(null);
        setShowCompletion(true);
        return;
      }
      const response = await classApi.getCompletionPreview(id);
      setCompletionPreview(unwrapApiData(response));
      setShowCompletion(true);
    } catch (err) {
      toast.error(parseApiError(err, 'Failed to preview class completion').message);
    } finally {
      setCompletionLoading(false);
    }
  };

  const confirmCompletion = async () => {
    setCompletionLoading(true);
    try {
      const payload = { rowVersion: cls.rowVersion, reason: completionReason.trim() };
      if (cls.status === 'Completed') {
        await classApi.reopen(id, payload);
        toast.success('Class reopened successfully');
      } else {
        await classApi.complete(id, payload);
        toast.success('Class completed successfully');
        setRosterStatus('Completed');
      }
      setShowCompletion(false);
      setCompletionReason('');
      setCompletionPreview(null);
      await fetchData();
    } catch (err) {
      toast.error(parseApiError(err, 'Failed to change class completion status').message);
    } finally {
      setCompletionLoading(false);
    }
  };

  if (loading) return <LoadingSkeleton />;
  if (!cls)    return <div className="text-center py-20 text-slate-400">Class not found.</div>;

  const safeStudents = Array.isArray(students) ? students : [];
  const safeTeams    = Array.isArray(teams) ? teams : [];
  const unassignedCount = safeStudents.filter(s => !s.teamId).length;
  
  const isAdmin = hasClassRole(user, 'ADMIN');
  const canManageClass = canManageClassPermission(user, cls);
  const isReadOnly = isClassReadOnly(cls.status);
  const isArchived = isArchivedClass(cls.status);
  const isCompleted = cls.status === 'Completed';
  const lifecyclePresentation = getClassLifecyclePresentation(cls.status);

  const getUniqueMentors = () => {
    const teamMentors = safeTeams
      .map(team => team.currentMentorAssignment?.mentor)
      .filter(Boolean)
      .map(mentor => ({ _id: mentor.mentorProfileId, name: mentor.fullName, email: mentor.email }));
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

    teamMentors.forEach(addMentor);
    return unique;
  };

  const activeMentors = getUniqueMentors();
  const schedules = Array.isArray(cls.schedule) ? cls.schedule : (cls.schedule ? [cls.schedule] : []);
  const primarySchedule = schedules[0];
  const scheduleDayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  const showDevelopmentControls = classFeatureFlags.showDevelopmentControls;
  const isFeatureVisible = (enabled) => enabled || showDevelopmentControls;
  const runFeatureAction = (enabled, featureName, action) => {
    if (!enabled) {
      toast(`${featureName} is visible for local development, but its API is not enabled yet.`);
      return;
    }

    action();
  };

  const confirmReEnrollStudent = async () => {
    if (!studentToReEnroll?._id) return;
    setReEnrollingStudent(true);
    try {
      await classApi.reEnrollStudent(id, studentToReEnroll._id);
      toast.success('Student re-enrolled successfully');
      setStudentToReEnroll(null);
      await fetchData();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to re-enroll student').message);
    } finally {
      setReEnrollingStudent(false);
    }
  };
  const teamControlsVisible = isFeatureVisible(classFeatureFlags.teamManagement);

  return (
    <div className="space-y-5">
      {/* ── Class heading + compact actions ── */}
      <section className="space-y-3">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => navigate(-1)}
            aria-label="Go back"
            className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-400 shadow-xs transition-all hover:border-slate-300 hover:bg-slate-50 hover:text-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/25"
          >
            <ArrowLeft className="h-4.5 w-4.5" />
          </button>
          <div className="min-w-0">
            <div className="flex items-center gap-1.5">
              <h1 className="truncate text-xl font-bold tracking-tight text-slate-900 sm:text-2xl">{cls.classCode}</h1>
              {!isReadOnly && isFeatureVisible(classFeatureFlags.rename) && canManageClass && (
                <button
                  type="button"
                  id="btn-rename-class"
                  onClick={() => runFeatureAction(classFeatureFlags.rename, 'Class rename', () => setShowRename(true))}
                  title="Đổi tên lớp"
                  aria-label="Đổi tên lớp"
                  className="inline-flex h-7 w-7 items-center justify-center rounded-md text-slate-400 transition-colors hover:bg-primary-50 hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/25"
                >
                  <Pencil className="h-3.5 w-3.5" />
                </button>
              )}
            </div>
            <p className="mt-0.5 truncate text-xs font-medium text-slate-500 sm:text-sm">
              {cls.subjectCode || '—'} · {cls.semester || '—'} {cls.year || ''}
            </p>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-1.5">
          {isFeatureVisible(classFeatureFlags.chatBackfill) && canManageClass && (
            <ClassActionButton
              icon={MessagesSquare}
              loading={backfilling}
              onClick={() => runFeatureAction(classFeatureFlags.chatBackfill, 'Chat membership repair', handleBackfillChats)}
              disabled={backfilling}
            >
              {backfilling ? 'Repairing...' : 'Repair Chats'}
            </ClassActionButton>
          )}

          {canManageClass && (
            <ClassActionButton icon={Download} loading={exporting} onClick={handleExportExcel} disabled={exporting}>
              Export
            </ClassActionButton>
          )}

          {!isReadOnly && canManageClass && (
            <>
              <ClassActionButton
                icon={Database}
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
              >
                Open Data Bank
              </ClassActionButton>

              <ClassActionButton icon={UserPlus} tone="primary" onClick={() => setShowAddStudent(true)}>
                Thêm 1 SV
              </ClassActionButton>

            </>
          )}

          {!isReadOnly && canManageClass && (isAdmin || classFeatureFlags.lecturerStudentImport) && (
            <ClassActionButton icon={Upload} tone="primary" onClick={() => setShowImport(true)}>
              Import Excel
            </ClassActionButton>
          )}

          {!isReadOnly && isFeatureVisible(classFeatureFlags.majorVerification) && canManageClass && (
            <ClassActionButton
              id="btn-verify-majors"
              icon={ShieldCheck}
              tone="indigo"
              onClick={() => runFeatureAction(classFeatureFlags.majorVerification, 'Major verification', () => setShowVerify(true))}
            >
              Kiểm tra Chuyên ngành
            </ClassActionButton>
          )}

          {!isReadOnly && canManageClass && (
            <ClassActionButton
              icon={cls.isMajorLocked ? Lock : Unlock}
              tone={cls.isMajorLocked ? 'danger' : 'success'}
              loading={togglingLock}
              onClick={() => runFeatureAction(classFeatureFlags.majorVerification, 'Major locking', handleToggleMajorLock)}
              disabled={togglingLock}
            >
              {cls.isMajorLocked ? 'Mở khóa cập nhật' : 'Khóa cập nhật CN'}
            </ClassActionButton>
          )}

          {((cls.status === 'Active' && canManageClass) || (isCompleted && isAdmin)) && (
            <ClassActionButton
              icon={isCompleted ? Play : CircleCheck}
              tone={isCompleted ? 'primary' : 'success'}
              loading={completionLoading}
              onClick={openCompletionDialog}
              disabled={completionLoading}
            >
              {isCompleted ? 'Reopen Class' : 'Complete Class'}
            </ClassActionButton>
          )}

          {isFeatureVisible(classFeatureFlags.lifecycle) && canManageClass && (
            <ClassActionButton
              icon={isArchived ? RotateCcw : Archive}
              tone={isArchived ? 'success' : 'danger'}
              onClick={() => runFeatureAction(classFeatureFlags.lifecycle, 'Class lifecycle management', () => setShowDeleteClass(true))}
            >
              {lifecyclePresentation.label}
            </ClassActionButton>
          )}
        </div>
      </section>

      {isReadOnly && (
        <div className="flex items-start gap-2.5 rounded-xl border border-amber-200 bg-amber-50 px-3.5 py-3 text-sm text-amber-800">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <div>
            <p className="font-semibold">{isArchived ? 'Archived class' : 'Completed class'} — read-only</p>
            <p className="mt-0.5 text-xs text-amber-700">
              {isArchived
                ? 'Roster, teams, chat membership and history are retained. Restore the class before changing operational data.'
                : 'Academic data and history are retained. Only an administrator can reopen the class; archive remains a separate lifecycle action.'}
            </p>
          </div>
        </div>
      )}

      {/* ── Info Cards Grid ── */}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {/* Lecturer Card */}
        <div className="group relative flex min-h-20 items-center justify-between gap-2.5 rounded-xl border border-slate-200/70 bg-white p-3.5 shadow-xs transition-all hover:border-slate-300 hover:shadow-sm">
          <div className="flex min-w-0 items-center gap-2.5">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary-100">
              <GraduationCap className="h-4.5 w-4.5 text-primary" />
            </div>
            <div className="min-w-0">
            </div>
            <div className="min-w-0">
              <p className="text-[10px] text-slate-400 uppercase font-semibold tracking-wider">Lecturer</p>
              <p className="font-semibold text-slate-800 text-sm truncate">{cls.lectureId?.name || <span className="text-amber-500">Not assigned</span>}</p>
              {cls.lectureId?.email && <p className="text-[11px] text-slate-400 truncate">{cls.lectureId.email}</p>}
            </div>
          </div>
          {!isReadOnly && user?.role === 'ADMIN' && (
            <button
              onClick={() => setShowAssignLecturer(true)}
              className="shrink-0 cursor-pointer rounded-md border border-primary-100 bg-primary-50 px-2 py-1 text-[11px] font-semibold text-primary transition-colors hover:border-primary-200 hover:bg-primary-100"
            >
              Edit
            </button>
          )}
        </div>

        {/* Schedule Card */}
        <div className="group relative flex min-h-20 items-center justify-between gap-2.5 rounded-xl border border-slate-200/70 bg-white p-3.5 shadow-xs transition-all hover:border-slate-300 hover:shadow-sm">
          <div className="flex min-w-0 items-center gap-2.5">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-indigo-100">
              <Calendar className="h-4.5 w-4.5 text-indigo-500" />
            </div>
            <div className="min-w-0">
              <p className="text-[10px] text-slate-400 uppercase font-semibold tracking-wider">Schedule</p>
              {primarySchedule ? (
                <div className="space-y-0.5">
                  {schedules.slice(0, 3).map((schedule, index) => {
                    const day = schedule.dayOfWeek ?? schedule.DayOfWeek;
                    const slot = schedule.slotNumber ?? schedule.SlotNumber ?? schedule.slot;
                    const room = schedule.room ?? schedule.Room ?? cls.room;
                    return <p key={`${day}-${slot}-${index}`} className="truncate text-xs font-semibold text-slate-700">{typeof day === 'number' ? scheduleDayNames[day] : day}, Slot {slot} · Room {room || 'TBD'}</p>;
                  })}
                  {schedules.length > 3 && <p className="text-[11px] font-medium text-primary">+{schedules.length - 3} more sessions</p>}
                </div>
              ) : (
                <p className="font-semibold text-slate-800 text-sm truncate">TBD</p>
              )}
            </div>
          </div>
          {!isReadOnly && canManageClass && (
            <button
              onClick={() => setShowEditSchedule(true)}
              className="shrink-0 cursor-pointer rounded-md border border-primary-100 bg-primary-50 px-2 py-1 text-[11px] font-semibold text-primary transition-colors hover:border-primary-200 hover:bg-primary-100"
            >
              Edit
            </button>
          )}
        </div>

        {/* Mentors Card */}
        <div className="group relative flex min-h-20 items-center justify-between gap-2.5 rounded-xl border border-slate-200/70 bg-white p-3.5 shadow-xs transition-all hover:border-slate-300 hover:shadow-sm">
          <div className="flex min-w-0 items-center gap-2.5">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-amber-100">
              <Users className="h-4.5 w-4.5 text-amber-500" />
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
          {!isReadOnly && isFeatureVisible(classFeatureFlags.mentorAssignment) && canManageClass && (
            <button
              onClick={() => runFeatureAction(classFeatureFlags.mentorAssignment, 'Mentor assignment', () => setShowAssignMentors(true))}
              className="shrink-0 cursor-pointer rounded-md border border-primary-100 bg-primary-50 px-2 py-1 text-[11px] font-semibold text-primary transition-colors hover:border-primary-200 hover:bg-primary-100"
            >
              Manage
            </button>
          )}
        </div>

        {/* Students Card */}
        <div className="flex min-h-20 items-center gap-2.5 rounded-xl border border-slate-200/70 bg-white p-3.5 shadow-xs transition-all hover:border-slate-300 hover:shadow-sm">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-secondary-100">
            <Users className="h-4.5 w-4.5 text-secondary" />
          </div>
          <div>
            <p className="text-[10px] text-slate-400 uppercase font-semibold tracking-wider">Students</p>
            <p className="mt-0.5 text-xl font-bold leading-none text-slate-900">{rosterLoadError ? '—' : (cls.studentCount ?? rosterMeta.totalCount)}</p>
            <p className="mt-1 text-[11px] text-slate-400">{rosterLoadError ? 'Unable to load roster' : `${unassignedCount} unassigned`}</p>
          </div>
        </div>

        {/* Teams Card */}
        {teamControlsVisible && <div className="flex min-h-20 items-center gap-2.5 rounded-xl border border-slate-200/70 bg-white p-3.5 shadow-xs transition-all hover:border-slate-300 hover:shadow-sm">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-green-100">
            <BookOpen className="h-4.5 w-4.5 text-green-600" />
          </div>
          <div>
            <p className="text-[10px] text-slate-400 uppercase font-semibold tracking-wider">Teams</p>
            <p className="mt-0.5 text-xl font-bold leading-none text-slate-900">{safeTeams.length}</p>
          </div>
        </div>}
      </div>

      {/* ── Team Generation Panel (always visible when students exist) ── */}
      {teamControlsVisible && safeStudents.length > 0 && selected.length > 0 && (
        <div className="sticky top-20 z-40 rounded-xl bg-white/85 shadow-elevated backdrop-blur-md">
          {user?.role === 'STUDENT' ? (
            <StudentTeamGeneratePanel
              classId={id}
              selected={selected}
              students={safeStudents}
              onTeamCreated={handleTeamCreated}
              currentStudentId={safeStudents.find(s => s.userId === user._id)?._id}
            />
          ) : (
            <div className="flex flex-col gap-2.5 rounded-xl border border-primary-100 bg-white p-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-center gap-2.5">
                <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary-50 text-primary"><Users className="h-4 w-4" /></div>
                <div>
                  <p className="text-sm font-semibold text-slate-800">{selected.length} student{selected.length === 1 ? '' : 's'} selected</p>
                  <p className="text-[11px] text-slate-500">Continue to enter the team name and review members.</p>
                </div>
              </div>
              <div className="flex flex-col gap-1.5 sm:flex-row">
                <button onClick={() => runFeatureAction(classFeatureFlags.teamManagement, 'Team management', () => openCreateTeam(selected))} className="inline-flex min-h-8 items-center justify-center gap-1.5 rounded-lg bg-gradient-primary px-2.5 py-1.5 text-xs font-semibold text-white shadow-xs hover:shadow-sm">
                  <UserPlus className="h-3.5 w-3.5" /> Create team with selected
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* ── Tabs ── */}
      <div className="flex w-fit gap-0.5 rounded-lg bg-slate-100 p-0.5">
        {(teamControlsVisible ? ['students', 'teams'] : ['students']).map(t => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`cursor-pointer rounded-md px-3.5 py-1.5 text-xs font-semibold capitalize transition-all ${
              tab === t ? 'bg-white text-slate-900 shadow-xs' : 'text-slate-500 hover:bg-white/60 hover:text-slate-700'
            }`}
          >
            {t === 'students' ? `Students (${rosterLoadError ? '—' : rosterMeta.totalCount})` : `Teams (${safeTeams.length})`}
          </button>
        ))}
      </div>

      {/* ── Tab Content ── */}
      <motion.div key={tab} initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ duration: 0.2 }}>
        {tab === 'students' && rosterLoadError ? (
          <div className="rounded-xl border border-red-200 bg-red-50 p-5 text-center">
            <AlertTriangle className="mx-auto h-6 w-6 text-red-500" />
            <p className="mt-2 text-sm font-semibold text-red-800">The class roster could not be loaded</p>
            <p className="mt-1 text-sm text-red-600">{rosterLoadError}</p>
            <button
              type="button"
              onClick={() => void fetchData()}
              className="mt-3 rounded-lg border border-red-300 bg-white px-3 py-1.5 text-xs font-semibold text-red-700 hover:bg-red-100"
            >
              Retry
            </button>
          </div>
        ) : tab === 'students' ? (
          <StudentTable
            students={safeStudents}
            teams={teamControlsVisible ? safeTeams : []}
            cls={cls}
            selected={teamControlsVisible ? selected : []}
            onSelectionChange={teamControlsVisible ? setSelected : undefined}
            onRefresh={fetchData}
            onDeleteStudent={!isReadOnly && canManageClass ? handleRemoveStudent : undefined}
            onReEnrollStudent={!isReadOnly && canManageClass ? setStudentToReEnroll : undefined}
            serverQuery={{
              search: rosterSearch,
              majorCode: rosterMajor,
              status: rosterStatus,
              page: rosterMeta.page,
              pageSize: rosterMeta.pageSize,
              totalCount: rosterMeta.totalCount,
              totalPages: rosterMeta.totalPages,
            }}
            onServerQueryChange={(next) => {
              if (Object.prototype.hasOwnProperty.call(next, 'search')) setRosterSearch(next.search);
              if (Object.prototype.hasOwnProperty.call(next, 'majorCode')) setRosterMajor(next.majorCode);
              if (Object.prototype.hasOwnProperty.call(next, 'status')) setRosterStatus(next.status);
              if (Object.prototype.hasOwnProperty.call(next, 'page')) setRosterPage(next.page);
              if (!Object.prototype.hasOwnProperty.call(next, 'page')) setRosterPage(1);
            }}
            toolbarAction={!isReadOnly && teamControlsVisible && selected.length === 0 ? (
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
                <button onClick={() => runFeatureAction(classFeatureFlags.teamManagement, 'Team management', () => openCreateTeam())} className="inline-flex min-h-8 items-center gap-1.5 rounded-lg border border-primary-200 bg-primary-50/60 px-2.5 py-1.5 text-xs font-semibold text-primary hover:bg-primary-50">
                  <UserPlus className="h-3.5 w-3.5" /> Create team
                </button>
              )
            ) : null}
          />
        ) : (
          <TeamList
            teams={[...safeTeams, ...teamProposals]}
            onReview={!isReadOnly && canManageClass ? (team) => setReviewTeam(team) : undefined}
            canDelete={!isReadOnly && canManageClass}
            canManageInfo={!isReadOnly && canManageClass}
            classStudents={safeStudents}
            onCreate={!isReadOnly && canManageClass ? () => runFeatureAction(classFeatureFlags.teamManagement, 'Team management', () => openCreateTeam()) : undefined}
            onEdit={!isReadOnly && canManageClass ? (team) => !team.isProposal && runFeatureAction(classFeatureFlags.teamManagement, 'Team management', () => openEditTeam(team)) : undefined}
            onDelete={undefined}
            onProjectDirection={!isReadOnly && classFeatureFlags.projectDirection ? setDirectionTeam : undefined}
          />
        )}
      </motion.div>

      {/* ── Modals ── */}
      {!isReadOnly && showImport && canManageClass && (
        <ImportStudentsModal
          classId={id || cls?._id}
          onClose={() => setShowImport(false)}
          onImported={() => {
            fetchData();
          }}
        />
      )}

      {!isReadOnly && classFeatureFlags.teamManagement && showTeamManagement && canManageClass && (
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

      {!isReadOnly && showEditSchedule && canManageClass && (
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

      {!isReadOnly && showAssignLecturer && user?.role === 'ADMIN' && (
        <AssignLectureModal
          classId={id}
          currentLecture={cls.lectureId}
          rowVersion={cls.rowVersion}
          allowUnassign={cls.status === 'Draft'}
          onClose={() => setShowAssignLecturer(false)}
          onAssigned={async () => {
            setShowAssignLecturer(false);
            await fetchData();
          }}
        />
      )}

      {!isReadOnly && classFeatureFlags.mentorAssignment && showAssignMentors && canManageClass && (
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
      {!isReadOnly && classFeatureFlags.rename && showRename && (
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
      {classFeatureFlags.majorVerification && showVerify && canManageClass && (
        <VerifyMajorModal
          classId={id}
          onClose={() => setShowVerify(false)}
        />
      )}

      {/* ── Add Student Modal ── */}
      {!isReadOnly && showAddStudent && canManageClass && (
        <AddStudentModal
          classId={id}
          onClose={() => setShowAddStudent(false)}
          onAdded={() => {
            setShowAddStudent(false);
            fetchData();
          }}
        />
      )}

      {classFeatureFlags.teamManagement && reviewTeam && canManageClass && (
        <ReviewTeamProposalModal
          team={reviewTeam}
          classStudents={safeStudents}
          onClose={() => setReviewTeam(null)}
          onRefresh={handleTeamCreated}
        />
      )}

      {classFeatureFlags.projectDirection && directionTeam && (
        <ProjectDirectionModal
          team={directionTeam}
          role={user?.role}
          onClose={() => setDirectionTeam(null)}
          onChanged={fetchData}
        />
      )}

      <ConfirmDialog
        isOpen={!!studentToDelete}
        onClose={() => setStudentToDelete(null)}
        onConfirm={confirmRemoveStudent}
        isSubmitting={removingStudent}
        title="Drop this enrollment?"
        description={
          studentToDelete
            ? `"${studentToDelete.fullName}" will move to Dropped history. This does not delete the global student profile.`
            : ''
        }
        confirmText="Drop enrollment"
        cancelText="Cancel"
      />

      <ConfirmDialog
        isOpen={!!studentToReEnroll}
        onClose={() => setStudentToReEnroll(null)}
        onConfirm={confirmReEnrollStudent}
        isSubmitting={reEnrollingStudent}
        title="Re-enroll this student?"
        description={studentToReEnroll ? `Restore "${studentToReEnroll.fullName}" as an Active enrollment in this class.` : ''}
        confirmText="Re-enroll"
        cancelText="Cancel"
      />

      <ConfirmDialog
        isOpen={showCompletion}
        onClose={() => { setShowCompletion(false); setCompletionReason(''); setCompletionPreview(null); }}
        onConfirm={confirmCompletion}
        isSubmitting={completionLoading}
        title={isCompleted ? 'Reopen this class?' : 'Complete this class?'}
        description={isCompleted
          ? `Class "${cls.classCode}" will return to Active. Completed enrollments become Active again; mentor assignments and proposals are not automatically restored.`
          : completionPreview?.blockers?.length
            ? `Completion is currently blocked: ${completionPreview.blockers.join(' ')}`
            : `All Active enrollments in "${cls.classCode}" will become Completed. Chat becomes read-only, active mentor assignments end, and open team proposals are cancelled.${completionPreview?.warnings?.length ? ` ${completionPreview.warnings.join(' ')}` : ''}`}
        confirmText={isCompleted ? 'Reopen class' : 'Complete class'}
        confirmVariant="primary"
        reason={completionReason}
        onReasonChange={setCompletionReason}
        reasonRequired
        confirmDisabled={!isCompleted && (completionPreview?.blockers?.length ?? 0) > 0}
      />

      {classFeatureFlags.lifecycle && canManageClass && <ConfirmDialog
        isOpen={showDeleteClass}
        onClose={() => setShowDeleteClass(false)}
        onConfirm={confirmDeleteClass}
        isSubmitting={deletingClass}
        title={isArchived ? 'Restore this class?' : 'Archive this class?'}
        description={isArchived
          ? `Class "${cls.classCode}" will be validated again for subject, semester, lecturer, schedule, and conflicts before becoming editable.`
          : `Class "${cls.classCode}" will become read-only and leave active lists. Roster, teams, chats, and history are retained.`}
        confirmText={lifecyclePresentation.confirmLabel}
        cancelText="Cancel"
        confirmVariant={isArchived ? 'primary' : 'danger'}
        reason={lifecycleReason}
        onReasonChange={setLifecycleReason}
        reasonRequired
      />}
    </div>
  );
}

