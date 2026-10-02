import { useCallback, useDeferredValue, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import { Award, CheckCircle2, Filter, Loader2, RefreshCw, Search, Trophy, Users } from 'lucide-react';
import { rankingApi } from '../../api/rankingApi';
import RankingTable from '../../components/workspace/RankingTable';
import EmptyState from '../../components/ui/EmptyState';
import type {
  TeamRankingItem,
  TeamRankingList,
  TeamRankingScoreScope,
  TeamRankingStatus,
} from '../../types/rankings';
import { filterTeamRankingRows, rankTeamResults } from '../../utils/teamRankings';
import { parseApiError } from '../../utils/apiError';

const emptyData: TeamRankingList = {
  activeSemester: null,
  selectedSemester: null,
  availableSemesters: [],
  items: [],
};

const statusOptions: Array<{ value: '' | TeamRankingStatus; label: string }> = [
  { value: '', label: 'All statuses' },
  { value: 'PUBLISHED', label: 'Published' },
  { value: 'READY_TO_PUBLISH', label: 'Ready to publish' },
  { value: 'INCOMPLETE', label: 'In progress' },
];

export default function Rankings() {
  const initialLoadStartedRef = useRef(false);
  const requestIdRef = useRef(0);
  const [data, setData] = useState<TeamRankingList>(emptyData);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState('');
  const [search, setSearch] = useState('');
  const [appliedSearch, setAppliedSearch] = useState('');
  const [semester, setSemester] = useState('');
  const [year, setYear] = useState('');
  const [classId, setClassId] = useState('');
  const [teamId, setTeamId] = useState('');
  const [scoreScope, setScoreScope] = useState<TeamRankingScoreScope>('course');
  const [status, setStatus] = useState('');

  const load = useCallback(async (scope?: { semester?: string; year?: number }) => {
    const requestId = ++requestIdRef.current;
    try {
      setLoading(true);
      const response = await rankingApi.getTeams(scope);
      if (!response.success || !response.data) throw new Error(response.message || 'Unable to load team rankings.');
      if (requestId !== requestIdRef.current) return;
      setErrorMessage('');
      setData(response.data);
      setSemester(response.data.selectedSemester?.semester || '');
      setYear(response.data.selectedSemester ? String(response.data.selectedSemester.year) : '');
    } catch (error: unknown) {
      if (requestId !== requestIdRef.current) return;
      const message = parseApiError(error, 'Unable to load team rankings.').message;
      setErrorMessage(message);
      toast.error(message, { id: 'team-rankings-load-error' });
    } finally {
      if (requestId === requestIdRef.current) setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (initialLoadStartedRef.current) return;
    initialLoadStartedRef.current = true;
    void load();
  }, [load]);

  const classes = useMemo(() => {
    const unique = new Map<string, TeamRankingItem>();
    data.items.forEach(item => unique.set(item.classId, item));
    return [...unique.values()].sort((left, right) => left.classCode.localeCompare(right.classCode, undefined, { numeric: true }));
  }, [data.items]);
  const teams = useMemo(() => data.items
    .filter(item => !classId || item.classId === classId)
    .sort((left, right) => left.teamName.localeCompare(right.teamName, undefined, { numeric: true })), [classId, data.items]);
  const checkpoints = useMemo(() => {
    const unique = new Map<number, { number: number; title: string }>();
    data.items.filter(item => !classId || item.classId === classId).forEach(item => {
      item.checkpoints.forEach(checkpoint => {
        if (!unique.has(checkpoint.number)) unique.set(checkpoint.number, { number: checkpoint.number, title: checkpoint.title });
      });
    });
    return [...unique.values()].sort((left, right) => left.number - right.number);
  }, [classId, data.items]);
  const semesters = useMemo(() => [...new Set(data.availableSemesters.map(item => item.semester))].sort(), [data.availableSemesters]);
  const years = useMemo(() => [...new Set(data.availableSemesters.map(item => item.year))].sort((left, right) => right - left), [data.availableSemesters]);

  const rankedRows = useMemo(() => rankTeamResults(data.items, scoreScope, classId), [classId, data.items, scoreScope]);
  const deferredFilters = useDeferredValue({ search: appliedSearch, teamId, status });
  const rows = useMemo(() => filterTeamRankingRows(rankedRows, deferredFilters), [deferredFilters, rankedRows]);
  const rankedRowsInView = rows.filter(item => item.rank !== null && item.rankingScore !== null);
  const highestScore = rankedRowsInView.length ? Math.max(...rankedRowsInView.map(item => Number(item.rankingScore))) : null;
  const averageScore = rankedRowsInView.length
    ? rankedRowsInView.reduce((sum, item) => sum + Number(item.rankingScore), 0) / rankedRowsInView.length
    : null;

  const selectAcademicScope = (nextSemester: string, nextYear: string) => {
    const matching = data.availableSemesters.find(item =>
      item.semester === nextSemester && String(item.year) === nextYear)
      || data.availableSemesters.find(item => item.semester === nextSemester)
      || data.availableSemesters.find(item => String(item.year) === nextYear);
    if (!matching) return;
    setSemester(matching.semester);
    setYear(String(matching.year));
    setClassId('');
    setTeamId('');
    setScoreScope('course');
    void load({ semester: matching.semester, year: matching.year });
  };
  const reset = () => {
    setSearch('');
    setAppliedSearch('');
    setClassId('');
    setTeamId('');
    setScoreScope('course');
    setStatus('');
    const active = data.activeSemester;
    setSemester(active?.semester || '');
    setYear(active ? String(active.year) : '');
    void load(active ? { semester: active.semester, year: active.year } : undefined);
  };

  if (loading && data.items.length === 0) {
    return <div className="flex min-h-72 flex-col items-center justify-center rounded-2xl border border-slate-200 bg-white"><Loader2 className="h-8 w-8 animate-spin text-primary" /><p className="mt-3 text-sm font-medium text-slate-500">Loading team rankings…</p></div>;
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-end">
        <div>
          <h2 className="flex items-center gap-2 text-xl font-black text-slate-900"><Trophy className="h-5 w-5 text-amber-500" /> Team Rankings</h2>
          <p className="mt-1 text-sm text-slate-500">Rankings use checkpoint Team Scores only. Assessments and individual member scores are not included; missing checkpoints contribute 0.</p>
        </div>
        <button type="button" onClick={() => void load(semester && year ? { semester, year: Number(year) } : undefined)} disabled={loading} className="inline-flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-600 shadow-sm hover:border-primary/30 hover:text-primary disabled:opacity-60"><RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} /> Refresh</button>
      </div>

      <section className="grid grid-cols-2 gap-3 lg:grid-cols-4" aria-label="Ranking summary">
        {[
          { label: 'Teams in view', value: rows.length, icon: Users, color: 'border-blue-100 bg-blue-50 text-blue-600' },
          { label: 'Ranked teams', value: rankedRowsInView.length, icon: CheckCircle2, color: 'border-emerald-100 bg-emerald-50 text-emerald-600' },
          { label: 'Highest score', value: highestScore === null ? '—' : highestScore.toFixed(2), icon: Trophy, color: 'border-amber-100 bg-amber-50 text-amber-600' },
          { label: 'Average score', value: averageScore === null ? '—' : averageScore.toFixed(2), icon: Award, color: 'border-orange-100 bg-orange-50 text-primary' },
        ].map(item => {
          const Icon = item.icon;
          return <div key={item.label} className="rounded-2xl border border-slate-200/70 bg-white p-4 shadow-sm sm:p-5"><div className={`mb-3 flex h-9 w-9 items-center justify-center rounded-xl border ${item.color}`}><Icon className="h-4 w-4" /></div><p className="text-[11px] font-bold uppercase tracking-wider text-slate-400">{item.label}</p><p className="mt-1 text-2xl font-black text-slate-900">{item.value}</p></div>;
        })}
      </section>

      <section className="rounded-2xl border border-slate-200/70 bg-white p-4 shadow-sm" aria-label="Ranking filters">
        <form onSubmit={event => { event.preventDefault(); setAppliedSearch(search.trim()); }} className="flex flex-wrap items-center gap-3">
          <div className="relative min-w-[220px] flex-1"><Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" /><input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Search project name, description, team, or class…" aria-label="Search team rankings" className="w-full rounded-xl border border-slate-200 py-2 pl-9 pr-4 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" /></div>
          <select value={semester} onChange={event => selectAcademicScope(event.target.value, year)} aria-label="Filter rankings by semester" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 sm:w-[130px]">{semesters.map(value => <option key={value} value={value}>{value}</option>)}</select>
          <select value={year} onChange={event => selectAcademicScope(semester, event.target.value)} aria-label="Filter rankings by year" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 sm:w-[120px]">{years.map(value => <option key={value} value={value}>{value}</option>)}</select>
          <select value={classId} onChange={event => { setClassId(event.target.value); setTeamId(''); setScoreScope('course'); }} aria-label="Filter rankings by class" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 sm:w-[160px]"><option value="">All classes</option>{classes.map(item => <option key={item.classId} value={item.classId}>{item.classCode}</option>)}</select>
          <select value={teamId} onChange={event => setTeamId(event.target.value)} aria-label="Filter rankings by team" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 sm:w-[180px]"><option value="">All teams</option>{teams.map(item => <option key={item.teamId} value={item.teamId}>{item.teamName}</option>)}</select>
          <select value={scoreScope} onChange={event => setScoreScope(event.target.value as TeamRankingScoreScope)} aria-label="Select ranking score scope" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 sm:w-[210px]"><option value="course">Checkpoint Total</option>{checkpoints.map(item => <option key={item.number} value={`checkpoint:${item.number}`}>Checkpoint {item.number} · {item.title}</option>)}</select>
          <select value={status} onChange={event => setStatus(event.target.value)} aria-label="Filter rankings by status" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 sm:w-[170px]">{statusOptions.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select>
          <button type="submit" className="inline-flex items-center gap-2 rounded-xl bg-secondary px-4 py-2 text-sm font-semibold text-white hover:bg-secondary-700"><Filter className="h-4 w-4" /> Search</button>
          <button type="button" onClick={reset} className="px-2 text-sm font-medium text-slate-400 hover:text-slate-700">Reset</button>
        </form>
      </section>

      {errorMessage && <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert"><span>{errorMessage}</span><button type="button" onClick={() => void load(semester && year ? { semester, year: Number(year) } : undefined)} className="font-bold hover:underline">Retry</button></div>}

      {data.selectedSemester === null ? (
        <div className="rounded-2xl border border-slate-200/70 bg-white shadow-sm"><EmptyState icon={Trophy} title="No ranking semester available" description="No accessible teams are available in an active or historical semester." /></div>
      ) : (
        <RankingTable rankings={rows} checkpoints={checkpoints} scoreScope={scoreScope} />
      )}
    </div>
  );
}
