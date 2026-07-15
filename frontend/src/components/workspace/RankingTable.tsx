// @ts-nocheck
import { Trophy, TrendingUp, CheckCircle, Brain, Kanban, FileText, Layout } from 'lucide-react';

const RankingTable = ({ rankings }) => {
  if (!Array.isArray(rankings) || rankings.length === 0) {
    return (
      <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-12 text-center text-slate-400">
        <Trophy className="w-12 h-12 mx-auto mb-3 text-slate-200" />
        <h3 className="font-semibold text-slate-700 text-sm">No Rankings Available</h3>
        <p className="text-xs text-slate-400 mt-1">There are no startup scores calculated in this class/system yet.</p>
      </div>
    );
  }

  const getRankBadge = (rank) => {
    if (rank === 1) {
      return (
        <span className="w-6 h-6 rounded-full bg-amber-100 border border-amber-200 flex items-center justify-center font-bold text-xs text-amber-700 shadow-sm ring-4 ring-amber-50">
          🥇
        </span>
      );
    }
    if (rank === 2) {
      return (
        <span className="w-6 h-6 rounded-full bg-slate-100 border border-slate-200 flex items-center justify-center font-bold text-xs text-slate-600 shadow-sm ring-4 ring-slate-50">
          🥈
        </span>
      );
    }
    if (rank === 3) {
      return (
        <span className="w-6 h-6 rounded-full bg-amber-50 shadow-sm ring-4 ring-amber-50 border border-amber-200 flex items-center justify-center font-bold text-xs text-amber-800">
          🥉
        </span>
      );
    }
    return (
      <span className="w-6 h-6 rounded-full bg-slate-50 border border-slate-100 flex items-center justify-center font-bold text-xs text-slate-500">
        {rank}
      </span>
    );
  };

  return (
    <div className="bg-white border border-slate-200/60 rounded-2xl shadow-sm overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="bg-slate-50/70 border-b border-slate-100 text-slate-400 text-[10px] font-bold uppercase tracking-wider">
              <th className="px-6 py-4 text-center w-16">Rank</th>
              <th className="px-6 py-4">Startup / Team</th>
              <th className="px-6 py-4">Class</th>
              <th className="px-6 py-4">
                <div className="flex items-center gap-1">
                  <Brain className="w-3.5 h-3.5 text-slate-400" />
                  <span>Evaluation (60%)</span>
                </div>
              </th>
              <th className="px-6 py-4">
                <div className="flex items-center gap-1">
                  <Kanban className="w-3.5 h-3.5 text-slate-400" />
                  <span>Sprint (25%)</span>
                </div>
              </th>
              <th className="px-6 py-4">
                <div className="flex items-center gap-1">
                  <FileText className="w-3.5 h-3.5 text-slate-400" />
                  <span>Proposal (10%)</span>
                </div>
              </th>
              <th className="px-6 py-4">
                <div className="flex items-center gap-1">
                  <Layout className="w-3.5 h-3.5 text-slate-400" />
                  <span>Deck (5%)</span>
                </div>
              </th>
              <th className="px-6 py-4 text-right pr-8">
                <div className="flex items-center justify-end gap-1 font-bold text-primary">
                  <TrendingUp className="w-3.5 h-3.5" />
                  <span>Total Score</span>
                </div>
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 text-sm">
            {rankings.map((r, index) => {
              const rank = index + 1;
              return (
                <tr
                  key={r.teamId}
                  className={`hover:bg-slate-50/50 transition-colors ${rank <= 3 ? 'bg-primary-50/5' : ''}`}
                >
                  <td className="px-6 py-4 text-center">
                    <div className="flex justify-center">{getRankBadge(rank)}</div>
                  </td>
                  <td className="px-6 py-4">
                    <div>
                      <h4 className="font-bold text-slate-800 leading-tight">
                        {r.startupName}
                      </h4>
                      <p className="text-xs text-slate-400 font-semibold mt-0.5">
                        Team: {r.teamName} ({r.teamCode})
                      </p>
                    </div>
                  </td>
                  <td className="px-6 py-4">
                    <span className="bg-slate-100 text-slate-600 text-xs px-2 py-1 rounded-md font-semibold">
                      {r.className}
                    </span>
                  </td>
                  <td className="px-6 py-4">
                    <div className="space-y-1">
                      <span className="font-bold text-slate-700">{r.scores?.evaluationScore}%</span>
                      <div className="w-16 h-1.5 bg-slate-100 rounded-full overflow-hidden">
                        <div
                          className="h-full bg-emerald-500 rounded-full"
                          style={{ width: `${r.scores?.evaluationScore || 0}%` }}
                        />
                      </div>
                    </div>
                  </td>
                  <td className="px-6 py-4">
                    <div className="space-y-1">
                      <span className="font-bold text-slate-700">{Math.round(r.scores?.sprintProgress)}%</span>
                      <div className="w-16 h-1.5 bg-slate-100 rounded-full overflow-hidden">
                        <div
                          className="h-full bg-sky-500 rounded-full"
                          style={{ width: `${r.scores?.sprintProgress || 0}%` }}
                        />
                      </div>
                    </div>
                  </td>
                  <td className="px-6 py-4">
                    {r.scores?.proposalScore === 100 ? (
                      <span className="inline-flex items-center gap-1 text-xs text-emerald-600 font-bold bg-emerald-50 px-2 py-0.5 rounded-full border border-emerald-100">
                        <CheckCircle className="w-3 h-3" /> Submitted
                      </span>
                    ) : r.scores?.proposalScore === 55 ? (
                      <span className="inline-flex items-center gap-1 text-xs text-amber-600 font-bold bg-amber-50 px-2 py-0.5 rounded-full border border-amber-100">
                        Draft
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 text-xs text-slate-400 font-medium bg-slate-50 px-2 py-0.5 rounded-full border border-slate-100">
                        Missing
                      </span>
                    )}
                  </td>
                  <td className="px-6 py-4">
                    {r.scores?.deckScore === 100 ? (
                      <span className="inline-flex items-center gap-1 text-xs text-emerald-600 font-bold bg-emerald-50 px-2 py-0.5 rounded-full border border-emerald-100">
                        <CheckCircle className="w-3 h-3" /> Active
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 text-xs text-slate-400 font-medium bg-slate-50 px-2 py-0.5 rounded-full border border-slate-100">
                        Missing
                      </span>
                    )}
                  </td>
                  <td className="px-6 py-4 text-right pr-8">
                    <span className="inline-block bg-primary text-white text-xs font-black px-3 py-1.5 rounded-xl shadow-glow-primary">
                      {r.finalScore} / 100
                    </span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default RankingTable;
