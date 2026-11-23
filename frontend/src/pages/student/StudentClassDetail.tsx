import { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ArrowRight, ChevronLeft, GraduationCap, Users, Mail, Loader2, LayoutGrid, Lock, UserPlus } from 'lucide-react';
import toast from 'react-hot-toast';
import { classApi } from '../../api/classApi';
import { teamFormationApi } from '../../api/teamFormationApi';
import TeamList from '../../components/class/TeamList';
import OwnTeamMajorWarning from '../../components/class/OwnTeamMajorWarning';
import StudentTable from '../../components/class/StudentTable';
import StudentTeamGeneratePanel from '../../components/class/StudentTeamGeneratePanel';
import TeamFormationCard from '../../components/class/TeamFormationCard';
import StudentMajorActionCard, { type StudentMajorNextAction } from '../../components/class/StudentMajorActionCard';
import type { TeamFormation } from '../../types/teamFormation';
import TeamSuggestionTooltip from '../../components/class/TeamSuggestionTooltip';
import { useAuth } from '../../hooks/useAuth';
import { unwrapApiData } from '../../utils/classMappers';
import { isOpenFormationForMe, remainingInviteSlots } from '../../utils/teamFormation';
import { entityId, getTeamMajorWarning, normalizeManagedTeam, normalizeTeamProposal, getTeamMemberIds, isMissingTeamMajor, isVerifiedEnrollmentMajor, mergeTeamsWithLinkedProposals, prioritizeOwnTeams, sortTeamsByCode } from '../../utils/teamManagement';
import ProjectDirectionModal from '../../components/class/ProjectDirectionModal';
import { teamApi } from '../../api/teamApi';
import { parseApiError } from '../../utils/apiError';
import { formatSemesterCode } from '../../utils/semester';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import Modal from '../../components/ui/Modal';
import { subscribeProjectDirectionRealtime } from '../../api/projectDirectionRealtime';
import { updateOwnMajor } from '../../api/authApi';
import type { ClassMentorSummary } from '../../types/classes';
import { useTranslation } from 'react-i18next';

const mentorInitials = (fullName: string) => fullName
  .trim()
  .split(/\s+/)
  .filter(Boolean)
  .slice(-2)
  .map(part => part[0]?.toUpperCase())
  .join('') || 'M';

const mentorRoleLabel = (mentorType?: string) => {
  if (mentorType === 'Academic') return 'Academic mentor';
  if (mentorType === 'Enterprise') return 'Enterprise mentor';
  return 'Mentor';
};

export default function StudentClassDetail() {
  const { t } = useTranslation('class');
  const { slug: id } = useParams();
  const navigate = useNavigate();
  const { user, updateUser } = useAuth();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('classmates');
  const [selected, setSelected] = useState([]);
  const [proposals, setProposals] = useState([]);
  const [formations, setFormations] = useState<TeamFormation[]>([]);
  const [formationError, setFormationError] = useState(false);
  const [proposalToRevise, setProposalToRevise] = useState(null);
  const [directionTeam, setDirectionTeam] = useState(null);
  const [proposalToCancel, setProposalToCancel] = useState(null);
  const [cancellationReason, setCancellationReason] = useState('');
  const [cancellingProposal, setCancellingProposal] = useState(false);
  const [updatingMajor, setUpdatingMajor] = useState(false);
  const [isMentorModalOpen, setIsMentorModalOpen] = useState(false);

  const fetchClassDetail = useCallback(async () => {
    try {
      const detailResponse = await classApi.getMyClassDetail(id);
      const detail = unwrapApiData<any>(detailResponse as any);
      const classInfo = detail?.class || {};
      const currentClassId = String(classInfo.id || classInfo._id || id || '');
      const canonicalSlug = String(classInfo.slug || '').trim();

      if (canonicalSlug && id !== canonicalSlug) {
        navigate(`/student/classes/${canonicalSlug}`, { replace: true });
      }

      const proposalResponse = await classApi.getTeamProposals(currentClassId);
      const normalizedStudents = (detail?.students || []).map(student => ({
        ...student,
        _id: student.studentId,
        major: student.majorCode,
        enrollmentStatus: student.enrollmentStatus || classInfo.enrollmentStatus || 'Active',
        classId: currentClassId,
      }));
      setSelected(current => current.filter(studentId => {
        const student = normalizedStudents.find(item => item._id === studentId);
        return student
          && !student.teamId
          && !student.hasPendingTeamInvitation
          && student.enrollmentStatus === 'Active';
      }));
      const normalizedTeams = (detail?.teams || []).map(normalizeManagedTeam);
      setData({ ...detail, students: normalizedStudents, teams: normalizedTeams });
      const proposalData = unwrapApiData(proposalResponse);
      setProposals((Array.isArray(proposalData) ? proposalData : []).map(normalizeTeamProposal));
      try {
        const formationResponse = await teamFormationApi.mine(currentClassId);
        const formationData = unwrapApiData<TeamFormation[]>(formationResponse);
        setFormations(Array.isArray(formationData) ? formationData : []);
        setFormationError(false);
      } catch {
        setFormationError(true);
      }
    } catch (err) {
      toast.error(err?.message || t('studentClassDetail.failedToLoadClassDetails'));
      navigate('/student/classes');
    } finally {
      setLoading(false);
    }
  }, [id, navigate]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchClassDetail();
  }, [fetchClassDetail]);

  useEffect(() => subscribeProjectDirectionRealtime((event) => {
    const currentClassId = String(data?.class?.id || data?.class?._id || '');
    if (currentClassId && (event.eventType === 'ClassMajorUpdated' || event.eventType === 'ClassMajorsChanged'
      || event.eventType === 'TeamProposalReviewed'
      || event.eventType === 'TeamFormationChanged' || event.eventType === 'TeamCreated'
      || event.eventType === 'ProjectDirectionReviewed')
      && String(event.classId) === currentClassId) {
      void fetchClassDetail();
    }
  }, () => {
    void fetchClassDetail();
  }), [data?.class?.id, data?.class?._id, fetchClassDetail]);

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh]">
        <Loader2 className="w-8 h-8 text-primary animate-spin" />
        <p className="text-sm text-slate-400 mt-2 font-medium">{t('studentClassDetail.loadingClassDetails')}</p>
      </div>
    );
  }

  const cls      = data?.class;
  const loadedClassId = cls?.id || cls?._id || id;
  const students = Array.isArray(data?.students) ? data.students : [];
  const teams    = Array.isArray(data?.teams) ? data.teams : [];
  const lecturer = cls?.lectureId;
  const classMentors: ClassMentorSummary[] = Array.isArray(cls?.mentors)
    ? [...cls.mentors].sort((left, right) => {
        const typeOrder = { Enterprise: 0, Academic: 1 } as Record<string, number>;
        const byType = (typeOrder[left.mentorType || ''] ?? 2) - (typeOrder[right.mentorType || ''] ?? 2);
        return byType || (left.fullName || '').localeCompare(right.fullName || '', 'vi');
      })
    : [];
  const isReadOnly = cls?.classStatus === 'Completed' || cls?.classStatus === 'Archived';
  const currentUserId = (user?._id || user?.id || '').toString();
  const currentStudent = students.find(s => {
    const studentUserId = (s.userId?._id || s.userId || '').toString();
    return (studentUserId && studentUserId === currentUserId)
      || (user?.email && s.email?.toLowerCase() === user.email.toLowerCase());
  });
  const hasTeam = Boolean(currentStudent?.teamId);
  const ownTeam = currentStudent?.teamId
    ? teams.find(team => String(team._id) === String(currentStudent.teamId))
    : undefined;
  const ownTeamMajorWarning = ownTeam ? getTeamMajorWarning(ownTeam) : null;
  // The student's own team (and own proposals) come first so they do not have to scroll to find them.
  const displayedTeams = prioritizeOwnTeams(
    mergeTeamsWithLinkedProposals(sortTeamsByCode(teams), proposals),
    currentStudent?._id,
    currentStudent?.teamId,
  );
  const majorActionRequired = Boolean(currentStudent && isMissingTeamMajor(currentStudent.major));
  const majorVerified = isVerifiedEnrollmentMajor(currentStudent?.majorVerificationStatus);
  const openFormations = formations.filter(isOpenFormationForMe);
  const hasPendingFormation = openFormations.length > 0;
  // The creator keeps inviting classmates into their own pending formation while slots remain.
  const ownOpenFormation = openFormations.find(formation => formation.creatorStudentId === currentStudent?._id) ?? null;
  const inviteSlotsLeft = ownOpenFormation ? remainingInviteSlots(ownOpenFormation) : 0;
  const canInviteMore = inviteSlotsLeft > 0;
  const reservedProposal = proposals.find((proposal) => {
    const status = String(proposal.status || '').toUpperCase();
    return ['DRAFT', 'PENDING', 'NEEDS_REVISION', 'NEEDSREVISION'].includes(status)
      && getTeamMemberIds(proposal).includes(currentStudent?._id || '');
  });
  const canEditReservedProposal = Boolean(
    proposalToRevise
    && reservedProposal?._id === proposalToRevise._id
    && ['NEEDS_REVISION', 'NEEDSREVISION'].includes(String(reservedProposal.status || '').toUpperCase()),
  );
  const reservedProposalStatus = String(reservedProposal?.status || '').toUpperCase();
  const reservedProposalTeamId = entityId(reservedProposal?.approvedTeamId);
  const isPendingProjectProposal = reservedProposalStatus === 'PENDING' && Boolean(reservedProposalTeamId);
  const selectionDisabled = isReadOnly
    || majorActionRequired
    || (hasTeam && !canEditReservedProposal)
    || (hasPendingFormation && !canInviteMore)
    || Boolean(reservedProposal && !canEditReservedProposal);

  const handleTeamCreated = async () => {
    setSelected([]);
    setProposalToRevise(null);
    await fetchClassDetail();
    setActiveTab('classmates');
  };

  const startTeamProposal = () => {
    if (majorActionRequired) {
      toast.error(t('studentClassDetail.selectYourMajorBeforeCreatingA'));
      document.getElementById('major-action-card')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      document.getElementById('select-major-action')?.focus();
      return;
    }
    if (!currentStudent?._id) {
      toast.error(t('studentClassDetail.yourStudentProfileCouldNotBe'));
      return;
    }
    setProposalToRevise(null);
    setSelected([currentStudent._id]);
    setActiveTab('classmates');
  };

  const handleOwnMajorChange = async (_student, majorCode) => {
    if (updatingMajor || !majorCode) return false;
    setUpdatingMajor(true);
    try {
      const result = await updateOwnMajor(majorCode);
      updateUser({ major: result.majorCode, majorCode: result.majorCode });
      toast.success(
        result.updatedEnrollmentCount > 1
          ? t('studentClassDetail.majorUpdatedInYourProfileAnd', { updatedEnrollmentCount: result.updatedEnrollmentCount })
          : t('studentClassDetail.majorUpdatedSuccessfully'),
      );
      await fetchClassDetail();
      return true;
    } catch (error) {
      toast.error(parseApiError(error, t('studentClassDetail.failedToUpdateYourMajor')).message);
      return false;
    } finally {
      setUpdatingMajor(false);
    }
  };

  const requestProposalCancellation = (proposal) => {
    setProposalToCancel(proposal);
    setCancellationReason('');
  };

  const closeCancellationDialog = () => {
    if (cancellingProposal) return;
    setProposalToCancel(null);
    setCancellationReason('');
  };

  const cancelProposal = async () => {
    if (!proposalToCancel || cancellationReason.trim().length < 3) return;

    setCancellingProposal(true);
    try {
      await teamApi.cancelProposal(
        proposalToCancel._id,
        proposalToCancel.rowVersion,
        cancellationReason.trim(),
      );
      toast.success(t('studentClassDetail.teamProposalCancelled'));
      setProposalToCancel(null);
      setCancellationReason('');
      await fetchClassDetail();
    } catch (error) {
      toast.error(parseApiError(error, t('studentClassDetail.failedToCancelTeamProposal')).message);
    } finally {
      setCancellingProposal(false);
    }
  };

  const scrollToTeamSection = (targetId: string) => {
    window.requestAnimationFrame(() => {
      document.getElementById(targetId)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
  };

  const openReservedProposal = () => {
    const needsRevision = ['NEEDS_REVISION', 'NEEDSREVISION'].includes(reservedProposalStatus);
    if (needsRevision && reservedProposal) {
      setProposalToRevise(reservedProposal);
      setSelected(getTeamMemberIds(reservedProposal));
      setActiveTab('classmates');
    } else {
      setActiveTab('teams');
    }
    scrollToTeamSection('class-team-tabs');
  };

  const workspaceTeamId = entityId(currentStudent?.teamId) || reservedProposalTeamId;
  const majorNextAction: StudentMajorNextAction | undefined = (() => {
    if (workspaceTeamId) {
      return {
        label: t('myTeam.openStartupWorkspace'),
        kind: 'workspace',
        onClick: () => navigate(`/student/workspace/${workspaceTeamId}`),
      };
    }
    if (!majorVerified) return undefined;
    if (isReadOnly) return undefined;
    if (formationError) {
      return {
        label: t('studentClassDetail.retryTeamStatus'),
        kind: 'retry',
        onClick: () => { void fetchClassDetail(); },
      };
    }
    if (hasPendingFormation) {
      return {
        label: t('studentClassDetail.reviewTeamInvitation'),
        kind: 'review',
        onClick: () => scrollToTeamSection('pending-team-formation'),
      };
    }
    if (reservedProposal) {
      return {
        label: ['NEEDS_REVISION', 'NEEDSREVISION'].includes(reservedProposalStatus)
          ? t('studentClassDetail.continueTeamSetup')
          : t('studentClassDetail.viewTeamProposal'),
        kind: 'review',
        onClick: openReservedProposal,
      };
    }
    return {
      label: t('studentClassDetail.createATeam'),
      kind: 'team',
      onClick: startTeamProposal,
    };
  })();

  return (
    <div className="space-y-6 max-w-5xl mx-auto">
      {/* Back link */}
      <button
        onClick={() => navigate('/student/classes')}
        className="flex items-center gap-1.5 text-slate-500 hover:text-slate-800 transition-colors text-sm font-semibold"
      >
        <ChevronLeft className="w-4 h-4" /> {t('studentClassDetail.backToMyClasses')}
      </button>

      {/* Hero Header */}
      <div className="rounded-2xl border border-slate-200/60 bg-white p-6 shadow-sm">
        <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
          <div className="flex min-w-0 items-start gap-4">
            <div className="w-12 h-12 rounded-2xl flex items-center justify-center text-white shrink-0 shadow-sm bg-primary">
              <GraduationCap className="w-6 h-6" />
            </div>
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-xs font-semibold px-2 py-0.5 bg-primary-50 text-primary-700 border border-primary-100 rounded-md uppercase">
                  {cls?.subjectCode}
                </span>
                <span className="text-xs font-semibold px-2 py-0.5 bg-slate-100 text-slate-500 border border-slate-200/40 rounded-md">
                  {formatSemesterCode(cls?.semester, cls?.year)}
                </span>
              </div>
              <h1 className="mt-1.5 text-heading sm:text-display text-secondary tracking-tight">{cls?.classCode}</h1>
              <p className="text-sm text-slate-500 mt-0.5">{cls?.description || t('studentClassDetail.classroomForStartupIdeaDevelopment')}</p>
            </div>
          </div>

          <div className="grid w-full gap-3 sm:grid-cols-2 lg:w-auto">
            {lecturer && (
              <div className="min-w-0 rounded-xl border border-slate-100 bg-slate-50 p-4 sm:min-w-[230px]">
                <p className="text-overline font-semibold text-slate-400 uppercase tracking-wider">{t('classDetail.lecturer')}</p>
                <p className="font-bold text-slate-800 text-sm mt-1">{lecturer.name}</p>
                <p className="text-xs text-slate-400 mt-0.5 flex items-center gap-1">
                  <Mail className="w-3.5 h-3.5 shrink-0 text-slate-300" />
                  <span className="truncate" title={lecturer.email}>{lecturer.email}</span>
                </p>
              </div>
            )}

            <div className="min-w-0 rounded-xl border border-slate-100 bg-slate-50 p-4 sm:min-w-[230px]">
              <div className="flex items-center justify-between gap-3">
                <p className="text-overline font-semibold uppercase tracking-wider text-slate-400">{t('classDetail.mentors')}</p>
                <span className="flex h-6 min-w-6 items-center justify-center rounded-full bg-primary-50 px-1.5 text-caption font-bold text-primary-700">
                  {classMentors.length}
                </span>
              </div>
              {classMentors.length > 0 ? (
                <div className="mt-2 flex items-center justify-between gap-4">
                  <div className="flex -space-x-2" aria-hidden="true">
                    {classMentors.slice(0, 3).map(mentor => (
                      <span
                        key={mentor.mentorProfileId || mentor.userId || mentor.email}
                        className="flex h-8 w-8 items-center justify-center rounded-full border-2 border-white text-overline font-bold text-primary-700 bg-primary-100"
                      >
                        {mentorInitials(mentor.fullName || 'Mentor')}
                      </span>
                    ))}
                    {classMentors.length > 3 && (
                      <span className="flex h-8 w-8 items-center justify-center rounded-full border-2 border-white bg-slate-200 text-overline font-bold text-slate-600">
                        +{classMentors.length - 3}
                      </span>
                    )}
                  </div>
                  <button
                    type="button"
                    onClick={() => setIsMentorModalOpen(true)}
                    className="inline-flex shrink-0 items-center gap-1 text-xs font-semibold text-primary-700 transition hover:text-primary-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 rounded-md"
                  >
                    {t('studentClassDetail.viewMentors')} <ArrowRight className="h-3.5 w-3.5" />
                  </button>
                </div>
              ) : (
                <p className="mt-2 text-xs text-slate-400">{t('studentClassDetail.noMentorsAssignedYet')}</p>
              )}
            </div>
          </div>
        </div>
      </div>

      <Modal
        isOpen={isMentorModalOpen}
        onClose={() => setIsMentorModalOpen(false)}
        title={t('studentClassDetail.classMentors')}
        size="lg"
      >
        <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm text-slate-500">
            {t('studentClassDetail.mentorsSupporting')} <span className="font-semibold text-slate-700">{cls?.classCode}</span>
          </p>
          <span className="rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-semibold text-slate-600">
            {classMentors.length} {classMentors.length === 1 ? 'mentor' : 'mentors'}
          </span>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          {classMentors.map(mentor => {
            const fullName = mentor.fullName || 'Unknown mentor';
            const affiliation = mentor.department || mentor.organization;
            return (
              <article key={mentor.mentorProfileId || mentor.userId || mentor.email} className="flex min-w-0 items-start gap-3 rounded-xl border border-slate-200/80 bg-slate-50/70 p-3.5">
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-xs font-bold text-primary-700 ring-2 ring-white bg-primary-100">
                  {mentorInitials(fullName)}
                </div>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-start gap-1.5">
                    <h3 className="min-w-0 text-sm font-bold leading-5 text-slate-800">{fullName}</h3>
                    <span className={`rounded-full px-2 py-0.5 text-overline font-semibold ${mentor.mentorType === 'Academic' ? 'bg-blue-50 text-blue-700' : mentor.mentorType === 'Enterprise' ? 'bg-orange-50 text-orange-700' : 'bg-slate-100 text-slate-600'}`}>
                      {mentorRoleLabel(mentor.mentorType)}
                    </span>
                  </div>
                  {mentor.jobTitle && <p className="mt-1 text-xs font-medium text-slate-600">{mentor.jobTitle}</p>}
                  {affiliation && <p className="mt-0.5 text-xs leading-4 text-slate-400">{affiliation}</p>}
                  <a href={`mailto:${mentor.email}`} className="mt-1.5 flex min-w-0 items-center gap-1 text-xs text-slate-400 transition hover:text-primary-700" title={mentor.email}>
                    <Mail className="h-3.5 w-3.5 shrink-0" />
                    <span className="truncate">{mentor.email}</span>
                  </a>
                </div>
              </article>
            );
          })}
        </div>
      </Modal>

      {currentStudent && (
        <StudentMajorActionCard
          currentMajor={currentStudent.major}
          verificationStatus={currentStudent.majorVerificationStatus}
          canEdit={Boolean(currentStudent.canEditMajor) && !isReadOnly}
          isLocked={Boolean(currentStudent.isMajorLocked)}
          updating={updatingMajor}
          lecturerName={lecturer?.name}
          nextAction={majorNextAction}
          onSave={majorCode => handleOwnMajorChange(currentStudent, majorCode)}
        />
      )}

      {ownTeamMajorWarning && <OwnTeamMajorWarning warning={ownTeamMajorWarning} testId="class-own-team-major-warning" />}

      {isReadOnly && (
        <div className="flex items-start gap-2.5 rounded-xl border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-800">
          <Lock className="mt-0.5 h-4 w-4 shrink-0" />
          <div>
            <p className="font-semibold">
              {t(cls?.classStatus === 'Archived' ? 'studentClassDetail.archivedHistoryReadOnly' : 'studentClassDetail.completedHistoryReadOnly')}
            </p>
            <p className="mt-0.5 text-xs text-blue-700">
              {t('studentClassDetail.youCanReviewClassmatesTeamsAnd')}
            </p>
          </div>
        </div>
      )}

      {!isReadOnly && !majorVerified && !hasTeam && !reservedProposal && !hasPendingFormation && selected.length === 0 && (
        <div className="flex flex-col gap-3 rounded-xl border border-primary-100 bg-primary-50/60 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-sm font-semibold text-slate-800">
              {majorActionRequired ? t('studentClassDetail.teamActionsAreLockedUntilYou') : t('studentClassDetail.youDoNotHaveATeam')}
            </p>
            <p className="mt-0.5 text-xs text-slate-600">
              {majorActionRequired
                ? t('studentClassDetail.completeTheRequiredStepAboveTo')
                : t('studentClassDetail.choose46ClassmatesAndA')}
            </p>
          </div>
          <button
            type="button"
            onClick={startTeamProposal}
            disabled={majorActionRequired}
            className={`inline-flex shrink-0 items-center justify-center gap-2 rounded-lg px-3.5 py-2 text-xs font-bold shadow-sm transition ${majorActionRequired
              ? 'cursor-not-allowed border border-slate-200 bg-white text-slate-500 shadow-none'
              : 'bg-primary text-white hover:bg-primary-600'}`}
          >
            {majorActionRequired ? <Lock className="h-4 w-4" /> : <UserPlus className="h-4 w-4" />}
            {majorActionRequired ? t('studentClassDetail.selectMajorFirst') : t('studentTeamGeneratePanel.createTeam')}
          </button>
        </div>
      )}

      {formationError && <div className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
        {t('studentClassDetail.couldNotLoadTeamInvitations')} <button type="button" onClick={() => void fetchClassDetail()} className="font-semibold underline">{t('assignMentorsModal.retry')}</button>
      </div>}
      {openFormations.map((formation, index) => (
        <div key={formation.id} id={index === 0 ? 'pending-team-formation' : undefined}>
          <TeamFormationCard
            formation={formation}
            onChanged={fetchClassDetail}
            acceptDisabledReason={majorActionRequired ? 'Select your major before accepting this team invitation.' : undefined}
          />
        </div>
      ))}

      {!isReadOnly && canInviteMore && (
        <div className="rounded-xl border border-primary-100 bg-primary-50/60 px-4 py-3 text-sm text-slate-700" role="status">
          {t('studentClassDetail.youCanInviteUpTo')} <strong>{inviteSlotsLeft}</strong> {t('studentClassDetail.moreClassmate')}{inviteSlotsLeft === 1 ? '' : 's'}{t('studentClassDetail.selectThemInTheClassmatesTab')}
        </div>
      )}

      {!isReadOnly && reservedProposal && !canEditReservedProposal && (
        <div className="flex flex-col gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="font-semibold">{t(reservedProposal.approvedTeamId ? 'studentClassDetail.yourProjectProposalIs' : 'studentClassDetail.yourTeamProposalIs', { status: t(`studentClassDetail.proposalStatus.${String(reservedProposal.status || 'PENDING').toUpperCase()}`, { defaultValue: String(reservedProposal.status || 'pending').replaceAll('_', ' ').toLowerCase() }) })}</p>
            <p className="mt-0.5 text-xs text-amber-700">
              {isPendingProjectProposal
                ? t('studentClassDetail.yourTeamIsAlreadyActiveOnly')
                : reservedProposal.approvedTeamId
                  ? t('studentClassDetail.yourTeamIsAlreadyActiveCheck')
                  : t('studentClassDetail.youCannotJoinAnotherProposalWhile')}
            </p>
          </div>
        </div>
      )}

      {students.length > 0 && !selectionDisabled && selected.length > 0 && (
        <div className="sticky top-20 z-20 rounded-2xl bg-white/80 shadow-xl backdrop-blur-md">
          <StudentTeamGeneratePanel
            classId={loadedClassId}
            selected={selected}
            students={students}
            onTeamCreated={handleTeamCreated}
            currentStudentId={currentStudent?._id}
            proposal={proposalToRevise}
            inviteToFormation={canInviteMore ? ownOpenFormation : null}
          />
        </div>
      )}

      {/* Tabs */}
      <div id="class-team-tabs" className="flex border-b border-slate-200">
        <button
          onClick={() => setActiveTab('classmates')}
          className={`px-5 py-3 text-sm font-semibold border-b-2 transition-all flex items-center gap-2 ${
            activeTab === 'classmates'
              ? 'border-primary text-primary-700'
              : 'border-transparent text-slate-400 hover:text-slate-600'
          }`}
        >
          <Users className="w-4 h-4" /> {t('studentClassDetail.classmates')}{students.length})
        </button>
        <button
          onClick={() => setActiveTab('teams')}
          className={`px-5 py-3 text-sm font-semibold border-b-2 transition-all flex items-center gap-2 ${
            activeTab === 'teams'
              ? 'border-primary text-primary-700'
              : 'border-transparent text-slate-400 hover:text-slate-600'
          }`}
        >
          <LayoutGrid className="w-4 h-4" /> {t('studentClassDetail.classTeams')}{teams.length})
        </button>
      </div>

      {/* Tab Contents */}
      {activeTab === 'classmates' ? (
        <StudentTable
          students={students}
          teams={sortTeamsByCode(teams)}
          cls={cls}
          selected={selected}
          onSelectionChange={setSelected}
          onRefresh={fetchClassDetail}
          maxSelection={canInviteMore ? inviteSlotsLeft : 6}
          priorityTeamId={currentStudent?.teamId}
          descriptionFromWidth="4xl"
          selectionDisabled={selectionDisabled}
          toolbarAction={!selectionDisabled && selected.length === 0 && !canInviteMore ? (
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={startTeamProposal}
                className="inline-flex min-h-8 items-center gap-1.5 rounded-lg bg-primary px-2.5 py-1.5 text-xs font-semibold text-white hover:bg-primary-600"
              >
                <UserPlus className="h-3.5 w-3.5" /> {t('studentTeamGeneratePanel.createTeam')}
              </button>
              <TeamSuggestionTooltip label={t('studentClassDetail.xemHuongDanTaoNhom')}>
                <div className="space-y-2">
                  <p className="font-semibold text-white">{t('studentClassDetail.huongDanTaoNhom')}</p>
                  <p className="text-slate-200">
                    {t('studentClassDetail.chonChinhBanVaCacThanh')}
                  </p>
                </div>
              </TeamSuggestionTooltip>
            </div>
          ) : null}
        />
      ) : (
        <TeamList
          teams={displayedTeams}
          onRefresh={fetchClassDetail}
          canDelete={false}
          canManageInfo={false}
          currentStudentId={currentStudent?._id}
          onRevise={(proposal) => {
            setProposalToRevise(proposal);
            setSelected(getTeamMemberIds(proposal));
            setActiveTab('classmates');
          }}
          onProjectDirection={setDirectionTeam}
          onCancelProposal={requestProposalCancellation}
        />
      )}

      {directionTeam && (
        <ProjectDirectionModal
          team={directionTeam}
          role="STUDENT"
          currentStudentId={currentStudent?._id}
          onClose={() => setDirectionTeam(null)}
        />
      )}

      <ConfirmDialog
        isOpen={Boolean(proposalToCancel)}
        onClose={closeCancellationDialog}
        onConfirm={cancelProposal}
        title={t('studentClassDetail.cancelTeamProposal')}
        description={t('studentClassDetail.thisWillCancelTheCurrentProposal')}
        isSubmitting={cancellingProposal}
        confirmText={t('studentClassDetail.cancelProposal')}
        cancelText={t('studentClassDetail.keepProposal')}
        reason={cancellationReason}
        onReasonChange={setCancellationReason}
        reasonLabel={t('studentClassDetail.reasonForCancellation')}
        reasonRequired
        actionLayout="confirmWide"
      />
    </div>
  );
}

