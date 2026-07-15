// @ts-nocheck
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Users, Trash2, MessageSquare, MessageSquareDashed, ChevronDown, ChevronRight, Loader2, UserCog } from 'lucide-react';
import { teamApi } from '../../api/teamApi';
import TeamMemberEditModal from './TeamMemberEditModal';
import { getDisplayGroupName, getDisplayTeamName } from '../../utils/teamDisplay';

const majorColor = (major) => {
  const palette = ['bg-blue-50 text-blue-700', 'bg-purple-50 text-purple-700', 'bg-cyan-50 text-cyan-700', 'bg-orange-50 text-orange-700', 'bg-pink-50 text-pink-700'];
  let h = 0; for (const c of (major || '')) h = c.charCodeAt(0) + ((h << 5) - h);
  return palette[Math.abs(h) % palette.length];
};

// Safe display: avoids rendering raw ObjectId or object
const safeName = (ref) => {
  if (!ref) return null;
  if (typeof ref === 'string') return null; // unpopulated ObjectId string — don't render
  if (typeof ref === 'object' && ref.name) return ref.name;
  return null;
};

const normalizeTeamText = (value) => (typeof value === 'string' ? value.trim() : '');

function TeamCard({ team, onRefresh, onReview, canDelete = true, canManageInfo = true, currentStudentId, classStudents = [] }) {
  const navigate = useNavigate();
  const displayTeamName = getDisplayTeamName(team) || 'Unnamed Team';
  const [expanded,  setExpanded]  = useState(false);
  const [deleting,  setDeleting]  = useState(false);
  const [editing,   setEditing]   = useState(false);
  const [saving,    setSaving]    = useState(false);
  const [showEditMembers, setShowEditMembers] = useState(false);
  const [formData,  setFormData]  = useState({
    teamName: getDisplayTeamName(team),
    groupName: getDisplayGroupName(team),
    projectName: team.projectName || '',
    description: team.description || '',
  });

  const resetFormData = () => {
    setFormData({
      teamName: getDisplayTeamName(team),
      groupName: getDisplayGroupName(team),
      projectName: team.projectName || '',
      description: team.description || '',
    });
  };

  const beginEdit = () => {
    resetFormData();
    setEditing(true);
  };

  const handleDelete = async () => {
    if (!confirm(`Delete ${displayTeamName}? This will also remove the chat group.`)) return;
    setDeleting(true);
    try {
      await teamApi.delete(team._id);
      toast.success(`${displayTeamName} deleted`);
      onRefresh();
    } catch (e) {
      toast.error(e?.message || 'Failed to delete team');
    } finally {
      setDeleting(false);
    }
  };

  const handleSave = async () => {
    const nextData = {
      groupName: normalizeTeamText(formData.groupName),
      projectName: normalizeTeamText(formData.projectName),
      description: normalizeTeamText(formData.description),
    };

    if (canManageInfo) {
      nextData.teamName = normalizeTeamText(formData.teamName);
      if (nextData.teamName.length < 3 || nextData.teamName.length > 60) {
        toast.error('Team name must be 3-60 characters.');
        return;
      }
    }
    if (nextData.groupName.length < 3 || nextData.groupName.length > 60) {
      toast.error('Group name must be 3-60 characters.');
      return;
    }
    if (nextData.projectName.length < 3 || nextData.projectName.length > 60) {
      toast.error('Project name must be 3-60 characters.');
      return;
    }
    if (nextData.description && (nextData.description.length < 20 || nextData.description.length > 500)) {
      toast.error('Description must be 20-500 characters when provided.');
      return;
    }

    setSaving(true);
    try {
      await teamApi.update(team._id, nextData);
      toast.success('Team info updated');
      setEditing(false);
      onRefresh();
    } catch (e) {
      toast.error(e?.message || 'Failed to update team info');
    } finally {
      setSaving(false);
    }
  };

  const members = Array.isArray(team.members) ? team.members : [];
  const lecturerName = safeName(team.lectureId);
  const mentorName   = safeName(team.mentorId);
  const teamNameText = displayTeamName;
  const teamNumberMatch = teamNameText.match(/\bTeam\s*(\d+)\b/i);
  const teamBadge = teamNumberMatch?.[1] || teamNameText.trim().charAt(0)?.toUpperCase() || '?';
  
  const isPending = team.status === 'PENDING' || team.status === 'NEEDS_REVISION';
  const leaderId = (team.leaderId?._id || team.leaderId || '').toString();
  const isLeader = currentStudentId && leaderId === currentStudentId.toString();
  const canEditInfo = canManageInfo || isLeader;

  return (
    <div className={`bg-white rounded-2xl border ${isPending ? 'border-orange-300 shadow-orange-100' : 'border-slate-200/60'} shadow-sm overflow-hidden`}>
      {/* Team header */}
      <div className="flex items-center justify-between p-4">
        <button
          onClick={() => setExpanded(!expanded)}
          className="flex items-center gap-3 flex-1 text-left"
        >
          <div className={`w-9 h-9 rounded-xl flex items-center justify-center ${isPending ? 'bg-orange-100 text-orange-600' : 'bg-gradient-to-br from-primary to-secondary text-white'}`}>
            <span className="font-bold text-sm">{teamBadge}</span>
          </div>
          <div>
            <div className="flex items-center gap-2">
              <p className="font-bold text-slate-900">{teamNameText}</p>
              {team.status === 'PENDING' && (
                <span className="px-2 py-0.5 bg-orange-100 text-orange-700 text-[10px] font-bold rounded-full uppercase">Pending</span>
              )}
              {team.status === 'NEEDS_REVISION' && (
                <span className="px-2 py-0.5 bg-amber-100 text-amber-700 text-[10px] font-bold rounded-full uppercase">Needs revision</span>
              )}
              {team.status === 'REJECTED' && (
                <span className="px-2 py-0.5 bg-red-100 text-red-700 text-[10px] font-bold rounded-full uppercase">Rejected</span>
              )}
            </div>
            <p className="text-xs text-slate-400 font-mono">{team.teamCode || '—'}</p>
          </div>
          {expanded ? <ChevronDown className="w-4 h-4 text-slate-400 ml-2" /> : <ChevronRight className="w-4 h-4 text-slate-400 ml-2" />}
        </button>

        <div className="flex items-center gap-2">
          <span className="text-xs text-slate-400 flex items-center gap-1">
            <Users className="w-3.5 h-3.5" /> {members.length}
          </span>
          {/* Chat group status badge */}
          {team.chatGroupId ? (
            <span className="px-2 py-0.5 bg-green-50 text-green-600 text-xs font-semibold rounded-full flex items-center gap-1 hidden sm:flex">
              <MessageSquare className="w-3 h-3" /> Chat
            </span>
          ) : (
            <span className="px-2 py-0.5 bg-slate-100 text-slate-400 text-xs font-semibold rounded-full flex items-center gap-1 hidden sm:flex">
              <MessageSquareDashed className="w-3 h-3" /> No Chat
            </span>
          )}
          
          {isPending && onReview && (
            <button
              onClick={(e) => { e.stopPropagation(); onReview(team); }}
              className="px-2.5 py-1 text-xs font-semibold border border-orange-200 bg-orange-50 text-orange-600 hover:bg-orange-600 hover:text-white rounded-lg transition-all shadow-sm"
            >
              Review
            </button>
          )}

          {/* Edit Members button — Lecturer/Admin only */}
          {canManageInfo && (
            <button
              onClick={(e) => { e.stopPropagation(); setShowEditMembers(true); }}
              title="Chỉnh sửa thành viên nhóm"
              className="px-2.5 py-1 text-xs font-semibold border border-indigo-200 bg-indigo-50 text-indigo-600 hover:bg-indigo-600 hover:text-white rounded-lg transition-all shadow-sm flex items-center gap-1"
            >
              <UserCog className="w-3.5 h-3.5" />
              Thành viên
            </button>
          )}

          <button
            onClick={() => navigate(`/workspace/teams/${team._id}`)}
            className="px-2.5 py-1 text-xs font-semibold border border-slate-200 text-slate-600 hover:border-primary hover:text-primary rounded-lg transition-all bg-white shrink-0 cursor-pointer shadow-2xs"
          >
            Workspace
          </button>
          {canDelete && (
            <button
              onClick={handleDelete}
              disabled={deleting}
              className="p-1.5 rounded-lg text-slate-300 hover:text-red-500 hover:bg-red-50 transition-all shrink-0"
            >
              {deleting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />}
            </button>
          )}
        </div>
      </div>

      {/* Members & Details */}
      {expanded && (
        <div className="border-t border-slate-100 p-4">
          {team.status === 'NEEDS_REVISION' && team.rejectReason && (
            <div className="mb-4 rounded-xl border border-amber-200 bg-amber-50 p-3">
              <p className="text-xs font-bold uppercase text-amber-700">Nội dung cần chỉnh sửa</p>
              <p className="mt-1 whitespace-pre-wrap text-sm text-amber-800">{team.rejectReason}</p>
            </div>
          )}
          {team.status === 'REJECTED' && team.rejectReason && (
            <div className="mb-4 rounded-xl border border-red-200 bg-red-50 p-3">
              <p className="text-xs font-bold uppercase text-red-700">Lý do từ chối</p>
              <p className="mt-1 whitespace-pre-wrap text-sm text-red-800">{team.rejectReason}</p>
            </div>
          )}
          
          {/* Team Info Section */}
          <div className="mb-5 bg-slate-50 rounded-xl p-3 border border-slate-100">
            <div className="flex items-center justify-between mb-3">
              <h4 className="text-xs font-bold text-slate-500 uppercase tracking-wider">Team Info</h4>
              {!editing && canEditInfo && (
                <button onClick={beginEdit} className="text-xs font-medium text-primary hover:underline">
                  Edit Info
                </button>
              )}
            </div>

            {editing ? (
              <div className="space-y-3">
                {canManageInfo && (
                  <div>
                    <label className="block text-xs font-medium text-slate-600 mb-1">Team Name</label>
                    <input
                      type="text"
                      value={formData.teamName}
                      onChange={e => setFormData({ ...formData, teamName: e.target.value })}
                      className="w-full px-3 py-1.5 border border-slate-200 rounded-lg text-sm outline-none focus:border-primary"
                      maxLength={60}
                    />
                  </div>
                )}
                <div>
                  <label className="block text-xs font-medium text-slate-600 mb-1">Group Name</label>
                  <input
                    type="text"
                    value={formData.groupName}
                    onChange={e => setFormData({ ...formData, groupName: e.target.value })}
                    className="w-full px-3 py-1.5 border border-slate-200 rounded-lg text-sm outline-none focus:border-primary"
                    maxLength={60}
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-600 mb-1">Project Name</label>
                  <input type="text" value={formData.projectName} onChange={e => setFormData({ ...formData, projectName: e.target.value })} className="w-full px-3 py-1.5 border border-slate-200 rounded-lg text-sm outline-none focus:border-primary" maxLength={60} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-600 mb-1">Description</label>
                  <textarea value={formData.description} onChange={e => setFormData({ ...formData, description: e.target.value })} className="w-full px-3 py-1.5 border border-slate-200 rounded-lg text-sm outline-none focus:border-primary" rows={2} maxLength={500} />
                </div>
                <div className="flex items-center gap-2 pt-1">
                  <button onClick={handleSave} disabled={saving} className="px-3 py-1.5 bg-primary text-white rounded-lg text-xs font-medium hover:bg-primary-700 transition-all flex items-center gap-1">
                    {saving && <Loader2 className="w-3 h-3 animate-spin" />} Save
                  </button>
                  <button onClick={() => { resetFormData(); setEditing(false); }} disabled={saving} className="px-3 py-1.5 border border-slate-200 text-slate-600 rounded-lg text-xs hover:bg-slate-100 transition-all">
                    Cancel
                  </button>
                </div>
              </div>
            ) : (
              <div className="grid grid-cols-2 gap-y-2 gap-x-4">
                <div>
                  <p className="text-[10px] uppercase font-semibold text-slate-400">Team Name</p>
                  <p className="text-sm text-slate-700 font-medium">{teamNameText}</p>
                </div>
                <div>
                  <p className="text-[10px] uppercase font-semibold text-slate-400">Group Name</p>
                  <p className="text-sm text-slate-700 font-medium">{getDisplayGroupName(team) || 'Not set'}</p>
                </div>
                <div className="col-span-2">
                  <p className="text-[10px] uppercase font-semibold text-slate-400">Project Name</p>
                  <p className="text-sm text-slate-700">{team.projectName || 'Not set'}</p>
                </div>
                <div className="col-span-2">
                  <p className="text-[10px] uppercase font-semibold text-slate-400">Description</p>
                  <p className="text-sm text-slate-700">{team.description || 'No description'}</p>
                </div>
              </div>
            )}
          </div>

          <h4 className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">Members</h4>
          {members.length === 0 ? (
            <p className="text-sm text-slate-400 text-center py-2">No members</p>
          ) : (
            <div className="space-y-2">
              {members.map((m, i) => {
                const s = m?.studentId;
                // Guard: studentId could be null or unpopulated ObjectId
                const studentName  = (typeof s === 'object' && s?.fullName) ? s.fullName : 'Unknown';
                const studentEmail = (typeof s === 'object' && s?.email) ? s.email : '';
                const studentMajor = (typeof s === 'object' && s?.major) ? s.major : null;
                const initial      = studentName.charAt(0)?.toUpperCase() || '?';

                return (
                  <div key={i} className="flex items-center gap-3 p-2 rounded-xl hover:bg-slate-50 transition-colors">
                    <div className="w-7 h-7 rounded-full bg-gradient-to-br from-secondary-300 to-secondary flex items-center justify-center text-white text-xs font-bold shrink-0">
                      {initial}
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-slate-800 truncate">{studentName}</p>
                      {studentEmail && <p className="text-xs text-slate-400 truncate">{studentEmail}</p>}
                    </div>
                    {studentMajor && (
                      <span className={`text-xs font-semibold px-2 py-0.5 rounded-full shrink-0 ${majorColor(studentMajor)}`}>
                        {studentMajor}
                      </span>
                    )}
                    <span className="text-xs text-slate-400 shrink-0">{m.roleInTeam || 'Member'}</span>
                  </div>
                );
              })}
            </div>
          )}

          {/* Mentor / Lecture — safe display */}
          {(lecturerName || mentorName) && (
            <div className="mt-3 pt-3 border-t border-slate-100 flex flex-wrap gap-3">
              {lecturerName && (
                <div className="flex items-center gap-2 text-xs text-slate-500">
                  <span className="px-2 py-0.5 bg-secondary-50 text-secondary rounded-full font-semibold">Lecturer</span>
                  {lecturerName}
                </div>
              )}
              {mentorName && (
                <div className="flex items-center gap-2 text-xs text-slate-500">
                  <span className="px-2 py-0.5 bg-primary-50 text-primary rounded-full font-semibold">Mentor</span>
                  {mentorName}
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* Edit Members Modal */}
      {showEditMembers && (
        <TeamMemberEditModal
          team={team}
          classStudents={classStudents}
          onClose={() => setShowEditMembers(false)}
          onRefresh={onRefresh}
        />
      )}
    </div>
  );
}

export default function TeamList({ teams, onRefresh, onReview, canDelete = true, canManageInfo = true, currentStudentId, classStudents = [] }) {
  // Guard: teams might be null/undefined
  const safeTeams = Array.isArray(teams) ? teams : [];

  if (safeTeams.length === 0) {
    return (
      <div className="bg-slate-50 rounded-2xl border border-dashed border-slate-200 p-10 text-center text-slate-400">
        <Users className="w-10 h-10 mx-auto mb-3 opacity-30" />
        <p className="font-medium">No teams yet</p>
        <p className="text-sm mt-1">Select students above and click &quot;Create Team&quot;</p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
      {safeTeams.map(team => (
        <TeamCard
          key={team._id}
          team={team}
          onRefresh={onRefresh}
          onReview={onReview}
          canDelete={canDelete}
          canManageInfo={canManageInfo}
          currentStudentId={currentStudentId}
          classStudents={classStudents}
        />
      ))}
    </div>
  );
}
