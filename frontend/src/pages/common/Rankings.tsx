import { useEffect, useMemo, useState } from 'react';
import { Loader2, RefreshCw, Trophy } from 'lucide-react';
import { rankingApi } from '../../api/rankingApi';
import RankingTable from '../../components/workspace/RankingTable';
import type { TeamRankingItem, TeamRankingList } from '../../types/rankings';
import { filterTeamRankingRows, rankTeamResults } from '../../utils/teamRankings';
import { parseApiError } from '../../utils/apiError';

export default function Rankings() {
  const [data, setData] = useState<TeamRankingList | null>(null);
  const [options, setOptions] = useState<TeamRankingItem[]>([]);
  const [semesterCode, setSemesterCode] = useState('');
  const [classId, setClassId] = useState('');
  const [checkpointNumber, setCheckpointNumber] = useState('');
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [refresh, setRefresh] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    rankingApi.getTeams({ semester: semesterCode || undefined, classId: classId || undefined,
      checkpointNumber: checkpointNumber ? Number(checkpointNumber) : undefined }, controller.signal)
      .then(response => {
        if (controller.signal.aborted) return;
        if (!response.success) throw new Error(response.message || 'Unable to load rankings.');
        setData(response.data);
        if (!classId) setOptions(response.data.items);
      }).catch((reason: unknown) => {
        if (!controller.signal.aborted) setError(parseApiError(reason, 'Unable to load rankings.').message);
      }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [semesterCode, classId, checkpointNumber, refresh]);

  const classes = useMemo(() => [...new Map(options.map(item => [item.classId, item])).values()], [options]);
  const checkpoints = useMemo(() => [...new Map(options.filter(item => !classId || item.classId === classId)
    .flatMap(item => item.checkpoints.map(checkpoint => [checkpoint.number, checkpoint] as const))).values()]
    .sort((left, right) => left.number - right.number), [options, classId]);
  const rows = useMemo(() => filterTeamRankingRows(rankTeamResults(data?.items || []),
    { search, teamId: '', status: '' }), [data, search]);
  return <div className="space-y-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div><h2 className="flex items-center gap-2 text-xl font-black text-slate-900"><Trophy className="h-5 w-5 text-amber-500" /> Team Rankings</h2>
        <p className="mt-1 text-sm text-slate-500">Rankings use published checkpoints within each subject. Teams awaiting publication remain unranked.</p></div>
      <button type="button" disabled={loading} onClick={() => setRefresh(value => value + 1)} className="inline-flex items-center gap-2 rounded-xl border px-4 py-2 text-sm disabled:opacity-50"><RefreshCw className="h-4 w-4" /> Refresh</button>
    </div>
    <div className="flex flex-wrap gap-3 rounded-2xl border border-slate-200 bg-white p-4">
      <input type="search" aria-label="Search rankings" placeholder="Search team, project or class…" value={search} onChange={event => setSearch(event.target.value)} className="min-w-48 flex-1 rounded-xl border border-slate-200 px-3 py-2" />
      <select aria-label="Ranking semester" value={semesterCode || data?.selectedSemester?.code || ''} onChange={event => { setSemesterCode(event.target.value); setClassId(''); setCheckpointNumber(''); }} className="rounded-xl border border-slate-200 px-3 py-2">
        {data?.availableSemesters.map(item => <option key={item.id} value={item.code}>{item.code}</option>)}
      </select>
      <select aria-label="Ranking class" value={classId} onChange={event => { setClassId(event.target.value); setCheckpointNumber(''); }} className="rounded-xl border border-slate-200 px-3 py-2">
        <option value="">All assigned classes</option>{classes.map(item => <option key={item.classId} value={item.classId}>{item.classCode}</option>)}
      </select>
      <select aria-label="Ranking checkpoint" value={checkpointNumber} onChange={event => setCheckpointNumber(event.target.value)} className="rounded-xl border border-slate-200 px-3 py-2">
        <option value="">Checkpoint total</option>{checkpoints.map(item => <option key={item.number} value={item.number}>Checkpoint {item.number}</option>)}
      </select>
    </div>
    {loading ? <div className="flex items-center justify-center gap-2 p-12" role="status"><Loader2 className="h-6 w-6 animate-spin" /> Loading rankings…</div>
      : error ? <div role="alert" className="rounded-xl bg-red-50 p-4 text-red-700">{error}<button type="button" onClick={() => setRefresh(value => value + 1)} className="ml-3 font-bold underline">Retry</button></div>
        : <RankingTable rankings={rows} />}
  </div>;
}
