// @ts-nocheck
import { useCallback, useEffect, useRef, useState } from 'react';
import { FileText, Loader2, Save, Send } from 'lucide-react';
import toast from 'react-hot-toast';
import { teamApi } from '../../api/teamApi';
import { unwrapApiData } from '../../utils/classMappers';
import { parseApiError } from '../../utils/apiError';
import {
  canSubmitProjectDirection,
  getProjectDirectionDecisionNotice,
  getProjectDirectionSubmitGuidance,
  hasUnsavedProjectDirectionChanges,
  hasProjectDirectionChanged,
} from '../../utils/projectDirectionSync';
import { subscribeProjectDirectionRealtime } from '../../api/projectDirectionRealtime';

export default function ProjectDirectionCard({ team, canEdit }) {
  const [direction, setDirection] = useState(null);
  const [title, setTitle] = useState('');
  const [summary, setSummary] = useState('');
  const [loading, setLoading] = useState(true);
  const [activeAction, setActiveAction] = useState(null);
  const directionRef = useRef(null);
  const busy = activeAction !== null;
  const editableState = !direction || ['Draft', 'NeedsRevision'].includes(direction.status);

  const applyDirection = useCallback((value, announceDecision = false) => {
    if (!value) return;
    const previous = directionRef.current;
    if (previous && !hasProjectDirectionChanged(previous, value)) return;

    directionRef.current = value;
    setDirection(value);
    setTitle(value.title || '');
    setSummary(value.summary || '');

    if (announceDecision) {
      const notice = getProjectDirectionDecisionNotice(previous, value);
      if (notice) toast(notice, { id: `project-direction-${value.rowVersion}` });
    }
  }, []);

  useEffect(() => {
    let active = true;
    const load = async () => {
      try {
        const response = await teamApi.getProjectDirection(team._id);
        const value = unwrapApiData(response);
        if (!active) return;
        applyDirection(value);
      } catch (error) {
        const parsed = parseApiError(error, 'Unable to load project direction.');
        if (parsed.code !== 'PROJECT_DIRECTION_NOT_FOUND') toast.error(parsed.message);
      } finally {
        if (active) setLoading(false);
      }
    };
    load();
    return () => { active = false; };
  }, [applyDirection, team._id]);

  useEffect(() => {
    if (direction?.status !== 'Submitted') return undefined;

    let active = true;
    const synchronizeAfterReconnect = async () => {
      try {
        const response = await teamApi.getProjectDirection(team._id);
        if (active) applyDirection(unwrapApiData(response), true);
      } catch {
        // The next WebSocket event or reconnect will synchronize the card.
      }
    };
    const unsubscribe = subscribeProjectDirectionRealtime(
      (event) => {
        if (event.eventType === 'ProjectDirectionReviewed' && event.teamId === team._id)
          applyDirection(event.direction, true);
      },
      () => void synchronizeAfterReconnect(),
    );
    return () => {
      active = false;
      unsubscribe();
    };
  }, [applyDirection, direction?.status, team._id]);

  const save = async () => {
    if (busy) return;
    setActiveAction('save');
    try {
      const response = await teamApi.saveProjectDirection(team._id, {
        title: title.trim(),
        summary: summary.trim(),
        rowVersion: direction?.rowVersion || null,
      });
      const value = unwrapApiData(response);
      applyDirection(value);
      toast.success('Project direction saved as draft.');
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to save project direction.').message);
    } finally {
      setActiveAction(null);
    }
  };

  const submit = async () => {
    if (!direction || busy) return;
    setActiveAction('submit');
    try {
      const response = await teamApi.submitProjectDirection(team._id, direction.rowVersion);
      const value = unwrapApiData(response);
      applyDirection(value);
      toast.success('Project direction submitted for lecturer review.');
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to submit project direction.').message);
    } finally {
      setActiveAction(null);
    }
  };

  const valid = title.trim().length >= 3 && summary.trim().length >= 20 && summary.trim().length <= 5000;
  const hasUnsavedChanges = hasUnsavedProjectDirectionChanges(direction, title, summary);
  const canSubmit = canSubmitProjectDirection(direction, title, summary);
  const submitGuidance = getProjectDirectionSubmitGuidance(direction, title, summary);
  const latestReview = direction?.reviews?.[0];
  return (
    <section className="rounded-2xl border border-slate-200/60 bg-white p-6 shadow-sm">
      <div className="flex items-start justify-between gap-4 border-b border-slate-100 pb-4">
        <div className="flex items-center gap-2"><FileText className="h-5 w-5 text-primary" /><div><h2 className="text-lg font-bold text-slate-800">Project Direction</h2><p className="mt-0.5 text-xs text-slate-500">Separate from the team proposal and reviewed only by the assigned lecturer.</p></div></div>
        <span className="rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-semibold text-slate-600">{direction?.status || 'Not created'}</span>
      </div>
      {loading ? <div className="flex min-h-40 items-center justify-center"><Loader2 className="h-5 w-5 animate-spin text-primary" /></div> : canEdit && editableState ? (
        <div className="mt-4 space-y-3" aria-busy={busy || undefined}>
          <input value={title} onChange={event => setTitle(event.target.value)} disabled={busy} maxLength={200} placeholder="Direction title" className="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-primary disabled:bg-slate-50 disabled:text-slate-500" />
          <textarea value={summary} onChange={event => setSummary(event.target.value)} disabled={busy} maxLength={5000} rows={8} placeholder="Describe the problem, target users, proposed solution, and implementation direction..." className="w-full resize-y rounded-xl border border-slate-200 px-4 py-3 text-sm leading-6 text-slate-700 outline-none focus:border-primary disabled:bg-slate-50 disabled:text-slate-500" />
          <div className="flex flex-wrap items-center justify-between gap-3"><p className={`text-xs ${valid ? 'text-slate-500' : 'text-red-500'}`}>{summary.length}/5000 characters · minimum 20</p><div className="flex gap-2"><button type="button" onClick={save} disabled={busy || !valid || !hasUnsavedChanges} className="inline-flex items-center gap-2 rounded-lg border border-primary-200 px-4 py-2 text-sm font-semibold text-primary disabled:opacity-50">{activeAction === 'save' ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />} {activeAction === 'save' ? 'Saving...' : 'Save draft'}</button>{direction && <button type="button" onClick={submit} disabled={busy || !canSubmit} className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:bg-slate-300 disabled:text-slate-600 disabled:opacity-100">{activeAction === 'submit' ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />} {activeAction === 'submit' ? 'Submitting...' : 'Submit'}</button>}</div></div>
          {submitGuidance && <p role="status" className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">{submitGuidance}</p>}
        </div>
      ) : (
        <div className="mt-4 space-y-3">{direction ? <><h3 className="font-semibold text-slate-900">{direction.title}</h3><p className="whitespace-pre-wrap text-sm leading-7 text-slate-700">{direction.summary}</p></> : <p className="rounded-xl border border-dashed border-slate-200 bg-slate-50 px-4 py-6 text-center text-sm text-slate-500">The team leader has not created a project direction yet.</p>}</div>
      )}
      {direction?.status === 'Submitted' && <div className="mt-4 flex items-center gap-2 text-xs font-medium text-slate-500"><span className="h-2 w-2 rounded-full bg-emerald-500" /> Waiting for lecturer decision · live updates enabled</div>}
      {latestReview && <div role="status" aria-live="polite" className="mt-4 rounded-xl border border-blue-100 bg-blue-50 px-4 py-3"><p className="text-xs font-bold uppercase text-blue-700">Latest lecturer review · {latestReview.toStatus}</p><p className="mt-1 text-sm leading-6 text-blue-900">{latestReview.comment}</p></div>}
    </section>
  );
}
