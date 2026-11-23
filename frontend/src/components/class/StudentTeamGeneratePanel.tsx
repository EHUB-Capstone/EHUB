import { useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import toast from 'react-hot-toast';
import { Users, AlertTriangle, CheckCircle2, Crown, Loader2, AlertCircle, Send, X } from 'lucide-react';
import { teamApi } from '../../api/teamApi';
import { teamFormationApi } from '../../api/teamFormationApi';
import { classApi } from '../../api/classApi';
import { unwrapApiData } from '../../utils/classMappers';
import { isMissingTeamMajor, validateTeamSelection } from '../../utils/teamManagement';
import { MAX_FORMATION_MEMBERS, remainingInviteSlots } from '../../utils/teamFormation';
import TeamSuggestionTooltip from './TeamSuggestionTooltip';
import { useTranslation } from 'react-i18next';

const getTeamSizeSuggestion = (count) => {
  if (count === 0) return '';
  if (count === 1 || count === 2) {
    return `${count} students remain, which is not enough for a team. Rebalance members from other teams.`;
  }
  if (count === 3) {
    return 'Three students remain. Rebalance members from other teams before submitting a proposal.';
  }
  if (count === 7) {
    return 'The remaining students cannot be divided into teams of 4–6. Rebalance them across existing teams.';
  }

  const candidates = [];
  for (let sixes = 0; sixes <= Math.floor(count / 6); sixes += 1) {
    for (let fives = 0; fives <= Math.floor(count / 5); fives += 1) {
      const remainder = count - (sixes * 6) - (fives * 5);
      if (remainder < 0 || remainder % 4 !== 0) continue;
      const fours = remainder / 4;
      candidates.push({
        sixes,
        fives,
        fours,
        groupCount: sixes + fives + fours,
      });
    }
  }

  candidates.sort((a, b) =>
    a.groupCount - b.groupCount
    || b.sixes - a.sixes
    || b.fives - a.fives
  );

  const best = candidates[0];
  if (!best) {
    return 'The remaining students cannot be divided into teams of 4–6. Rebalance members between teams.';
  }

  const summary = [
    best.sixes && `${best.sixes} × 6`,
    best.fives && `${best.fives} × 5`,
    best.fours && `${best.fours} × 4`,
  ].filter(Boolean);

  return `Suggested split: ${summary.join(', ')}. Every team still needs both major groups.`;
};

export default function StudentTeamGeneratePanel({
  classId,
  selected: rawSelected,
  students: rawStudents,
  onTeamCreated,
  currentStudentId = '',
  requireCurrentStudentMembership = true,
  proposal = null,
  creationMode = 'formation',
  inviteToFormation = null,
}) {
  const { t } = useTranslation('class');
  const [submitting, setSubmitting] = useState(false);
  const students = useMemo(() => (Array.isArray(rawStudents) ? rawStudents : []), [rawStudents]);
  const selected = useMemo(() => (Array.isArray(rawSelected) ? rawSelected : []), [rawSelected]);

  const [groupName, setGroupName] = useState('');
  const [projectName, setProjectName] = useState('');
  const [description, setDescription] = useState('');
  const [isProjectNameSameAsGroup, setIsProjectNameSameAsGroup] = useState(true);
  const [selectedLeaderId, setSelectedLeaderId] = useState('');
  const [showConfirmation, setShowConfirmation] = useState(false);

  useEffect(() => {
    if (!proposal) return;
    setGroupName(proposal.teamName || '');
    setProjectName(proposal.projectName || '');
    setDescription(proposal.description || '');
    setIsProjectNameSameAsGroup(proposal.projectName === proposal.teamName);
    setSelectedLeaderId(proposal.leaderId?._id || proposal.leaderId || '');
  }, [proposal]);

  useEffect(() => {
    if (!showConfirmation) return undefined;

    const previousOverflow = document.body.style.overflow;
    const handleEscape = (event) => {
      if (event.key === 'Escape' && !submitting) setShowConfirmation(false);
    };
    document.body.style.overflow = 'hidden';
    window.addEventListener('keydown', handleEscape);

    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener('keydown', handleEscape);
    };
  }, [showConfirmation, submitting]);

  const suggestionInfo = useMemo(() => {
    const unassignedCount = students.filter(s => !s.teamId).length;
    if (unassignedCount === 0) return null;

    return {
      total: students.length,
      unassigned: unassignedCount,
      suggestion: getTeamSizeSuggestion(unassignedCount)
    };
  }, [students]);

  // Real-time validation
  const validation = useMemo(() => {
    const selectedStudents = students.filter(s => selected.includes(s._id));
    const studentCount = selectedStudents.length;

    const missingMajorCount = selectedStudents.filter(s => isMissingTeamMajor(s.major)).length;

    const teamSelection = validateTeamSelection(selectedStudents, selectedLeaderId);
    const hasGroup1 = teamSelection.hasGroupOne;
    const hasGroup2 = teamSelection.hasGroupTwo;
    const isFullyValid = teamSelection.isMemberCountValid && teamSelection.isMajorRequirementValid;
    const uniqueMajors = [...new Set(
      selectedStudents
        .map(s => s.major)
        .filter(m => !isMissingTeamMajor(m))
        .map(m => m.trim().toUpperCase())
    )];

    // Form validation
    const isGroupNameValid = groupName.trim().length >= 3 && groupName.trim().length <= 60;
    const isProjectNameValid = isProjectNameSameAsGroup
      ? isGroupNameValid
      : projectName.trim().length >= 3 && projectName.trim().length <= 60;
    const isDescriptionValid = description.trim().length >= 20 && description.trim().length <= 500;
    const hasCurrentUser = !requireCurrentStudentMembership || selected.includes(currentStudentId);

    const hasLeader = teamSelection.isTeamLeaderValid;
    const isFormValid = isGroupNameValid && (!proposal || (isProjectNameValid && isDescriptionValid)) && hasCurrentUser && hasLeader;

    return {
      selectedStudents,
      studentCount,
      uniqueMajors,
      hasGroup1,
      hasGroup2,
      isFullyValid,
      missingMajorCount,
      isFormValid,
      isGroupNameValid,
      hasCurrentUser,
      hasLeader,
    };
  }, [selected, students, groupName, projectName, description, isProjectNameSameAsGroup, currentStudentId, requireCurrentStudentMembership, selectedLeaderId, proposal]);

  const {
    selectedStudents, studentCount, uniqueMajors,
    hasGroup1, hasGroup2, isFullyValid,
    missingMajorCount, isFormValid, isGroupNameValid, hasCurrentUser, hasLeader
  } = validation;
  const createsTeamDirectly = creationMode === 'direct' && !proposal;
  // Student formations only invite: the 4–6 members and BBA/BIT rules are enforced when the creator finalizes.
  const isFormationFlow = !createsTeamDirectly && !proposal;
  const inviteMode = isFormationFlow && Boolean(inviteToFormation);
  const maxSelection = inviteMode ? remainingInviteSlots(inviteToFormation) : MAX_FORMATION_MEMBERS;
  const formationMembersValid = inviteMode
    ? studentCount >= 1 && studentCount <= maxSelection
    : studentCount >= 2 && studentCount <= MAX_FORMATION_MEMBERS;
  const formationReady = inviteMode
    ? formationMembersValid
    : formationMembersValid && isGroupNameValid && hasCurrentUser && hasLeader;
  const canSubmit = isFormationFlow ? formationReady : isFormValid && isFullyValid;
  const selectedLeader = selectedStudents.find(student => student._id === selectedLeaderId);

  const requestSubmission = () => {
    if (submitting || !canSubmit) {
      toast.error(t('studentTeamGeneratePanel.completeTheRequiredTeamInformation'));
      return;
    }

    if (proposal || inviteMode) {
      void handleSubmit();
      return;
    }

    setShowConfirmation(true);
  };

  const handleSubmit = async () => {
    if (submitting || !canSubmit) {
      toast.error(t('studentTeamGeneratePanel.completeTheRequiredTeamInformation'));
      return;
    }

    setSubmitting(true);
    try {
      if (proposal) {
        const response = await teamApi.updateProposal(proposal._id, {
          memberIds: selected,
          teamName: groupName.trim(),
          projectName: isProjectNameSameAsGroup ? groupName.trim() : projectName.trim(),
          description: description.trim(),
          leaderStudentId: selectedLeaderId,
          rowVersion: proposal.rowVersion,
        });
        const draft = unwrapApiData<any>(response);
        await teamApi.submitProposal(draft.id, draft.rowVersion);
      } else {
        if (createsTeamDirectly) {
          await classApi.createTeam(classId, {
            memberStudentIds: selected,
            leaderStudentId: selectedLeaderId,
            teamName: groupName.trim(),
          });
        } else if (inviteMode) {
          await teamFormationApi.invite(inviteToFormation.id, selected);
        } else {
          await teamFormationApi.create(classId, {
            inviteeStudentIds: selected.filter(studentId => studentId !== currentStudentId),
            leaderStudentId: selectedLeaderId,
            teamName: groupName.trim(),
          });
        }
      }

      toast.success(
        proposal
          ? t('studentTeamGeneratePanel.projectProposalResubmittedYourTeamIs')
          : createsTeamDirectly
            ? t('studentTeamGeneratePanel.teamCreatedSuccessfullyStudentsCanView')
            : t('studentTeamGeneratePanel.invitationsSentEachStudentHas24'),
      );
      
      // Reset form
      setGroupName('');
      setProjectName('');
      setDescription('');
      setIsProjectNameSameAsGroup(true);
      setSelectedLeaderId('');
      setShowConfirmation(false);
      onTeamCreated();
    } catch (e) {
      toast.error(e?.response?.data?.message || e?.message || t('studentTeamGeneratePanel.failedToCreateTheTeam'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
      <div className="hidden">
        <div>
          <h3 className="text-lg font-bold text-slate-900">{t('studentTeamGeneratePanel.createTeam')}</h3>
          <p className="mt-0.5 text-xs text-slate-500">
            {createsTeamDirectly
              ? t('studentTeamGeneratePanel.theTeamBecomesActiveImmediatelyAfter')
              : t('studentTeamGeneratePanel.theTeamBecomesActiveAfterAll')}
          </p>
        </div>
        {suggestionInfo && (
          <TeamSuggestionTooltip>
              {t('studentTeamGeneratePanel.studentsDoNotHaveATeam', { unassigned: suggestionInfo.unassigned, suggestion: suggestionInfo.suggestion })}
          </TeamSuggestionTooltip>
        )}
      </div>

      <div className="grid grid-cols-1">
        {/* Form Info */}
        {!inviteMode && hasLeader && (
          <div className="order-2 grid gap-3 border-t border-slate-200 bg-slate-50/30 p-3 md:grid-cols-[0.8fr_0.8fr_1.4fr]">
            <div>
              <label className="mb-1.5 block text-xs font-semibold text-slate-600">
                {t('studentTeamGeneratePanel.teamName')} <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={groupName}
                onChange={e => setGroupName(e.target.value)}
                placeholder={t('studentTeamGeneratePanel.exampleAlphaTeam')}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
                required
                minLength={3}
                maxLength={60}
              />
              {groupName.length > 0 && groupName.trim().length < 3 && (
                <p className="mt-1 text-xs text-red-500">{t('studentTeamGeneratePanel.teamNameMustBe360')}</p>
              )}
            </div>

            <div className="hidden">
              <label className="mb-1.5 block text-xs font-semibold text-slate-600">
                {t('studentTeamGeneratePanel.teamLeader')} <span className="text-red-500">*</span>
              </label>
              <select
                value={selected.includes(selectedLeaderId) ? selectedLeaderId : ''}
                onChange={(event) => setSelectedLeaderId(event.target.value)}
                className="w-full px-3 py-2 border border-slate-300 rounded-xl text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none bg-white"
              >
                <option value="">{t('studentTeamGeneratePanel.selectATeamMember')}</option>
                {selectedStudents.map(student => (
                  <option key={student._id} value={student._id}>{student.fullName} ({student.rollNumber || t('studentAssignmentModal.noStudentCode')})</option>
                ))}
              </select>
            </div>

            {proposal && <div>
              <label className="mb-1.5 flex items-center justify-between gap-2 text-xs font-semibold text-slate-600">
                <span>{t('classDirectionOverview.projectName')} <span className="text-red-500">*</span></span>
                <span className="flex items-center gap-1.5 text-overline font-medium uppercase tracking-wide text-slate-500">
                  <input
                    type="checkbox"
                    checked={isProjectNameSameAsGroup}
                    onChange={e => setIsProjectNameSameAsGroup(e.target.checked)}
                    className="rounded border-slate-300 text-primary-700 focus:ring-primary"
                  />
                  {t('studentTeamGeneratePanel.sameAsTeam')}
                </span>
              </label>
              <input
                type="text"
                value={isProjectNameSameAsGroup ? groupName : projectName}
                onChange={e => setProjectName(e.target.value)}
                placeholder={t('studentTeamGeneratePanel.enterAProjectName')}
                disabled={isProjectNameSameAsGroup}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:bg-slate-100 disabled:text-slate-500"
                required
                minLength={3}
                maxLength={60}
              />
              {!isProjectNameSameAsGroup && projectName.length > 0 && projectName.trim().length < 3 && (
                <p className="mt-1 text-xs text-red-500">{t('studentTeamGeneratePanel.projectNameMustBe360')}</p>
              )}
            </div>}

            {proposal && <div>
              <label className="mb-1.5 block text-xs font-semibold text-slate-600">
                {t('projectDirectionModal.projectDescription')} <span className="text-red-500">*</span>
              </label>
              <textarea
                value={description}
                onChange={e => setDescription(e.target.value)}
                placeholder={t('studentTeamGeneratePanel.brieflyDescribeTheProjectIdea')}
                className="h-10 w-full resize-none rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
                required
                minLength={20}
                maxLength={500}
              />
              <div className="mt-1 flex items-center justify-between gap-3">
                <span>
                  {description.length > 0 && description.trim().length < 20 && (
                    <span className="text-xs text-red-500">{t('studentTeamGeneratePanel.descriptionMustBe20500Characters')}</span>
                  )}
                </span>
                <span className="text-xs text-slate-400">{description.length}/500</span>
              </div>
            </div>}
          </div>
        )}

        {/* Validation Info & Submit */}
        <div className="order-1 grid lg:grid-cols-[minmax(0,1fr)_260px]">
          <div className="min-w-0 p-3">
            <h4 className="mb-2 flex items-center gap-2 text-caption font-bold uppercase tracking-wide text-slate-600">
              <Users className="h-4 w-4" /> {t('studentTeamGeneratePanel.teamRequirements')}
            </h4>
            
            <div className="grid overflow-hidden rounded-lg border border-slate-200 text-xs sm:grid-cols-2 [&>span]:min-h-11 [&>span]:px-3 [&>span]:py-2 [&>span:nth-child(-n+2)]:border-b [&>span:nth-child(odd)]:sm:border-r [&>span:nth-child(odd)]:sm:border-slate-200">
              {isFormationFlow ? (
                <span className={`flex items-center gap-1.5 font-medium ${formationMembersValid ? 'text-green-600' : studentCount > 0 ? 'text-red-500' : 'text-slate-400'}`}>
                  <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-overline ${formationMembersValid ? 'bg-green-500' : studentCount > 0 ? 'bg-red-400' : 'bg-slate-300'}`}>
                    {formationMembersValid ? '✓' : '✗'}
                  </span>
                  {inviteMode
                    ? t('studentTeamGeneratePanel.inviteesSlots', { studentCount, maxSelection })
                    : t('studentTeamGeneratePanel.invite15Classmates5', { v1: Math.max(0, studentCount - 1) })}
                </span>
              ) : (
                <span className={`flex items-center gap-1.5 font-medium ${(studentCount >= 4 && studentCount <= 6) ? 'text-green-600' : studentCount > 0 ? 'text-red-500' : 'text-slate-400'}`}>
                  <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-overline ${(studentCount >= 4 && studentCount <= 6) ? 'bg-green-500' : studentCount > 0 ? 'bg-red-400' : 'bg-slate-300'}`}>
                    {(studentCount >= 4 && studentCount <= 6) ? '✓' : '✗'}
                  </span>
                  {t('studentTeamGeneratePanel.46Members')}{studentCount}/6)
                </span>
              )}

              {/* Group rules are checked when the creator finalizes, so they only guide formations. */}
              <span className={`flex items-center gap-1.5 font-medium ${hasGroup1 ? 'text-green-600' : isFormationFlow ? 'text-amber-600' : studentCount > 0 ? 'text-red-500' : 'text-slate-400'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-overline shrink-0 ${hasGroup1 ? 'bg-green-500' : isFormationFlow ? 'bg-amber-400' : studentCount > 0 ? 'bg-red-400' : 'bg-slate-300'}`}>
                  {hasGroup1 ? '✓' : isFormationFlow ? '!' : '✗'}
                </span>
                <span>{isFormationFlow ? t('studentTeamGeneratePanel.group1BbaNeededToFinalize') : t('studentTeamGeneratePanel.group1BbaRequired')}</span>
              </span>

              <span className={`flex items-center gap-1.5 font-medium ${hasGroup2 ? 'text-green-600' : isFormationFlow ? 'text-amber-600' : studentCount > 0 ? 'text-red-500' : 'text-slate-400'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-overline shrink-0 ${hasGroup2 ? 'bg-green-500' : isFormationFlow ? 'bg-amber-400' : studentCount > 0 ? 'bg-red-400' : 'bg-slate-300'}`}>
                  {hasGroup2 ? '✓' : isFormationFlow ? '!' : '✗'}
                </span>
                <span>{isFormationFlow ? t('studentTeamGeneratePanel.group2BitNeededToFinalize') : t('studentTeamGeneratePanel.group2BitRequired')}</span>
              </span>
              
              <span className={`hidden items-center gap-1.5 font-medium ${hasCurrentUser ? 'text-green-600' : 'text-red-500'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-overline shrink-0 ${hasCurrentUser ? 'bg-green-500' : 'bg-red-400'}`}>
                  {hasCurrentUser ? '✓' : '✗'}
                </span>
                <span>{t('studentTeamGeneratePanel.youMustBeATeamMember')}</span>
              </span>

              {!inviteMode && (
                <span className={`flex items-center gap-1.5 font-medium ${hasLeader ? 'text-green-600' : 'text-red-500'}`}>
                  <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-overline shrink-0 ${hasLeader ? 'bg-green-500' : 'bg-red-400'}`}>
                    {hasLeader ? '✓' : '×'}
                  </span>
                  <span>{isFormationFlow ? t('studentTeamGeneratePanel.proposedTeamLeaderRequired') : t('studentTeamGeneratePanel.teamLeaderRequired')}</span>
                </span>
              )}
            </div>

            {uniqueMajors.length > 0 && (
              <p className="mt-1.5 text-overline text-slate-500">
                {t('studentTeamGeneratePanel.majors', { v1: uniqueMajors.join(', ') })}
              </p>
            )}

            {missingMajorCount > 0 && (
              <div className="mt-1.5 flex items-start gap-2 text-overline text-amber-700">
                <AlertCircle className="w-4 h-4 text-amber-500 shrink-0" />
                <p className="text-xs text-amber-700">
                  {t('studentTeamGeneratePanel.selectedStudentSHaveNoDeclared', { missingMajorCount })}
                </p>
              </div>
            )}
            
            {isFormationFlow && studentCount > 0 && (
              <div className="mt-1.5 flex items-start gap-2 text-overline text-slate-600">
                <AlertCircle className="w-4 h-4 text-slate-400 shrink-0" />
                <p className="text-xs font-medium">
                  {inviteMode
                    ? t('studentTeamGeneratePanel.eachNewStudentHas24Hours')
                    : t('studentTeamGeneratePanel.eachStudentHas24HoursTo')}
                </p>
              </div>
            )}

            {!isFormationFlow && studentCount > 0 && !isFullyValid && (
              <div className="mt-1.5 flex items-start gap-2 text-overline text-orange-700">
                <AlertTriangle className="w-4 h-4 text-orange-500 shrink-0" />
                <p className="text-xs text-orange-700 font-medium">
                  {t('studentTeamGeneratePanel.select46MembersAndInclude')}
                </p>
              </div>
            )}
          </div>

          <div className="border-t border-slate-200 bg-slate-50/60 p-3 lg:border-l lg:border-t-0">
            {!inviteMode && (
              <>
                <label htmlFor="proposed-team-leader" className="mb-1.5 flex items-center gap-1 text-caption font-bold text-slate-600">
                  <Crown className="h-3.5 w-3.5 text-amber-500" /> {isFormationFlow ? t('studentTeamGeneratePanel.proposedTeamLeader') : t('studentTeamGeneratePanel.teamLeader')} <span className="text-red-500">*</span>
                </label>
                <select
                  id="proposed-team-leader"
                  value={selected.includes(selectedLeaderId) ? selectedLeaderId : ''}
                  onChange={(event) => setSelectedLeaderId(event.target.value)}
                  className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
                >
                  <option value="">{t('studentTeamGeneratePanel.selectLeader')}</option>
                  {selectedStudents.map(student => <option key={student._id} value={student._id}>{student.fullName} ({student.rollNumber || t('studentAssignmentModal.noStudentCode')})</option>)}
                </select>
              </>
            )}
            <button
              type="button"
              onClick={requestSubmission}
              disabled={submitting || !canSubmit}
              className={`mt-2 flex w-full items-center justify-center gap-1.5 rounded-lg py-2 text-xs font-bold transition-all
                ${!canSubmit
                  ? 'cursor-not-allowed bg-slate-200 text-slate-500'
                  : 'bg-green-500 text-white hover:bg-green-600'
                }`}
            >
              {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : 
                (canSubmit ? <CheckCircle2 className="w-4 h-4" /> : <Send className="w-4 h-4" />)
              }
              {canSubmit
                ? (proposal ? t('studentTeamGeneratePanel.resubmitProjectProposal') : createsTeamDirectly ? t('studentTeamGeneratePanel.createTeam') : t('studentTeamGeneratePanel.sendInvitations'))
                : t('studentTeamGeneratePanel.requirementsNotMet')}
            </button>
          </div>
        </div>
      </div>
      </div>

      {showConfirmation && !proposal && createPortal(
        <div
          className="fixed inset-0 z-[90] flex items-end justify-center p-0 sm:items-center sm:p-6"
          role="dialog"
          aria-modal="true"
          aria-labelledby="team-confirmation-title"
        >
          <button
            type="button"
            className="absolute inset-0 cursor-default bg-slate-900/50 backdrop-blur-sm"
            onClick={() => !submitting && setShowConfirmation(false)}
            aria-label={t('studentTeamGeneratePanel.closeTeamConfirmation')}
          />

          <div className="relative flex max-h-[92vh] w-full max-w-2xl flex-col overflow-hidden rounded-t-2xl bg-white shadow-2xl sm:rounded-2xl">
            <header className="flex items-start justify-between gap-4 border-b border-slate-200 px-5 py-4 sm:px-6">
              <div>
                <h2 id="team-confirmation-title" className="text-lg font-bold text-slate-900">
                  {t('studentTeamGeneratePanel.confirmTeamInformation')}
                </h2>
                <p className="mt-1 text-sm text-slate-500">
                  {createsTeamDirectly
                    ? t('studentTeamGeneratePanel.reviewTheMembersBeforeCreatingThe')
                    : t('studentTeamGeneratePanel.reviewTheInvitationsBeforeSendingThem')}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setShowConfirmation(false)}
                disabled={submitting}
                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700 disabled:opacity-50"
                aria-label={t('common:actions.close')}
              >
                <X className="h-5 w-5" />
              </button>
            </header>

            <div className="space-y-5 overflow-y-auto px-5 py-5 sm:px-6">
              <dl className="grid gap-3 sm:grid-cols-2">
                <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{t('studentTeamGeneratePanel.teamName')}</dt>
                  <dd className="mt-1 break-words text-sm font-semibold text-slate-900">{groupName.trim()}</dd>
                </div>
              </dl>

              <section>
                <div className="mb-2 flex items-center justify-between gap-3">
                  <h3 className="flex items-center gap-2 text-sm font-bold text-slate-900">
                    <Users className="h-4 w-4 text-primary" /> {t('studentTeamGeneratePanel.teamMembers')}
                  </h3>
                  <span className="rounded-full bg-primary-50 px-2.5 py-1 text-xs font-bold text-primary-700">
                    {t('studentTeamGeneratePanel.members', { length: selectedStudents.length })}
                  </span>
                </div>
                <div className="divide-y divide-slate-100 overflow-hidden rounded-xl border border-slate-200">
                  {selectedStudents.map(student => {
                    const isLeader = student._id === selectedLeaderId;
                    return (
                      <div key={student._id} className="flex items-center gap-3 px-3 py-2.5">
                        <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary-50 text-sm font-bold text-primary-700">
                          {student.fullName?.charAt(0)?.toUpperCase() || '?'}
                        </div>
                        <div className="min-w-0 flex-1">
                          <p className="truncate text-sm font-semibold text-slate-800">{student.fullName}</p>
                          <p className="truncate text-xs text-slate-500">
                            {student.rollNumber || t('studentAssignmentModal.noStudentCode')}{student.major ? ` · ${student.major}` : ''}
                          </p>
                        </div>
                        {isLeader && (
                          <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-amber-50 px-2 py-1 text-caption font-bold text-amber-700">
                            <Crown className="h-3 w-3" /> {t('studentTeamGeneratePanel.teamLeader')}
                          </span>
                        )}
                      </div>
                    );
                  })}
                </div>
              </section>

              <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                <p>
                  {createsTeamDirectly
                    ? t('studentTeamGeneratePanel.theTeamAndAllMembershipsWill')
                    : t('studentTeamGeneratePanel.eachInvitedStudentHas24Hours')}
                  {selectedLeader && <>
                    {' '}<strong>{selectedLeader.fullName} ({selectedLeader.rollNumber || t('studentAssignmentModal.noStudentCode')})</strong>
                    {createsTeamDirectly ? t('studentTeamGeneratePanel.willBeAssignedAsTeamLeader') : t('studentTeamGeneratePanel.isInvitedAsTeamLeaderAnd')}
                  </>}
                </p>
              </div>
            </div>

            <footer className="flex flex-col-reverse gap-2 border-t border-slate-200 bg-slate-50 px-5 py-4 sm:flex-row sm:justify-end sm:px-6">
              <button
                type="button"
                onClick={() => setShowConfirmation(false)}
                disabled={submitting}
                className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50"
              >
                {t('studentTeamGeneratePanel.backToEdit')}
              </button>
              <button
                type="button"
                onClick={handleSubmit}
                disabled={submitting}
                className="inline-flex items-center justify-center gap-2 rounded-lg bg-green-600 px-4 py-2 text-sm font-bold text-white hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <CheckCircle2 className="h-4 w-4" />}
                {submitting
                  ? createsTeamDirectly ? t('studentTeamGeneratePanel.creatingTeam') : t('studentTeamGeneratePanel.sendingInvitations')
                  : createsTeamDirectly ? t('studentTeamGeneratePanel.confirmCreateTeam') : t('studentTeamGeneratePanel.confirmSendInvitations')}
              </button>
            </footer>
          </div>
        </div>,
        document.body,
      )}
    </>
  );
}


