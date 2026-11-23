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
import { filterWorkspaces, groupWorkspacesByClass, latestWorkspaceSemester, normalizeAccessibleWorkspaces, parseWorkspaceSemester, resolveWorkspaceSemesterScope } from '../../utils/workspaceHub';
import MentorHistorySection from '../../components/workspace/MentorHistorySection';
import type { MentorHistoryItem } from '../../types/teamManagement';
import { useTranslation } from 'react-i18next';
import i18n from '../../i18n';

const roleHint = {
  get ADMIN() { return i18n.t('workspace:startupWorkspaceHub.roleHint.ADMIN'); },
  get LECTURER() { return i18n.t('workspace:startupWorkspaceHub.roleHint.LECTURER'); },
  get MENTOR() { return i18n.t('workspace:startupWorkspaceHub.roleHint.MENTOR'); },
};

type WorkspaceHubRole = keyof typeof roleHint;

export default function StartupWorkspaceHub() {
  const { t } = useTranslation('workspace');
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { user } = useAuth();
  const [teams, setTeams] = useState<WorkspaceOption[]>([]);
  const [activeSemester, setActiveSemester] = useState<{ semester: string; year: number } | null>(null);
  const [history, setHistory] = useState<MentorHistoryItem[]>([]);
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
  const fallbackSemester = useMemo(() => latestWorkspaceSemester(teams), [teams]);
  const semesterScope = resolveWorkspaceSemesterScope(searchParams, activeSemester, fallbackSemester);
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
        setErrorMessage(response.message || t('startupWorkspaceHub.failedToLoadStartupWorkspaces'));
        return;
      }
      if (!semesterResponse.success) {
        setTeams([]);
        setErrorMessage(semesterResponse.message || t('startupWorkspaceHub.failedToLoadTheActiveSemester'));
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

  // The previous-teams list is a convenience for mentors: if it cannot load, the hub still works without it.
  const isMentor = (user?.role || '').toUpperCase() === 'MENTOR';
  useEffect(() => {
    if (!isMentor) return undefined;
    let cancelled = false;
    workspaceApi.getMyMentorHistory()
      .then(response => { if (!cancelled && response.success) setHistory(response.data ?? []); })
      .catch(() => { if (!cancelled) setHistory([]); });
    return () => { cancelled = true; };
  }, [isMentor]);

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
        <p className="text-sm text-slate-500 mt-3 font-medium">{t('startupWorkspaceHub.loadingStartupWorkspaces')}</p>
      </div>
    );
  }

  if (errorMessage) {
    return (
      <EmptyState
        icon={Kanban}
        title={t('startupWorkspaceHub.unableToLoadStartupWorkspaces')}
        description={errorMessage}
        action={{ label: t('startupWorkspaceHub.tryAgain'), onClick: () => void loadTeams() }}
      />
    );
  }

  return (
    <div className="space-y-6 max-w-6xl mx-auto">
      <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-4">
        <div>
          <div className="flex items-center gap-3">
            <div className="w-11 h-11 rounded-2xl flex items-center justify-center text-white shadow-sm bg-primary">
              <Kanban className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-heading sm:text-display text-secondary tracking-tight">{t('startupWorkspaceHub.startupWorkspace')}</h1>
              <p className="text-sm text-slate-500 mt-0.5">
                {roleHint[role] || t('startupWorkspaceHub.teamsYouCanAccess')}
              </p>
            </div>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <p className="text-sm font-semibold text-slate-600 bg-white border border-slate-200/80 px-4 py-2 rounded-xl shadow-sm">
            {filtered.length === teams.length ? `${teams.length} team${teams.length !== 1 ? 's' : ''}` : t('startupWorkspaceHub.ofTeams', { length: filtered.length, length2: teams.length })}
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
              placeholder={t('startupWorkspaceHub.searchTeamClassCourseOrSemester')}
              aria-label={t('startupWorkspaceHub.searchWorkspaces')}
              className="w-full pl-9 pr-4 py-2 border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>
          <select value={subject} onChange={event => updateFilter('subject', event.target.value)} aria-label={t('startupWorkspaceHub.filterBySubject')} className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary">
            <option value="">{t('startupWorkspaceHub.allSubjects')}</option>
            {subjects.map(code => <option key={code} value={code}>{code}</option>)}
          </select>
          <select value={semester} onChange={event => updateFilter('semester', event.target.value)} aria-label={t('startupWorkspaceHub.filterBySemester')} className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary">
            <option value="all">{t('startupWorkspaceHub.allSemesters')}</option>
            {semester === 'none' && <option value="none" disabled>{t('startupWorkspaceHub.noActiveSemester')}</option>}
            {['SP', 'SU', 'FA'].map(code => <option key={code} value={code}>{code}</option>)}
          </select>
          <select value={year} onChange={event => updateFilter('year', event.target.value)} aria-label={t('startupWorkspaceHub.filterByYear')} className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary">
            <option value="all">{t('startupWorkspaceHub.allYears')}</option>
            {year === 'none' && <option value="none" disabled>{t('startupWorkspaceHub.noActiveYear')}</option>}
            {years.map(value => <option key={value} value={value}>{value}</option>)}
          </select>
          <select value={workspaceStatus} onChange={event => updateFilter('workspaceStatus', event.target.value)} aria-label={t('startupWorkspaceHub.filterByWorkspaceStatus')} className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary">
            <option value="">{t('startupWorkspaceHub.allWorkspaces')}</option>
            <option value="created">{t('startupWorkspaceHub.created')}</option>
            <option value="not-created">{t('startupWorkspaceHub.notCreatedYet')}</option>
          </select>
          <select value={access} onChange={event => updateFilter('access', event.target.value)} aria-label={t('startupWorkspaceHub.filterByAccess')} className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary">
            <option value="">{t('startupWorkspaceHub.allAccess')}</option>
            <option value="READ_WRITE">{t('startupWorkspaceHub.available')}</option>
            <option value="READ_ONLY">{t('startupWorkspaceHub.readOnly')}</option>
          </select>
          <select value={sort} onChange={event => updateFilter('sort', event.target.value)} aria-label={t('startupWorkspaceHub.sortWorkspaces')} className="border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary">
            <option value="class-asc">{t('startupWorkspaceHub.classCodeAZ')}</option>
            <option value="class-desc">{t('startupWorkspaceHub.classCodeZA')}</option>
            <option value="team-asc">{t('startupWorkspaceHub.teamNameAZWithinClass')}</option>
            <option value="team-desc">{t('startupWorkspaceHub.teamNameZAWithinClass')}</option>
          </select>
          <button type="submit" className="flex items-center gap-2 px-4 py-2 bg-secondary text-white rounded-xl text-sm hover:bg-secondary-700 transition-all">
            <Filter className="w-4 h-4" /> {t('startupWorkspaceHub.filter')}
          </button>
          <button type="button" onClick={() => { setSearch(''); setSearchParams(new URLSearchParams(), { replace: true }); }} className="text-sm text-slate-400 hover:text-slate-600 px-2">
            {t('common:actions.reset')}
          </button>
        </form>
      </div>

      {semesterScope.usesFallback && (
        <p className="rounded-xl border border-blue-100 bg-blue-50 px-4 py-2.5 text-xs text-blue-800">
          {t('startupWorkspaceHub.noSemesterIsActiveRightNow', { semester, year })}
        </p>
      )}

      {filtered.length === 0 ? (
        <EmptyState
          icon={Kanban}
          title={semesterScope.isDefault && !activeSemester ? t('startupWorkspaceHub.noActiveSemester') : teams.length === 0 ? t('startupWorkspaceHub.noTeamsAvailable') : t('startupWorkspaceHub.noMatches')}
          description={
            semesterScope.isDefault && !activeSemester
              ? t('startupWorkspaceHub.thereIsNoActiveSemesterSelect')
              : teams.length === 0
              ? t('startupWorkspaceHub.youAreNotAssignedToAny')
              : t('startupWorkspaceHub.tryADifferentSearchOrFilter')
          }
        />
      ) : (
        <div className="space-y-8">
          {workspaceGroups.map(group => (
            <section key={group.classId} aria-labelledby={`workspace-class-${group.classId}`}>
              <div className="mb-4 flex items-center gap-3">
                <div className="min-w-0">
                  <h2 id={`workspace-class-${group.classId}`} className="text-lg font-bold text-slate-900">
                    {group.classCode || t('startupWorkspaceHub.unknownClass')}
                  </h2>
                  <p className="text-xs font-medium text-slate-500">
                    {[group.courseCode, group.semester].filter(Boolean).join(' · ') || t('startupWorkspaceHub.academicContextUnavailable')}
                  </p>
                </div>
                <span className="h-px flex-1 bg-slate-200" />
                <span className="shrink-0 rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-500">
                  {t('startupWorkspaceHub.teamCount', { count: group.workspaces.length })}
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
                            <span className="rounded-md border border-emerald-100 bg-emerald-50 px-2 py-0.5 text-overline font-bold uppercase tracking-wider text-emerald-700">
                              {t('teamLineageHistory.current')}
                            </span>
                          )}
                          {team.isArchived && (
                            <span className="inline-flex items-center gap-1 rounded-md border border-amber-100 bg-amber-50 px-2 py-0.5 text-overline font-bold uppercase tracking-wider text-amber-700">
                              <Archive className="w-3 h-3" /> {t('startupWorkspaceHub.archived')}
                            </span>
                          )}
                        </div>
                        <h3 className="mt-2 truncate text-lg font-bold text-slate-900 transition-colors group-hover:text-primary-700">
                          {team.teamName}
                        </h3>
                      </div>
                      <ChevronRight className="w-5 h-5 text-slate-300 group-hover:text-primary shrink-0 mt-1" />
                    </div>

                    <div className="flex flex-wrap gap-2 mt-4 pt-4 border-t border-slate-100">
                      <span className="inline-flex items-center gap-1 text-overline font-semibold px-2 py-1 rounded-lg bg-slate-50 text-slate-600 border border-slate-100">
                        <FolderKanban className="w-3 h-3" /> {team.hasWorkspace ? t('startupWorkspaceHub.workspaceCreated') : t('startupWorkspaceHub.notCreatedYet')}
                      </span>
                      <span className={`inline-flex items-center gap-1 text-overline font-semibold px-2 py-1 rounded-lg border ${
                        team.accessMode === 'READ_ONLY'
                          ? 'bg-amber-50 text-amber-700 border-amber-100'
                          : 'bg-emerald-50 text-emerald-700 border-emerald-100'
                      }`}>
                        {team.accessMode === 'READ_ONLY' ? <Eye className="w-3 h-3" /> : <Pencil className="w-3 h-3" />}
                        {team.accessMode === 'READ_ONLY' ? t('startupWorkspaceHub.readOnly') : t('startupWorkspaceHub.available')}
                      </span>
                    </div>
                  </button>
                ))}
              </div>
            </section>
          ))}
        </div>
      )}

      {isMentor && <MentorHistorySection items={history} />}

      <p className="text-xs text-slate-400 text-center pb-4">
        {t('startupWorkspaceHub.openATeamToReviewIts')}
      </p>
    </div>
  );
}
