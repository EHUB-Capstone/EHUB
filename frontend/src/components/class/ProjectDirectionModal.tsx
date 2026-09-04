import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { CheckCircle2, FilePenLine, Loader2, Send, X } from 'lucide-react';
import { teamApi } from '../../api/teamApi';
import { unwrapApiData } from '../../utils/classMappers';
import { parseApiError } from '../../utils/apiError';
import { entityId } from '../../utils/teamManagement';

const formatStatus = (status) => String(status || 'Not created')
  .replace(/^NEEDSREVISION$/i, 'Needs revision')
  .replace(/([a-z])([A-Z])/g, '$1 $2');

export default function ProjectDirectionModal({ team, role, currentStudentId = '', onClose, onChanged }) {
  const [direction, setDirection] = useState(null);
  const [title, setTitle] = useState('');
  const [summary, setSummary] = useState('');
  const [decision, setDecision] = useState('Approved');
  const [comment, setComment] = useState('');
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const isLeader = role === 'STUDENT' && entityId(team.leaderId) === currentStudentId;
  const canEdit = isLeader && (!direction || ['Draft', 'NeedsRevision'].includes(direction.status));
  const canReview = role === 'LECTURER' && direction?.status === 'Submitted';

  useEffect(() => {
    let active = true;
    const load = async () => {
      try {
        const response = await teamApi.getProjectDirection(team._id);
        const value = unwrapApiData<any>(response as any);
        if (!active) return;
        setDirection(value);
        setTitle(value.title || '');
        setSummary(value.summary || '');
      } catch (error) {
        const parsed = parseApiError(error, 'Failed to load project direction.');
        if (parsed.code !== 'PROJECT_DIRECTION_NOT_FOUND') toast.error(parsed.message);
      } finally {
        if (active) setLoading(false);
      }
    };
    load();
    return () => { active = false; };
  }, [team._id]);

  const save = async () => {
    setSubmitting(true);
    try {
      const response = await teamApi.saveProjectDirection(team._id, {
        title: title.trim(),
        summary: summary.trim(),
        rowVersion: direction?.rowVersion || null,
      });
      const value = unwrapApiData(response);
      setDirection(value);
      toast.success('Project direction saved as draft.');
      await onChanged?.();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to save project direction.').message);
    } finally {
      setSubmitting(false);
    }
  };

  const submit = async () => {
    if (!direction) return;
    setSubmitting(true);
    try {
      const response = await teamApi.submitProjectDirection(team._id, direction.rowVersion);
      setDirection(unwrapApiData(response));
      toast.success('Project direction submitted to the assigned lecturer.');
      await onChanged?.();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to submit project direction.').message);
    } finally {
      setSubmitting(false);
    }
  };

  const review = async () => {
    if (comment.trim().length < 3 || comment.trim().length > 1000) {
      toast.error('Review comment must be between 3 and 1000 characters.');
      return;
    }
    setSubmitting(true);
    try {
      const response = await teamApi.reviewProjectDirection(team._id, {
        decision,
        comment: comment.trim(),
        rowVersion: direction.rowVersion,
      });
      setDirection(unwrapApiData(response));
      setComment('');
      toast.success(decision === 'Approved' ? 'Project direction approved.' : 'Changes requested.');
      await onChanged?.();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to review project direction.').message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[85] flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm">
      <div className="flex max-h-[92vh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl bg-white shadow-xl">
        <header className="flex items-start justify-between border-b border-slate-100 bg-slate-50 px-5 py-4">
          <div><h3 className="font-bold text-slate-900">Project direction · {team.teamName}</h3><p className="mt-0.5 text-xs text-slate-500">This workflow is separate from the team proposal.</p></div>
          <button type="button" onClick={onClose} className="rounded-lg p-2 text-slate-400 hover:bg-slate-200"><X className="h-4 w-4" /></button>
        </header>

        {loading ? <div className="flex min-h-64 items-center justify-center"><Loader2 className="h-6 w-6 animate-spin text-primary" /></div> : (
          <div className="flex-1 space-y-5 overflow-y-auto p-5">
            <div className="flex items-center justify-between"><span className="text-xs font-bold uppercase text-slate-500">Current status</span><span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-700">{formatStatus(direction?.status)}</span></div>
            <div>
              <label className="text-xs font-semibold text-slate-600">Direction title</label>
              <input value={title} onChange={event => setTitle(event.target.value)} disabled={!canEdit} maxLength={200} className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary disabled:bg-slate-50" />
            </div>
            <div>
              <label className="text-xs font-semibold text-slate-600">Summary</label>
              <textarea value={summary} onChange={event => setSummary(event.target.value)} disabled={!canEdit} maxLength={5000} rows={7} className="mt-1.5 w-full resize-none rounded-xl border border-slate-200 px-3 py-2 text-sm leading-6 outline-none focus:border-primary disabled:bg-slate-50" />
              <p className="mt-1 text-right text-[11px] text-slate-400">{summary.length}/5000 · minimum 20</p>
            </div>

            {canEdit && <div className="flex justify-end gap-2"><button type="button" onClick={save} disabled={submitting} className="rounded-xl border border-primary-200 px-4 py-2 text-sm font-semibold text-primary disabled:opacity-50">Save draft</button>{direction && <button type="button" onClick={submit} disabled={submitting} className="inline-flex items-center gap-2 rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"><Send className="h-4 w-4" /> Submit</button>}</div>}

            {canReview && <section className="rounded-xl border border-indigo-100 bg-indigo-50/40 p-4">
              <h4 className="text-sm font-bold text-slate-800">Lecturer review</h4>
              <div className="mt-3 flex gap-2">
                <button type="button" onClick={() => setDecision('Approved')} className={`flex items-center gap-1.5 rounded-lg border px-3 py-2 text-xs font-semibold ${decision === 'Approved' ? 'border-green-400 bg-green-50 text-green-700' : 'border-slate-200 bg-white text-slate-600'}`}><CheckCircle2 className="h-4 w-4" /> Approve</button>
                <button type="button" onClick={() => setDecision('NeedsRevision')} className={`flex items-center gap-1.5 rounded-lg border px-3 py-2 text-xs font-semibold ${decision === 'NeedsRevision' ? 'border-amber-400 bg-amber-50 text-amber-700' : 'border-slate-200 bg-white text-slate-600'}`}><FilePenLine className="h-4 w-4" /> Request changes</button>
              </div>
              <textarea value={comment} onChange={event => setComment(event.target.value)} maxLength={1000} rows={3} placeholder="Required: 3–1000 characters" className="mt-3 w-full resize-none rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary" />
              <div className="mt-2 flex justify-end"><button type="button" onClick={review} disabled={submitting || comment.trim().length < 3} className="rounded-xl bg-indigo-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Submit review</button></div>
            </section>}

            {direction?.reviews?.length > 0 && <section><h4 className="mb-2 text-xs font-bold uppercase tracking-wider text-slate-500">Review history</h4><div className="space-y-2">{direction.reviews.map(review => <div key={review.id} className="rounded-xl border border-slate-100 bg-slate-50 p-3"><div className="flex justify-between gap-3 text-xs font-semibold text-slate-700"><span>{formatStatus(review.fromStatus)} → {formatStatus(review.toStatus)}</span><span className="text-slate-400">{new Date(review.occurredAtUtc).toLocaleString()}</span></div><p className="mt-1 text-sm text-slate-600">{review.comment}</p></div>)}</div></section>}
          </div>
        )}
      </div>
    </div>
  );
}
