import { useEffect, useState } from 'react';
import { Loader2, X } from 'lucide-react';
import { studentPreviousScoresApi } from '../../api/studentPreviousScoresApi';
import type { StudentPreviousScores } from '../../types/studentPreviousScores';
import { parseApiError } from '../../utils/apiError';

export default function StudentPreviousScoresDialog({ classId, studentId, fullName, onClose }: {
  classId: string; studentId: string; fullName: string; onClose: () => void;
}) {
  const [data, setData] = useState<StudentPreviousScores | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    studentPreviousScoresApi.get(classId, studentId, controller.signal).then(response => {
      if (controller.signal.aborted) return;
      if (!response.success) throw new Error(response.message || 'Unable to load previous scores.');
      setData(response.data);
    }).catch((reason: unknown) => {
      if (!controller.signal.aborted) setError(parseApiError(reason, 'Unable to load previous scores.').message);
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [classId, studentId, retry]);
  return <div className="fixed inset-0 z-[70] flex items-center justify-center bg-slate-950/50 p-4" role="dialog" aria-modal="true" aria-labelledby="previous-scores-title">
    <div className="max-h-[90vh] w-full max-w-xl overflow-y-auto rounded-2xl bg-white p-5 shadow-xl">
      <div className="mb-4 flex items-start justify-between gap-3"><div>
        <h2 id="previous-scores-title" className="text-lg font-bold">Previous semester scores</h2>
        <p className="text-sm text-slate-500">{fullName}{data?.semesterCode ? ` · ${data.semesterCode}` : ''}</p>
        <p className="mt-1 text-xs text-slate-500">Published personal scores for the same subject.</p>
      </div><button type="button" aria-label="Close previous scores" onClick={onClose} className="rounded-lg p-2 hover:bg-slate-100"><X className="h-5 w-5" /></button></div>
      {loading ? <div role="status" className="flex items-center justify-center gap-2 p-8"><Loader2 className="h-5 w-5 animate-spin" /> Loading…</div>
        : error ? <div role="alert" className="rounded-xl bg-red-50 p-4 text-red-700">{error}<button type="button" onClick={() => setRetry(value => value + 1)} className="ml-2 font-bold underline">Retry</button></div>
          : !data?.components.length ? <p className="rounded-xl bg-slate-50 p-6 text-center text-sm text-slate-500">No published scores available for the previous semester.</p>
            : <table className="w-full text-left text-sm"><thead><tr><th scope="col" className="py-2">Assessment</th><th scope="col">Weight</th><th scope="col">Personal score</th></tr></thead><tbody>
              {data.components.map(item => <tr key={item.assessmentId} className="border-t border-slate-200"><td className="py-3">{item.checkpointNumber ? `Checkpoint ${item.checkpointNumber}: ` : ''}{item.name}</td><td>{item.weight}%</td><td className="font-bold">{item.score.toFixed(2)} / 10</td></tr>)}
            </tbody></table>}
    </div>
  </div>;
}
