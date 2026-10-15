import { useEffect, useMemo, useState } from 'react';
import { FolderKanban, Loader2 } from 'lucide-react';
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
import StartupIndustryPicker from './StartupIndustryPicker';

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
  const [industriesReload, setIndustriesReload] = useState(0);
  const teamId = String(team._id || team.id || '');
  const contextLabel = useMemo(() => [classInfo.classCode, classInfo.subjectCode, classInfo.semesterCode].filter(Boolean).join(' · '), [classInfo]);

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
      toast.error('Please complete the required team and project information.');
      return;
    }
    setSubmitting(true);
    try {
      await workspaceApi.createWorkspace(teamId, {
        teamName: draft.teamName.trim(),
        projectName: draft.projectName.trim(),
        description: draft.description.trim(),
        zaloGroupUrl: draft.zaloGroupUrl.trim(),
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
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary-50 text-primary-700"><FolderKanban className="h-5 w-5" /></div>
        <div>
          <h2 className="text-lg font-bold text-slate-900">Create project workspace</h2>
          <p className="mt-0.5 text-xs text-slate-500">{contextLabel || 'This workspace will be linked to your team’s academic context.'}</p>
        </div>
      </div>
      <div className="grid gap-4 p-5 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <label htmlFor="workspace-team-name" className="mb-1.5 block text-xs font-semibold text-slate-700">Team name <span className="text-red-500">*</span></label>
          <input id="workspace-team-name" value={draft.teamName} onChange={(event) => setField('teamName', event.target.value)} maxLength={100} className={`w-full rounded-xl border px-3 py-2.5 text-sm outline-none focus:ring-2 focus:ring-primary/15 ${errors.teamName ? 'border-red-300' : 'border-slate-200 focus:border-primary'}`} />
          <p className="mt-1 text-xs text-slate-400">Pre-filled from your team. You can change it before creating the workspace.</p>
          {errors.teamName && <p className="mt-1 text-xs text-red-600">{errors.teamName}</p>}
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
          <label htmlFor="workspace-zalo-group-url" className="mb-1.5 block text-xs font-semibold text-slate-700">Zalo group link <span className="text-red-500">*</span></label>
          <input id="workspace-zalo-group-url" type="url" value={draft.zaloGroupUrl} onChange={(event) => setField('zaloGroupUrl', event.target.value)} maxLength={500} placeholder="https://zalo.me/g/..." className={`w-full rounded-xl border px-3 py-2.5 text-sm outline-none focus:ring-2 focus:ring-primary/15 ${errors.zaloGroupUrl ? 'border-red-300' : 'border-slate-200 focus:border-primary'}`} />
          <p className="mt-1 text-xs text-slate-400">This link will be available and editable in Project Profile.</p>
          {errors.zaloGroupUrl && <p className="mt-1 text-xs text-red-600">{errors.zaloGroupUrl}</p>}
        </div>
        <div className="sm:col-span-2">
          <label id="startup-industry-label" className="mb-1.5 block text-xs font-semibold text-slate-700">
            Startup Industry <span className="text-red-500">*</span>
          </label>
          <StartupIndustryPicker
            industries={industries}
            selectedIds={draft.startupIndustryIds}
            onChange={(ids) => setField('startupIndustryIds', ids)}
            labelId="startup-industry-label"
            loading={industriesLoading}
            error={industriesError}
            onRetry={() => {
              setIndustriesLoading(true);
              setIndustriesError('');
              setIndustriesReload((value) => value + 1);
            }}
            invalid={Boolean(errors.startupIndustryIds)}
            errorMessage={errors.startupIndustryIds}
          />
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
