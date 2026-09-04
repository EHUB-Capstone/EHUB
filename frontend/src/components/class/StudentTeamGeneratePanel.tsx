import { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { Users, AlertTriangle, CheckCircle2, Crown, Loader2, AlertCircle, Send } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { teamApi } from '../../api/teamApi';
import { unwrapApiData } from '../../utils/classMappers';
import { isMissingTeamMajor, validateTeamSelection } from '../../utils/teamManagement';
import TeamSuggestionTooltip from './TeamSuggestionTooltip';

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

export default function StudentTeamGeneratePanel({ classId, selected: rawSelected, students: rawStudents, onTeamCreated, currentStudentId, proposal = null }) {
  const [submitting, setSubmitting] = useState(false);
  const students = useMemo(() => (Array.isArray(rawStudents) ? rawStudents : []), [rawStudents]);
  const selected = useMemo(() => (Array.isArray(rawSelected) ? rawSelected : []), [rawSelected]);

  const [groupName, setGroupName] = useState('');
  const [projectName, setProjectName] = useState('');
  const [description, setDescription] = useState('');
  const [isProjectNameSameAsGroup, setIsProjectNameSameAsGroup] = useState(true);
  const [selectedLeaderId, setSelectedLeaderId] = useState('');

  useEffect(() => {
    if (!proposal) return;
    setGroupName(proposal.teamName || '');
    setProjectName(proposal.projectName || '');
    setDescription(proposal.description || '');
    setIsProjectNameSameAsGroup(proposal.projectName === proposal.teamName);
    setSelectedLeaderId(proposal.leaderId?._id || proposal.leaderId || '');
  }, [proposal]);

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
    const hasCurrentUser = selected.includes(currentStudentId);

    const hasLeader = teamSelection.isTeamLeaderValid;
    const isFormValid = isGroupNameValid && isProjectNameValid && isDescriptionValid && hasCurrentUser && hasLeader;

    return {
      selectedStudents,
      studentCount,
      uniqueMajors,
      hasGroup1,
      hasGroup2,
      isFullyValid,
      missingMajorCount,
      isFormValid,
      hasCurrentUser,
      hasLeader,
    };
  }, [selected, students, groupName, projectName, description, isProjectNameSameAsGroup, currentStudentId, selectedLeaderId]);

  const {
    selectedStudents, studentCount, uniqueMajors,
    hasGroup1, hasGroup2, isFullyValid,
    missingMajorCount, isFormValid, hasCurrentUser, hasLeader
  } = validation;
  const canSubmit = isFormValid && isFullyValid;

  const handleSubmit = async () => {
    if (submitting || !canSubmit) {
      toast.error('Complete the required team and project information.');
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
        await classApi.studentProposeTeam(classId, {
          studentIds: selected,
          leaderStudentId: selectedLeaderId,
          groupName: groupName.trim(),
          projectName: isProjectNameSameAsGroup ? groupName.trim() : projectName.trim(),
          isProjectNameSameAsGroup,
          description: description.trim(),
        });
      }

      toast.success(
        proposal ? 'Project proposal resubmitted. Your team is unchanged.' : 'Team created. The project proposal is awaiting lecturer review.',
      );
      
      // Reset form
      setGroupName('');
      setProjectName('');
      setDescription('');
      setIsProjectNameSameAsGroup(true);
      setSelectedLeaderId('');
      onTeamCreated();
    } catch (e) {
      toast.error(e?.response?.data?.message || e?.message || 'Failed to create the team.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
      <div className="hidden">
        <div>
          <h3 className="text-lg font-bold text-slate-900">Create team</h3>
          <p className="mt-0.5 text-xs text-slate-500">Teams are active immediately. Project proposals require lecturer approval.</p>
        </div>
        {suggestionInfo && (
          <TeamSuggestionTooltip>
              {suggestionInfo.unassigned} students do not have a team. {suggestionInfo.suggestion}
          </TeamSuggestionTooltip>
        )}
      </div>

      <div className="grid grid-cols-1">
        {/* Form Info */}
        <div className="order-2 grid gap-3 border-t border-slate-200 bg-slate-50/30 p-3 md:grid-cols-[0.8fr_0.8fr_1.4fr]">
          <div>
            <label className="mb-1.5 block text-xs font-semibold text-slate-600">
              Team name <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={groupName}
              onChange={e => setGroupName(e.target.value)}
              placeholder="Example: Alpha Team"
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
              required
              minLength={3}
              maxLength={60}
            />
            {groupName.length > 0 && groupName.trim().length < 3 && (
              <p className="mt-1 text-xs text-red-500">Team name must be 3–60 characters.</p>
            )}
          </div>

          <div className="hidden">
            <label className="mb-1.5 block text-xs font-semibold text-slate-600">
              Team Leader <span className="text-red-500">*</span>
            </label>
            <select
              value={selected.includes(selectedLeaderId) ? selectedLeaderId : ''}
              onChange={(event) => setSelectedLeaderId(event.target.value)}
              className="w-full px-3 py-2 border border-slate-300 rounded-xl text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none bg-white"
            >
              <option value="">Select a team member</option>
              {selectedStudents.map(student => (
                <option key={student._id} value={student._id}>{student.fullName}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1.5 flex items-center justify-between gap-2 text-xs font-semibold text-slate-600">
              <span>Project name <span className="text-red-500">*</span></span>
              <span className="flex items-center gap-1.5 text-[10px] font-medium uppercase tracking-wide text-slate-500">
              <input
                type="checkbox"
                checked={isProjectNameSameAsGroup}
                onChange={e => setIsProjectNameSameAsGroup(e.target.checked)}
                className="rounded border-slate-300 text-primary focus:ring-primary"
              />
                Same as team
              </span>
            </label>
            <input
              type="text"
              value={isProjectNameSameAsGroup ? groupName : projectName}
              onChange={e => setProjectName(e.target.value)}
              placeholder="Enter a project name"
              disabled={isProjectNameSameAsGroup}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:bg-slate-100 disabled:text-slate-500"
              required
              minLength={3}
              maxLength={60}
            />
            {!isProjectNameSameAsGroup && projectName.length > 0 && projectName.trim().length < 3 && (
              <p className="mt-1 text-xs text-red-500">Project name must be 3–60 characters.</p>
            )}
          </div>

          <div>
            <label className="mb-1.5 block text-xs font-semibold text-slate-600">
              Project description <span className="text-red-500">*</span>
            </label>
            <textarea
              value={description}
              onChange={e => setDescription(e.target.value)}
              placeholder="Briefly describe the project idea"
              className="h-10 w-full resize-none rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
              required
              minLength={20}
              maxLength={500}
            />
            <div className="mt-1 flex items-center justify-between gap-3">
              <span>
                {description.length > 0 && description.trim().length < 20 && (
                  <span className="text-xs text-red-500">Description must be 20–500 characters.</span>
                )}
              </span>
              <span className="text-xs text-slate-400">{description.length}/500</span>
            </div>
          </div>
        </div>

        {/* Validation Info & Submit */}
        <div className="order-1 grid lg:grid-cols-[minmax(0,1fr)_260px]">
          <div className="min-w-0 p-3">
            <h4 className="mb-2 flex items-center gap-2 text-[11px] font-bold uppercase tracking-wide text-slate-600">
              <Users className="h-4 w-4" /> Team requirements
            </h4>
            
            <div className="grid overflow-hidden rounded-lg border border-slate-200 text-xs sm:grid-cols-2 [&>span]:min-h-11 [&>span]:px-3 [&>span]:py-2 [&>span:nth-child(-n+2)]:border-b [&>span:nth-child(odd)]:sm:border-r [&>span:nth-child(odd)]:sm:border-slate-200">
              <span className={`flex items-center gap-1.5 font-medium ${(studentCount >= 4 && studentCount <= 6) ? 'text-green-600' : studentCount > 0 ? 'text-red-500' : 'text-slate-400'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-[10px] ${(studentCount >= 4 && studentCount <= 6) ? 'bg-green-500' : studentCount > 0 ? 'bg-red-400' : 'bg-slate-300'}`}>
                  {(studentCount >= 4 && studentCount <= 6) ? '✓' : '✗'}
                </span>
                4–6 members ({studentCount}/6)
              </span>

              <span className={`flex items-center gap-1.5 font-medium ${hasGroup1 ? 'text-green-600' : studentCount > 0 ? 'text-red-500' : 'text-slate-400'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-[10px] shrink-0 ${hasGroup1 ? 'bg-green-500' : studentCount > 0 ? 'bg-red-400' : 'bg-slate-300'}`}>
                  {hasGroup1 ? '✓' : '✗'}
                </span>
                <span>Group 1 (BBA) required</span>
              </span>

              <span className={`flex items-center gap-1.5 font-medium ${hasGroup2 ? 'text-green-600' : studentCount > 0 ? 'text-red-500' : 'text-slate-400'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-[10px] shrink-0 ${hasGroup2 ? 'bg-green-500' : studentCount > 0 ? 'bg-red-400' : 'bg-slate-300'}`}>
                  {hasGroup2 ? '✓' : '✗'}
                </span>
                <span>Group 2 (BIT) required</span>
              </span>
              
              <span className={`hidden items-center gap-1.5 font-medium ${hasCurrentUser ? 'text-green-600' : 'text-red-500'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-[10px] shrink-0 ${hasCurrentUser ? 'bg-green-500' : 'bg-red-400'}`}>
                  {hasCurrentUser ? '✓' : '✗'}
                </span>
                <span>You must be a team member</span>
              </span>

              <span className={`flex items-center gap-1.5 font-medium ${hasLeader ? 'text-green-600' : 'text-red-500'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-[10px] shrink-0 ${hasLeader ? 'bg-green-500' : 'bg-red-400'}`}>
                  {hasLeader ? '✓' : '×'}
                </span>
                <span>Team Leader required</span>
              </span>
            </div>

            {uniqueMajors.length > 0 && (
              <p className="mt-1.5 text-[10px] text-slate-500">
                Majors: {uniqueMajors.join(', ')}
              </p>
            )}

            {missingMajorCount > 0 && (
              <div className="mt-1.5 flex items-start gap-2 text-[10px] text-amber-700">
                <AlertCircle className="w-4 h-4 text-amber-500 shrink-0" />
                <p className="text-xs text-amber-700">
                  {missingMajorCount} selected student(s) have no declared major.
                </p>
              </div>
            )}
            
            {studentCount > 0 && !isFullyValid && (
              <div className="mt-1.5 flex items-start gap-2 text-[10px] text-orange-700">
                <AlertTriangle className="w-4 h-4 text-orange-500 shrink-0" />
                <p className="text-xs text-orange-700 font-medium">
                  Select 4–6 members and include both GROUP_1 and GROUP_2.
                </p>
              </div>
            )}
          </div>

          <div className="border-t border-slate-200 bg-slate-50/60 p-3 lg:border-l lg:border-t-0">
            <label className="mb-1.5 flex items-center gap-1 text-[11px] font-bold text-slate-600">
              <Crown className="h-3.5 w-3.5 text-amber-500" /> Team Leader <span className="text-red-500">*</span>
            </label>
            <select
              value={selected.includes(selectedLeaderId) ? selectedLeaderId : ''}
              onChange={(event) => setSelectedLeaderId(event.target.value)}
              className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            >
              <option value="">Select leader</option>
              {selectedStudents.map(student => <option key={student._id} value={student._id}>{student.fullName}</option>)}
            </select>
            <button
              type="button"
              onClick={handleSubmit}
              disabled={submitting || !canSubmit}
              className={`mt-2 flex w-full items-center justify-center gap-1.5 rounded-lg py-2 text-xs font-bold transition-all
                ${!canSubmit
                  ? 'cursor-not-allowed bg-slate-200 text-slate-500'
                  : 'bg-green-500 text-white hover:bg-green-600'
                }`}
            >
              {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : 
                (isFullyValid ? <CheckCircle2 className="w-4 h-4" /> : <Send className="w-4 h-4" />)
              }
              {isFullyValid
                ? (proposal ? 'Resubmit project proposal' : 'Create Team')
                : 'Requirements not met'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}


