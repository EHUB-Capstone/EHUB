import { useEffect, useMemo, useState } from 'react';
import { ChevronDown, FolderKanban, Loader2, RefreshCw, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { startupIndustryApi } from '../../api/startupIndustryApi';
import { workspaceApi } from '../../api/workspaceApi';
import {
  resolveWorkspaceCreationDefaults,
  validateProjectWorkspace,
  type ProjectWorkspaceDraft,
  type ProjectWorkspaceErrors,
} from '../../utils/projectWorkspace';
import { parseApiError } from '../../utils/apiError';
import type { StartupIndustryDto } from '../../types/startupIndustries';

interface Props {
  team: { _id?: string; id?: string; teamName?: string; name?: string };
  proposal?: {
    teamName?: string | null;
    projectName?: string | null;
    projectDescription?: string | null;
    description?: string | null;
  } | null;
  classInfo: { classCode?: string; subjectCode?: string; semesterCode?: string };
  onCreated: () => void | Promise<void>;
}

export default function CreateProjectWorkspaceForm({ team, proposal, classInfo, onCreated }: Props) {
  const creationDefaults = resolveWorkspaceCreationDefaults(team, proposal);
  const [draft, setDraft] = useState<ProjectWorkspaceDraft>(creationDefaults.draft);
  const [errors, setErrors] = useState<ProjectWorkspaceErrors>({});
  const [submitting, setSubmitting] = useState(false);
  const [industries, setIndustries] = useState<StartupIndustryDto[]>([]);
  const [industriesLoading, setIndustriesLoading] = useState(true);
  const [industriesError, setIndustriesError] = useState('');
  const [industriesOpen, setIndustriesOpen] = useState(false);
  const [industriesReload, setIndustriesReload] = useState(0);
  const teamId = String(team._id || team.id || '');
  const contextLabel = useMemo(() => [classInfo.classCode, classInfo.subjectCode, classInfo.semesterCode].filter(Boolean).join(' · '), [classInfo]);
  const selectedIndustries = useMemo(
    () => draft.startupIndustryIds
      .map((id) => industries.find((industry) => industry.id === id))
      .filter((industry): industry is StartupIndustryDto => Boolean(industry)),
    [draft.startupIndustryIds, industries],
  );

  useEffect(() => {
    const controller = new AbortController();
    startupIndustryApi.getActiveOptions(controller.signal)
      .then((response) => {
        const payload = response?.data ?? response;
        setIndustries(Array.isArray(payload?.industries) ? payload.industries : []);
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

  const setField = <K extends keyof ProjectWorkspaceDraft>(field: K, value: ProjectWorkspaceDraft[K]) => {
    setDraft((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  };

  const submit = async () => {
    const nextErrors = validateProjectWorkspace(draft);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      toast.error('Please complete the required project information.');
      return;
    }
    setSubmitting(true);
    try {
      await workspaceApi.createWorkspace(teamId, {
        projectName: draft.projectName.trim(),
        description: draft.description.trim(),
        startupIndustryIds: draft.startupIndustryIds,
      });
      toast.success('Workspace created and project direction submitted for lecturer review.');
      await onCreated();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to create project workspace.').message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="mx-auto max-w-3xl rounded-2xl border border-slate-200 bg-white shadow-sm">
      <div className="flex items-start gap-3 border-b border-slate-100 px-5 py-4">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary-50 text-primary"><FolderKanban className="h-5 w-5" /></div>
        <div>
          <h2 className="text-lg font-bold text-slate-900">Create project workspace</h2>
          <p className="mt-0.5 text-xs text-slate-500">{contextLabel || 'This workspace will be linked to your team’s academic context.'}</p>
        </div>
      </div>
      <div className="grid gap-4 p-5 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <label htmlFor="workspace-team-name" className="mb-1.5 block text-xs font-semibold text-slate-700">Team name</label>
          <input id="workspace-team-name" value={creationDefaults.teamName} readOnly aria-readonly="true" className="w-full cursor-not-allowed rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 text-sm text-slate-600 outline-none" />
          <p className="mt-1 text-xs text-slate-400">Team name comes from the submitted team proposal.</p>
        </div>
        <div className="sm:col-span-2">
          <label htmlFor="workspace-project-name" className="mb-1.5 block text-xs font-semibold text-slate-700">Project name <span className="text-red-500">*</span></label>
          <input id="workspace-project-name" value={draft.projectName} onChange={(event) => setField('projectName', event.target.value)} maxLength={200} className={`w-full rounded-xl border px-3 py-2.5 text-sm outline-none focus:ring-2 focus:ring-primary/15 ${errors.projectName ? 'border-red-300' : 'border-slate-200 focus:border-primary'}`} />
          {errors.projectName && <p className="mt-1 text-xs text-red-600">{errors.projectName}</p>}
        </div>
        <div className="sm:col-span-2">
          <label htmlFor="workspace-description" className="mb-1.5 block text-xs font-semibold text-slate-700">Project description <span className="text-red-500">*</span></label>
          <textarea id="workspace-description" value={draft.description} onChange={(event) => setField('description', event.target.value)} rows={4} maxLength={2000} placeholder="Describe the problem, target users, and initial solution…" className={`w-full resize-none rounded-xl border px-3 py-2.5 text-sm outline-none focus:ring-2 focus:ring-primary/15 ${errors.description ? 'border-red-300' : 'border-slate-200 focus:border-primary'}`} />
          <div className="mt-1 flex justify-between text-xs"><span className="text-red-600">{errors.description}</span><span className="text-slate-400">{draft.description.length}/2000</span></div>
        </div>
        <div className="sm:col-span-2">
          <label id="startup-industry-label" className="mb-1.5 block text-xs font-semibold text-slate-700">
            Startup Industry <span className="text-red-500">*</span>
          </label>
          <div className="relative">
            <div className={`rounded-xl border bg-white p-2 transition focus-within:ring-2 focus-within:ring-primary/15 ${errors.startupIndustryIds ? 'border-red-300' : 'border-slate-200 focus-within:border-primary'}`}>
              {selectedIndustries.length > 0 && (
                <div className="mb-2 flex flex-wrap gap-1.5">
                  {selectedIndustries.map((industry) => (
                    <span key={industry.id} className="inline-flex items-center gap-1 rounded-md bg-primary-50 px-2 py-1 text-xs font-semibold text-primary-700">
                      {industry.name}
                      <button
                        type="button"
                        onClick={() => setField('startupIndustryIds', draft.startupIndustryIds.filter((id) => id !== industry.id))}
                        aria-label={`Remove ${industry.name}`}
                        className="text-primary-400 hover:text-red-500"
                      >
                        <X className="h-3 w-3" />
                      </button>
                    </span>
                  ))}
                </div>
              )}
              <button
                type="button"
                aria-labelledby="startup-industry-label"
                aria-expanded={industriesOpen}
                aria-haspopup="listbox"
                disabled={industriesLoading || Boolean(industriesError) || industries.length === 0}
                onClick={() => setIndustriesOpen((open) => !open)}
                className="flex w-full items-center justify-between gap-3 rounded-lg px-1 py-1 text-left text-sm text-slate-500 outline-none disabled:cursor-not-allowed disabled:opacity-60"
              >
                <span>{industriesLoading ? 'Loading industries…' : selectedIndustries.length >= 3 ? 'Maximum 3 industries selected' : 'Select startup industries'}</span>
                {industriesLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <ChevronDown className={`h-4 w-4 transition ${industriesOpen ? 'rotate-180' : ''}`} />}
              </button>
            </div>
            {industriesOpen && (
              <div role="listbox" aria-multiselectable="true" aria-labelledby="startup-industry-label" className="relative z-20 mt-1 w-full rounded-xl border border-slate-200 bg-white p-1.5 shadow-lg">
                {industries.map((industry) => {
                  const selected = draft.startupIndustryIds.includes(industry.id);
                  const disabled = !selected && draft.startupIndustryIds.length >= 3;
                  return (
                    <button
                      key={industry.id}
                      type="button"
                      role="option"
                      aria-selected={selected}
                      disabled={disabled}
                      onClick={() => setField(
                        'startupIndustryIds',
                        selected
                          ? draft.startupIndustryIds.filter((id) => id !== industry.id)
                          : [...draft.startupIndustryIds, industry.id],
                      )}
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
            <span className="text-red-600">{errors.startupIndustryIds}</span>
            <span className="text-slate-400">{draft.startupIndustryIds.length}/3 selected</span>
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
          {!industriesLoading && !industriesError && industries.length === 0 && (
            <p className="mt-2 text-xs text-amber-700">No active startup industries are available. Ask an administrator to create or activate one.</p>
          )}
        </div>
      </div>
      <div className="flex items-center justify-between gap-3 border-t border-slate-100 bg-slate-50/70 px-5 py-4">
        <p className="text-xs text-slate-500">Creating the workspace also submits this information to the lecturer for review.</p>
        <button type="button" disabled={submitting} onClick={submit} className="inline-flex items-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-sm font-bold text-white shadow-sm transition hover:bg-primary-600 disabled:cursor-not-allowed disabled:opacity-60">
          {submitting && <Loader2 className="h-4 w-4 animate-spin" />} Create workspace & submit
        </button>
      </div>
    </div>
  );
}
