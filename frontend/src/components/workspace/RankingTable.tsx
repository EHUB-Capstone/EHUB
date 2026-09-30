import { Award, CheckCircle2, Clock3, Trophy } from 'lucide-react';
import type {
  TeamRankingCheckpoint,
  TeamRankingScoreScope,
  TeamRankingStatus,
  TeamRankingViewItem,
} from '../../types/rankings';

interface RankingTableProps {
  rankings: TeamRankingViewItem[];
  checkpoints: Array<Pick<TeamRankingCheckpoint, 'number' | 'title'>>;
  scoreScope: TeamRankingScoreScope;
}

const statusStyle: Record<TeamRankingStatus, string> = {
  PUBLISHED: 'border-emerald-200 bg-emerald-50 text-emerald-700',
  READY_TO_PUBLISH: 'border-amber-200 bg-amber-50 text-amber-700',
  INCOMPLETE: 'border-slate-200 bg-slate-50 text-slate-500',
};

const statusLabel: Record<TeamRankingStatus, string> = {
  PUBLISHED: 'Published',
  READY_TO_PUBLISH: 'Ready to publish',
  INCOMPLETE: 'In progress',
};

function scoreText(score: number | null | undefined): string {
  return score === null || score === undefined ? '—' : Number(score).toFixed(2);
}

function RankBadge({ rank }: { rank: number | null }) {
  if (rank === 1) return <span className="inline-flex h-8 w-8 items-center justify-center rounded-xl bg-amber-100 text-lg">🥇</span>;
  if (rank === 2) return <span className="inline-flex h-8 w-8 items-center justify-center rounded-xl bg-slate-200 text-lg">🥈</span>;
  if (rank === 3) return <span className="inline-flex h-8 w-8 items-center justify-center rounded-xl bg-orange-100 text-lg">🥉</span>;
  return <span className="inline-flex h-8 min-w-8 items-center justify-center rounded-xl bg-slate-100 px-2 text-xs font-black text-slate-600">{rank ?? '—'}</span>;
}

export default function RankingTable({ rankings, checkpoints, scoreScope }: RankingTableProps) {
  if (rankings.length === 0) {
    return (
      <div className="rounded-2xl border border-slate-200/70 bg-white p-12 text-center shadow-sm">
        <Trophy className="mx-auto h-12 w-12 text-slate-200" />
        <h3 className="mt-3 text-sm font-bold text-slate-700">No matching team rankings</h3>
        <p className="mt-1 text-xs text-slate-400">Try another search term or filter.</p>
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-2xl border border-slate-200/70 bg-white shadow-sm">
      <div className="overflow-x-auto">
        <table className="min-w-max border-separate border-spacing-0 text-left text-sm">
          <thead>
            <tr className="text-[10px] font-black uppercase tracking-wider text-slate-500">
              <th className="sticky left-0 z-30 w-20 border-b border-r border-slate-200 bg-slate-50 px-4 py-4 text-center">Rank</th>
              <th className="sticky left-20 z-30 w-[320px] min-w-[320px] border-b border-r border-slate-200 bg-slate-50 px-5 py-4">Team</th>
              <th className="min-w-[140px] border-b border-r border-slate-200 bg-slate-50 px-4 py-4">Class</th>
              {checkpoints.map(checkpoint => {
                const selected = scoreScope === `checkpoint:${checkpoint.number}`;
                return (
                  <th key={checkpoint.number} className={`min-w-[150px] border-b border-r border-slate-200 px-4 py-4 text-center ${selected ? 'bg-orange-50 text-primary' : 'bg-orange-50/40'}`}>
                    <p>Checkpoint {checkpoint.number}</p>
                    <p className="mt-1 max-w-[145px] truncate text-[9px] font-semibold normal-case tracking-normal text-slate-500" title={checkpoint.title}>{checkpoint.title}</p>
                  </th>
                );
              })}
              <th className={`min-w-[150px] border-b border-r border-slate-200 px-4 py-4 text-center ${scoreScope === 'course' ? 'bg-emerald-100/70 text-emerald-800' : 'bg-emerald-50 text-emerald-700'}`}>Course Total</th>
              <th className="min-w-[150px] border-b border-r border-slate-200 bg-slate-50 px-4 py-4 text-center">Completion</th>
              <th className="min-w-[170px] border-b border-slate-200 bg-slate-50 px-4 py-4 text-center">Ranking status</th>
            </tr>
          </thead>
          <tbody>
            {rankings.map(item => (
              <tr key={item.teamId} className="group">
                <td className="sticky left-0 z-20 border-b border-r border-slate-200 bg-white px-4 py-4 text-center group-hover:bg-slate-50">
                  <div className="flex justify-center"><RankBadge rank={item.rank} /></div>
                </td>
                <td className="sticky left-20 z-20 w-[320px] min-w-[320px] align-top border-b border-r border-slate-200 bg-white px-5 py-4 group-hover:bg-slate-50">
                  <p className="whitespace-normal break-words font-black leading-5 text-slate-900">{item.projectName || 'No project name'}</p>
                  <p className="mt-1.5 whitespace-normal break-words text-xs font-medium leading-5 text-slate-500">{item.projectDescription || 'No project description'}</p>
                </td>
                <td className="border-b border-r border-slate-200 px-4 py-4">
                  <p className="font-bold text-slate-800">{item.classCode}</p>
                  <p className="mt-1 text-xs text-slate-400">{item.courseCode} · {item.semester}</p>
                </td>
                {checkpoints.map(checkpoint => {
                  const result = item.checkpoints.find(entry => entry.number === checkpoint.number);
                  const selected = scoreScope === `checkpoint:${checkpoint.number}`;
                  return (
                    <td key={checkpoint.number} className={`border-b border-r border-slate-200 px-4 py-4 text-center ${selected ? 'bg-orange-50/40' : ''}`}>
                      <p className={`text-base font-black ${result?.score === null || result?.score === undefined ? 'text-slate-300' : result.status === 'PUBLISHED' ? 'text-slate-900' : 'text-amber-700'}`}>{scoreText(result?.score)}</p>
                      {result?.status === 'SUBMITTED' && <p className="mt-1 text-[9px] font-bold uppercase text-amber-600">Not published</p>}
                      {result?.status === 'PUBLISHED' && <p className="mt-1 text-[9px] font-bold uppercase text-emerald-600">Published</p>}
                    </td>
                  );
                })}
                <td className={`border-b border-r border-slate-200 px-4 py-4 text-center ${scoreScope === 'course' ? 'bg-emerald-50/70' : 'bg-emerald-50/30'}`}>
                  <p className={`text-lg font-black ${item.courseTotal === null ? 'text-slate-300' : 'text-emerald-700'}`}>{scoreText(item.courseTotal)}</p>
                  <p className="mt-1 text-[9px] font-bold uppercase text-slate-400">out of 10</p>
                </td>
                <td className="border-b border-r border-slate-200 px-4 py-4 text-center">
                  <div className="flex items-center justify-center gap-1.5 font-bold text-slate-700">
                    {item.completedComponentCount === item.totalComponentCount && item.totalComponentCount > 0
                      ? <CheckCircle2 className="h-4 w-4 text-emerald-500" />
                      : <Clock3 className="h-4 w-4 text-amber-500" />}
                    {item.completedComponentCount}/{item.totalComponentCount}
                  </div>
                  <p className="mt-1 text-[9px] font-semibold uppercase text-slate-400">components graded</p>
                </td>
                <td className="border-b border-slate-200 px-4 py-4 text-center">
                  <span className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[10px] font-bold uppercase ${statusStyle[item.rankingStatus]}`}>
                    {item.rankingStatus === 'PUBLISHED' && <Award className="h-3 w-3" />}
                    {statusLabel[item.rankingStatus]}
                  </span>
                  {item.rank === null && <p className="mt-2 text-[10px] text-slate-400">No graded score</p>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
