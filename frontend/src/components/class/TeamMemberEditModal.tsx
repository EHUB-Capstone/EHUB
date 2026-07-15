// @ts-nocheck
import { useState, useMemo } from 'react';
import toast from 'react-hot-toast';
import {
  X, UserPlus, UserMinus, Users, AlertTriangle, CheckCircle2, Loader2, Search, UserCheck
} from 'lucide-react';
import { teamApi } from '../../api/teamApi';
import { getTeamGroupFromMajor } from '../../constants/majors';

const majorColor = (major) => {
  const palette = [
    'bg-blue-50 text-blue-700 border-blue-200',
    'bg-purple-50 text-purple-700 border-purple-200',
    'bg-cyan-50 text-cyan-700 border-cyan-200',
    'bg-orange-50 text-orange-700 border-orange-200',
    'bg-pink-50 text-pink-700 border-pink-200',
  ];
  let h = 0;
  for (const c of (major || '')) h = c.charCodeAt(0) + ((h << 5) - h);
  return palette[Math.abs(h) % palette.length];
};

/**
 * Modal for Lecturer/Admin to add or remove students from a team at any time.
 * Props:
 *  - team: the team document (with populated members.studentId)
 *  - classStudents: all students in the class
 *  - onClose: callback to close the modal
 *  - onRefresh: callback after successful update
 */
export default function TeamMemberEditModal({ team, classStudents, onClose, onRefresh }) {
  const [submitting, setSubmitting] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const initialLeaderId = (team.leaderId?._id || team.leaderId || '').toString();
  const [selectedLeaderId, setSelectedLeaderId] = useState(initialLeaderId);
  const [showLeaderConfirmation, setShowLeaderConfirmation] = useState(false);

  // Build initial member ID set from the team
  const initialMemberIds = useMemo(() => {
    return new Set(
      team.members.map(m => {
        const sid = m.studentId;
        return (typeof sid === 'object' && sid?._id) ? sid._id.toString() : sid?.toString();
      }).filter(Boolean)
    );
  }, [team]);

  const [pendingAddIds, setPendingAddIds] = useState(new Set());
  const [pendingRemoveIds, setPendingRemoveIds] = useState(new Set());

  // Effective member list = initial + pending adds - pending removes
  const effectiveMemberIds = useMemo(() => {
    const ids = new Set(initialMemberIds);
    pendingAddIds.forEach(id => ids.add(id));
    pendingRemoveIds.forEach(id => ids.delete(id));
    return ids;
  }, [initialMemberIds, pendingAddIds, pendingRemoveIds]);

  // Validation: check the resulting team against hard rules
  const validation = useMemo(() => {
    const members = classStudents.filter(s => effectiveMemberIds.has(s._id));
    const count = members.length;

    const groupsPresent = new Set();
    for (const s of members) {
      if (s.major) {
        const g = getTeamGroupFromMajor(s.major);
        if (g) groupsPresent.add(g);
      }
    }

    const hasGroup1 = groupsPresent.has('GROUP_1');
    const hasGroup2 = groupsPresent.has('GROUP_2');
    const isValidSize = count >= 4 && count <= 6;
    const isFullyValid = isValidSize && hasGroup1 && hasGroup2;
    const isException = !isValidSize && count > 0;

    return { members, count, hasGroup1, hasGroup2, isValidSize, isFullyValid, isException };
  }, [effectiveMemberIds, classStudents]);

  // Students that can be added (not in the effective team, not in another team — unless they're being removed)
  const availableStudents = useMemo(() => {
    return classStudents.filter(s => {
      const id = s._id;
      if (effectiveMemberIds.has(id)) return false; // already in team
      // Can add if they have no team or are being removed from their current team
      if (!s.teamId) return true;
      const teamIdStr = typeof s.teamId === 'object' && s.teamId !== null && s.teamId._id ? s.teamId._id.toString() : s.teamId.toString();
      if (teamIdStr === team._id.toString()) return true; // their team IS this team
      return false;
    }).filter(s => {
      if (!searchQuery.trim()) return true;
      const q = searchQuery.toLowerCase();
      return (
        s.fullName?.toLowerCase().includes(q) ||
        s.major?.toLowerCase().includes(q) ||
        s.rollNumber?.toLowerCase().includes(q)
      );
    });
  }, [classStudents, effectiveMemberIds, team._id, searchQuery]);

  const toggleAdd = (id) => {
    setPendingAddIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
    // Remove from pending removes if they were there
    setPendingRemoveIds(prev => {
      const next = new Set(prev);
      next.delete(id);
      return next;
    });
  };

  const toggleRemove = (id) => {
    if (selectedLeaderId === id) setSelectedLeaderId('');
    if (!initialMemberIds.has(id)) {
      // It was a pending add — just cancel the add
      setPendingAddIds(prev => {
        const next = new Set(prev);
        next.delete(id);
        return next;
      });
      return;
    }
    setPendingRemoveIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const hasMemberChanges = pendingAddIds.size > 0 || pendingRemoveIds.size > 0;
  const hasLeaderChange = selectedLeaderId !== initialLeaderId;
  const hasChanges = hasMemberChanges || hasLeaderChange;

  const persistChanges = async () => {
    if (!hasChanges) {
      toast.error('Chưa có thay đổi nào');
      return;
    }
    if (!selectedLeaderId || !effectiveMemberIds.has(selectedLeaderId)) {
      toast.error('Please select a leader from the team members');
      return;
    }

    setSubmitting(true);
    try {
      if (hasMemberChanges) {
        await teamApi.updateMembers(team._id, {
          addStudentIds: [...pendingAddIds],
          removeStudentIds: [...pendingRemoveIds],
        });
      }
      if (hasLeaderChange) {
        await teamApi.assignLeader(team._id, selectedLeaderId);
      }
      toast.success(hasLeaderChange ? 'Team leader confirmed and team notified' : 'Team members updated');
      onRefresh();
      onClose();
    } catch (e) {
      toast.error(e?.response?.data?.message || 'Lỗi khi cập nhật thành viên');
    } finally {
      setSubmitting(false);
    }
  };

  const handleSave = () => {
    if (hasLeaderChange) {
      setShowLeaderConfirmation(true);
      return;
    }
    persistChanges();
  };

  const selectedLeader = validation.members.find(student => student._id === selectedLeaderId);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm">
      <div className="bg-white rounded-2xl w-full max-w-3xl shadow-2xl overflow-hidden flex flex-col max-h-[92vh]">
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-100 bg-slate-50 flex items-center justify-between shrink-0">
          <div>
            <h3 className="font-bold text-lg text-slate-800">Chỉnh sửa thành viên nhóm</h3>
            <p className="text-xs text-slate-500 mt-0.5">{team.teamName} · {team.teamCode}</p>
          </div>
          <button onClick={onClose} className="p-2 hover:bg-slate-200 rounded-full transition-colors text-slate-500">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto custom-scrollbar">
          <div className="p-6 space-y-6">
            {/* Validation Summary */}
            <div className={`flex flex-wrap gap-3 items-center px-4 py-3 rounded-xl border text-sm font-medium transition-all ${
              validation.isFullyValid
                ? 'bg-green-50 border-green-200 text-green-700'
                : validation.isException
                ? 'bg-amber-50 border-amber-200 text-amber-700'
                : 'bg-red-50 border-red-200 text-red-700'
            }`}>
              {validation.isFullyValid
                ? <CheckCircle2 className="w-4 h-4 shrink-0" />
                : <AlertTriangle className="w-4 h-4 shrink-0" />}
              <span>
                {validation.count} thành viên
                {!validation.isValidSize && ` (quy định 4–6${validation.isException ? ', ngoại lệ' : ''})`}
                {!validation.hasGroup1 && ' · Thiếu SV Nhóm 1 (BBA)'}
                {!validation.hasGroup2 && ' · Thiếu SV Nhóm 2 (BIT)'}
                {validation.isFullyValid && ' · Hợp lệ ✓'}
              </span>
              {hasChanges && (
                <span className="ml-auto text-xs bg-white/70 px-2 py-0.5 rounded-lg border">
                  {pendingAddIds.size > 0 && `+${pendingAddIds.size} thêm`}
                  {pendingAddIds.size > 0 && pendingRemoveIds.size > 0 && ', '}
                  {pendingRemoveIds.size > 0 && `-${pendingRemoveIds.size} xóa`}
                </span>
              )}
            </div>

            {/* Current Members */}
            <section>
              <h4 className="flex items-center gap-2 text-sm font-bold text-slate-700 mb-3">
                <Users className="w-4 h-4 text-slate-500" />
                Thành viên hiện tại ({validation.count})
              </h4>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                {validation.members.map(s => {
                  const isLeader = s._id === selectedLeaderId;
                  const isBeingRemoved = pendingRemoveIds.has(s._id);
                  const isNewlyAdded = pendingAddIds.has(s._id);
                  return (
                    <div
                      key={s._id}
                      className={`flex items-center gap-3 p-3 rounded-xl border transition-all ${
                        isBeingRemoved
                          ? 'bg-red-50 border-red-200 opacity-60'
                          : isNewlyAdded
                          ? 'bg-green-50 border-green-200'
                          : 'bg-slate-50 border-slate-200'
                      }`}
                    >
                      {!isBeingRemoved && (
                        <input
                          type="radio"
                          name={`leader-${team._id}`}
                          checked={isLeader}
                          onChange={() => setSelectedLeaderId(s._id)}
                          title="Set as Team Leader"
                          className="h-4 w-4 shrink-0 border-slate-300 text-primary focus:ring-primary"
                        />
                      )}
                      <div className="w-8 h-8 rounded-full bg-gradient-to-br from-primary/70 to-secondary flex items-center justify-center text-white text-xs font-bold shrink-0">
                        {s.fullName?.charAt(0)?.toUpperCase() || '?'}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-1.5 flex-wrap">
                          <span className="text-sm font-semibold text-slate-800 truncate">{s.fullName}</span>
                          {isLeader && (
                            <span className="text-[10px] bg-amber-100 text-amber-700 px-1.5 py-0.5 rounded font-bold">Leader</span>
                          )}
                          {isNewlyAdded && (
                            <span className="text-[10px] bg-green-100 text-green-700 px-1.5 py-0.5 rounded font-bold">Mới</span>
                          )}
                          {isBeingRemoved && (
                            <span className="text-[10px] bg-red-100 text-red-700 px-1.5 py-0.5 rounded font-bold">Xóa</span>
                          )}
                        </div>
                        {s.major && (
                          <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded border ${majorColor(s.major)}`}>
                            {s.major}
                          </span>
                        )}
                      </div>
                      <button
                        onClick={() => toggleRemove(s._id)}
                        className={`p-1.5 rounded-lg transition-all shrink-0 ${
                          isBeingRemoved
                            ? 'bg-red-100 text-red-600 hover:bg-slate-100 hover:text-slate-500'
                            : 'text-slate-300 hover:text-red-500 hover:bg-red-50'
                        }`}
                        title={isBeingRemoved ? 'Hoàn tác xóa' : 'Xóa khỏi nhóm'}
                      >
                        <UserMinus className="w-4 h-4" />
                      </button>
                    </div>
                  );
                })}
              </div>
            </section>

            {/* Add Students */}
            <section>
              <h4 className="flex items-center gap-2 text-sm font-bold text-slate-700 mb-3">
                <UserPlus className="w-4 h-4 text-slate-500" />
                Thêm sinh viên vào nhóm
              </h4>

              {/* Search */}
              <div className="relative mb-3">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                <input
                  type="text"
                  placeholder="Tìm theo tên, mã SV, chuyên ngành..."
                  value={searchQuery}
                  onChange={e => setSearchQuery(e.target.value)}
                  className="w-full pl-9 pr-4 py-2 text-sm border border-slate-200 rounded-xl outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
                />
              </div>

              {availableStudents.length === 0 ? (
                <p className="text-sm text-slate-400 text-center py-4 bg-slate-50 rounded-xl border border-dashed">
                  {searchQuery ? 'Không tìm thấy sinh viên phù hợp' : 'Không có sinh viên nào có thể thêm vào nhóm này'}
                </p>
              ) : (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 max-h-56 overflow-y-auto custom-scrollbar pr-1">
                  {availableStudents.map(s => {
                    const isPendingAdd = pendingAddIds.has(s._id);
                    return (
                      <label
                        key={s._id}
                        className={`flex items-center gap-3 p-3 rounded-xl border cursor-pointer transition-all ${
                          isPendingAdd
                            ? 'border-primary bg-primary-50'
                            : 'border-slate-200 hover:border-primary/50 hover:bg-slate-50'
                        }`}
                      >
                        <input
                          type="checkbox"
                          checked={isPendingAdd}
                          onChange={() => toggleAdd(s._id)}
                          className="rounded border-slate-300 text-primary focus:ring-primary w-4 h-4 shrink-0"
                        />
                        <div className="flex-1 min-w-0">
                          <span className="text-sm font-semibold text-slate-800 block truncate">{s.fullName}</span>
                          <div className="flex items-center gap-1.5 mt-0.5">
                            {s.major && (
                              <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded border ${majorColor(s.major)}`}>
                                {s.major}
                              </span>
                            )}
                            {s.rollNumber && (
                              <span className="text-[10px] text-slate-400">{s.rollNumber}</span>
                            )}
                          </div>
                        </div>
                      </label>
                    );
                  })}
                </div>
              )}
            </section>
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-slate-100 bg-slate-50 flex justify-between items-center gap-3 shrink-0">
          <p className="text-xs text-slate-500">
            {validation.isFullyValid
              ? '✓ Nhóm hợp lệ (4–6 SV, đủ 2 nhóm ngành)'
              : '⚠️ Nhóm chưa đạt chuẩn. Không thể lưu thay đổi.'}
          </p>
          <div className="flex gap-2">
            <button
              onClick={onClose}
              disabled={submitting}
              className="px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-200 rounded-xl transition-all"
            >
              Hủy
            </button>
            <button
              onClick={handleSave}
              disabled={submitting || !hasChanges || !validation.isFullyValid}
              className="px-6 py-2 text-sm font-bold text-white bg-primary rounded-xl hover:bg-primary-700 transition-all flex items-center gap-2 disabled:opacity-50"
            >
              {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
              Lưu thay đổi
            </button>
          </div>
        </div>
      </div>

      {showLeaderConfirmation && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-900/60 p-4 backdrop-blur-sm">
          <div className="w-full max-w-md rounded-xl bg-white p-6 text-center shadow-2xl">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-amber-100 text-amber-700">
              <UserCheck className="h-6 w-6" />
            </div>
            <h3 className="mt-4 text-lg font-bold text-slate-900">Confirm Team Leader</h3>
            <p className="mt-2 text-sm leading-6 text-slate-600">
              Assign <strong>{selectedLeader?.fullName || 'this member'}</strong> as the Team Leader?
              The whole team will receive an in-system notification after confirmation.
            </p>
            <div className="mt-6 flex gap-3">
              <button
                type="button"
                onClick={() => setShowLeaderConfirmation(false)}
                disabled={submitting}
                className="flex-1 rounded-lg border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-50"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={persistChanges}
                disabled={submitting}
                className="flex flex-1 items-center justify-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white hover:bg-primary-700 disabled:opacity-50"
              >
                {submitting && <Loader2 className="h-4 w-4 animate-spin" />}
                Confirm
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
