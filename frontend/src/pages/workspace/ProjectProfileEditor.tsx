import { useCallback, useEffect, useState } from 'react';
import { ArrowLeft, FolderKanban, Loader2, LockKeyhole, Save } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { workspaceApi } from '../../api/workspaceApi';
import { teamApi } from '../../api/teamApi';
import { useAuth } from '../../hooks/useAuth';
import { parseApiError } from '../../utils/apiError';
import { unwrapApiData } from '../../utils/classMappers';
import {
  hasPersistedProjectProfile,
  validateProjectProfile,
  type ProjectProfileDraft,
  type ProjectProfileErrors,
} from '../../utils/projectWorkspace';
import { isProjectProfileAvailable, type ProjectDirectionSyncValue } from '../../utils/projectDirectionSync';
import type { ProjectWorkspaceDetail, ProjectWorkspaceProfile } from '../../types/projectWorkspace';

const emptyDraft: ProjectProfileDraft = {
  projectName: '',
  description: '',
  problem: '',
  solution: '',
  targetUsers: '',
};

const toDraft = (project: ProjectWorkspaceProfile): ProjectProfileDraft => ({
  projectName: project.projectName || '',
  description: project.description || '',
  problem: project.problem || '',
  solution: project.solution || '',
  targetUsers: project.targetUsers || '',
});

const userIdOf = (value: ProjectWorkspaceDetail['members'][number]['userId']): string => {
  if (!value) return '';
  if (typeof value === 'string') return value;
  return value._id || value.id || '';
};

type FieldProps = {
  id: keyof ProjectProfileDraft;
  label: string;
  value: string;
  error?: string;
  maxLength: number;
  multiline?: boolean;
  readOnly: boolean;
  onChange: (field: keyof ProjectProfileDraft, value: string) => void;
};

function ProfileField({ id, label, value, error, maxLength, multiline = false, readOnly, onChange }: FieldProps) {
  const classes = `mt-1.5 w-full rounded-xl border px-3 py-2.5 text-sm leading-6 outline-none transition focus:ring-2 focus:ring-primary/15 disabled:bg-slate-50 disabled:text-slate-600 ${error ? 'border-red-300' : 'border-slate-200 focus:border-primary'}`;
  return (
    <div>
      <label htmlFor={`project-profile-${id}`} className="text-sm font-semibold text-slate-700">{label} <span className="text-red-500">*</span></label>
      {multiline ? (
        <textarea id={`project-profile-${id}`} value={value} onChange={(event) => onChange(id, event.target.value)} disabled={readOnly} rows={5} maxLength={maxLength} aria-invalid={Boolean(error)} aria-describedby={error ? `project-profile-${id}-error` : undefined} className={`${classes} resize-y`} />
      ) : (
        <input id={`project-profile-${id}`} value={value} onChange={(event) => onChange(id, event.target.value)} disabled={readOnly} maxLength={maxLength} aria-invalid={Boolean(error)} aria-describedby={error ? `project-profile-${id}-error` : undefined} className={classes} />
      )}
      <div className="mt-1 flex min-h-5 justify-between gap-3 text-xs">
        <span id={`project-profile-${id}-error`} className="text-red-600">{error}</span>
        <span className="shrink-0 text-slate-400">{value.length}/{maxLength}</span>
      </div>
    </div>
  );
}

export default function ProjectProfileEditor() {
  const { teamId = '' } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [workspace, setWorkspace] = useState<ProjectWorkspaceDetail | null>(null);
  const [direction, setDirection] = useState<ProjectDirectionSyncValue | null>(null);
  const [draft, setDraft] = useState<ProjectProfileDraft>(emptyDraft);
  const [errors, setErrors] = useState<ProjectProfileErrors>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [loadError, setLoadError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError('');
    try {
      const [workspaceResponse, directionResponse] = await Promise.all([
        workspaceApi.getTeamWorkspace(teamId),
        teamApi.getProjectDirection(teamId),
      ]);
      const detail = unwrapApiData<ProjectWorkspaceDetail>(workspaceResponse);
      const latestDirection = unwrapApiData<ProjectDirectionSyncValue>(directionResponse);
      if (!detail.project) throw new Error('This team does not have a project profile.');
      setWorkspace(detail);
      setDirection(latestDirection);
      setDraft(toDraft(detail.project));
    } catch (error) {
      setLoadError(parseApiError(error, 'Unable to load the project profile.').message);
    } finally {
      setLoading(false);
    }
  }, [teamId]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  const currentUserId = String(user?._id || user?.id || '');
  const currentMember = workspace?.members.find((member) => userIdOf(member.userId) === currentUserId);
  const isLeader = Boolean(currentMember && String(currentMember.studentId) === String(workspace?.team.leaderId || ''));
  const isApproved = isProjectProfileAvailable(direction);
  const canEdit = isApproved && isLeader;
  const workspacePath = user?.role === 'STUDENT' ? `/student/workspace/${teamId}` : `/workspace/teams/${teamId}`;
  const originalDraft = workspace?.project ? toDraft(workspace.project) : emptyDraft;
  const hasChanges = JSON.stringify(draft) !== JSON.stringify(originalDraft);

  const setField = (field: keyof ProjectProfileDraft, value: string) => {
    setDraft((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  };

  const save = async () => {
    const nextErrors = validateProjectProfile(draft);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      toast.error('Please correct the highlighted project information.');
      return;
    }
    if (!canEdit || !workspace?.project || saving) return;
    setSaving(true);
    try {
      const payload = {
        projectName: draft.projectName.trim(),
        description: draft.description.trim(),
        problem: draft.problem.trim(),
        solution: draft.solution.trim(),
        targetUsers: draft.targetUsers.trim(),
        keywords: workspace.project.keywords || [],
      };
      const response = await workspaceApi.updateWorkspaceProfile(teamId, payload);
      const updated = unwrapApiData<ProjectWorkspaceProfile>(response);
      if (!hasPersistedProjectProfile(payload, updated)) {
        throw new Error('The server response did not contain all saved project profile fields.');
      }
      setWorkspace((current) => current ? { ...current, project: updated } : current);
      setDraft(toDraft(updated));
      toast.success('Project profile updated successfully.');
    } catch (error) {
      toast.error(parseApiError(error, 'The server did not save all project profile fields. Please retry after the API is updated.').message);
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <div className="flex min-h-[60vh] items-center justify-center" role="status"><Loader2 className="h-8 w-8 animate-spin text-primary" /></div>;

  if (loadError || !workspace?.project) {
    return <div className="mx-auto max-w-xl rounded-2xl border border-red-100 bg-white p-8 text-center shadow-sm"><h1 className="text-xl font-bold text-slate-900">Unable to open Project Profile</h1><p className="mt-2 text-sm text-slate-600">{loadError}</p><div className="mt-5 flex justify-center gap-2"><button type="button" onClick={() => navigate(workspacePath)} className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold">Back to workspace</button><button type="button" onClick={() => void load()} className="rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-white">Retry</button></div></div>;
  }

  if (!isApproved) {
    return <div className="mx-auto max-w-xl rounded-2xl border border-amber-200 bg-amber-50 p-8 text-center"><LockKeyhole className="mx-auto h-8 w-8 text-amber-600" /><h1 className="mt-3 text-xl font-bold text-slate-900">Project Profile is not available yet</h1><p className="mt-2 text-sm text-slate-600">The Project Direction must be approved by the assigned lecturer first.</p><button type="button" onClick={() => navigate(workspacePath)} className="mt-5 rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-white">Back to workspace</button></div>;
  }

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <button type="button" onClick={() => navigate(workspacePath)} className="inline-flex items-center gap-2 text-sm font-semibold text-slate-600 hover:text-primary"><ArrowLeft className="h-4 w-4" /> Back to workspace</button>
      <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <header className="flex flex-col gap-3 border-b border-slate-100 bg-slate-50/60 px-6 py-5 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex gap-3"><span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-primary-50 text-primary"><FolderKanban className="h-5 w-5" /></span><div><h1 className="text-xl font-bold text-slate-900">Project Profile</h1><p className="mt-1 text-sm text-slate-500">Keep the project name, description, problem, solution, and target users clear and up to date.</p></div></div>
          <span className="self-start rounded-full bg-emerald-50 px-3 py-1 text-xs font-bold text-emerald-700">Approved</span>
        </header>

        {!isLeader && <div className="mx-6 mt-5 flex items-start gap-2 rounded-xl border border-blue-100 bg-blue-50 px-4 py-3 text-sm text-blue-800"><LockKeyhole className="mt-0.5 h-4 w-4 shrink-0" /><p>You can view the latest profile. Only the team leader can update it.</p></div>}

        <div className="grid gap-5 p-6">
          <ProfileField id="projectName" label="Project name" value={draft.projectName} error={errors.projectName} maxLength={200} readOnly={!canEdit} onChange={setField} />
          <ProfileField id="description" label="Description" value={draft.description} error={errors.description} maxLength={2000} multiline readOnly={!canEdit} onChange={setField} />
          <ProfileField id="problem" label="Problem" value={draft.problem} error={errors.problem} maxLength={2000} multiline readOnly={!canEdit} onChange={setField} />
          <ProfileField id="solution" label="Solution" value={draft.solution} error={errors.solution} maxLength={2000} multiline readOnly={!canEdit} onChange={setField} />
          <ProfileField id="targetUsers" label="Target users" value={draft.targetUsers} error={errors.targetUsers} maxLength={2000} multiline readOnly={!canEdit} onChange={setField} />
        </div>

        {canEdit && <footer className="flex items-center justify-between gap-3 border-t border-slate-100 bg-slate-50/70 px-6 py-4"><p className="text-xs text-slate-500">Changes are immediately visible to authorized workspace viewers.</p><button type="button" onClick={() => void save()} disabled={saving || !hasChanges} className="inline-flex items-center gap-2 rounded-xl bg-primary px-5 py-2.5 text-sm font-bold text-white disabled:cursor-not-allowed disabled:opacity-50">{saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />} {saving ? 'Saving…' : 'Save profile'}</button></footer>}
      </section>
    </div>
  );
}
