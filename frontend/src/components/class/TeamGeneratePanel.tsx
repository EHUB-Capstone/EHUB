// @ts-nocheck
import { useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { Users, AlertTriangle, Loader2, Info, AlertCircle, Send, ShieldAlert } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { getMajorName, getTeamGroupFromMajor, TEAM_MAJOR_GROUPS } from '../../constants/majors';
import TeamSuggestionTooltip from './TeamSuggestionTooltip';

const majorDisplay = (code) => {
  if (!code) return code;
  const upper = code.toUpperCase();
  const name = getMajorName(upper);
  return name ? `${upper} (${name})` : upper;
};

/**
 * Calculate optimal team groupings for a given student count.
 * Returns the best distribution of teams with 4-6 members each.
 */
const getTeamSizeSuggestion = (count) => {
  if (count === 0) return null;
  if (count < 3) return { type: 'error', text: `Chỉ còn ${count} SV chưa có nhóm — chưa đủ để tạo thêm nhóm.` };
  if (count === 3) return { type: 'exception', text: 'Còn 3 SV. Có thể gửi đề xuất nhóm ngoại lệ (3 người) lên Admin duyệt, hoặc phân vào các nhóm khác.' };
  if (count === 7) return { type: 'exception', text: 'Còn 7 SV. Không chia đều được thành nhóm 4-6 người. Có thể tạo 1 nhóm 4 SV + ghép 3 SV vào nhóm khác, hoặc gửi nhóm ngoại lệ 7 người lên Admin.' };

  const candidates = [];
  for (let sixes = 0; sixes <= Math.floor(count / 6); sixes++) {
    for (let fives = 0; fives <= Math.floor(count / 5); fives++) {
      const remainder = count - sixes * 6 - fives * 5;
      if (remainder < 0 || remainder % 4 !== 0) continue;
      const fours = remainder / 4;
      candidates.push({ sixes, fives, fours, groupCount: sixes + fives + fours });
    }
  }

  candidates.sort((a, b) => a.groupCount - b.groupCount || b.sixes - a.sixes || b.fives - a.fives);
  const best = candidates[0];
  if (!best) return { type: 'warning', text: 'Không thể chia đều thành nhóm 4-6 người. Cần điều chỉnh thủ công.' };

  const parts = [
    best.sixes && `${best.sixes} nhóm × 6 SV`,
    best.fives && `${best.fives} nhóm × 5 SV`,
    best.fours && `${best.fours} nhóm × 4 SV`,
  ].filter(Boolean);

  return {
    type: 'suggestion',
    text: `Gợi ý: ${parts.join(', ')} (tổng ${best.groupCount} nhóm). Mỗi nhóm cần đủ 2 nhóm chuyên ngành.`,
    groupCount: best.groupCount,
  };
};

export default function TeamGeneratePanel({ classId, selected: rawSelected, students: rawStudents, classMentors = [], onTeamCreated }) {
  const [submitting, setSubmitting] = useState(false);
  const [selectedMentorId, setSelectedMentorId] = useState('');
  const [selectedLeaderId, setSelectedLeaderId] = useState('');
  const students = useMemo(() => (Array.isArray(rawStudents) ? rawStudents : []), [rawStudents]);
  const selected = useMemo(() => (Array.isArray(rawSelected) ? rawSelected : []), [rawSelected]);

  // Class-level stats
  const classStats = useMemo(() => {
    const unassigned = students.filter(s => !s.teamId);
    const suggestion = getTeamSizeSuggestion(unassigned.length);
    return { total: students.length, unassigned: unassigned.length, suggestion };
  }, [students]);

  // Real-time validation of selected students
  const validation = useMemo(() => {
    const selectedStudents = students.filter(s => selected.includes(s._id));
    const studentCount = selectedStudents.length;

    const missingMajorCount = selectedStudents.filter(
      s => !s.major || typeof s.major !== 'string' || !s.major.trim()
    ).length;

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
    const canCreate = isValidSize && isValidGroups;
    const sizeError = studentCount > 0 && !isValidSize;
    const groupError = studentCount > 0 && !isValidGroups;

    // Exception: 3 or 7 members with both major groups
    const canSendException = (studentCount === 3 || studentCount === 7) && isValidGroups;

    const uniqueMajors = [...new Set(
      selectedStudents
        .map(s => s.major)
        .filter(m => typeof m === 'string' && m.trim())
        .map(m => m.trim().toUpperCase())
    )];

    return {
      selectedStudents, studentCount, uniqueMajors,
      hasGroup1, hasGroup2, hasBothGroups,
      canCreate, sizeError, groupError,
      canSendException, missingMajorCount, studentsByGroup,
    };
  }, [selected, students]);

  const {
    selectedStudents, studentCount, uniqueMajors,
    hasGroup1, hasGroup2,
    canCreate, sizeError, groupError, missingMajorCount, canSendException,
  } = validation;

  const generate = async (mode) => {
    if (!selectedLeaderId || !selected.includes(selectedLeaderId)) {
      toast.error('Please select a leader from the team members');
      return;
    }
    setSubmitting(true);
    try {
      const res = await classApi.generateTeam(classId, {
        studentIds: selected,
        mode,
        mentorId: selectedMentorId || undefined,
        leaderStudentId: selectedLeaderId,
      });
      const data = res?.data || res;
      const teamName = data?.team?.teamName || 'New Team';
      if (mode === 'exception') {
        toast.success(`Đề xuất nhóm ngoại lệ "${teamName}" đã gửi lên Admin để duyệt! 📨`);
      } else {
        toast.success(`Đề xuất nhóm "${teamName}" đã gửi cho Lecturer duyệt! 📋`);
      }
      setSelectedMentorId('');
      setSelectedLeaderId('');
      onTeamCreated();
    } catch (e) {
      toast.error(e?.response?.data?.message || e?.message || 'Failed to create team');
    } finally {
      setSubmitting(false);
    }
  };

  if (selected.length === 0) {
    if (classStats.unassigned === 0) return null;

    return (
      <TeamSuggestionTooltip label="Xem thông tin và hướng dẫn tạo nhóm">
          <div className="space-y-2">
            <p className="font-semibold text-white">
              {classStats.total} SV trong lớp · {classStats.unassigned} chưa có nhóm
            </p>
            {classStats.suggestion && (
              <p className="text-slate-200">{classStats.suggestion.text}</p>
            )}
            <div className="border-t border-slate-700 pt-2 text-slate-200">
              <p className="font-semibold text-white">Chọn sinh viên để tạo nhóm</p>
              <p className="mt-1">
                Nhóm cần 4–6 thành viên từ cả 2 nhóm ngành: Nhóm 1
                (BBA: BBA_HM, BBA_IB, BBA_MC, BBA_MKT, BEN, BBA_TM) và Nhóm 2
                (BIT: BIT_AI, BIT_GD, BIT_IA, BIT_SE). Tỉ lệ không quan trọng.
              </p>
            </div>
          </div>
      </TeamSuggestionTooltip>
    );
  }

  return (
    <div className="bg-white rounded-2xl border border-slate-200 p-4 shadow-sm space-y-3">
      {/* ── Class-level Statistics & Suggestion ── */}
      {classStats.unassigned > 0 && (
        <div className="flex flex-wrap gap-3 items-start">
          {/* Stats pill */}
          <div className="flex items-center gap-2 px-3 py-1.5 bg-slate-100 rounded-xl text-xs font-semibold text-slate-600">
            <Users className="w-3.5 h-3.5" />
            <span>{classStats.total} SV trong lớp</span>
            <span className="w-px h-3.5 bg-slate-300" />
            <span className={classStats.unassigned > 0 ? 'text-orange-600' : 'text-green-600'}>
              {classStats.unassigned} chưa có nhóm
            </span>
          </div>

          {/* Suggestion tooltip */}
          {classStats.suggestion && (
            <TeamSuggestionTooltip>
              {classStats.suggestion.text}
            </TeamSuggestionTooltip>
          )}
        </div>
      )}

      {/* ── Selection panel ── */}
      {selected.length === 0 ? (
        <div className="bg-slate-50 rounded-2xl border border-dashed border-slate-200 p-4 flex items-start gap-3 text-slate-400">
          <Info className="w-5 h-5 shrink-0 mt-0.5" />
          <div>
            <p className="text-sm font-medium text-slate-500">Chọn sinh viên để tạo nhóm</p>
            <p className="text-xs mt-1">
              Nhóm cần <strong>4–6 thành viên</strong> từ <strong>cả 2 nhóm ngành</strong>:
              {' '}Nhóm 1 (BBA: BBA_HM, BBA_IB, BBA_MC, BBA_MKT, BEN, BBA_TM) và Nhóm 2 (BIT: BIT_AI, BIT_GD, BIT_IA, BIT_SE).
              Tỉ lệ không quan trọng.
            </p>
          </div>
        </div>
      ) : (
        <div className={`rounded-2xl border p-4 transition-all ${
          (sizeError || groupError) && !canSendException ? 'bg-red-50 border-red-200' :
          canSendException ? 'bg-amber-50 border-amber-200' :
          canCreate ? 'bg-blue-50 border-blue-200' :
          'bg-slate-50 border-slate-200'
        }`}>
          <div className="flex flex-wrap items-start gap-4">
            {/* Status indicator */}
            <div className="flex items-start gap-3 flex-1 min-w-[200px]">
              {canCreate && <Send className="w-6 h-6 text-primary shrink-0 mt-0.5" />}
              {canSendException && !canCreate && <ShieldAlert className="w-6 h-6 text-amber-500 shrink-0 mt-0.5" />}
              {!canCreate && !canSendException && (sizeError || groupError) && <AlertTriangle className="w-6 h-6 text-red-500 shrink-0 mt-0.5" />}
              {!canCreate && !canSendException && !sizeError && !groupError && <Users className="w-6 h-6 text-slate-400 shrink-0 mt-0.5" />}

              <div>
                <p className="font-semibold text-slate-800">
                  {studentCount} sinh viên đã chọn
                  {canSendException && !canCreate && (
                    <span className="ml-2 text-xs font-bold text-amber-600 bg-amber-100 px-2 py-0.5 rounded-full">
                      Ngoại lệ
                    </span>
                  )}
                </p>
                <p className="text-xs text-slate-500 mt-0.5">
                  {uniqueMajors.length > 0
                    ? uniqueMajors.map(m => majorDisplay(m)).join(', ')
                    : 'Chưa có chuyên ngành'}
                </p>
              </div>
            </div>

            {/* Requirements checklist */}
            <div className="flex flex-col gap-2 text-xs min-w-[260px]">
              {/* Size check */}
              <span className={`flex items-center gap-1.5 font-medium ${(studentCount >= 4 && studentCount <= 6) ? 'text-green-600' : canSendException ? 'text-amber-600' : studentCount > 0 ? 'text-red-500' : 'text-slate-400'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-[10px] ${(studentCount >= 4 && studentCount <= 6) ? 'bg-green-500' : canSendException ? 'bg-amber-500' : studentCount > 0 ? 'bg-red-400' : 'bg-slate-300'}`}>
                  {(studentCount >= 4 && studentCount <= 6) ? '✓' : canSendException ? '!' : '✗'}
                </span>
                4–6 thành viên ({studentCount}/6)
                {canSendException && !canCreate && <span className="text-amber-600 font-normal ml-1">— ngoại lệ</span>}
              </span>

              {/* Group 1 check */}
              <span className={`flex items-center gap-1.5 font-medium ${hasGroup1 ? 'text-green-600' : studentCount > 0 ? 'text-red-500' : 'text-slate-400'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-[10px] shrink-0 ${hasGroup1 ? 'bg-green-500' : studentCount > 0 ? 'bg-red-400' : 'bg-slate-300'}`}>
                  {hasGroup1 ? '✓' : '✗'}
                </span>
                <span>
                  {TEAM_MAJOR_GROUPS[0].label}:{' '}
                  <span className="font-normal text-slate-500">{TEAM_MAJOR_GROUPS[0].majors.map(m => m.code).join(', ')}</span>
                </span>
              </span>

              {/* Group 2 check */}
              <span className={`flex items-center gap-1.5 font-medium ${hasGroup2 ? 'text-green-600' : studentCount > 0 ? 'text-red-500' : 'text-slate-400'}`}>
                <span className={`w-4 h-4 rounded-full flex items-center justify-center text-white text-[10px] shrink-0 ${hasGroup2 ? 'bg-green-500' : studentCount > 0 ? 'bg-red-400' : 'bg-slate-300'}`}>
                  {hasGroup2 ? '✓' : '✗'}
                </span>
                <span>
                  {TEAM_MAJOR_GROUPS[1].label}:{' '}
                  <span className="font-normal text-slate-500">{TEAM_MAJOR_GROUPS[1].majors.map(m => m.code).join(', ')}</span>
                </span>
              </span>
            </div>

            {/* Mentor Selection */}
            <div className="flex items-center gap-2 text-xs">
              <span className="font-semibold text-slate-600">Team Leader:</span>
              <select
                value={selected.includes(selectedLeaderId) ? selectedLeaderId : ''}
                onChange={(e) => setSelectedLeaderId(e.target.value)}
                className="border border-slate-200 rounded-lg px-2.5 py-1.5 text-xs outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary font-medium"
              >
                <option value="">Select leader</option>
                {selectedStudents.map(student => (
                  <option key={student._id} value={student._id}>{student.fullName}</option>
                ))}
              </select>
            </div>

            {/* Mentor Selection */}
            {classMentors.length > 0 && (
              <div className="flex items-center gap-2 text-xs">
                <span className="font-semibold text-slate-600">Assign Mentor:</span>
                <select
                  value={selectedMentorId}
                  onChange={(e) => setSelectedMentorId(e.target.value)}
                  className="border border-slate-200 rounded-lg px-2.5 py-1.5 text-xs outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary font-medium"
                >
                  <option value="">— Automatic / None —</option>
                  {classMentors.map(m => (
                    <option key={m._id} value={m._id}>{m.name}</option>
                  ))}
                </select>
              </div>
            )}

            {/* Action buttons */}
            <div className="flex gap-2 items-center flex-wrap">
              {groupError && !sizeError && !canSendException && (
                <div className="px-3 py-2 bg-red-100 text-red-600 rounded-xl text-xs font-medium max-w-[220px]">
                  Nhóm phải có SV từ cả 2 nhóm ngành.
                </div>
              )}
              {sizeError && !canSendException && (
                <div className="px-3 py-2 bg-red-100 text-red-600 rounded-xl text-xs font-medium">
                  Nhóm cần 4–6 thành viên.
                </div>
              )}

              {canCreate && (
                <button
                  onClick={() => generate('auto')}
                  disabled={submitting || !selected.includes(selectedLeaderId)}
                  className="px-4 py-2 bg-primary text-white rounded-xl text-sm font-semibold hover:bg-primary-700 disabled:opacity-50 transition-all flex items-center gap-2"
                >
                  {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
                  Gửi đề xuất cho Lecturer duyệt
                </button>
              )}

              {canSendException && !canCreate && (
                <button
                  onClick={() => generate('exception')}
                  disabled={submitting || !selected.includes(selectedLeaderId)}
                  className="px-4 py-2 bg-amber-500 text-white rounded-xl text-sm font-bold hover:bg-amber-600 disabled:opacity-50 transition-all flex items-center gap-2"
                  title={`Nhóm ${studentCount} thành viên — ngoại lệ, cần Admin duyệt`}
                >
                  {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
                  Gửi lên Admin duyệt
                </button>
              )}
            </div>
          </div>

          {/* Warning: students with missing major */}
          {missingMajorCount > 0 && (
            <div className="mt-3 flex items-start gap-2 bg-amber-50 rounded-xl p-3 border border-amber-200">
              <AlertCircle className="w-4 h-4 text-amber-500 mt-0.5 shrink-0" />
              <p className="text-xs text-amber-700">
                <strong>{missingMajorCount} sinh viên</strong> chưa có chuyên ngành.
                Vui lòng yêu cầu họ cập nhật hồ sơ hoặc kiểm tra dữ liệu đăng ký.
              </p>
            </div>
          )}

          {/* Exception info */}
          {canSendException && !canCreate && (
            <div className="mt-3 flex items-start gap-2 bg-amber-50 rounded-xl p-3 border border-amber-200">
              <ShieldAlert className="w-4 h-4 text-amber-500 mt-0.5 shrink-0" />
              <p className="text-xs text-amber-700">
                Nhóm {studentCount} thành viên là trường hợp ngoại lệ (quy định 4–6). Sau khi gửi, Admin sẽ xem xét và duyệt nhóm này.
              </p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
