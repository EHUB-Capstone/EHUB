// @ts-nocheck
// frontend/src/pages/workspace/TeamWorkspace.jsx
import { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import {
  Users, Mail, Loader2,
  Award, ArrowLeft, Shield
} from 'lucide-react';
import { workspaceApi } from '../../api/workspaceApi';
import { teamWorkspaceApi } from '../../api/teamWorkspaceApi';
import { useAuth } from '../../hooks/useAuth';
import EvaluationPanel from '../../components/workspace/EvaluationPanel';
import MentoringPanel from '../../components/workspace/MentoringPanel';
import SprintPanel from '../../components/workspace/SprintPanel';
import WeeklyRoadmapPlanner from '../../components/workspace/WeeklyRoadmapPlanner';
import QuickShortcuts from '../../components/workspace/shortcuts/QuickShortcuts';
import CheckpointSection from '../../components/workspace/checkpoints/CheckpointSection';
import WorkspaceSelector from '../../components/workspace/WorkspaceSelector';
import ProjectDirectionCard from '../../components/workspace/ProjectDirectionCard';
import { getDisplayTeamName } from '../../utils/teamDisplay';

const clearWorkspaceSelectionCache = () => {
  [
    'selectedClassId',
    'selectedTeamId',
    'currentClass',
    'currentTeam',
    'studentClass',
    'teamId',
    'classId',
    'courseCode',
    'semester',
  ].forEach((key) => localStorage.removeItem(key));
};

export default function TeamWorkspace() {
  const { teamId } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const { user } = useAuth();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState(null);
  const [emptyMessage, setEmptyMessage] = useState(null);
  const [workspaceContext, setWorkspaceContext] = useState(null);

  // Initialise active tab from URL query param (?tab=roadmap)
  const [activeTab, setActiveTab] = useState(() => {
    const params = new URLSearchParams(location.search);
    return params.get('tab') || 'overview';
  });

  // Sync tab when query param changes externally
  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const tab = params.get('tab');
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (tab) setActiveTab(tab);
  }, [location.search]);

  const fetchWorkspaceData = useCallback(async (targetTeamId = teamId || null) => {
    try {
      setLoading(true);
      setErrorMsg(null);
      setEmptyMessage(null);

      let contextRes;
      if (targetTeamId) {
        contextRes = await teamWorkspaceApi.getWorkspaceContext(targetTeamId);
      } else {
        contextRes = await teamWorkspaceApi.getCurrentWorkspace();
      }

      const context = contextRes?.data || null;
      if (!context) {
        clearWorkspaceSelectionCache();
        setWorkspaceContext(null);
        setData(null);
        setEmptyMessage(contextRes?.message || 'You have joined this class but have not been assigned to a team yet.');
        return;
      }

      setWorkspaceContext(context);
      const resolvedTeamId = context.selectedWorkspace?.teamId;
      const res = await workspaceApi.getTeamWorkspace(resolvedTeamId);

      if (res.data === null) {
        // Not assigned to team yet
        clearWorkspaceSelectionCache();
        setData(null);
        setEmptyMessage(res.message || 'You have joined this class but have not been assigned to a team yet.');
      } else {
        setData(res.data);
      }
    } catch (err) {
      console.error(err);
      if (err.status === 403) clearWorkspaceSelectionCache();
      setErrorMsg(err.response?.data?.error || err.message || 'Failed to load workspace');
    } finally {
      setLoading(false);
    }
  }, [teamId]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchWorkspaceData();
  }, [fetchWorkspaceData]);

  const handleWorkspaceChange = (nextTeamId) => {
    fetchWorkspaceData(nextTeamId);
  };

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh]">
        <Loader2 className="w-10 h-10 text-primary animate-spin" />
        <p className="text-sm text-slate-400 mt-2 font-medium">Loading Startup Workspace...</p>
      </div>
    );
  }

  if (errorMsg) {
    return (
      <div className="max-w-2xl mx-auto text-center py-12 px-4">
        <div className="w-16 h-16 bg-red-50 text-red-500 rounded-2xl flex items-center justify-center mx-auto mb-4 border border-red-100">
          <Shield className="w-8 h-8" />
        </div>
        <h2 className="text-xl font-bold text-slate-800">Access Restricted</h2>
        <p className="text-slate-500 mt-2">{errorMsg}</p>
        <button
          onClick={() => navigate(-1)}
          className="mt-6 inline-flex items-center gap-2 text-sm font-semibold text-primary hover:underline"
        >
          <ArrowLeft className="w-4 h-4" /> Go Back
        </button>
      </div>
    );
  }

  if (!data) {
    return (
      <div className="max-w-2xl mx-auto text-center py-16 px-4 bg-white rounded-2xl border border-slate-200/60 shadow-sm mt-8">
        <div className="w-16 h-16 bg-primary-50 text-primary rounded-2xl flex items-center justify-center mx-auto mb-4 border border-primary-100">
          <Users className="w-8 h-8" />
        </div>
        <h2 className="text-xl font-bold text-slate-800">No Team Assigned</h2>
        <p className="text-slate-500 mt-2 max-w-md mx-auto">
          {emptyMessage || 'You have joined this class but have not been assigned to a team yet.'}
        </p>
      </div>
    );
  }

  const { team, class: cls, members, lecturer, mentor, proposal, latestDeck } = data;
  const privilegedRoles = ['ADMIN', 'LECTURER', 'MENTOR'];
  const isTeamMember = members && members.some(m => m.userId?._id === user?._id);
  const accessMode = workspaceContext?.accessMode || workspaceContext?.selectedWorkspace?.accessMode || null;
  const isReadOnly = accessMode === 'READ_ONLY';
  const isEditable = !isReadOnly && (privilegedRoles.includes(user?.role) || isTeamMember);
  const currentMember = members?.find((member) => {
    const memberUserId = member.userId?._id || member.userId;
    return String(memberUserId || '') === String(user?._id || '')
      || (user?.email && member.email?.toLowerCase() === user.email.toLowerCase());
  });
  const isTeamLeader = Boolean(
    currentMember && String(team.leaderId?._id || team.leaderId || '') === String(currentMember._id)
  );
  const displayTeamName = getDisplayTeamName(team) || 'Unnamed Team';

  return (
    <div className="space-y-6 max-w-6xl mx-auto">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-white rounded-2xl border border-slate-200/60 p-6 shadow-sm">
        <div className="flex items-center gap-4">
          <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-primary-500 to-secondary flex items-center justify-center text-white shrink-0 shadow-sm">
            <Award className="w-6 h-6" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="text-xs font-semibold px-2 py-0.5 bg-primary-50 text-primary border border-primary-100 rounded-md">
                {cls?.classCode}
              </span>
            </div>
            <h1 className="text-2xl font-bold text-slate-900 mt-1">{displayTeamName} Workspace</h1>
            <p className="text-sm text-slate-500 mt-0.5">{team.description || 'Startup Workspace'}</p>
          </div>
        </div>

        {teamId && (
          <button
            onClick={() => navigate(-1)}
            className="flex items-center gap-1.5 text-slate-500 hover:text-slate-800 transition-colors text-sm font-semibold px-4 py-2 border border-slate-200 rounded-xl"
          >
            <ArrowLeft className="w-4 h-4" /> Back
          </button>
        )}
        <WorkspaceSelector
          selectedWorkspace={workspaceContext?.selectedWorkspace}
          availableWorkspaces={workspaceContext?.availableWorkspaces || []}
          onChangeWorkspace={handleWorkspaceChange}
          disabled={loading}
        />
      </div>

      {isReadOnly && (
        <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm font-medium text-amber-800">
          {workspaceContext?.selectedWorkspace?.isArchived
            ? 'This is an archived workspace. Editing actions are disabled.'
            : 'You are viewing this previous course workspace in read-only mode.'}
        </div>
      )}

      {/* Tabs */}
      <div className="flex border-b border-slate-200 overflow-x-auto">
        {[
          { key: 'overview', label: 'Workspace Overview' },
          { key: 'roadmap', label: 'Weekly Roadmap' },
          { key: 'evaluation', label: 'Evaluation' },
          { key: 'mentoring', label: 'Mentoring Sessions' },
          { key: 'shortcut', label: 'Quick Shortcuts' },
        ].map(({ key, label, icon: Icon }) => (
          <button
            key={key}
            onClick={() => setActiveTab(key)}
            className={`flex items-center gap-1.5 py-3 px-6 text-sm font-medium border-b-2 whitespace-nowrap transition-colors ${activeTab === key
                ? 'border-primary text-primary'
                : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
              }`}
          >
            {Icon && <Icon className="w-3.5 h-3.5" />}
            {label}
          </button>
        ))}
      </div>

      {/* Tab Content */}
      {activeTab === 'overview' && (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">

          {/* Left Side - Proposal & Checkpoints */}
          <div className="lg:col-span-8 space-y-6">

            <ProjectDirectionCard
              key={`${team._id}-${team.projectDirectionUpdatedAt || ''}`}
              team={team}
              canEdit={!isReadOnly && isTeamLeader}
              onSaved={() => fetchWorkspaceData(team._id)}
            />

            {/* Startup Checkpoints */}
            <CheckpointSection 
              teamId={String(team._id)} 
              isEditable={isEditable}
            />
          </div>

          {/* Right Side - Team & Versions */}
          <div className="lg:col-span-4 space-y-6">

            {/* Stakeholders Info */}
            <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-5 space-y-4">
              <h3 className="font-bold text-slate-800 border-b border-slate-100 pb-3 flex items-center gap-2">
                <Users className="w-4 h-4 text-primary" /> Members & Mentors
              </h3>

              {/* Lecturer */}
              {lecturer && (
                <div className="bg-slate-50/50 rounded-xl p-3 border border-slate-100">
                  <p className="text-[10px] font-bold text-slate-400 uppercase tracking-wider">Lecturer</p>
                  <div className="flex items-center gap-2.5 mt-1.5">
                    <div className="w-8 h-8 rounded-full bg-blue-100 flex items-center justify-center text-blue-600 font-semibold text-xs">
                      {lecturer.name?.charAt(0)}
                    </div>
                    <div className="min-w-0">
                      <p className="text-sm font-semibold text-slate-700 truncate">{lecturer.name}</p>
                      <p className="text-xs text-slate-400 truncate flex items-center gap-1"><Mail className="w-3 h-3" />{lecturer.email}</p>
                    </div>
                  </div>
                </div>
              )}

              {/* Mentor */}
              <div className="bg-slate-50/50 rounded-xl p-3 border border-slate-100">
                <p className="text-[10px] font-bold text-slate-400 uppercase tracking-wider">Mentor</p>
                {mentor ? (
                  <div className="flex items-center gap-2.5 mt-1.5">
                    <div className="w-8 h-8 rounded-full bg-amber-100 flex items-center justify-center text-amber-600 font-semibold text-xs">
                      {mentor.name?.charAt(0)}
                    </div>
                    <div className="min-w-0">
                      <p className="text-sm font-semibold text-slate-700 truncate">{mentor.name}</p>
                      <p className="text-xs text-slate-400 truncate flex items-center gap-1"><Mail className="w-3 h-3" />{mentor.email}</p>
                    </div>
                  </div>
                ) : (
                  <p className="text-xs text-slate-400 mt-1 italic">No mentor assigned yet</p>
                )}
              </div>

              {/* Members List */}
              <div className="space-y-3">
                <p className="text-[10px] font-bold text-slate-400 uppercase tracking-wider px-1">Startup Team Members</p>
                <div className="space-y-2">
                  {members && members.map(member => (
                    <div key={member._id} className="flex items-center justify-between p-2.5 rounded-xl hover:bg-slate-50/60 border border-transparent hover:border-slate-100 transition-all">
                      <div className="flex items-center gap-2.5 min-w-0">
                        <div className="w-8 h-8 rounded-full bg-gradient-to-br from-primary-100 to-primary-200 flex items-center justify-center text-primary font-bold text-xs">
                          {member.fullName?.charAt(0)}
                        </div>
                        <div className="min-w-0">
                          <p className="text-sm font-semibold text-slate-800 truncate">{member.fullName}</p>
                          <p className="text-[10px] text-slate-400 font-mono">{member.rollNumber}</p>
                        </div>
                      </div>
                      <span className="text-[10px] font-bold px-2 py-0.5 bg-slate-100 text-slate-500 rounded-md">
                        {member.roleInTeam || 'Member'}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            </div>

          </div>
        </div>
      )}

      {activeTab === 'roadmap' && (() => {
        // Map TeamWorkspace variables to WeeklyRoadmapPlanner props
        const courseCode = cls?.subjectCode || '';
        const classId = cls?._id || '';
        const teamIdProp = team?._id || '';
        // Find the current user's roleInTeam from members array
        const myMember = members?.find(m => m.userId?._id === user?._id || m.userId === user?._id);
        const roleInTeam = myMember?.roleInTeam || '';

        return (
          <WeeklyRoadmapPlanner
            courseCode={courseCode}
            classId={String(classId)}
            teamId={String(teamIdProp)}
            currentUser={user}
            roleInTeam={roleInTeam}
            teamMembers={members || []}
            isReadOnly={isReadOnly}
            accessMode={accessMode}
          />
        );
      })()}

      {activeTab === 'evaluation' && (
        <EvaluationPanel
          teamId={team._id}
          proposalId={proposal?._id}
          pitchDeckId={latestDeck?._id}
          isReadOnly={isReadOnly}
        />
      )}

      {activeTab === 'mentoring' && (
        <MentoringPanel teamId={team._id} isReadOnly={isReadOnly} />
      )}

      {activeTab === 'sprint' && (
        <SprintPanel teamId={team._id} members={members} isEditable={isEditable} isReadOnly={isReadOnly} />
      )}

      {activeTab === 'shortcut' && (
        <QuickShortcuts teamId={team._id} isEditable={isEditable && !isReadOnly} isReadOnly={isReadOnly} />
      )}
    </div>
  );
}
