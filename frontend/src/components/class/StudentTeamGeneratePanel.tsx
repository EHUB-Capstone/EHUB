import { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { Users, AlertTriangle, CheckCircle2, Loader2, AlertCircle, Send } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { teamApi } from '../../api/teamApi';
import { unwrapApiData } from '../../utils/classMappers';
import { getTeamGroupFromMajor } from '../../constants/majors';
import { isMissingTeamMajor } from '../../utils/teamManagement';
import TeamSuggestionTooltip from './TeamSuggestionTooltip';

const getTeamSizeSuggestion = (count) => {
  if (count === 0) return '';
  if (count === 1 || count === 2) {
    return `Còn ${count} sinh viên, chưa đủ để tạo nhóm. Cần điều chỉnh thành viên từ các nhóm khác.`;
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
    return 'Không thể chia đều thành nhóm 4-6 người. Cần điều chỉnh thành viên giữa các nhóm.';
  }

  const summary = [
    best.sixes && `${best.sixes} nhóm x 6 sinh viên`,
    best.fives && `${best.fives} nhóm x 5 sinh viên`,
    best.fours && `${best.fours} nhóm x 4 sinh viên`,
  ].filter(Boolean);

  return `Gợi ý: ${summary.join(', ')}. Mỗi nhóm vẫn cần có đủ 2 nhóm chuyên ngành.`;
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

    const groupsPresent = new Set();
    const studentsByGroup = { GROUP_1: [], GROUP_2: [] };

    for (const s of selectedStudents) {
      const major = s.major;
      if (!major) continue;
      const group = getTeamGroupFromMajor(major);
      if (group) {
        groupsPresent.add(group);
        studentsByGroup[group] = [...(studentsByGroup[group] || []), s];
      }
    }

    const hasGroup1 = groupsPresent.has('GROUP_1');
    const hasGroup2 = groupsPresent.has('GROUP_2');
    const hasBothGroups = hasGroup1 && hasGroup2;

    const isValidSize = studentCount >= 4 && studentCount <= 6;
    const isValidGroups = hasBothGroups;
    
    const isFullyValid = isValidSize && isValidGroups && studentCount > 0;
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

    const hasLeader = selected.includes(selectedLeaderId);
    const isFormValid = isGroupNameValid && isProjectNameValid && isDescriptionValid && hasCurrentUser && hasLeader;

    return {
      selectedStudents,
      studentCount,
      uniqueMajors,
      hasGroup1,
      hasGroup2,
      hasBothGroups,
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
    if (!isFormValid) {
      toast.error('Vui lòng nhập đầy đủ tên nhóm, tên dự án và mô tả dự án hợp lệ.');
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
        'Team proposal submitted for review.',
      );
      
      // Reset form
      setGroupName('');
      setProjectName('');
      setDescription('');
      setIsProjectNameSameAsGroup(true);
      setSelectedLeaderId('');
      onTeamCreated();
    } catch (e) {
      toast.error(e?.response?.data?.message || e?.message || 'Lỗi khi tạo nhóm');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="bg-white rounded-2xl border p-4 shadow-sm">
      <div className="mb-4 flex items-center justify-between gap-3 border-b border-slate-100 pb-4">
        <h3 className="font-bold text-lg text-slate-800">Tạo nhóm</h3>
        {suggestionInfo && (
          <TeamSuggestionTooltip>
              Lớp có {suggestionInfo.unassigned} SV chưa có nhóm. {suggestionInfo.suggestion}
          </TeamSuggestionTooltip>
        )}
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Form Info */}
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1">
              Tên nhóm <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={groupName}
              onChange={e => setGroupName(e.target.value)}
              placeholder="VD: Nhóm 1, Alpha Team..."
              className="w-full px-3 py-2 border border-slate-300 rounded-xl text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none"
              required
              minLength={3}
              maxLength={60}
            />
            {groupName.length > 0 && groupName.trim().length < 3 && (
              <p className="text-xs text-red-500 mt-1">Tên nhóm phải từ 3-60 ký tự</p>
            )}
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1">
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
            <label className="flex items-center gap-2 text-sm font-medium text-slate-700 mb-2">
              <input
                type="checkbox"
                checked={isProjectNameSameAsGroup}
                onChange={e => setIsProjectNameSameAsGroup(e.target.checked)}
                className="rounded border-slate-300 text-primary focus:ring-primary"
              />
              Dùng tên nhóm làm tên dự án
            </label>

            {!isProjectNameSameAsGroup && (
              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-1">
                  Tên dự án <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={projectName}
                  onChange={e => setProjectName(e.target.value)}
                  placeholder="Nhập tên dự án..."
                  className="w-full px-3 py-2 border border-slate-300 rounded-xl text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none"
                  required
                  minLength={3}
                  maxLength={60}
                />
                {projectName.length > 0 && projectName.trim().length < 3 && (
                  <p className="text-xs text-red-500 mt-1">Tên dự án phải từ 3-60 ký tự</p>
                )}
              </div>
            )}
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1">
              Mô tả dự án <span className="text-red-500">*</span>
            </label>
            <textarea
              value={description}
              onChange={e => setDescription(e.target.value)}
              placeholder="Mô tả ngắn về ý tưởng dự án..."
              className="w-full px-3 py-2 border border-slate-300 rounded-xl text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none resize-none h-20"
              required
              minLength={20}
              maxLength={500}
            />
            <div className="mt-1 flex items-center justify-between gap-3">
              <span>
                {description.length > 0 && description.trim().length < 20 && (
                  <span className="text-xs text-red-500">Mô tả phải từ 20-500 ký tự</span>
                )}
              </span>
              <span className="text-xs text-slate-400">{description.length}/500</span>
            </div>
          </div>
        </div>

        {/* Validation Info & Submit */}
        <div className="bg-slate-50 p-4 rounded-2xl border border-slate-200 flex flex-col justify-between">
          <div>
            <h4 className="font-semibold text-slate-700 mb-3 text-sm flex items-center gap-2">
              <Users className="w-4 h-4" /> Thành viên đã chọn ({studentCount})
            </h4>
            
            <div className="mb-4 flex flex-wrap gap-x-4 gap-y-2 text-xs">
              <span className={`flex items-center gap-1.5 font-medium ${(studentCount >= 4 && studentCount <= 6) ? 'text-green-600' : studentCount > 0 ? 'text-red-500' : 'text-slate-400'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-[10px] ${(studentCount >= 4 && studentCount <= 6) ? 'bg-green-500' : studentCount > 0 ? 'bg-red-400' : 'bg-slate-300'}`}>
                  {(studentCount >= 4 && studentCount <= 6) ? '✓' : '✗'}
                </span>
                4–6 thành viên ({studentCount}/6)
              </span>

              <span className={`flex items-center gap-1.5 font-medium ${hasGroup1 ? 'text-green-600' : studentCount > 0 ? 'text-red-500' : 'text-slate-400'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-[10px] shrink-0 ${hasGroup1 ? 'bg-green-500' : studentCount > 0 ? 'bg-red-400' : 'bg-slate-300'}`}>
                  {hasGroup1 ? '✓' : '✗'}
                </span>
                <span>Ít nhất 1 SV Nhóm 1 (BBA)</span>
              </span>

              <span className={`flex items-center gap-1.5 font-medium ${hasGroup2 ? 'text-green-600' : studentCount > 0 ? 'text-red-500' : 'text-slate-400'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-[10px] shrink-0 ${hasGroup2 ? 'bg-green-500' : studentCount > 0 ? 'bg-red-400' : 'bg-slate-300'}`}>
                  {hasGroup2 ? '✓' : '✗'}
                </span>
                <span>Ít nhất 1 SV Nhóm 2 (BIT)</span>
              </span>
              
              <span className={`flex items-center gap-1.5 font-medium ${hasCurrentUser ? 'text-green-600' : 'text-red-500'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-[10px] shrink-0 ${hasCurrentUser ? 'bg-green-500' : 'bg-red-400'}`}>
                  {hasCurrentUser ? '✓' : '✗'}
                </span>
                <span>Bạn phải nằm trong danh sách nhóm</span>
              </span>

              <span className={`flex items-center gap-1.5 font-medium ${hasLeader ? 'text-green-600' : 'text-red-500'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-[10px] shrink-0 ${hasLeader ? 'bg-green-500' : 'bg-red-400'}`}>
                  {hasLeader ? '✓' : '×'}
                </span>
                <span>Đã chọn Team Leader</span>
              </span>
            </div>

            {uniqueMajors.length > 0 && (
              <p className="text-xs text-slate-500 mb-3">
                Chuyên ngành: {uniqueMajors.join(', ')}
              </p>
            )}

            {missingMajorCount > 0 && (
              <div className="flex items-start gap-2 bg-amber-50 rounded-xl p-2 border border-amber-200 mb-3">
                <AlertCircle className="w-4 h-4 text-amber-500 shrink-0" />
                <p className="text-xs text-amber-700">
                  Có {missingMajorCount} SV chưa có chuyên ngành.
                </p>
              </div>
            )}
            
            {studentCount > 0 && !isFullyValid && (
              <div className="flex items-start gap-2 bg-orange-50 rounded-xl p-2 border border-orange-200 mb-3">
                <AlertTriangle className="w-4 h-4 text-orange-500 shrink-0" />
                <p className="text-xs text-orange-700 font-medium">
                  Nhóm chưa đạt chuẩn. Nhóm hợp lệ phải có 4-6 thành viên và đủ GROUP_1/GROUP_2.
                </p>
              </div>
            )}
          </div>

          <div>
            <button
              onClick={handleSubmit}
              disabled={submitting || !canSubmit}
              className={`w-full py-2.5 rounded-xl text-sm font-bold flex justify-center items-center gap-2 transition-all
                ${!canSubmit
                  ? 'cursor-not-allowed bg-slate-200 text-slate-500'
                  : 'bg-green-500 text-white hover:bg-green-600'
                }`}
            >
              {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : 
                (isFullyValid ? <CheckCircle2 className="w-4 h-4" /> : <Send className="w-4 h-4" />)
              }
              {isFullyValid
                ? 'Tạo nhóm'
                : 'Chưa đủ điều kiện tạo nhóm'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}


