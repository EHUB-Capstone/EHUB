// @ts-nocheck
import { useState, useMemo } from 'react';
import toast from 'react-hot-toast';
import {
  X,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Loader2,
  FilePenLine,
} from 'lucide-react';
import { teamApi } from '../../api/teamApi';
import { getDisplayGroupName } from '../../utils/teamDisplay';
import { getTeamGroupFromMajor } from '../../constants/majors';

export default function ReviewTeamProposalModal({ team, classStudents, onClose, onRefresh }) {
  const [submitting, setSubmitting] = useState(false);
  const [status, setStatus] = useState('APPROVED');
  const [reviewNote, setReviewNote] = useState('');
  const [teamInfo, setTeamInfo] = useState({
    groupName: getDisplayGroupName(team),
    projectName: team.projectName || '',
    description: team.description || '',
  });
  
  // Maintain a local list of member IDs to allow lecturer to add/remove members before approving
  const initialMemberIds = useMemo(() => {
    return team.members.map(m => typeof m.studentId === 'string' ? m.studentId : m.studentId._id);
  }, [team]);
  
  const [memberIds, setMemberIds] = useState(initialMemberIds);

  const validation = useMemo(() => {
    const selectedStudents = classStudents.filter(s => memberIds.includes(s._id));
    const studentCount = selectedStudents.length;

    const groupsPresent = new Set();
    for (const s of selectedStudents) {
      if (s.major) {
        const group = getTeamGroupFromMajor(s.major);
        if (group) groupsPresent.add(group);
      }
    }

    const hasGroup1 = groupsPresent.has('GROUP_1');
    const hasGroup2 = groupsPresent.has('GROUP_2');
    const isValidSize = studentCount >= 4 && studentCount <= 6;
    const isFullyValid = isValidSize && hasGroup1 && hasGroup2;

    return {
      selectedStudents,
      studentCount,
      hasGroup1,
      hasGroup2,
      isValidSize,
      isFullyValid
    };
  }, [memberIds, classStudents]);

  const toggleMember = (id) => {
    setMemberIds(prev => 
      prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]
    );
  };

  const handleReview = async () => {
    const cleanGroupName = teamInfo.groupName.trim();
    const cleanProjectName = teamInfo.projectName.trim();
    const cleanDescription = teamInfo.description.trim();

    if (cleanGroupName.length < 3 || cleanGroupName.length > 60) {
      toast.error('Tên nhóm phải từ 3-60 ký tự');
      return;
    }
    if (cleanProjectName.length < 3 || cleanProjectName.length > 60) {
      toast.error('Tên dự án phải từ 3-60 ký tự');
      return;
    }
    if (cleanDescription.length < 20 || cleanDescription.length > 500) {
      toast.error('Mô tả dự án phải từ 20-500 ký tự');
      return;
    }
    if (['REJECTED', 'NEEDS_REVISION'].includes(status) && !reviewNote.trim()) {
      toast.error(
        status === 'REJECTED'
          ? 'Vui lòng nhập lý do từ chối'
          : 'Vui lòng nhập nội dung cần chỉnh sửa'
      );
      return;
    }
    
    setSubmitting(true);
    try {
      await teamApi.reviewProposal(team._id, {
        status,
        rejectReason: status === 'APPROVED' ? null : reviewNote.trim(),
        newMemberIds: status === 'APPROVED' ? memberIds : undefined,
        groupName: cleanGroupName,
        projectName: cleanProjectName,
        description: cleanDescription,
      });
      const successMessage = {
        APPROVED: 'Đã duyệt nhóm',
        NEEDS_REVISION: 'Đã gửi yêu cầu chỉnh sửa',
        REJECTED: 'Đã từ chối nhóm',
      }[status];
      toast.success(successMessage);
      onRefresh();
      onClose();
    } catch (e) {
      toast.error(e?.response?.data?.message || 'Lỗi khi duyệt nhóm');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm">
      <div className="bg-white rounded-2xl w-full max-w-3xl shadow-xl overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50">
          <h3 className="font-bold text-lg text-slate-800">Duyệt đề xuất nhóm</h3>
          <button onClick={onClose} className="p-2 hover:bg-slate-200 rounded-full transition-colors text-slate-500">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="p-6 overflow-y-auto flex-1 custom-scrollbar space-y-6">
          {/* Team Info */}
          <div className="bg-slate-50 rounded-xl p-4 border border-slate-200">
            <div className="mb-3 flex items-center gap-2">
              <FilePenLine className="h-4 w-4 text-primary" />
              <h4 className="text-sm font-bold text-slate-700">Thông tin nhóm</h4>
            </div>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="mb-1 block text-xs font-bold uppercase text-slate-500">
                  Tên nhóm <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={teamInfo.groupName}
                  onChange={(event) => setTeamInfo(prev => ({ ...prev, groupName: event.target.value }))}
                  maxLength={60}
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-bold uppercase text-slate-500">
                  Tên dự án <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={teamInfo.projectName}
                  onChange={(event) => setTeamInfo(prev => ({ ...prev, projectName: event.target.value }))}
                  maxLength={60}
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
                />
              </div>
              <div className="sm:col-span-2">
                <div className="mb-1 flex items-center justify-between gap-3">
                  <label className="text-xs font-bold uppercase text-slate-500">
                    Mô tả dự án <span className="text-red-500">*</span>
                  </label>
                  <span className="text-[10px] text-slate-400">{teamInfo.description.length}/500</span>
                </div>
                <textarea
                  value={teamInfo.description}
                  onChange={(event) => setTeamInfo(prev => ({ ...prev, description: event.target.value }))}
                  minLength={20}
                  maxLength={500}
                  rows={3}
                  className="w-full resize-none rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
                />
              </div>
            </div>
          </div>

          {/* Members */}
          <div>
            <div className="flex items-center justify-between mb-3">
              <h4 className="font-bold text-slate-700">Thành viên ({validation.studentCount})</h4>
              
              {!validation.isFullyValid && status === 'APPROVED' && (
                <span className="text-xs font-semibold text-orange-600 bg-orange-100 px-2 py-1 rounded-lg flex items-center gap-1">
                  <AlertTriangle className="w-3.5 h-3.5" />
                  Chưa đạt chuẩn ({validation.studentCount}/6 thành viên, {validation.hasGroup1 ? 'Có N1' : 'Thiếu N1'}, {validation.hasGroup2 ? 'Có N2' : 'Thiếu N2'})
                </span>
              )}
            </div>

            {status === 'APPROVED' ? (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                {/* When approving, lecturer can adjust members. Show all class students. */}
                {classStudents.map(s => {
                  const isSelected = memberIds.includes(s._id);
                  const leaderId = team.leaderId?._id || team.leaderId;
                  const isLeader = s._id === leaderId;
                  
                  // Disable if assigned to another team AND not part of this proposal
                  const isAssignedElse = s.teamId && s.teamId.toString() !== team._id.toString();

                  return (
                    <label key={s._id} className={`flex items-center gap-3 p-2 rounded-xl border cursor-pointer transition-all ${
                      isSelected ? 'border-primary bg-primary-50' : isAssignedElse ? 'opacity-50 cursor-not-allowed bg-slate-50' : 'border-slate-200 hover:border-primary/50 hover:bg-slate-50'
                    }`}>
                      <input 
                        type="checkbox" 
                        checked={isSelected}
                        disabled={isAssignedElse}
                        onChange={() => toggleMember(s._id)}
                        className="rounded border-slate-300 text-primary focus:ring-primary w-4 h-4"
                      />
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2">
                          <span className="text-sm font-semibold text-slate-800 truncate">{s.fullName}</span>
                          {isLeader && <span className="text-[10px] bg-amber-100 text-amber-700 px-1 rounded font-bold">Leader</span>}
                        </div>
                        <span className="text-xs text-slate-500 block truncate">{s.major}</span>
                      </div>
                    </label>
                  );
                })}
              </div>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                {/* Just view the original members */}
                {validation.selectedStudents.map(s => (
                  <div key={s._id} className="flex items-center gap-3 p-2 rounded-xl border border-slate-200 bg-slate-50">
                    <div className="flex-1 min-w-0">
                      <span className="text-sm font-semibold text-slate-800 truncate block">{s.fullName}</span>
                      <span className="text-xs text-slate-500 block truncate">{s.major}</span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Action form */}
          <div className="border-t border-slate-200 pt-4">
            <h4 className="font-bold text-slate-700 mb-3">Quyết định</h4>
            <div className="grid grid-cols-1 gap-3 mb-4 sm:grid-cols-3">
              <button
                onClick={() => setStatus('APPROVED')}
                className={`flex-1 flex flex-col items-center justify-center p-3 rounded-xl border-2 transition-all ${
                  status === 'APPROVED' ? 'border-green-500 bg-green-50 text-green-700' : 'border-slate-200 text-slate-500 hover:border-green-200 hover:bg-slate-50'
                }`}
              >
                <CheckCircle2 className={`w-6 h-6 mb-1 ${status === 'APPROVED' ? 'text-green-500' : 'text-slate-400'}`} />
                <span className="font-semibold">Chấp nhận</span>
                <span className="text-[10px] text-center mt-1 opacity-70">Có thể điều chỉnh thành viên trước khi duyệt</span>
              </button>

              <button
                onClick={() => setStatus('NEEDS_REVISION')}
                className={`flex min-h-28 flex-col items-center justify-center rounded-xl border-2 p-3 transition-all ${
                  status === 'NEEDS_REVISION'
                    ? 'border-amber-500 bg-amber-50 text-amber-700'
                    : 'border-slate-200 text-slate-500 hover:border-amber-200 hover:bg-slate-50'
                }`}
              >
                <FilePenLine className={`mb-1 h-6 w-6 ${
                  status === 'NEEDS_REVISION' ? 'text-amber-500' : 'text-slate-400'
                }`} />
                <span className="font-semibold">Yêu cầu chỉnh sửa</span>
                <span className="mt-1 text-center text-[10px] opacity-70">Gửi phản hồi để nhóm cập nhật</span>
              </button>

              <button
                onClick={() => setStatus('REJECTED')}
                className={`flex-1 flex flex-col items-center justify-center p-3 rounded-xl border-2 transition-all ${
                  status === 'REJECTED' ? 'border-red-500 bg-red-50 text-red-700' : 'border-slate-200 text-slate-500 hover:border-red-200 hover:bg-slate-50'
                }`}
              >
                <XCircle className={`w-6 h-6 mb-1 ${status === 'REJECTED' ? 'text-red-500' : 'text-slate-400'}`} />
                <span className="font-semibold">Từ chối</span>
                <span className="text-[10px] text-center mt-1 opacity-70">Bắt buộc nhập lý do từ chối</span>
              </button>
            </div>

            {['REJECTED', 'NEEDS_REVISION'].includes(status) && (
              <div className="mb-4">
                <label className="block text-sm font-semibold text-slate-700 mb-1">
                  {status === 'REJECTED' ? 'Lý do từ chối' : 'Nội dung cần chỉnh sửa'}{' '}
                  <span className="text-red-500">*</span>
                </label>
                <textarea
                  value={reviewNote}
                  onChange={e => setReviewNote(e.target.value)}
                  placeholder={
                    status === 'REJECTED'
                      ? 'Nhập lý do từ chối...'
                      : 'Mô tả rõ những nội dung nhóm cần cập nhật...'
                  }
                  className={`h-20 w-full resize-none rounded-xl border px-3 py-2 text-sm outline-none focus:ring-2 ${
                    status === 'REJECTED'
                      ? 'border-slate-300 focus:border-red-500 focus:ring-red-500/20'
                      : 'border-slate-300 focus:border-amber-500 focus:ring-amber-500/20'
                  }`}
                />
              </div>
            )}
            
            {status === 'APPROVED' && !validation.isFullyValid && (
              <div className="bg-amber-50 text-amber-700 p-3 rounded-xl text-sm border border-amber-200 mb-4 flex gap-2">
                <AlertTriangle className="w-5 h-5 shrink-0" />
                <span>Bạn đang duyệt một nhóm không đủ chuẩn. Hãy chắc chắn rằng bạn muốn cấp quyền ngoại lệ cho nhóm này.</span>
              </div>
            )}
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-slate-100 bg-slate-50 flex justify-end gap-3">
          <button
            onClick={onClose}
            disabled={submitting}
            className="px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-200 rounded-xl transition-all"
          >
            Hủy
          </button>
          <button
            onClick={handleReview}
            disabled={
              submitting
              || (['REJECTED', 'NEEDS_REVISION'].includes(status) && !reviewNote.trim())
            }
            className={`px-6 py-2 text-sm font-bold text-white rounded-xl transition-all flex items-center gap-2 ${
              status === 'APPROVED'
                ? 'bg-green-500 hover:bg-green-600'
                : status === 'NEEDS_REVISION'
                  ? 'bg-amber-500 hover:bg-amber-600'
                  : 'bg-red-500 hover:bg-red-600'
            } disabled:opacity-50`}
          >
            {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
            Xác nhận {{
              APPROVED: 'Duyệt',
              NEEDS_REVISION: 'Yêu cầu sửa',
              REJECTED: 'Từ chối',
            }[status]}
          </button>
        </div>
      </div>
    </div>
  );
}
