// @ts-nocheck
// Hub: list teams the user can open in Startup Workspace (Admin / Lecturer / Mentor)
import { useEffect, useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Kanban, Search, Loader2, Users, ChevronRight, FileText, Flag,
  GraduationCap, Award, Link2, Archive,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { workspaceApi } from '../../api/workspaceApi';
import { teamWorkspaceApi } from '../../api/teamWorkspaceApi';
import { useAuth } from '../../hooks/useAuth';
import EmptyState from '../../components/ui/EmptyState';
import Modal from '../../components/ui/Modal';
import Button from '../../components/ui/Button';

const roleHint = {
  ADMIN: 'All startup teams in the system',
  LECTURER: 'Teams in classes you teach',
  MENTOR: 'Teams you mentor',
};

export default function StartupWorkspaceHub() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [teams, setTeams] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [linkModalOpen, setLinkModalOpen] = useState(false);
  const [previousTeamId, setPreviousTeamId] = useState('');
  const [nextTeamId, setNextTeamId] = useState('');
  const [linking, setLinking] = useState(false);

  const role = (user?.role || 'STUDENT').toUpperCase();

  const loadTeams = async () => {
      try {
        setLoading(true);
        const res = await workspaceApi.getAccessibleTeams();
        if (res.success) {
          setTeams(res.data?.teams || []);
        }
      } catch (e) {
        toast.error(e?.message || 'Failed to load teams');
      } finally {
        setLoading(false);
      }
    };

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadTeams();
  }, []);

  const canLinkContinuity = role === 'ADMIN' || role === 'LECTURER' || role === 'LECTURE';

  const teamLabel = (team) => {
    const classCode = team.class?.classCode || 'Class';
    const course = team.courseCode || team.class?.subjectCode || '';
    const semester = team.semester || [team.class?.semester, team.class?.year].filter(Boolean).join('');
    return `${team.teamName} (${classCode}${course ? ` - ${course}` : ''}${semester ? ` - ${semester}` : ''})`;
  };

  const openLinkModal = () => {
    const activeTeams = teams.filter((team) => !team.nextTeamId);
    setPreviousTeamId(activeTeams[0]?._id || teams[0]?._id || '');
    setNextTeamId(activeTeams.find((team) => team._id !== (activeTeams[0]?._id || teams[0]?._id))?._id || '');
    setLinkModalOpen(true);
  };

  const handleLinkWorkspaces = async () => {
    if (!previousTeamId || !nextTeamId) {
      toast.error('Please select both previous and current workspaces.');
      return;
    }
    if (previousTeamId === nextTeamId) {
      toast.error('Previous and current workspace must be different.');
      return;
    }

    setLinking(true);
    try {
      await teamWorkspaceApi.linkWorkspaces({ previousTeamId, nextTeamId });
      toast.success('Workspace continuity linked successfully.');
      setLinkModalOpen(false);
      await loadTeams();
    } catch (e) {
      toast.error(e?.response?.data?.message || e?.message || 'Failed to link workspaces');
    } finally {
      setLinking(false);
    }
  };

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return teams;
    return teams.filter((t) => {
      const cls = t.class?.classCode || '';
      return (
        t.teamName?.toLowerCase().includes(q) ||
        t.teamCode?.toLowerCase().includes(q) ||
        t.startupName?.toLowerCase().includes(q) ||
        cls.toLowerCase().includes(q)
      );
    });
  }, [teams, search]);

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh]">
        <Loader2 className="w-9 h-9 text-primary animate-spin" />
        <p className="text-sm text-slate-500 mt-3 font-medium">Loading startup workspaces…</p>
      </div>
    );
  }

  return (
    <div className="space-y-6 max-w-6xl mx-auto">
      <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-4">
        <div>
          <div className="flex items-center gap-3">
            <div className="w-11 h-11 rounded-2xl bg-gradient-to-br from-primary to-secondary flex items-center justify-center text-white shadow-sm">
              <Kanban className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-2xl font-bold text-slate-900">Startup Workspace</h1>
              <p className="text-sm text-slate-500 mt-0.5">
                {roleHint[role] || 'Teams you can access'}
              </p>
            </div>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {canLinkContinuity && teams.length >= 2 && (
            <Button variant="outline" size="sm" icon={Link2} onClick={openLinkModal}>
              Link Continuity
            </Button>
          )}
          <p className="text-sm font-semibold text-slate-600 bg-white border border-slate-200/80 px-4 py-2 rounded-xl shadow-sm">
            {teams.length} team{teams.length !== 1 ? 's' : ''}
          </p>
        </div>
      </div>

      <div className="relative max-w-md">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
        <input
          type="search"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search team, class code, startup name…"
          className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-slate-200 bg-white text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
        />
      </div>

      {filtered.length === 0 ? (
        <EmptyState
          icon={Kanban}
          title={teams.length === 0 ? 'No teams available' : 'No matches'}
          description={
            teams.length === 0
              ? 'You are not assigned to any class teams yet, or teams have not been created.'
              : 'Try a different search term.'
          }
        />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {filtered.map((team) => (
            <button
              key={team._id}
              type="button"
              onClick={() => navigate(`/workspace/teams/${team._id}`)}
              className="group text-left bg-white rounded-2xl border border-slate-200/80 p-5 shadow-sm hover:shadow-md hover:border-primary/30 transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0 flex-1">
                <span className="text-[10px] font-bold uppercase tracking-wider text-primary bg-primary-50 px-2 py-0.5 rounded-md">
                  {team.class?.classCode || 'Class'}
                </span>
                {team.isArchived && (
                  <span className="ml-2 inline-flex items-center gap-1 text-[10px] font-bold uppercase tracking-wider text-amber-700 bg-amber-50 px-2 py-0.5 rounded-md border border-amber-100">
                    <Archive className="w-3 h-3" /> Archived
                  </span>
                )}
                  <h2 className="text-lg font-bold text-slate-900 mt-2 group-hover:text-primary transition-colors truncate">
                    {team.teamName}
                  </h2>
                  <p className="text-xs text-slate-400 font-mono mt-0.5">{team.teamCode}</p>
                  {team.startupName && (
                    <p className="text-sm text-slate-600 mt-2 truncate">{team.startupName}</p>
                  )}
                </div>
                <ChevronRight className="w-5 h-5 text-slate-300 group-hover:text-primary shrink-0 mt-1" />
              </div>

              <div className="flex flex-wrap gap-2 mt-4 pt-4 border-t border-slate-100">
                <span className="inline-flex items-center gap-1 text-[10px] font-semibold px-2 py-1 rounded-lg bg-slate-50 text-slate-600 border border-slate-100">
                  <Users className="w-3 h-3" /> {team.memberCount} members
                </span>
                {team.proposalStatus && (
                  <span className="inline-flex items-center gap-1 text-[10px] font-semibold px-2 py-1 rounded-lg bg-blue-50 text-blue-700 border border-blue-100">
                    <FileText className="w-3 h-3" /> {team.proposalStatus}
                  </span>
                )}
                <span className="inline-flex items-center gap-1 text-[10px] font-semibold px-2 py-1 rounded-lg bg-orange-50 text-orange-700 border border-orange-100">
                  <Flag className="w-3 h-3" /> {team.checkpointFileCount} checkpoint file{team.checkpointFileCount !== 1 ? 's' : ''}
                </span>
              </div>

              {(team.lecturer || team.mentor) && (
                <div className="mt-3 flex flex-wrap gap-3 text-[10px] text-slate-400">
                  {team.lecturer && (
                    <span className="flex items-center gap-1">
                      <GraduationCap className="w-3 h-3" /> {team.lecturer.name}
                    </span>
                  )}
                  {team.mentor && (
                    <span className="flex items-center gap-1">
                      <Award className="w-3 h-3" /> {team.mentor.name}
                    </span>
                  )}
                </div>
              )}
            </button>
          ))}
        </div>
      )}

      <p className="text-xs text-slate-400 text-center pb-4">
        View-only for your role: you can review proposals, pitch decks, and checkpoint submissions. Upload is reserved for students.
      </p>

      <Modal
        isOpen={linkModalOpen}
        onClose={() => !linking && setLinkModalOpen(false)}
        title="Link Startup Continuity"
        submitText="Link Workspaces"
        isSubmitting={linking}
        onSubmit={handleLinkWorkspaces}
      >
        <div className="space-y-4">
          <p className="text-sm text-slate-500">
            Link an earlier course workspace to the current startup workspace. The previous workspace will become archived/read-only.
          </p>

          <div>
            <label className="block text-xs font-bold uppercase tracking-wide text-slate-500 mb-1.5">
              Previous workspace
            </label>
            <select
              value={previousTeamId}
              onChange={(event) => setPreviousTeamId(event.target.value)}
              className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm font-medium text-slate-700 outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            >
              <option value="">Select previous team</option>
              {teams.map((team) => (
                <option key={team._id} value={team._id}>
                  {teamLabel(team)}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-xs font-bold uppercase tracking-wide text-slate-500 mb-1.5">
              Current workspace
            </label>
            <select
              value={nextTeamId}
              onChange={(event) => setNextTeamId(event.target.value)}
              className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm font-medium text-slate-700 outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            >
              <option value="">Select current team</option>
              {teams.map((team) => (
                <option key={team._id} value={team._id} disabled={team._id === previousTeamId}>
                  {teamLabel(team)}
                </option>
              ))}
            </select>
          </div>

          <div className="rounded-xl border border-amber-100 bg-amber-50 px-3 py-2 text-xs font-medium text-amber-800">
            After linking, shared shortcuts, pitch deck history, and proposal version history can be viewed across the startup lineage.
          </div>
        </div>
      </Modal>
    </div>
  );
}
