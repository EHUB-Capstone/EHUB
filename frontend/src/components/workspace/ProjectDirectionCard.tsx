// @ts-nocheck
import { useCallback, useEffect, useRef, useState } from 'react';
import { ArrowRight, FileText, FolderKanban, Loader2, Save, Send } from 'lucide-react';
import toast from 'react-hot-toast';
import { teamApi } from '../../api/teamApi';
import { startupIndustryApi } from '../../api/startupIndustryApi';
import { unwrapApiData } from '../../utils/classMappers';
import { parseApiError } from '../../utils/apiError';
import {
  getProjectDirectionDraftSeed,
  getApprovedProjectProfileDisplay,
  getProjectDirectionDecisionNotice,
  hasUnsavedProjectDirectionChanges,
  hasProjectDirectionChanged,
  isProjectDirectionConcurrencyConflict,
  isProjectProfileAvailable,
} from '../../utils/projectDirectionSync';
import { subscribeProjectDirectionRealtime } from '../../api/projectDirectionRealtime';
import StartupIndustryPicker from './StartupIndustryPicker';
import { PROJECT_DESCRIPTION_MAX_LENGTH, PROJECT_NAME_MAX_LENGTH } from '../../constants/projectProfile';
import { useTranslation } from 'react-i18next';

const IndustryChips = ({ names }: { names?: string[] | null }) => (
  <div className="mt-1 flex flex-wrap gap-2">
    {(names || []).map((name) => <span key={name} className="rounded-full border border-primary-100 bg-primary-50 px-2.5 py-1 text-xs font-semibold text-primary-700">{name}</span>)}
  </div>
);

export default function ProjectDirectionCard({ team, project, canEdit, onOpenProjectProfile }) {
  const { t } = useTranslation('workspace');
  const [direction, setDirection] = useState(null);
  const [title, setTitle] = useState('');
  const [summary, setSummary] = useState('');
  const [industries, setIndustries] = useState([]);
  const [selectedIndustryIds, setSelectedIndustryIds] = useState([]);
  const [industriesLoading, setIndustriesLoading] = useState(true);
  const [industriesError, setIndustriesError] = useState('');
  const [industriesReload, setIndustriesReload] = useState(0);
  const [editing, setEditing] = useState(false);
  const [loading, setLoading] = useState(true);
  const [activeAction, setActiveAction] = useState(null);
  const directionRef = useRef(null);
  const initializedIndustryRevisionRef = useRef('');
  const actionInFlightRef = useRef(false);
  const busy = activeAction !== null;
  const editableState = !direction || ['Draft', 'NeedsRevision'].includes(direction.status);
  const isProfileChangeProposal = Boolean(direction?.isProjectProfileChangeProposal);

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
          setIndustriesError(parseApiError(error, t('createProjectWorkspaceForm.failedToLoadStartupIndustries')).message);
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
    const selectedNames = new Set((direction.proposedStartupIndustries || direction.startupIndustries || []).map((name) => name.trim().toLocaleUpperCase()));
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
        const parsed = parseApiError(error, t('projectDirectionCard.unableToLoadProjectDirection'));
        if (parsed.code === 'PROJECT_DIRECTION_NOT_FOUND') {
          const seed = getProjectDirectionDraftSeed(null, project);
          setTitle(seed.title);
          setSummary(seed.summary);
        } else {
          toast.error(parsed.message);
        }
      } finally {
        if (active) setLoading(false);
      }
    };
    load();
    return () => { active = false; };
  }, [applyDirection, project, team._id]);

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

  // Persists the current form as a draft. Returns the saved direction, or null when the save was not applied.
  const persistDraft = async () => {
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
        toast.error(t('projectDirectionCard.nowStatusKeepChanges', { status: latest?.status || t('projectDirectionCard.anotherState') }));
        return null;
      }
      response = await teamApi.saveProjectDirection(team._id, {
        ...payload,
        rowVersion: latest.rowVersion,
      });
    }
    return unwrapApiData(response);
  };

  const save = async () => {
    if (actionInFlightRef.current) return;
    actionInFlightRef.current = true;
    setActiveAction('save');
    try {
      const value = await persistDraft();
      if (!value) return;
      applyDirection(value);
      toast.success(t('projectDirectionCard.projectDirectionSavedAsDraft'));
    } catch (error) {
      toast.error(parseApiError(error, t('projectDirectionCard.unableToSaveProjectDirection')).message);
    } finally {
      actionInFlightRef.current = false;
      setActiveAction(null);
    }
  };

  const submit = async () => {
    if (actionInFlightRef.current) return;
    actionInFlightRef.current = true;
    setActiveAction('submit');
    try {
      // Unsaved edits are saved as a draft automatically so the user only needs one click.
      let target = direction;
      if (hasUnsavedChanges) {
        target = await persistDraft();
        if (!target) return;
        applyDirection(target);
      }
      if (!target) return;
      let response;
      try {
        response = await teamApi.submitProjectDirection(team._id, target.rowVersion);
      } catch (error) {
        if (!isProjectDirectionConcurrencyConflict(parseApiError(error, '').code)) throw error;
        const latest = unwrapApiData(await teamApi.getProjectDirection(team._id));
        if (latest?.status === 'Submitted') {
          applyDirection(latest);
          toast.success(t('projectDirectionCard.projectDirectionSubmittedForLecturerReview'));
          setEditing(false);
          return;
        }
        if (latest?.status !== 'Draft') {
          toast.error(t('projectDirectionCard.nowStatusReload', { status: latest?.status || t('projectDirectionCard.anotherState') }));
          return;
        }
        response = await teamApi.submitProjectDirection(team._id, latest.rowVersion);
      }
      const value = unwrapApiData(response);
      applyDirection(value);
      toast.success(t('projectDirectionCard.projectDirectionSubmittedForLecturerReview'));
      setEditing(false);
    } catch (error) {
      toast.error(parseApiError(error, t('projectDirectionCard.unableToSubmitProjectDirection')).message);
    } finally {
      actionInFlightRef.current = false;
      setActiveAction(null);
    }
  };

  const selectedIndustryNames = selectedIndustryIds
    .map((id) => industries.find((industry) => industry.id === id)?.name)
    .filter(Boolean);
  const valid = title.trim().length >= 3
    && title.trim().length <= PROJECT_NAME_MAX_LENGTH
    && summary.trim().length >= 20
    && summary.trim().length <= PROJECT_DESCRIPTION_MAX_LENGTH
    && selectedIndustryIds.length >= 1
    && selectedIndustryIds.length <= 3
    && !industriesLoading
    && !industriesError;
  const hasUnsavedChanges = hasUnsavedProjectDirectionChanges(direction, title, summary, selectedIndustryNames);
  // Submit saves pending edits first. A NeedsRevision direction must be changed before it can be resubmitted.
  const canSubmit = valid && (direction?.status === 'NeedsRevision' ? hasUnsavedChanges : true);
  const submitGuidance = direction?.status === 'NeedsRevision' && !hasUnsavedChanges
    ? 'The lecturer requested changes. Update the Project Name, Project description, or Startup Industry, then select Submit.'
    : '';
  const latestReview = direction?.reviews?.[0];
  const approvedProfile = getApprovedProjectProfileDisplay(direction, project);

  const openEditor = async () => {
    if (actionInFlightRef.current) return;
    if (!direction) {
      setEditing(true);
      return;
    }
    actionInFlightRef.current = true;
    setActiveAction('refresh');
    try {
      const latest = unwrapApiData(await teamApi.getProjectDirection(team._id));
      if (!['Draft', 'NeedsRevision'].includes(latest?.status)) {
        applyDirection(latest);
        toast.error(t('projectDirectionCard.nowStatusCannotEdit', { status: latest?.status || t('projectDirectionCard.anotherState') }));
        return;
      }
      applyDirection(latest);
      setEditing(true);
    } catch (error) {
      toast.error(parseApiError(error, t('projectDirectionCard.unableToRefreshProjectDirectionBefore')).message);
    } finally {
      actionInFlightRef.current = false;
      setActiveAction(null);
    }
  };

  if (!loading && isProjectProfileAvailable(direction)) {
    return (
      <button type="button" onClick={onOpenProjectProfile} className="group w-full overflow-hidden rounded-2xl border border-emerald-200 bg-white text-left shadow-sm transition hover:-translate-y-0.5 hover:border-emerald-300 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-emerald-300">
        <div className="flex items-start justify-between gap-4 border-b border-emerald-100 bg-emerald-50/60 px-6 py-4">
          <div className="flex items-center gap-3"><span className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-100 text-emerald-700"><FolderKanban className="h-5 w-5" /></span><div><h2 className="text-lg font-bold text-slate-800">{t('projectProfileEditor.projectProfile')}</h2><p className="mt-0.5 text-xs text-slate-500">{t('projectDirectionCard.keepTheApprovedProjectInformationClear')}</p></div></div>
          <span className="rounded-full border border-emerald-200 bg-white px-2.5 py-1 text-xs font-semibold text-emerald-700">{t('projectProfileEditor.approved')}</span>
        </div>
        <div className="px-6 py-5">
          <div className="flex items-start justify-between gap-4"><div className="min-w-0"><h3 className="truncate font-semibold text-slate-900">{approvedProfile.projectName}</h3><p className="mt-1 line-clamp-2 text-sm leading-6 text-slate-600">{approvedProfile.description}</p></div><ArrowRight className="mt-1 h-5 w-5 shrink-0 text-slate-400 transition group-hover:translate-x-1 group-hover:text-emerald-600" /></div>
          <div className="mt-4 grid gap-3 sm:grid-cols-3">
            {[
              ['Problem', project?.problem],
              ['Solution', project?.solution],
              ['Target users', project?.targetUsers],
            ].map(([label, value]) => (
              <div key={label} className="rounded-xl border border-slate-100 bg-slate-50/70 px-3 py-2.5">
                <p className="text-xs font-bold uppercase tracking-wide text-slate-500">{label}</p>
                <p className="mt-1 line-clamp-2 text-sm leading-5 text-slate-700">{value || t('projectDirectionCard.notDocumentedYet')}</p>
              </div>
            ))}
          </div>
          {latestReview?.comment && (
            <div role="status" aria-live="polite" className="mt-4 rounded-xl border border-blue-100 bg-blue-50 px-4 py-3">
              <p className="text-xs font-bold uppercase text-blue-700">{t('projectProfileEditor.lecturerReview', { toStatus: latestReview.toStatus })}</p>
              <p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-blue-900">{latestReview.comment}</p>
            </div>
          )}
          <p className="mt-3 text-xs font-semibold text-emerald-700">{t(canEdit ? 'projectDirectionCard.openProfileViewOrUpdate' : 'projectDirectionCard.openProfileView')}</p>
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
        <div className="flex items-center gap-2"><FileText className="h-5 w-5 text-primary" /><div><h2 className="text-lg font-bold text-slate-800">{isProfileChangeProposal ? t('projectDirectionCard.projectProfileChangeRequest') : t('projectDirectionCard.projectDirection')}</h2><p className="mt-0.5 text-xs text-slate-500">{isProfileChangeProposal ? t('projectDirectionCard.theApprovedProfileRemainsUnchangedUntil') : t('projectDirectionCard.projectInformationSubmittedToTheAssigned')}</p></div></div>
        <span className="rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-semibold text-slate-600">{direction?.status || t('projectDirectionCard.notCreated')}</span>
      </div>
      {loading ? <div className="flex min-h-40 items-center justify-center"><Loader2 className="h-5 w-5 animate-spin text-primary" /></div> : canEdit && editableState && editing ? (
        <div className="mt-4 space-y-3" aria-busy={busy || undefined}>
          <label className="text-xs font-semibold text-slate-600">{t('projectDirectionCard.projectName')}</label>
          <input value={title} onChange={event => setTitle(event.target.value)} disabled={busy} maxLength={PROJECT_NAME_MAX_LENGTH} placeholder={t('createProjectWorkspaceForm.projectName')} className="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-primary disabled:bg-slate-50 disabled:text-slate-500" />
          <label className="text-xs font-semibold text-slate-600">{t('createProjectWorkspaceForm.projectDescription')}</label>
          <textarea value={summary} onChange={event => setSummary(event.target.value)} disabled={busy} maxLength={PROJECT_DESCRIPTION_MAX_LENGTH} rows={8} placeholder={t('projectDirectionCard.describeTheProject')} className="w-full resize-y rounded-xl border border-slate-200 px-4 py-3 text-sm leading-6 text-slate-700 outline-none focus:border-primary disabled:bg-slate-50 disabled:text-slate-500" />
          <div>
            <label id="direction-startup-industry-label" className="text-xs font-semibold text-slate-600">
              {t('createProjectWorkspaceForm.startupIndustry')} <span className="text-red-500">*</span>
            </label>
            <div className="mt-1.5">
              <StartupIndustryPicker
                industries={industries}
                selectedIds={selectedIndustryIds}
                onChange={setSelectedIndustryIds}
                labelId="direction-startup-industry-label"
                loading={industriesLoading}
                error={industriesError}
                onRetry={() => {
                  setIndustriesLoading(true);
                  setIndustriesError('');
                  setIndustriesReload((value) => value + 1);
                }}
                disabled={busy}
                invalid={selectedIndustryIds.length === 0 && !industriesLoading}
                errorMessage={!industriesLoading && !industriesError && selectedIndustryIds.length === 0 ? t('projectDirectionCard.selectAtLeastOneStartupIndustry') : ''}
              />
            </div>
          </div>
          <div className="flex flex-wrap items-center justify-between gap-3"><p className={`text-xs ${valid ? 'text-slate-500' : 'text-red-500'}`}>{t('projectDirectionCard.charactersMinimum20', { length: summary.length, PROJECT_DESCRIPTION_MAX_LENGTH })}</p><div className="flex gap-2"><button type="button" onClick={save} disabled={busy || !valid || !hasUnsavedChanges} className="inline-flex items-center gap-2 rounded-lg border border-primary-200 px-4 py-2 text-sm font-semibold text-primary-700 disabled:opacity-50">{activeAction === 'save' ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />} {activeAction === 'save' ? t('projectDirectionCard.saving') : t('projectDirectionCard.saveDraft')}</button><button type="button" onClick={submit} disabled={busy || !canSubmit} className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:bg-slate-300 disabled:text-slate-600 disabled:opacity-100">{activeAction === 'submit' ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />} {activeAction === 'submit' ? t('projectDirectionCard.submitting') : t('common:actions.submit')}</button></div></div>
          {submitGuidance && <p role="status" className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">{submitGuidance}</p>}
        </div>
      ) : (
          <div className="mt-4 space-y-3">{direction ? <>{isProfileChangeProposal ? <div className="grid gap-3 lg:grid-cols-2"><div className="rounded-xl border border-slate-200 bg-slate-50 p-4"><p className="text-xs font-bold uppercase tracking-wide text-slate-500">{t('projectDirectionCard.currentApprovedProfile')}</p><p className="mt-3 text-xs font-semibold text-slate-500">{t('createProjectWorkspaceForm.projectName')}</p><p className="mt-1 font-semibold text-slate-900">{direction.currentTitle}</p><p className="mt-3 text-xs font-semibold text-slate-500">{t('taskModal.description')}</p><p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-slate-700">{direction.currentSummary}</p>{direction.proposedStartupIndustries && <><p className="mt-3 text-xs font-semibold text-slate-500">{t('projectProfileEditor.startupIndustries')}</p><IndustryChips names={direction.startupIndustries} /></>}</div><div className="rounded-xl border border-secondary-200 bg-secondary-50 p-4"><p className="text-xs font-bold uppercase tracking-wide text-secondary">{t('projectDirectionCard.proposedChange')}</p><p className="mt-3 text-xs font-semibold text-secondary">{t('createProjectWorkspaceForm.projectName')}</p><p className="mt-1 font-semibold text-slate-900">{direction.title}</p><p className="mt-3 text-xs font-semibold text-secondary">{t('taskModal.description')}</p><p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-slate-700">{direction.summary}</p>{direction.proposedStartupIndustries && <><p className="mt-3 text-xs font-semibold text-secondary">{t('projectProfileEditor.startupIndustries')}</p><IndustryChips names={direction.proposedStartupIndustries} /></>}</div></div> : <><div><p className="text-xs font-bold uppercase tracking-wide text-slate-400">{t('projectDirectionCard.projectName')}</p><h3 className="mt-1 font-semibold text-slate-900">{direction.title}</h3></div><div><p className="text-xs font-bold uppercase tracking-wide text-slate-400">{t('createProjectWorkspaceForm.projectDescription')}</p><p className="mt-1 whitespace-pre-wrap text-sm leading-7 text-slate-700">{direction.summary}</p></div></>}{canEdit && editableState && <p className="rounded-lg bg-primary-50 px-3 py-2 text-sm font-semibold text-primary-700">{t(isProfileChangeProposal ? 'projectDirectionCard.clickToUpdateChangeRequest' : 'projectDirectionCard.clickToUpdateDirection')}</p>}</> : <><div><p className="text-xs font-bold uppercase tracking-wide text-slate-400">{t('projectDirectionCard.importedProject')}</p><h3 className="mt-1 font-semibold text-slate-900">{project?.projectName || t('projectDirectionCard.notProvided')}</h3><p className="mt-2 whitespace-pre-wrap text-sm leading-7 text-slate-700">{project?.description || t('projectDirectionCard.noProjectDescriptionWasImported')}</p></div><p className={`rounded-lg px-3 py-2 text-sm font-semibold ${canEdit ? 'bg-primary-50 text-primary-700' : 'bg-slate-50 text-slate-600'}`}>{canEdit ? t('projectDirectionCard.clickThisCardToCompleteAnd') : t('projectDirectionCard.theTeamLeaderMustCompleteThe')}</p></>}</div>
      )}
      {!editing && project?.zaloGroupUrl && <div className="mt-4 rounded-xl border border-blue-100 bg-blue-50 px-4 py-3"><p className="text-xs font-bold uppercase tracking-wide text-blue-600">{t('projectDirectionCard.zaloGroup')}</p><a href={project.zaloGroupUrl} target="_blank" rel="noreferrer" onClick={event => event.stopPropagation()} className="mt-1 block truncate text-sm font-semibold text-blue-800 underline decoration-blue-300 underline-offset-2">{project.zaloGroupUrl}</a></div>}
      {(!canEdit || !editableState || !editing) && direction?.startupIndustries?.length > 0 && !(isProfileChangeProposal && direction.proposedStartupIndustries) && <div className="mt-4"><p className="mb-2 text-xs font-bold uppercase tracking-wide text-slate-400">{t('createProjectWorkspaceForm.startupIndustry')}</p><div className="flex flex-wrap gap-2">{direction.startupIndustries.map(industry => <span key={industry} className="rounded-full border border-primary-100 bg-primary-50 px-2.5 py-1 text-xs font-semibold text-primary-700">{industry}</span>)}</div></div>}
      {direction?.status === 'Submitted' && <div className="mt-4 flex items-center gap-2 text-xs font-medium text-slate-500"><span className="h-2 w-2 rounded-full bg-emerald-500" /> {isProfileChangeProposal ? t('projectDirectionCard.pendingLecturerApprovalTheCurrentProject') : t('projectDirectionCard.waitingForLecturerDecisionLiveUpdates')}</div>}
      {direction?.status !== 'Submitted' && latestReview && <div role="status" aria-live="polite" className="mt-4 rounded-xl border border-blue-100 bg-blue-50 px-4 py-3"><p className="text-xs font-bold uppercase text-blue-700">{t('projectDirectionCard.latestLecturerReview', { toStatus: latestReview.toStatus })}</p>{latestReview.comment && <p className="mt-1 text-sm leading-6 text-blue-900">{latestReview.comment}</p>}</div>}
    </section>
  );
}
