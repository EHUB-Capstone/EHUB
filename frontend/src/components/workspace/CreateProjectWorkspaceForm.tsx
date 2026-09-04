import { useMemo, useState } from 'react';
import { FolderKanban, Loader2, Plus, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { workspaceApi } from '../../api/workspaceApi';
import {
  appendWorkspaceTag,
  validateProjectWorkspace,
  type ProjectWorkspaceDraft,
  type ProjectWorkspaceErrors,
} from '../../utils/projectWorkspace';
import { parseApiError } from '../../utils/apiError';

interface Props {
  team: { _id?: string; id?: string; teamName?: string; name?: string };
  classInfo: { classCode?: string; subjectCode?: string; semesterCode?: string };
  onCreated: () => void | Promise<void>;
}

interface TagInputProps {
  label: string;
  required?: boolean;
  values: string[];
  placeholder: string;
  error?: string;
  onChange: (values: string[]) => void;
}

function TagInput({ label, required = false, values, placeholder, error, onChange }: TagInputProps) {
  const [input, setInput] = useState('');
  const [localError, setLocalError] = useState('');

  const add = () => {
    const result = appendWorkspaceTag(values, input);
    if (result.error) {
      setLocalError(result.error);
      return;
    }
    onChange(result.values);
    setInput('');
    setLocalError('');
  };

  return (
    <div>
      <label className="mb-1.5 block text-xs font-semibold text-slate-700">
        {label}{required && <span className="text-red-500"> *</span>}
      </label>
      <div className={`rounded-xl border bg-white p-2 transition focus-within:ring-2 focus-within:ring-primary/15 ${error || localError ? 'border-red-300' : 'border-slate-200 focus-within:border-primary'}`}>
        {values.length > 0 && (
          <div className="mb-2 flex flex-wrap gap-1.5">
            {values.map((value) => (
              <span key={value} className="inline-flex items-center gap-1 rounded-md bg-slate-100 px-2 py-1 text-xs font-medium text-slate-700">
                {value}
                <button type="button" onClick={() => onChange(values.filter((item) => item !== value))} aria-label={`Remove ${value}`} className="text-slate-400 hover:text-red-500">
                  <X className="h-3 w-3" />
                </button>
              </span>
            ))}
          </div>
        )}
        <div className="flex gap-2">
          <input
            value={input}
            onChange={(event) => { setInput(event.target.value); setLocalError(''); }}
            onKeyDown={(event) => {
              if (event.key === 'Enter' || event.key === ',') {
                event.preventDefault();
                add();
              }
            }}
            onBlur={add}
            placeholder={placeholder}
            maxLength={50}
            className="min-w-0 flex-1 border-0 px-1 py-1 text-sm outline-none"
          />
          <button type="button" onMouseDown={(event) => event.preventDefault()} onClick={add} className="inline-flex h-8 w-8 items-center justify-center rounded-lg bg-slate-100 text-slate-600 hover:bg-primary-50 hover:text-primary" aria-label={`Add ${label}`}>
            <Plus className="h-4 w-4" />
          </button>
        </div>
      </div>
      {(localError || error) && <p className="mt-1 text-xs text-red-600">{localError || error}</p>}
    </div>
  );
}

export default function CreateProjectWorkspaceForm({ team, classInfo, onCreated }: Props) {
  const [draft, setDraft] = useState<ProjectWorkspaceDraft>({
    projectName: team.teamName || team.name || '',
    description: '',
    startupField: '',
    technologyStack: [],
    keywords: [],
  });
  const [errors, setErrors] = useState<ProjectWorkspaceErrors>({});
  const [submitting, setSubmitting] = useState(false);
  const teamId = String(team._id || team.id || '');
  const contextLabel = useMemo(() => [classInfo.classCode, classInfo.subjectCode, classInfo.semesterCode].filter(Boolean).join(' · '), [classInfo]);

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
        startupField: draft.startupField.trim(),
        technologyStack: draft.technologyStack,
        keywords: draft.keywords,
      });
      toast.success('Project workspace created successfully.');
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
          <label htmlFor="workspace-startup-field" className="mb-1.5 block text-xs font-semibold text-slate-700">Startup field <span className="text-red-500">*</span></label>
          <input id="workspace-startup-field" value={draft.startupField} onChange={(event) => setField('startupField', event.target.value)} maxLength={100} placeholder="EdTech, FinTech, GreenTech…" className={`w-full rounded-xl border px-3 py-2.5 text-sm outline-none focus:ring-2 focus:ring-primary/15 ${errors.startupField ? 'border-red-300' : 'border-slate-200 focus:border-primary'}`} />
          {errors.startupField && <p className="mt-1 text-xs text-red-600">{errors.startupField}</p>}
        </div>
        <TagInput label="Technology stack" required values={draft.technologyStack} onChange={(values) => setField('technologyStack', values)} placeholder="React, .NET, PostgreSQL…" error={errors.technologyStack} />
        <TagInput label="Keywords" values={draft.keywords} onChange={(values) => setField('keywords', values)} placeholder="education, marketplace…" error={errors.keywords} />
      </div>
      <div className="flex items-center justify-between gap-3 border-t border-slate-100 bg-slate-50/70 px-5 py-4">
        <p className="text-xs text-slate-500">Only one active workspace is allowed per team.</p>
        <button type="button" disabled={submitting} onClick={submit} className="inline-flex items-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-sm font-bold text-white shadow-sm transition hover:bg-primary-600 disabled:cursor-not-allowed disabled:opacity-60">
          {submitting && <Loader2 className="h-4 w-4 animate-spin" />} Create workspace
        </button>
      </div>
    </div>
  );
}
