// Hub: list teams the user can open in Startup Workspace (Admin / Lecturer / Mentor)
import { useCallback, useEffect, useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Kanban, Search, Loader2, ChevronRight, FolderKanban, Archive,
  Eye, Pencil,
} from 'lucide-react';
import { workspaceApi } from '../../api/workspaceApi';
import { useAuth } from '../../hooks/useAuth';
import EmptyState from '../../components/ui/EmptyState';
import type { WorkspaceOption } from '../../types/workspaceTools';
import { parseApiError } from '../../utils/apiError';
import { groupWorkspacesByClass, normalizeAccessibleWorkspaces } from '../../utils/workspaceHub';

const roleHint = {
  ADMIN: 'All startup teams in the system',
  LECTURER: 'Teams in classes you teach',
  MENTOR: 'Teams you mentor',
};

type WorkspaceHubRole = keyof typeof roleHint;

export default function StartupWorkspaceHub() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [teams, setTeams] = useState<WorkspaceOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState('');
  const [search, setSearch] = useState('');

  const role = (user?.role || 'STUDENT').toUpperCase() as WorkspaceHubRole;

  const loadTeams = useCallback(async () => {
    try {
      setLoading(true);
      setErrorMessage('');
      const response = await workspaceApi.getAccessibleTeams();
      if (!response.success) {
        setTeams([]);
        setErrorMessage(response.message || 'Failed to load startup workspaces.');
        return;
      }

      setTeams(normalizeAccessibleWorkspaces(response));
    } catch (error: unknown) {
      setTeams([]);
      setErrorMessage(parseApiError(error, 'Failed to load startup workspaces.').message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect -- fetch remote workspaces when the hub mounts
    void loadTeams();
  }, [loadTeams]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return teams;
    return teams.filter((t) => {
      return (
        t.teamName?.toLowerCase().includes(q) ||
        t.classCode?.toLowerCase().includes(q) ||
        t.courseCode?.toLowerCase().includes(q) ||
        t.semester?.toLowerCase().includes(q)
      );
    });
  }, [teams, search]);

  const workspaceGroups = useMemo(() => groupWorkspacesByClass(filtered), [filtered]);

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh]">
        <Loader2 className="w-9 h-9 text-primary animate-spin" />
        <p className="text-sm text-slate-500 mt-3 font-medium">Loading startup workspaces…</p>
      </div>
    );
  }

  if (errorMessage) {
    return (
      <EmptyState
        icon={Kanban}
        title="Unable to load startup workspaces"
        description={errorMessage}
        action={{ label: 'Try again', onClick: () => void loadTeams() }}
      />
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
          placeholder="Search team, class, course, or semester…"
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
        <div className="space-y-8">
          {workspaceGroups.map(group => (
            <section key={group.classId} aria-labelledby={`workspace-class-${group.classId}`}>
              <div className="mb-4 flex items-center gap-3">
                <div className="min-w-0">
                  <h2 id={`workspace-class-${group.classId}`} className="text-lg font-bold text-slate-900">
                    {group.classCode || 'Unknown Class'}
                  </h2>
                  <p className="text-xs font-medium text-slate-500">
                    {[group.courseCode, group.semester].filter(Boolean).join(' · ') || 'Academic context unavailable'}
                  </p>
                </div>
                <span className="h-px flex-1 bg-slate-200" />
                <span className="shrink-0 rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-500">
                  {group.workspaces.length} team{group.workspaces.length !== 1 ? 's' : ''}
                </span>
              </div>

              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                {group.workspaces.map(team => (
                  <button
                    key={team.teamId}
                    type="button"
                    onClick={() => navigate(`/workspace/teams/${team.teamId}`)}
                    className="group text-left bg-white rounded-2xl border border-slate-200/80 p-5 shadow-sm hover:shadow-md hover:border-primary/30 transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          {team.isCurrent && (
                            <span className="rounded-md border border-emerald-100 bg-emerald-50 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-emerald-700">
                              Current
                            </span>
                          )}
                          {team.isArchived && (
                            <span className="inline-flex items-center gap-1 rounded-md border border-amber-100 bg-amber-50 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-amber-700">
                              <Archive className="w-3 h-3" /> Archived
                            </span>
                          )}
                        </div>
                        <h3 className="mt-2 truncate text-lg font-bold text-slate-900 transition-colors group-hover:text-primary">
                          {team.teamName}
                        </h3>
                      </div>
                      <ChevronRight className="w-5 h-5 text-slate-300 group-hover:text-primary shrink-0 mt-1" />
                    </div>

                    <div className="flex flex-wrap gap-2 mt-4 pt-4 border-t border-slate-100">
                      <span className="inline-flex items-center gap-1 text-[10px] font-semibold px-2 py-1 rounded-lg bg-slate-50 text-slate-600 border border-slate-100">
                        <FolderKanban className="w-3 h-3" /> {team.hasWorkspace ? 'Workspace created' : 'Not created yet'}
                      </span>
                      <span className={`inline-flex items-center gap-1 text-[10px] font-semibold px-2 py-1 rounded-lg border ${
                        team.accessMode === 'READ_ONLY'
                          ? 'bg-amber-50 text-amber-700 border-amber-100'
                          : 'bg-emerald-50 text-emerald-700 border-emerald-100'
                      }`}>
                        {team.accessMode === 'READ_ONLY' ? <Eye className="w-3 h-3" /> : <Pencil className="w-3 h-3" />}
                        {team.accessMode === 'READ_ONLY' ? 'Read-only' : 'Available'}
                      </span>
                    </div>
                  </button>
                ))}
              </div>
            </section>
          ))}
        </div>
      )}

      <p className="text-xs text-slate-400 text-center pb-4">
        Open a team to review its project profile, roadmap, shortcuts, and checkpoint submissions.
      </p>
    </div>
  );
}
