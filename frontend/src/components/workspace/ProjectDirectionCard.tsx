// @ts-nocheck
import { useCallback, useEffect, useRef, useState } from 'react';
import { ArrowRight, ChevronDown, FileText, FolderKanban, Loader2, RefreshCw, Save, Send, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { teamApi } from '../../api/teamApi';
import { startupIndustryApi } from '../../api/startupIndustryApi';
import { unwrapApiData } from '../../utils/classMappers';
import { parseApiError } from '../../utils/apiError';
import {
  canSubmitProjectDirection,
  getProjectDirectionDecisionNotice,
  getProjectDirectionSubmitGuidance,
  hasUnsavedProjectDirectionChanges,
  hasProjectDirectionChanged,
  isProjectDirectionConcurrencyConflict,
  isProjectProfileAvailable,
} from '../../utils/projectDirectionSync';
import { subscribeProjectDirectionRealtime } from '../../api/projectDirectionRealtime';

export default function ProjectDirectionCard({ team, project, canEdit, onOpenProjectProfile }) {
  const [direction, setDirection] = useState(null);
  const [title, setTitle] = useState('');
  const [summary, setSummary] = useState('');
  const [industries, setIndustries] = useState([]);
  const [selectedIndustryIds, setSelectedIndustryIds] = useState([]);
  const [industriesLoading, setIndustriesLoading] = useState(true);
  const [industriesError, setIndustriesError] = useState('');
  const [industriesOpen, setIndustriesOpen] = useState(false);
  const [industriesReload, setIndustriesReload] = useState(0);
  const [editing, setEditing] = useState(false);
  const [loading, setLoading] = useState(true);
  const [activeAction, setActiveAction] = useState(null);
  const directionRef = useRef(null);
  const initializedIndustryRevisionRef = useRef('');
  const actionInFlightRef = useRef(false);
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
    const controller = new AbortController();
    startupIndustryApi.getActiveOptions(controller.signal)
      .then((response) => {
        const payload = response?.data ?? response;
        setIndustries(Array.isArray(payload?.industries) ? payload.industries : []);
        setIndustriesError('');
      })
      .catch((error) => {
        if (!controller.signal.aborted) {
          setIndustries([]);
          setIndustriesError(parseApiError(error, 'Failed to load startup industries.').message);
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setIndustriesLoading(false);
      });
    return () => controller.abort();
  }, [industriesReload]);

  useEffect(() => {
    if (!direction || industriesLoading || industriesError) return;
    const revisionKey = `${direction.id || ''}:${direction.rowVersion || ''}`;
    if (initializedIndustryRevisionRef.current === revisionKey) return;
    const selectedNames = new Set((direction.startupIndustries || []).map((name) => name.trim().toLocaleUpperCase()));
    setSelectedIndustryIds(industries
      .filter((industry) => selectedNames.has(industry.name.trim().toLocaleUpperCase()))
      .map((industry) => industry.id));
    initializedIndustryRevisionRef.current = revisionKey;
  }, [direction, industries, industriesError, industriesLoading]);

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
    if (actionInFlightRef.current) return;
    actionInFlightRef.current = true;
    setActiveAction('save');
    try {
      const payload = {
        title: title.trim(),
        summary: summary.trim(),
        startupIndustryIds: selectedIndustryIds,
      };
      let response;
      try {
        response = await teamApi.saveProjectDirection(team._id, {
          ...payload,
          rowVersion: direction?.rowVersion || null,
        });
      } catch (error) {
        if (!isProjectDirectionConcurrencyConflict(parseApiError(error, '').code)) throw error;
        const latest = unwrapApiData(await teamApi.getProjectDirection(team._id));
        if (!['Draft', 'NeedsRevision'].includes(latest?.status)) {
          toast.error(`Project direction is now ${latest?.status || 'in another state'}. Your changes remain on this form; reload before continuing.`);
          return;
        }
        response = await teamApi.saveProjectDirection(team._id, {
          ...payload,
          rowVersion: latest.rowVersion,
        });
      }
      const value = unwrapApiData(response);
      applyDirection(value);
      toast.success('Project direction saved as draft.');
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to save project direction.').message);
    } finally {
      actionInFlightRef.current = false;
      setActiveAction(null);
    }
  };

  const submit = async () => {
    if (!direction || actionInFlightRef.current) return;
    actionInFlightRef.current = true;
    setActiveAction('submit');
    try {
      let response;
      try {
        response = await teamApi.submitProjectDirection(team._id, direction.rowVersion);
      } catch (error) {
        if (!isProjectDirectionConcurrencyConflict(parseApiError(error, '').code)) throw error;
        const latest = unwrapApiData(await teamApi.getProjectDirection(team._id));
        if (latest?.status === 'Submitted') {
          applyDirection(latest);
          toast.success('Project direction submitted for lecturer review.');
          setEditing(false);
          return;
        }
        if (latest?.status !== 'Draft') {
          toast.error(`Project direction is now ${latest?.status || 'in another state'}. Reload before submitting again.`);
          return;
        }
        response = await teamApi.submitProjectDirection(team._id, latest.rowVersion);
      }
      const value = unwrapApiData(response);
      applyDirection(value);
      toast.success('Project direction submitted for lecturer review.');
      setEditing(false);
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to submit project direction.').message);
    } finally {
      actionInFlightRef.current = false;
      setActiveAction(null);
    }
  };

  const selectedIndustryNames = selectedIndustryIds
    .map((id) => industries.find((industry) => industry.id === id)?.name)
    .filter(Boolean);
  const valid = title.trim().length >= 3
    && summary.trim().length >= 20
    && summary.trim().length <= 2000
    && selectedIndustryIds.length >= 1
    && selectedIndustryIds.length <= 3
    && !industriesLoading
    && !industriesError;
  const hasUnsavedChanges = hasUnsavedProjectDirectionChanges(direction, title, summary, selectedIndustryNames);
  const canSubmit = canSubmitProjectDirection(direction, title, summary, selectedIndustryNames);
  const submitGuidance = getProjectDirectionSubmitGuidance(direction, title, summary, selectedIndustryNames);
  const latestReview = direction?.reviews?.[0];

  const openEditor = async () => {
    if (actionInFlightRef.current) return;
    actionInFlightRef.current = true;
    setActiveAction('refresh');
    try {
      const latest = unwrapApiData(await teamApi.getProjectDirection(team._id));
      if (!['Draft', 'NeedsRevision'].includes(latest?.status)) {
        applyDirection(latest);
        toast.error(`Project direction is now ${latest?.status || 'in another state'} and cannot be edited.`);
        return;
      }
      applyDirection(latest);
      setEditing(true);
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to refresh project direction before editing.').message);
    } finally {
      actionInFlightRef.current = false;
      setActiveAction(null);
    }
  };

  if (!loading && isProjectProfileAvailable(direction)) {
    return (
      <button type="button" onClick={onOpenProjectProfile} className="group w-full overflow-hidden rounded-2xl border border-emerald-200 bg-white text-left shadow-sm transition hover:-translate-y-0.5 hover:border-emerald-300 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-emerald-300">
        <div className="flex items-start justify-between gap-4 border-b border-emerald-100 bg-emerald-50/60 px-6 py-4">
          <div className="flex items-center gap-3"><span className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-100 text-emerald-700"><FolderKanban className="h-5 w-5" /></span><div><h2 className="text-lg font-bold text-slate-800">Project Profile</h2><p className="mt-0.5 text-xs text-slate-500">Keep the approved project information clear, complete, and up to date.</p></div></div>
          <span className="rounded-full border border-emerald-200 bg-white px-2.5 py-1 text-xs font-semibold text-emerald-700">Approved</span>
        </div>
        <div className="px-6 py-5">
          <div className="flex items-start justify-between gap-4"><div className="min-w-0"><h3 className="truncate font-semibold text-slate-900">{project?.projectName || direction.title}</h3><p className="mt-1 line-clamp-2 text-sm leading-6 text-slate-600">{project?.description || direction.summary}</p></div><ArrowRight className="mt-1 h-5 w-5 shrink-0 text-slate-400 transition group-hover:translate-x-1 group-hover:text-emerald-600" /></div>
          <div className="mt-4 grid gap-3 sm:grid-cols-3">
            {[
              ['Problem', project?.problem],
              ['Solution', project?.solution],
              ['Target users', project?.targetUsers],
            ].map(([label, value]) => (
              <div key={label} className="rounded-xl border border-slate-100 bg-slate-50/70 px-3 py-2.5">
                <p className="text-xs font-bold uppercase tracking-wide text-slate-500">{label}</p>
                <p className="mt-1 line-clamp-2 text-sm leading-5 text-slate-700">{value || 'Not documented yet.'}</p>
              </div>
            ))}
          </div>
          <p className="mt-3 text-xs font-semibold text-emerald-700">Open the profile to view details{canEdit ? ' or update it' : ''}</p>
        </div>
      </button>
    );
  }

  return (
    <section
      role={canEdit && editableState && !editing ? 'button' : undefined}
      tabIndex={canEdit && editableState && !editing ? 0 : undefined}
      onClick={canEdit && editableState && !editing ? () => void openEditor() : undefined}
      onKeyDown={canEdit && editableState && !editing ? (event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          void openEditor();
        }
      } : undefined}
      className={`rounded-2xl border border-slate-200/60 bg-white p-6 shadow-sm ${canEdit && editableState && !editing ? 'cursor-pointer transition hover:border-primary-200 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-primary/20' : ''}`}
    >
      <div className="flex items-start justify-between gap-4 border-b border-slate-100 pb-4">
        <div className="flex items-center gap-2"><FileText className="h-5 w-5 text-primary" /><div><h2 className="text-lg font-bold text-slate-800">Project Direction</h2><p className="mt-0.5 text-xs text-slate-500">Project information submitted to the assigned lecturer for review.</p></div></div>
        <span className="rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-semibold text-slate-600">{direction?.status || 'Not created'}</span>
      </div>
      {loading ? <div className="flex min-h-40 items-center justify-center"><Loader2 className="h-5 w-5 animate-spin text-primary" /></div> : canEdit && editableState && editing ? (
        <div className="mt-4 space-y-3" aria-busy={busy || undefined}>
          <label className="text-xs font-semibold text-slate-600">Project Name</label>
          <input value={title} onChange={event => setTitle(event.target.value)} disabled={busy} maxLength={200} placeholder="Project name" className="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-primary disabled:bg-slate-50 disabled:text-slate-500" />
          <label className="text-xs font-semibold text-slate-600">Project description</label>
          <textarea value={summary} onChange={event => setSummary(event.target.value)} disabled={busy} maxLength={2000} rows={8} placeholder="Describe the project..." className="w-full resize-y rounded-xl border border-slate-200 px-4 py-3 text-sm leading-6 text-slate-700 outline-none focus:border-primary disabled:bg-slate-50 disabled:text-slate-500" />
          <div>
            <label id="direction-startup-industry-label" className="text-xs font-semibold text-slate-600">
              Startup Industry <span className="text-red-500">*</span>
            </label>
            <div className="relative mt-1.5">
              <div className={`rounded-xl border bg-white p-2 transition focus-within:ring-2 focus-within:ring-primary/15 ${selectedIndustryIds.length === 0 && !industriesLoading ? 'border-red-300' : 'border-slate-200 focus-within:border-primary'}`}>
                {selectedIndustryIds.length > 0 && (
                  <div className="mb-2 flex flex-wrap gap-1.5">
                    {selectedIndustryIds.map((id) => industries.find((industry) => industry.id === id)).filter(Boolean).map((industry) => (
                      <span key={industry.id} className="inline-flex items-center gap-1 rounded-md bg-primary-50 px-2 py-1 text-xs font-semibold text-primary-700">
                        {industry.name}
                        <button
                          type="button"
                          onClick={() => setSelectedIndustryIds((current) => current.filter((id) => id !== industry.id))}
                          disabled={busy}
                          aria-label={`Remove ${industry.name}`}
                          className="text-primary-400 hover:text-red-500 disabled:opacity-50"
                        >
                          <X className="h-3 w-3" />
                        </button>
                      </span>
                    ))}
                  </div>
                )}
                <button
                  type="button"
                  aria-labelledby="direction-startup-industry-label"
                  aria-expanded={industriesOpen}
                  aria-haspopup="listbox"
                  disabled={busy || industriesLoading || Boolean(industriesError) || industries.length === 0}
                  onClick={() => setIndustriesOpen((open) => !open)}
                  className="flex w-full items-center justify-between gap-3 rounded-lg px-1 py-1 text-left text-sm text-slate-500 outline-none disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <span>{industriesLoading ? 'Loading industries…' : selectedIndustryIds.length >= 3 ? 'Maximum 3 industries selected' : 'Select startup industries'}</span>
                  {industriesLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <ChevronDown className={`h-4 w-4 transition ${industriesOpen ? 'rotate-180' : ''}`} />}
                </button>
              </div>
              {industriesOpen && (
                <div role="listbox" aria-multiselectable="true" aria-labelledby="direction-startup-industry-label" className="relative z-20 mt-1 w-full rounded-xl border border-slate-200 bg-white p-1.5 shadow-lg">
                  {industries.map((industry) => {
                    const selected = selectedIndustryIds.includes(industry.id);
                    const disabled = !selected && selectedIndustryIds.length >= 3;
                    return (
                      <button
                        key={industry.id}
                        type="button"
                        role="option"
                        aria-selected={selected}
                        disabled={busy || disabled}
                        onClick={() => setSelectedIndustryIds((current) => selected
                          ? current.filter((id) => id !== industry.id)
                          : [...current, industry.id])}
                        className={`block w-full rounded-lg px-3 py-2 text-left text-sm transition disabled:cursor-not-allowed disabled:opacity-40 ${selected ? 'bg-primary-50 text-primary-800' : 'text-slate-700 hover:bg-slate-50'}`}
                      >
                        <span className="font-semibold">{industry.name}</span>
                        <span className="text-slate-500"> - {industry.description || 'No description'}</span>
                      </button>
                    );
                  })}
                </div>
              )}
            </div>
            <div className="mt-1 flex items-center justify-between gap-3 text-xs">
              <span className="text-red-600">{!industriesLoading && !industriesError && selectedIndustryIds.length === 0 ? 'Select at least one startup industry.' : ''}</span>
              <span className="text-slate-400">{selectedIndustryIds.length}/3 selected</span>
            </div>
            {industriesError && (
              <div className="mt-2 flex items-center justify-between gap-3 rounded-lg bg-red-50 px-3 py-2 text-xs text-red-700">
                <span>{industriesError}</span>
                <button
                  type="button"
                  onClick={() => {
                    setIndustriesLoading(true);
                    setIndustriesError('');
                    setIndustriesReload((value) => value + 1);
                  }}
                  className="inline-flex shrink-0 items-center gap-1 font-semibold hover:text-red-900"
                >
                  <RefreshCw className="h-3.5 w-3.5" /> Retry
                </button>
              </div>
            )}
          </div>
          <div className="flex flex-wrap items-center justify-between gap-3"><p className={`text-xs ${valid ? 'text-slate-500' : 'text-red-500'}`}>{summary.length}/2000 characters · minimum 20</p><div className="flex gap-2"><button type="button" onClick={save} disabled={busy || !valid || !hasUnsavedChanges} className="inline-flex items-center gap-2 rounded-lg border border-primary-200 px-4 py-2 text-sm font-semibold text-primary disabled:opacity-50">{activeAction === 'save' ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />} {activeAction === 'save' ? 'Saving...' : 'Save draft'}</button>{direction && <button type="button" onClick={submit} disabled={busy || !canSubmit} className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:bg-slate-300 disabled:text-slate-600 disabled:opacity-100">{activeAction === 'submit' ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />} {activeAction === 'submit' ? 'Submitting...' : 'Submit'}</button>}</div></div>
          {submitGuidance && <p role="status" className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">{submitGuidance}</p>}
        </div>
      ) : (
        <div className="mt-4 space-y-3">{direction ? <><div><p className="text-xs font-bold uppercase tracking-wide text-slate-400">Project Name</p><h3 className="mt-1 font-semibold text-slate-900">{direction.title}</h3></div><div><p className="text-xs font-bold uppercase tracking-wide text-slate-400">Project description</p><p className="mt-1 whitespace-pre-wrap text-sm leading-7 text-slate-700">{direction.summary}</p></div>{canEdit && editableState && <p className="rounded-lg bg-primary-50 px-3 py-2 text-sm font-semibold text-primary-700">Click this Project Direction card to update the project information and Startup Industry.</p>}</> : <p className="rounded-xl border border-dashed border-slate-200 bg-slate-50 px-4 py-6 text-center text-sm text-slate-500">Create the project workspace to submit its project direction automatically.</p>}</div>
      )}
      {(!canEdit || !editableState || !editing) && direction?.startupIndustries?.length > 0 && <div className="mt-4"><p className="mb-2 text-xs font-bold uppercase tracking-wide text-slate-400">Startup Industry</p><div className="flex flex-wrap gap-2">{direction.startupIndustries.map(industry => <span key={industry} className="rounded-full border border-primary-100 bg-primary-50 px-2.5 py-1 text-xs font-semibold text-primary-700">{industry}</span>)}</div></div>}
      {direction?.status === 'Submitted' && <div className="mt-4 flex items-center gap-2 text-xs font-medium text-slate-500"><span className="h-2 w-2 rounded-full bg-emerald-500" /> Waiting for lecturer decision · live updates enabled</div>}
      {latestReview && <div role="status" aria-live="polite" className="mt-4 rounded-xl border border-blue-100 bg-blue-50 px-4 py-3"><p className="text-xs font-bold uppercase text-blue-700">Latest lecturer review · {latestReview.toStatus}</p><p className="mt-1 text-sm leading-6 text-blue-900">{latestReview.comment}</p></div>}
    </section>
  );
}
