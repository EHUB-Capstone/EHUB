import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { AlertTriangle, ArrowLeft, FileText, Loader2, Save, Send } from 'lucide-react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { teamWorkspaceApi } from '../../api/teamWorkspaceApi';
import { workspaceApi } from '../../api/workspaceApi';
import Button from '../../components/ui/Button';
import { useAuth } from '../../hooks/useAuth';
import type { ProjectProposal, ProjectProposalDraft } from '../../types/projectProposal';
import type { ProjectWorkspaceDetail } from '../../types/projectWorkspace';
import type { WorkspaceAccessMode, WorkspaceOption } from '../../types/workspaceTools';
import { parseApiError } from '../../utils/apiError';
import { unwrapApiData } from '../../utils/classMappers';
import {
  emptyProjectProposalDraft,
  projectProposalFields,
  projectProposalStatusLabel,
  proposalHasContentChanges,
  toProjectProposalContent,
  toProjectProposalDraft,
  validateProjectProposalDraft,
  validateProjectProposalSubmission,
  type ProjectProposalFieldErrors,
} from '../../utils/projectProposal';
import ProposalPreview from './ProposalPreview';
import ProposalReviewPanel from './ProposalReviewPanel';
import VersionHistory from './VersionHistory';

type WorkspaceContext = {
  selectedWorkspace: WorkspaceOption;
  availableWorkspaces: WorkspaceOption[];
  accessMode: WorkspaceAccessMode;
};

function memberUserId(member: ProjectWorkspaceDetail['members'][number]): string {
  if (!member.userId) return '';
  if (typeof member.userId === 'string') return member.userId;
  return member.userId._id || member.userId.id || '';
}

function Field({
  field,
  value,
  error,
  disabled,
  onChange,
}: {
  field: (typeof projectProposalFields)[number];
  value: string;
  error?: string;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  const controlClass = `mt-1.5 w-full rounded-xl border bg-white px-3 py-2.5 text-sm leading-6 text-slate-800 outline-none transition focus:ring-2 focus:ring-primary/15 disabled:bg-slate-50 disabled:text-slate-500 ${error ? 'border-red-300 focus:border-red-400' : 'border-slate-200 focus:border-primary'}`;
  return (
    <div>
      <label htmlFor={`proposal-${field.name}`} className="text-sm font-semibold text-slate-700">{field.label}{field.submitMinimum > 0 && <span className="ml-1 text-red-500">*</span>}</label>
      {field.type === 'textarea' ? (
        <textarea id={`proposal-${field.name}`} value={value} onChange={(event) => onChange(event.target.value)} disabled={disabled} maxLength={field.maxLength} rows={5} placeholder={field.placeholder} aria-invalid={Boolean(error)} aria-describedby={error ? `proposal-${field.name}-error` : undefined} className={`${controlClass} resize-y`} />
      ) : (
        <input id={`proposal-${field.name}`} value={value} onChange={(event) => onChange(event.target.value)} disabled={disabled} maxLength={field.maxLength} placeholder={field.placeholder} aria-invalid={Boolean(error)} aria-describedby={error ? `proposal-${field.name}-error` : undefined} className={controlClass} />
      )}
      <div className="mt-1 flex min-h-5 justify-between gap-3 text-xs"><span id={`proposal-${field.name}-error`} className="text-red-600">{error}</span><span className="shrink-0 text-slate-400">{value.length}/{field.maxLength}</span></div>
    </div>
  );
}

export default function ProposalEditor() {
  const { teamId: routeTeamId } = useParams();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { user } = useAuth();
  const [teamId, setTeamId] = useState(routeTeamId || '');
  const [workspace, setWorkspace] = useState<ProjectWorkspaceDetail | null>(null);
  const [proposal, setProposal] = useState<ProjectProposal | null>(null);
  const [accessMode, setAccessMode] = useState<WorkspaceAccessMode>('READ_ONLY');
  const [form, setForm] = useState<ProjectProposalDraft>({ ...emptyProjectProposalDraft });
  const [errors, setErrors] = useState<ProjectProposalFieldErrors>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [loadError, setLoadError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError('');
    try {
      const contextResponse = routeTeamId
        ? await teamWorkspaceApi.getWorkspaceContext(routeTeamId)
        : await teamWorkspaceApi.getCurrentWorkspace();
      const context = unwrapApiData<WorkspaceContext | null>(contextResponse);
      const resolvedTeamId = context?.selectedWorkspace?.teamId || routeTeamId || '';
      if (!context || !resolvedTeamId) throw new Error('No team workspace is available for this account.');

      setTeamId(resolvedTeamId);
      setAccessMode(context.accessMode || context.selectedWorkspace.accessMode);
      const workspaceResponse = await workspaceApi.getTeamWorkspace(resolvedTeamId);
      const workspaceDetail = unwrapApiData<ProjectWorkspaceDetail>(workspaceResponse);
      if (!workspaceDetail) throw new Error('The team workspace could not be loaded.');
      setWorkspace(workspaceDetail);

      try {
        const proposalResponse = await workspaceApi.getProposal(resolvedTeamId);
        const detailedProposal = unwrapApiData<ProjectProposal>(proposalResponse);
        setProposal(detailedProposal);
        setForm(toProjectProposalDraft(detailedProposal));
      } catch (error) {
        const parsed = parseApiError(error, 'Unable to load the detailed project proposal.');
        if (parsed.code !== 'PROJECT_PROPOSAL_NOT_FOUND') throw error;
        setProposal(null);
        setForm({ ...emptyProjectProposalDraft });
      }
      setErrors({});
    } catch (error) {
      setLoadError(parseApiError(error, error instanceof Error ? error.message : 'Unable to load the detailed project proposal.').message);
    } finally {
      setLoading(false);
    }
  }, [routeTeamId]);

  useEffect(() => { void load(); }, [load]);

  const currentUserId = String(user?._id || user?.id || '');
  const currentMember = workspace?.members.find((member) => memberUserId(member) === currentUserId);
  const isTeamMember = Boolean(currentMember);
  const isTeamLeader = Boolean(currentMember && String(currentMember.studentId) === String(workspace?.team.leaderId || ''));
  const role = String(user?.role || '').toUpperCase();
  const editableStatus = !proposal || proposal.status === 'Draft' || proposal.status === 'NeedsRevision';
  const canEdit = accessMode !== 'READ_ONLY' && role === 'STUDENT' && isTeamMember && editableStatus;
  const canSubmit = canEdit && isTeamLeader && proposal?.status === 'Draft';
  const canReview = role === 'LECTURER' && proposal?.status === 'Submitted';
  const previewOnly = searchParams.get('preview') === 'true' || !canEdit;
  const hasChanges = proposalHasContentChanges(proposal, form);
  const workspacePath = role === 'STUDENT'
    ? (routeTeamId ? `/student/workspace/${teamId}` : '/student/workspace')
    : `/workspace/teams/${teamId}`;

  useEffect(() => {
    if (!canEdit || !hasChanges) return undefined;
    const warn = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [canEdit, hasChanges]);

  const setField = (field: keyof ProjectProposalDraft, value: string) => {
    setForm((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  };

  const applyProposal = (updated: ProjectProposal) => {
    setProposal(updated);
    setForm(toProjectProposalDraft(updated));
    setErrors({});
  };

  const save = async (event?: FormEvent) => {
    event?.preventDefault();
    if (!canEdit || saving || submitting) return;
    const validationErrors = validateProjectProposalDraft(form);
    setErrors(validationErrors);
    if (Object.keys(validationErrors).length > 0) {
      toast.error('Please correct the highlighted proposal fields.');
      return;
    }
    if (proposal && !hasChanges) {
      toast('There are no proposal content changes to save.');
      return;
    }

    setSaving(true);
    try {
      const content = toProjectProposalContent(form);
      const response = proposal
        ? await workspaceApi.updateProposal(proposal.id, {
            ...content,
            rowVersion: proposal.rowVersion,
            changeNote: form.changeNote.trim() || 'Updated proposal draft.',
          })
        : await workspaceApi.createProposal(teamId, {
            ...content,
            changeNote: form.changeNote.trim() || 'Initial proposal draft.',
          });
      const updated = unwrapApiData<ProjectProposal>(response);
      if (!updated) throw new Error('The server did not return the saved proposal.');
      applyProposal(updated);
      toast.success(proposal ? 'Proposal draft version saved.' : 'Proposal draft created.');
    } catch (error) {
      const parsed = parseApiError(error, 'Unable to save the proposal draft.');
      if (parsed.code === 'PROJECT_PROPOSAL_CONCURRENCY_CONFLICT') {
        toast.error('Another team member changed this proposal. Your text remains on screen; copy it if needed, then reload the latest version.');
      } else {
        setErrors((current) => ({ ...current, ...parsed.fieldErrors }));
        toast.error(parsed.message);
      }
    } finally {
      setSaving(false);
    }
  };

  const submit = async () => {
    if (!proposal || !canSubmit || submitting || saving) return;
    if (hasChanges) {
      toast.error('Save the current changes as a draft version before submitting.');
      return;
    }
    const validationErrors = validateProjectProposalSubmission(proposal);
    setErrors(validationErrors);
    if (Object.keys(validationErrors).length > 0) {
      toast.error('Complete the required proposal sections before submitting.');
      return;
    }
    if (!window.confirm('Submit this saved proposal version for lecturer review? Editing will be locked until a revision is requested.')) return;

    setSubmitting(true);
    try {
      const response = await workspaceApi.submitProposal(proposal.id, {
        rowVersion: proposal.rowVersion,
        changeNote: form.changeNote.trim() || 'Submitted for lecturer review.',
      });
      const updated = unwrapApiData<ProjectProposal>(response);
      if (!updated) throw new Error('The server did not return the submitted proposal.');
      applyProposal(updated);
      toast.success('Proposal submitted for lecturer review.');
    } catch (error) {
      const parsed = parseApiError(error, 'Unable to submit this proposal.');
      setErrors((current) => ({ ...current, ...parsed.fieldErrors }));
      toast.error(parsed.message);
    } finally {
      setSubmitting(false);
    }
  };

  const contentLength = useMemo(() => projectProposalFields.reduce((total, field) => total + form[field.name].trim().length, 0), [form]);

  if (loading) return <div className="flex min-h-[60vh] flex-col items-center justify-center" role="status"><Loader2 className="h-9 w-9 animate-spin text-primary" /><p className="mt-2 text-sm font-medium text-slate-400">Loading proposal details…</p></div>;

  if (loadError || !workspace) {
    return <div className="mx-auto max-w-xl rounded-2xl border border-red-100 bg-white p-8 text-center shadow-sm"><AlertTriangle className="mx-auto h-9 w-9 text-red-400" /><h1 className="mt-3 text-xl font-bold text-slate-900">Unable to open project proposal</h1><p className="mt-2 text-sm text-slate-600">{loadError}</p><div className="mt-5 flex justify-center gap-2"><button type="button" onClick={() => navigate(-1)} className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold">Go back</button><button type="button" onClick={() => void load()} className="rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-white">Retry</button></div></div>;
  }

  if (!workspace.project) {
    return <div className="mx-auto max-w-xl rounded-2xl border border-amber-200 bg-amber-50 p-8 text-center"><FileText className="mx-auto h-9 w-9 text-amber-500" /><h1 className="mt-3 text-xl font-bold text-slate-900">Create the project workspace first</h1><p className="mt-2 text-sm text-slate-600">A detailed proposal must belong to an existing project workspace.</p><button type="button" onClick={() => navigate(workspacePath)} className="mt-5 rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-white">Back to workspace</button></div>;
  }

  if (previewOnly) {
    return (
      <div className="space-y-5">
        <ProposalPreview proposal={proposal} onBack={() => navigate(workspacePath)} />
        {proposal && canReview && <ProposalReviewPanel proposal={proposal} onReviewed={applyProposal} />}
        {proposal && <div className="mx-auto max-w-4xl"><VersionHistory proposal={proposal} canRestore={false} onProposalChanged={applyProposal} /></div>}
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl space-y-5">
      <header className="flex flex-col gap-4 rounded-2xl border border-slate-200/60 bg-white p-6 shadow-sm sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3">
          <button type="button" onClick={() => navigate(workspacePath)} className="flex h-10 w-10 items-center justify-center rounded-xl border border-slate-200 text-slate-500 hover:bg-slate-50 hover:text-slate-800" aria-label="Back to workspace"><ArrowLeft className="h-5 w-5" /></button>
          <div><div className="flex flex-wrap items-center gap-2"><h1 className="text-xl font-bold text-slate-900">{proposal ? proposal.startupName || 'Detailed project proposal' : 'Create detailed project proposal'}</h1>{proposal && <span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-bold text-slate-600">{projectProposalStatusLabel[proposal.status]}</span>}</div><p className="mt-0.5 text-sm text-slate-500">Every active team member can edit a draft. Only the team leader can submit it.</p></div>
        </div>
        <div className="flex flex-wrap justify-end gap-2"><Button variant="outline" size="sm" icon={Save} isLoading={saving} disabled={submitting || (Boolean(proposal) && !hasChanges)} onClick={() => void save()}>Save draft</Button>{proposal && <Button variant="gradient" size="sm" icon={Send} isLoading={submitting} disabled={!canSubmit || saving || hasChanges} onClick={() => void submit()}>Submit proposal</Button>}</div>
      </header>

      {proposal?.status === 'NeedsRevision' && proposal.reviews[0] && <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900"><strong>Revision requested:</strong> {proposal.reviews[0].feedback}</div>}
      {!isTeamLeader && <div className="rounded-xl border border-blue-100 bg-blue-50 px-4 py-3 text-sm text-blue-800">You can edit and save draft versions. The active team leader must complete the final submission.</div>}

      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_320px]">
        <form onSubmit={(event) => void save(event)} className="space-y-5 rounded-2xl border border-slate-200/60 bg-white p-6 shadow-sm sm:p-8">
          <div className="grid gap-5">
            {projectProposalFields.map((field) => <Field key={field.name} field={field} value={form[field.name]} error={errors[field.name]} disabled={saving || submitting} onChange={(value) => setField(field.name, value)} />)}
          </div>
          <div className="border-t border-slate-100 pt-5">
            <label htmlFor="proposal-change-note" className="text-sm font-semibold text-slate-700">Version change note</label>
            <input id="proposal-change-note" value={form.changeNote} onChange={(event) => setField('changeNote', event.target.value)} disabled={saving || submitting} maxLength={1000} className={`mt-1.5 w-full rounded-xl border px-3 py-2.5 text-sm outline-none focus:ring-2 focus:ring-primary/15 ${errors.changeNote ? 'border-red-300' : 'border-slate-200 focus:border-primary'}`} placeholder="Summarize what changed in this version." />
            <div className="mt-1 flex justify-between text-xs"><span className="text-red-600">{errors.changeNote}</span><span className="text-slate-400">{form.changeNote.length}/1000</span></div>
          </div>
          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-5"><p className="text-xs text-slate-500">Total proposal content: {contentLength.toLocaleString()}/30,000 characters</p><Button type="submit" icon={Save} isLoading={saving} disabled={submitting || (Boolean(proposal) && !hasChanges)}>Save draft version</Button></div>
        </form>

        <aside className="space-y-5">
          <div className="rounded-2xl border border-slate-200/60 bg-white p-5 text-sm shadow-sm"><h2 className="font-bold text-slate-800">Submission checklist</h2><ul className="mt-3 list-disc space-y-2 pl-5 leading-5 text-slate-600"><li>Save all edits before submission.</li><li>Required sections marked with * must meet their minimum length.</li><li>Project Direction must already be approved.</li><li>The project must have 1–3 startup industries.</li><li>Only the team leader can submit.</li></ul>{proposal && hasChanges && <p className="mt-4 rounded-lg bg-amber-50 p-2.5 text-xs font-medium text-amber-800">Unsaved changes must be saved before submission.</p>}</div>
          {proposal && <VersionHistory proposal={proposal} canRestore={canEdit && ['Draft', 'NeedsRevision'].includes(proposal.status)} onProposalChanged={applyProposal} />}
        </aside>
      </div>
    </div>
  );
}
