// @ts-nocheck
// Startup checkpoints — overview grid + full-screen detail panel
import { useState, useEffect, useCallback } from 'react';
import { Flag, Loader2, Target } from 'lucide-react';
import { checkpointApi } from '../../../api/checkpointApi';
import CheckpointCard from './CheckpointCard';
import CheckpointPanel from './CheckpointPanel';
import ErrorState from '../../ui/ErrorState';

export default function CheckpointSection({
  teamId,
  isEditable,
  isReadOnly = false,
  proposalId,
  pitchDeckId,
}) {
  const [configs, setConfigs] = useState([]);
  const [stats, setStats] = useState({});
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState(null);
  const [error, setError] = useState('');

  const fetchStats = useCallback(async () => {
    if (!teamId) return;
    setLoading(true);
    setError('');
    setConfigs([]);
    setStats({});
    try {
      const [config, res] = await Promise.all([checkpointApi.getConfig(), checkpointApi.getCheckpointData(String(teamId))]);
      if (!config.success || !res.success) throw new Error('Checkpoint request failed');
      setConfigs(config.data);
      if (res.success) {
        const map = {};
        res.data.submissions.forEach((sub) => {
          const sorted = [...(sub.files || [])].sort(
            (a, b) => new Date(b.uploadedAt) - new Date(a.uploadedAt)
          );
          const reqFilled = (sub.requirementContents || []).filter(
            (r) => r.content && String(r.content).trim()
          ).length;
          map[Number(sub.checkpointNumber)] = {
            count: sorted.length,
            latest: sorted[0] || null,
            reqFilled,
            reqTotal: sub.requirementContents?.length || 0,
          };
        });
        setStats(map);
      }
    } catch (e) {
      setError(e?.response?.status === 404 || e?.status === 404
        ? 'Checkpoints are not available on this server yet. Submission and feedback are unavailable.'
        : 'Unable to load checkpoints. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [teamId]);

  useEffect(() => {
    // Fetch checkpoint progress whenever the selected team changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchStats();
  }, [fetchStats]);

  const completedCount = configs.filter((cp) => {
    const s = stats[cp.number];
    return (s?.count || 0) > 0 || (s?.reqFilled || 0) > 0;
  }).length;

  if (error) return <section className="rounded-2xl border border-slate-200 bg-white p-5"><h2 className="mb-3 text-lg font-bold">Startup Checkpoints</h2><ErrorState message={error} onRetry={fetchStats} /></section>;

  return (
    <>
      <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm overflow-hidden">
        {/* Header */}
        <div className="px-6 py-5 border-b border-slate-100 bg-gradient-to-r from-orange-50/80 via-white to-primary-50/30">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="w-11 h-11 rounded-2xl bg-gradient-to-br from-orange-500 to-orange-600 flex items-center justify-center shadow-md shadow-orange-200/50">
                <Flag className="w-5 h-5 text-white" />
              </div>
              <div>
                <h2 className="text-lg font-bold text-slate-900">Startup Checkpoints</h2>
                <p className="text-xs text-slate-500 mt-0.5">
                  4 milestone stages · submit docs & receive feedback
                </p>
              </div>
            </div>

            <div className="flex items-center gap-3">
              {loading ? (
                <Loader2 className="w-5 h-5 text-orange-400 animate-spin" />
              ) : (
                <div className="flex items-center gap-2 px-4 py-2 rounded-xl bg-white border border-slate-200/80 shadow-sm">
                  <Target className="w-4 h-4 text-primary" />
                  <span className="text-sm font-bold text-slate-700">
                    {completedCount}
                    <span className="text-slate-400 font-medium"> / {configs.length || 4} completed</span>
                  </span>
                </div>
              )}
            </div>
          </div>

          {/* Progress bar */}
          {!loading && configs.length > 0 && (
            <div className="mt-4 h-2 rounded-full bg-slate-100 overflow-hidden">
              <div
                className="h-full rounded-full bg-gradient-to-r from-orange-400 to-orange-500 transition-all duration-500"
                style={{ width: `${(completedCount / configs.length) * 100}%` }}
              />
            </div>
          )}
        </div>

        {/* Vertical checkpoint list */}
        <div className="px-4 py-6 sm:px-6">
          {configs.length === 0 && !loading ? (
            <p className="text-sm text-slate-400 text-center py-8">No checkpoints configured.</p>
          ) : (
            <div className="space-y-4">
              {configs.map((cp, index) => (
                <CheckpointCard
                  key={cp.number}
                  checkpoint={cp}
                  submissionStats={stats}
                  isLast={index === configs.length - 1}
                  onOpen={() => setSelected(cp)}
                />
              ))}
            </div>
          )}
        </div>
      </div>

      {selected && (
        <CheckpointPanel
          checkpoint={selected}
          teamId={teamId}
          isEditable={isEditable}
          isReadOnly={isReadOnly}
          proposalId={proposalId}
          pitchDeckId={pitchDeckId}
          onRequirementsSaved={fetchStats}
          onClose={() => {
            setSelected(null);
            fetchStats();
          }}
        />
      )}
    </>
  );
}
