import { useState, useEffect, useContext, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { motion } from 'framer-motion';
import {
  ArrowLeft, GraduationCap, Users, BookOpen,
  Upload, Download, UserPlus, UserRoundCheck, Loader2, Calendar, Pencil, ShieldCheck, Lock, Unlock, AlertTriangle,
  Database, MessagesSquare, Archive, RotateCcw, CircleCheck, Play
} from 'lucide-react';
import { AuthContext } from '../../context/AuthContext';
import { classApi } from '../../api/classApi';
import { teamApi } from '../../api/teamApi';
import { userApi } from '../../api/userApi';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import StudentTable from '../../components/class/StudentTable';
import TeamList from '../../components/class/TeamList';
import TeamManagementModal from '../../components/class/TeamManagementModal';
import StudentAssignmentModal from '../../components/class/StudentAssignmentModal';
import TeamCreationSummary from '../../components/class/TeamCreationSummary';
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
import { getTeamMemberIds, normalizeManagedTeam, normalizeTeamProposal, resolveEffectiveTeamMajor } from '../../utils/teamManagement';
import { directoryRecordToStudent, mergeAssignmentCandidates, normalizeClassStudents } from '../../utils/studentAssignment';
import { classFeatureFlags } from '../../config/classFeatureFlags';
import { parseApiError } from '../../utils/apiError';
import { toClassViewModel, unwrapApiData } from '../../utils/classMappers';
import { getClassLifecyclePresentation, isArchivedClass, isClassReadOnly } from '../../utils/classComponentPolicy';
import { canManageClass as canManageClassPermission, hasClassRole } from '../../utils/classPermissions';
import type { ClassCompletionPreview } from '../../types/classes';
import type { StudentAssignmentMode } from '../../types/studentAssignment';

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
  const { slug: id } = useParams();
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
  const [selectedStudentSnapshots, setSelectedStudentSnapshots] = useState([]);
  const [selectedLeaderId, setSelectedLeaderId] = useState('');

  // Modals & Actions
  const [showImport, setShowImport] = useState(false);
  const [showTeamManagement, setShowTeamManagement] = useState(false);
  const [showStudentAssignment, setShowStudentAssignment] = useState(false);
  const [assignmentMode, setAssignmentMode] = useState<StudentAssignmentMode>('CLASS');
  const [assignmentInitialIds, setAssignmentInitialIds] = useState([]);
  const [assignmentCandidates, setAssignmentCandidates] = useState([]);
  const [assignmentCandidatesLoading, setAssignmentCandidatesLoading] = useState(false);
  const [teamToEdit, setTeamToEdit] = useState(null);
  const [teamFormMemberIds, setTeamFormMemberIds] = useState([]);
  const [teamFormLeaderId, setTeamFormLeaderId] = useState('');
  const [showAddStudent, setShowAddStudent] = useState(false);
  const [showEditSchedule, setShowEditSchedule] = useState(false);
  const [showAssignLecturer, setShowAssignLecturer] = useState(false);
  const [showAssignMentors, setShowAssignMentors] = useState(false);
  const [showRename, setShowRename] = useState(false);
  const [showVerify, setShowVerify] = useState(false);
  const [reviewTeam, setReviewTeam] = useState(null);
  const [teamToDelete, setTeamToDelete] = useState(null);
  const [directionTeam, setDirectionTeam] = useState(null);
  const [studentToDelete, setStudentToDelete] = useState(null);
  const [studentToReEnroll, setStudentToReEnroll] = useState(null);
  const [backfilling, setBackfilling] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [togglingLock, setTogglingLock] = useState(false);
  const [synchronizingMajors, setSynchronizingMajors] = useState(false);
  const [removingStudent, setRemovingStudent] = useState(false);
  const [reEnrollingStudent, setReEnrollingStudent] = useState(false);
  const [showDeleteClass, setShowDeleteClass] = useState(false);
  const [deletingClass, setDeletingClass] = useState(false);
  const [deletingTeam, setDeletingTeam] = useState(false);
  const [lifecycleReason, setLifecycleReason] = useState('');
  const [showCompletion, setShowCompletion] = useState(false);
  const [completionReason, setCompletionReason] = useState('');
  const [completionPreview, setCompletionPreview] = useState<ClassCompletionPreview | null>(null);
  const [completionLoading, setCompletionLoading] = useState(false);

  const fetchData = useCallback(async () => {
    if (!id || id === 'undefined') {
      toast.error('Invalid class identifier');
      setLoading(false);
      return null;
    }

    setLoading(true);
    setRosterLoadError('');

    try {
      const classRes = await classApi.getById(id);
      const classData = unwrapApiData(classRes);
      if (!classData) {
        toast.error('Failed to load class');
        return null;
      }

      const rawClass = classData.class || classData;
      const classViewModel = toClassViewModel(rawClass);
      const currentClassId = String(classViewModel.id || classViewModel._id || rawClass.id || rawClass._id || '');
      const canonicalSlug = String(classViewModel.slug || rawClass.slug || '').trim();

      if (canonicalSlug && id !== canonicalSlug) {
        navigate(`/classes/${canonicalSlug}`, { replace: true });
      }

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

      const studentRes = await Promise.resolve(classApi.getStudents(currentClassId, {
        page: rosterPage,
        pageSize: rosterPageSize,
        search: rosterSearch || undefined,
        majorCode: rosterMajor || undefined,
        status: rosterStatus || undefined,
      })).then(
        value => ({ status: 'fulfilled' as const, value }),
        reason => ({ status: 'rejected' as const, reason }),
      );

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

      const mappedStudents = rawStudents.map((s, idx) => {
        const effectiveMajor = resolveEffectiveTeamMajor(s.majorCode, s.profileMajorCode, s.major);
        const registeredMajor = typeof s.profileMajorCode === 'string'
          ? s.profileMajorCode.trim().toUpperCase()
          : '';
        const hasMajorMismatch = Boolean(
          effectiveMajor &&
          registeredMajor &&
          effectiveMajor !== 'UNDECLARED' &&
          registeredMajor !== 'UNDECLARED' &&
          effectiveMajor !== registeredMajor,
        );
        return {
          _id: s.studentId || s.id || s._id || `student-${idx}`,
          studentCode: s.rollNumber || s.studentCode,
          rollNumber: s.rollNumber || s.studentCode,
          fullName: s.fullName,
          email: s.email,
          major: effectiveMajor,
          majorCode: effectiveMajor,
          profileMajorCode: registeredMajor || null,
          hasMajorMismatch,
          majorVerificationStatus: s.majorVerificationStatus || 'Unverified',
          enrollmentStatus: s.enrollmentStatus || 'Active',
          classId: currentClassId,
          teamId: s.teamId || null,
          teamName: s.teamName || null,
          isTeamLeader: s.isTeamLeader || false
        };
      });

      let rawTeams = [];
      let rawProposals = [];
      if (classFeatureFlags.teamManagement) {
        try {
          const [teamRes, proposalRes] = await Promise.all([
            classApi.getTeams(currentClassId),
            classApi.getTeamProposals(currentClassId),
          ]);
          const tData = unwrapApiData(teamRes);
          const pData = unwrapApiData(proposalRes);
          rawTeams = Array.isArray(tData) ? tData : [];
          rawProposals = Array.isArray(pData) ? pData : [];
        } catch {
          // Class and roster remain usable if team data cannot be loaded.
        }
      }

      const normalizedTeams = rawTeams.map(normalizeManagedTeam);
      const normalizedProposals = rawProposals.map(normalizeTeamProposal);
      const reservedTeamIds = new Map();
      normalizedProposals.forEach((proposal) => {
        const proposalStatus = String(proposal.status || '').toUpperCase();
        if (!['DRAFT', 'PENDING', 'NEEDS_REVISION', 'NEEDSREVISION'].includes(proposalStatus)) return;
        getTeamMemberIds(proposal).forEach((studentId) => reservedTeamIds.set(studentId, proposal._id));
      });

      setStudents(mappedStudents.map((student) => ({
        ...student,
        teamId: student.teamId || reservedTeamIds.get(student._id) || null,
      })));
      setTeams(normalizedTeams);
      setTeamProposals(normalizedProposals);

      return classData;
    } catch (err) {
      toast.error(err?.message || 'Failed to load class');
      return null;
    } finally {
      setLoading(false);
    }
  }, [id, navigate, rosterMajor, rosterPage, rosterPageSize, rosterSearch, rosterStatus]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    if (selectedLeaderId && !selected.includes(selectedLeaderId)) {
      setSelectedLeaderId('');
    }
  }, [selected, selectedLeaderId]);

  useEffect(() => {
    setSelectedStudentSnapshots((current) => {
      const snapshots = new Map(current.map((student) => [student._id, student]));
      students.forEach((student) => {
        if (selected.includes(student._id)) {
          snapshots.set(student._id, student);
        }
      });

      return selected
        .map((studentId) => snapshots.get(studentId))
        .filter(Boolean);
    });
  }, [selected, students]);

  const handleTeamCreated = async () => {
    setSelected([]);
    setSelectedLeaderId('');
    await fetchData();
  };

  const openCreateTeam = (memberIds = [], leaderId = '') => {
    setTeamToEdit(null);
    setTeamFormMemberIds(memberIds);
    setTeamFormLeaderId(leaderId);
    setShowTeamManagement(true);
  };

  const openEditTeam = (team) => {
    setTeamToEdit(team);
    setTeamFormMemberIds([]);
    setTeamFormLeaderId('');
    setShowTeamManagement(true);
  };

  const closeTeamManagement = () => {
    setShowTeamManagement(false);
    setTeamToEdit(null);
    setTeamFormMemberIds([]);
    setTeamFormLeaderId('');
  };

  const handleTeamSaved = () => {
    setSelected([]);
    setSelectedLeaderId('');
    closeTeamManagement();
    void fetchData();
  };

  const openStudentAssignment = async (mode: StudentAssignmentMode = 'CLASS', initialStudentIds = []) => {
    setAssignmentMode(mode);
    setAssignmentInitialIds(initialStudentIds);
    setShowStudentAssignment(true);
    setAssignmentCandidatesLoading(true);

    const classCandidates = normalizeClassStudents(students, loadedClassId);
    setAssignmentCandidates(classCandidates);
    try {
      const response = await userApi.getAll({ role: 'STUDENT', status: 'APPROVED', page: 1, limit: 200 });
      const data = unwrapApiData(response);
      const records = Array.isArray(data?.users) ? data.users : Array.isArray(data) ? data : [];
      const directoryCandidates = records.map(directoryRecordToStudent).filter(Boolean);
      setAssignmentCandidates(mergeAssignmentCandidates(classCandidates, directoryCandidates));
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to load the student directory.').message);
    } finally {
      setAssignmentCandidatesLoading(false);
    }
  };

  const handleStudentAssignment = async (result) => {
    if (result.mode === 'CLASS') {
      await classApi.assignStudents(loadedClassId, { studentIds: result.assignedStudentIds });
    } else {
      await classApi.assignStudentsToTeam(loadedClassId, result.teamId, { studentIds: result.assignedStudentIds });
    }
    setShowStudentAssignment(false);
    setAssignmentInitialIds([]);
    await fetchData();
  };

  const confirmDeleteTeam = async () => {
    if (!teamToDelete?._id) return;
    setDeletingTeam(true);
    try {
      await teamApi.deleteTeam(teamToDelete._id);
      toast.success('Team archived and members were unassigned.');
      setTeamToDelete(null);
      await fetchData();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to delete the team.').message);
    } finally {
      setDeletingTeam(false);
    }
  };

  const handleBackfillChats = async () => {
    setBackfilling(true);
    try {
      const res: any = await classApi.repairChatMemberships(loadedClassId);
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
        ? await classApi.unlockMajors(loadedClassId)
        : await classApi.lockMajors(loadedClassId);
      setCls(prev => ({ ...prev, isMajorLocked: res.data.isLocked }));
      toast.success(res.message || 'Major update lock status changed successfully.');
    } catch (err) {
      toast.error(parseApiError(err, 'Failed to change the major update lock status.').message);
    } finally {
      setTogglingLock(false);
    }
  };

  const handleSynchronizeMajors = async () => {
    const mismatchCount = safeStudents.filter(student => student.hasMajorMismatch).length;
    if (mismatchCount === 0 || !window.confirm(
      `Synchronize registered majors from the official class enrollment for ${mismatchCount} student${mismatchCount > 1 ? 's' : ''}?`,
    )) return;

    setSynchronizingMajors(true);
    try {
      const response: any = await classApi.synchronizeProfileMajors(loadedClassId);
      const result = unwrapApiData(response);
      toast.success(`Synchronized ${result.synchronizedCount ?? 0} registered major${result.synchronizedCount === 1 ? '' : 's'}.`);
      await fetchData();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to synchronize registered majors.').message);
    } finally {
      setSynchronizingMajors(false);
    }
  };

  const handleExportExcel = async () => {
    setExporting(true);
    try {
      const response = await classApi.exportClassExcel(loadedClassId, {
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
      await classApi.dropStudent(loadedClassId, studentToDelete._id);
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
        await classApi.restore(loadedClassId, { rowVersion: cls.rowVersion, reason: lifecycleReason.trim() });
        toast.success('Class restored successfully');
        await fetchData();
      } else {
        await classApi.archive(loadedClassId, { rowVersion: cls.rowVersion, reason: lifecycleReason.trim() });
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
      const response = await classApi.getCompletionPreview(loadedClassId);
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
      const payload = {
        rowVersion: cls.status === 'Completed'
          ? cls.rowVersion
          : completionPreview?.rowVersion ?? cls.rowVersion,
        reason: completionReason.trim(),
      };
      if (cls.status === 'Completed') {
        await classApi.reopen(loadedClassId, payload);
        toast.success('Class reopened successfully');
      } else {
        await classApi.complete(loadedClassId, payload);
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
  const selectedTeamStudents = selectedStudentSnapshots.filter(student => selected.includes(student._id));
  const unassignedCount = safeStudents.filter(s => !s.teamId).length;
  
  const isAdmin = hasClassRole(user, 'ADMIN');
  const canManageClass = canManageClassPermission(user, cls);
  const isReadOnly = isClassReadOnly(cls.status);
  const isArchived = isArchivedClass(cls.status);
  const isCompleted = cls.status === 'Completed';

  const canSelectTeamMembers =
    !isReadOnly && (canManageClass || hasClassRole(user, 'STUDENT'));
  const lifecyclePresentation = getClassLifecyclePresentation(cls.status);
  const loadedClassId = cls?._id || cls?.id || id;

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
      await classApi.reEnrollStudent(loadedClassId, studentToReEnroll._id);
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
                  title="Rename class"
                  aria-label="Rename class"
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
                Add student
              </ClassActionButton>

              <ClassActionButton icon={UserRoundCheck} tone="secondary" onClick={() => openStudentAssignment('CLASS')}>
                Assign students
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
              Verify majors
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
              {cls.isMajorLocked ? 'Unlock major updates' : 'Lock major updates'}
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
            {cls.completedAtUtc && (
              <p className="mt-2 text-xs text-amber-800">
                <span className="font-semibold">Completed:</span>{' '}
                {new Date(cls.completedAtUtc).toLocaleString()}
                {cls.completionReason ? ` — ${cls.completionReason}` : ''}
              </p>
            )}
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
      {teamControlsVisible && safeStudents.length > 0 && tab === 'students' && canSelectTeamMembers && (
        <div className="rounded-xl bg-white/85 shadow-elevated backdrop-blur-md">
          {user?.role === 'STUDENT' ? selected.length > 0 ? (
            <StudentTeamGeneratePanel
              classId={loadedClassId}
              selected={selected}
              students={safeStudents}
              onTeamCreated={handleTeamCreated}
              currentStudentId={safeStudents.find(s => s.userId === user._id)?._id}
            />
          ) : null : canManageClass ? (
            <TeamCreationSummary
              selectedStudents={selectedTeamStudents}
              selectedLeaderId={selectedLeaderId}
              onLeaderChange={setSelectedLeaderId}
              onCreateTeam={() => runFeatureAction(
                classFeatureFlags.teamManagement,
                'Team management',
                () => openCreateTeam(selected, selectedLeaderId),
              )}
            />
          ) : null}
        </div>
      )}

      {/* ── Tabs ── */}
      <div className="flex w-fit gap-0.5 rounded-lg bg-slate-100 p-0.5">
        {(teamControlsVisible ? ['students', 'teams'] : ['students']).map(t => (
          <button
            key={t}
            type="button"
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
            teams={teamControlsVisible ? [...safeTeams, ...teamProposals] : []}
            cls={cls}
            selected={teamControlsVisible && canSelectTeamMembers ? selected : []}
            onSelectionChange={teamControlsVisible && canSelectTeamMembers ? setSelected : undefined}
            maxSelection={6}
            onRefresh={fetchData}
            onDeleteStudent={!isReadOnly && canManageClass ? handleRemoveStudent : undefined}
            onReEnrollStudent={!isReadOnly && canManageClass ? setStudentToReEnroll : undefined}
            onSynchronizeMajors={!isReadOnly && canManageClass ? handleSynchronizeMajors : undefined}
            synchronizingMajors={synchronizingMajors}
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
            toolbarAction={canSelectTeamMembers && teamControlsVisible && selected.length === 0 ? (
              user?.role === 'STUDENT' ? (
                <TeamSuggestionTooltip label="View team creation guidance">
                  <div className="space-y-2">
                    <p className="font-semibold text-white">
                      {unassignedCount} students are not assigned to a team
                    </p>
                    <p className="text-slate-200">
                      Select yourself and the other members in the table to start a team proposal.
                      A team requires 4–6 members, including at least one BBA student and one BIT student.
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
            onDelete={!isReadOnly && canManageClass ? setTeamToDelete : undefined}
            onProjectDirection={!isReadOnly && classFeatureFlags.projectDirection ? setDirectionTeam : undefined}
          />
        )}
      </motion.div>

      {/* ── Modals ── */}
      {!isReadOnly && showImport && canManageClass && (
        <ImportStudentsModal
          classId={loadedClassId}
          onClose={() => setShowImport(false)}
          onImported={() => {
            fetchData();
          }}
        />
      )}

      {!isReadOnly && classFeatureFlags.teamManagement && showTeamManagement && canManageClass && (
        <TeamManagementModal
          classInfo={{
            id: loadedClassId,
            code: cls.classCode || 'Class',
            name: cls.subjectName || cls.subjectCode || '',
          }}
          students={safeStudents}
          teams={safeTeams}
          team={teamToEdit}
          initialMemberIds={teamFormMemberIds}
          initialLeaderId={teamFormLeaderId}
          onClose={closeTeamManagement}
          onSave={handleTeamSaved}
        />
      )}

      {!isReadOnly && showStudentAssignment && canManageClass && (
        <StudentAssignmentModal
          classInfo={{
            id: loadedClassId,
            code: cls.classCode || 'Class',
            name: cls.subjectName || cls.subjectCode || '',
          }}
          students={assignmentCandidates}
          teams={safeTeams}
          initialMode={assignmentMode}
          initialStudentIds={assignmentInitialIds}
          loadingCandidates={assignmentCandidatesLoading}
          onClose={() => setShowStudentAssignment(false)}
          onSave={handleStudentAssignment}
        />
      )}

      {!isReadOnly && showEditSchedule && canManageClass && (
        <EditScheduleModal
          classId={loadedClassId}
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
          classId={loadedClassId}
          semester={cls.semester}
          year={cls.year}
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
          classId={loadedClassId}
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
          classId={loadedClassId}
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
          classId={loadedClassId}
          onClose={() => setShowVerify(false)}
        />
      )}

      {/* ── Add Student Modal ── */}
      {!isReadOnly && showAddStudent && canManageClass && (
        <AddStudentModal
          classId={loadedClassId}
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
        isOpen={!!teamToDelete}
        onClose={() => setTeamToDelete(null)}
        onConfirm={confirmDeleteTeam}
        isSubmitting={deletingTeam}
        title={`Delete ${teamToDelete?.teamName || 'this team'}?`}
        description="This will archive the team, remove the team chat group, and unassign all members. Teams with project, proposal, evaluation, checkpoint, or task data cannot be deleted."
        confirmText="Delete team"
        cancelText="Cancel"
      />

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

