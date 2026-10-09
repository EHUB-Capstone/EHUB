import { Trophy } from 'lucide-react';
import type { TeamRankingViewItem } from '../../types/rankings';

export default function RankingTable({ rankings }: { rankings: TeamRankingViewItem[] }) {
  if (!rankings.length) return <div className="rounded-2xl border border-slate-200 bg-white p-12 text-center">
    <Trophy className="mx-auto h-12 w-12 text-slate-200" />
    <h3 className="mt-3 font-bold text-slate-700">No matching team rankings</h3>
    <p className="mt-1 text-sm text-slate-500">Try another search term or filter.</p>
  </div>;
  return <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
    <table className="w-full text-left text-sm">
      <thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr>
        <th scope="col" className="px-4 py-4">Rank</th>
        <th scope="col" className="px-5 py-4">Team / Project</th>
        <th scope="col" className="px-4 py-4">Class / Subject</th>
        <th scope="col" className="px-4 py-4">Published checkpoints</th>
        <th scope="col" className="px-4 py-4">Status</th>
      </tr></thead>
      <tbody>{rankings.map(item => <tr key={item.teamId} className="border-t border-slate-200">
        <td className="px-4 py-4 font-black">{item.rank ?? '—'}</td>
        <td className="max-w-lg px-5 py-4"><p className="font-bold">{item.teamName}</p>
          <p className="mt-1 font-semibold">{item.projectName || 'No project name'}</p>
          <p className="mt-1 text-xs text-slate-500">{item.projectDescription || 'No project description'}</p></td>
        <td className="px-4 py-4"><p className="font-bold">{item.classCode}</p>
          <p className="mt-1 text-xs text-slate-500">{item.courseCode} · {item.semester}</p></td>
        <td className="px-4 py-4">{item.publishedComponentCount}/{item.totalComponentCount}</td>
        <td className="px-4 py-4 text-xs font-bold text-slate-600">{item.rank === null ? 'Awaiting publication' : 'Published'}</td>
      </tr>)}</tbody>
    </table>
  </div>;
}
