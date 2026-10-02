// Hub: list teams the user can open in Startup Workspace (Admin / Lecturer / Mentor)
import { useCallback, useEffect, useState, useMemo } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Kanban, Search, Filter, Loader2, ChevronRight, FolderKanban, Archive,
  Eye, Pencil,
} from 'lucide-react';
import { workspaceApi } from '../../api/workspaceApi';
import { useAuth } from '../../hooks/useAuth';
import EmptyState from '../../components/ui/EmptyState';
import type { WorkspaceOption } from '../../types/workspaceTools';
import { parseApiError } from '../../utils/apiError';
import { filterWorkspaces, groupWorkspacesByClass, normalizeAccessibleWorkspaces, parseWorkspaceSemester, resolveWorkspaceSemesterScope } from '../../utils/workspaceHub';

const roleHint = {
  ADMIN: 'Startup teams you can access',
  LECTURER: 'Teams in classes you teach',
  MENTOR: 'Teams you mentor',
};

type WorkspaceHubRole = keyof typeof roleHint;

export default function StartupWorkspaceHub() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { user } = useAuth();
  const [teams, setTeams] = useState<WorkspaceOption[]>([]);
  const [activeSemester, setActiveSemester] = useState<{ semester: string; year: number } | null>(null);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState('');
  const appliedSearch = searchParams.get('search') || '';
  const [search, setSearch] = useState(appliedSearch);
  const [previousSearch, setPreviousSearch] = useState(appliedSearch);
  if (previousSearch !== appliedSearch) {
    setPreviousSearch(appliedSearch);
    setSearch(appliedSearch);
  }

  const subject = searchParams.get('subject') || '';
  const semesterScope = resolveWorkspaceSemesterScope(searchParams, activeSemester);
  const { semester, year } = semesterScope;
  const workspaceStatus = searchParams.get('workspaceStatus') || '';
  const access = searchParams.get('access') || '';
  const sort = searchParams.get('sort') || 'class-asc';

  const updateFilter = (key: string, value: string) => {
    const next = new URLSearchParams(searchParams);
    if (semesterScope.isDefault && (key === 'semester' || key === 'year')) {
      next.set('semester', semester === 'none' ? 'all' : semester);
      next.set('year', year === 'none' ? 'all' : year);
    }
    if (value) next.set(key, value);
    else next.delete(key);
    setSearchParams(next, { replace: true });
  };

  const role = (user?.role || 'STUDENT').toUpperCase() as WorkspaceHubRole;

  const loadTeams = useCallback(async () => {
    try {
      setLoading(true);
      setErrorMessage('');
      const [response, semesterResponse] = await Promise.all([
        workspaceApi.getAccessibleTeams(),
        workspaceApi.getActiveSemester(),
      ]);
      if (!response.success) {
        setTeams([]);
        setErrorMessage(response.message || 'Failed to load startup workspaces.');
        return;
      }
      if (!semesterResponse.success) {
        setTeams([]);
        setErrorMessage(semesterResponse.message || 'Failed to load the active semester.');
        return;
      }

      setActiveSemester(semesterResponse.data?.currentSemester ?? null);
      setTeams(normalizeAccessibleWorkspaces(response));
    } catch (error: unknown) {
      setTeams([]);
      setActiveSemester(null);
      setErrorMessage(parseApiError(error, 'Failed to load startup workspaces or the active semester.').message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect -- fetch remote workspaces when the hub mounts
    void loadTeams();
  }, [loadTeams]);

  const subjects = useMemo(() => [...new Set([...teams.map(team => team.courseCode), subject].filter(Boolean))].sort(), [teams, subject]);
  const years = useMemo(() => [...new Set([...teams.map(team => parseWorkspaceSemester(team.semester)?.year), year].filter((value): value is string => typeof value === 'string' && /^\d{4}$/.test(value)))].sort(), [teams, year]);
  const filtered = useMemo(() => filterWorkspaces(teams, {
    search: appliedSearch, subject, semester, year, workspaceStatus, access,
  }), [teams, appliedSearch, subject, semester, year, workspaceStatus, access]);

  const workspaceGroups = useMemo(() => groupWorkspacesByClass(filtered, sort), [filtered, sort]);

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
            {filtered.length === teams.length ? `${teams.length} team${teams.length !== 1 ? 's' : ''}` : `${filtered.length} of ${teams.length} teams`}
          </p>
        </div>
      </div>

      <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-4">
        <form onSubmit={event => { event.preventDefault(); updateFilter('search', search.trim()); }} className="flex flex-wrap gap-3 items-center">
          <div className="relative flex-1 min-w-[200px]">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <input
              type="search"
              value={search}
              onChange={event => setSearch(event.target.value)}
              placeholder="Search team, class, course, or semester…"
              aria-label="Search workspaces"
              className="w-full pl-9 pr-4 py-2 border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>
          <select value={subject} onChange={event => updateFilter('subject', event.target.value)} aria-label="Filter by subject" className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary">
            <option value="">All Subjects</option>
            {subjects.map(code => <option key={code} value={code}>{code}</option>)}
          </select>
          <select value={semester} onChange={event => updateFilter('semester', event.target.value)} aria-label="Filter by semester" className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary">
            <option value="all">All Semesters</option>
            {semester === 'none' && <option value="none" disabled>No active semester</option>}
            {['SP', 'SU', 'FA'].map(code => <option key={code} value={code}>{code}</option>)}
          </select>
          <select value={year} onChange={event => updateFilter('year', event.target.value)} aria-label="Filter by year" className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary">
            <option value="all">All Years</option>
            {year === 'none' && <option value="none" disabled>No active year</option>}
            {years.map(value => <option key={value} value={value}>{value}</option>)}
          </select>
          <select value={workspaceStatus} onChange={event => updateFilter('workspaceStatus', event.target.value)} aria-label="Filter by workspace status" className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary">
            <option value="">All Workspaces</option>
            <option value="created">Created</option>
            <option value="not-created">Not created yet</option>
          </select>
          <select value={access} onChange={event => updateFilter('access', event.target.value)} aria-label="Filter by access" className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary">
            <option value="">All Access</option>
            <option value="READ_WRITE">Available</option>
            <option value="READ_ONLY">Read-only</option>
          </select>
          <select value={sort} onChange={event => updateFilter('sort', event.target.value)} aria-label="Sort workspaces" className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary">
            <option value="class-asc">Class code A–Z</option>
            <option value="class-desc">Class code Z–A</option>
            <option value="team-asc">Team name A–Z (within class)</option>
            <option value="team-desc">Team name Z–A (within class)</option>
          </select>
          <button type="submit" className="flex items-center gap-2 px-4 py-2 bg-secondary text-white rounded-xl text-sm hover:bg-secondary-700 transition-all">
            <Filter className="w-4 h-4" /> Filter
          </button>
          <button type="button" onClick={() => { setSearch(''); setSearchParams(new URLSearchParams(), { replace: true }); }} className="text-sm text-slate-400 hover:text-slate-600 px-2">
            Reset
          </button>
        </form>
      </div>

      {filtered.length === 0 ? (
        <EmptyState
          icon={Kanban}
          title={semesterScope.isDefault && !activeSemester ? 'No active semester' : teams.length === 0 ? 'No teams available' : 'No matches'}
          description={
            semesterScope.isDefault && !activeSemester
              ? 'There is no active semester. Select another semester and year to view teams from other terms.'
              : teams.length === 0
              ? 'You are not assigned to any class teams yet, or teams have not been created.'
              : 'Try a different search or filter, or reset the filters.'
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
