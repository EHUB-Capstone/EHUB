import { useState } from 'react';
import toast from 'react-hot-toast';
import { AlertTriangle, CheckCircle2, FilePenLine, Loader2, X, XCircle } from 'lucide-react';
import { teamApi } from '../../api/teamApi';
import { parseApiError } from '../../utils/apiError';
import { getTeamMembers } from '../../utils/teamManagement';
import { getTeamGroupFromMajor } from '../../constants/majors';

const decisions = [
  { value: 'Approved', label: 'Approve', icon: CheckCircle2, tone: 'border-green-500 bg-green-50 text-green-700' },
  { value: 'NeedsRevision', label: 'Needs revision', icon: FilePenLine, tone: 'border-amber-500 bg-amber-50 text-amber-700' },
  { value: 'Rejected', label: 'Reject', icon: XCircle, tone: 'border-red-500 bg-red-50 text-red-700' },
];

export default function ReviewTeamProposalModal({ team, classStudents, onClose, onRefresh }) {
  const [submitting, setSubmitting] = useState(false);
  const [decision, setDecision] = useState('Approved');
  const [comment, setComment] = useState('');
  const members = getTeamMembers(team, classStudents);
  const requiresComment = decision !== 'Approved';
  const hasGroupOne = members.some((member) => getTeamGroupFromMajor(member.major) === 'GROUP_1');
  const hasGroupTwo = members.some((member) => getTeamGroupFromMajor(member.major) === 'GROUP_2');
  const leaderId = String(team.leaderId?._id || team.leaderId || '');
  const hasLeader = Boolean(leaderId && members.some((member) => member._id === leaderId));
  const standardSize = members.length >= 4 && members.length <= 6;

  const handleReview = async () => {
    const cleanComment = comment.trim();
    if (requiresComment && (cleanComment.length < 3 || cleanComment.length > 1000)) {
      toast.error('Review comment must be between 3 and 1000 characters.');
      return;
    }
    if (cleanComment && (cleanComment.length < 3 || cleanComment.length > 1000)) {
      toast.error('Review comment must be between 3 and 1000 characters.');
      return;
    }
    setSubmitting(true);
    try {
      await teamApi.reviewProposal(team._id, {
        decision,
        comment: cleanComment || null,
        rowVersion: team.rowVersion,
      });
      toast.success(decision === 'Approved' ? 'Team proposal approved' : decision === 'NeedsRevision' ? 'Revision requested' : 'Team proposal rejected');
      await onRefresh();
      onClose();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to review team proposal.').message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[80] flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm">
      <div className="flex max-h-[90vh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl bg-white shadow-xl">
        <header className="flex items-start justify-between border-b border-slate-100 bg-slate-50 px-5 py-4">
          <div>
            <h3 className="font-bold text-slate-900">Review team proposal</h3>
            <p className="mt-0.5 text-xs text-slate-500">Review is explicit; the proposed members cannot be silently changed.</p>
          </div>
          <button type="button" onClick={onClose} className="rounded-lg p-2 text-slate-400 hover:bg-slate-200"><X className="h-4 w-4" /></button>
        </header>

        <div className="flex-1 space-y-5 overflow-y-auto p-5">
          <section className="rounded-xl border border-slate-200 p-4">
            <div className="flex items-center justify-between gap-3">
              <div><p className="font-bold text-slate-900">{team.teamName}</p><p className="text-xs text-slate-500">{team.projectName || 'No project name supplied'}</p></div>
              <span className="rounded-full bg-amber-100 px-2.5 py-1 text-xs font-semibold text-amber-700">{team.status}</span>
            </div>
            {team.description && <p className="mt-3 text-sm leading-6 text-slate-600">{team.description}</p>}
          </section>

          <section>
            <h4 className="mb-2 text-xs font-bold uppercase tracking-wider text-slate-500">Validation checklist</h4>
            <div className="grid gap-2 sm:grid-cols-2">
              <p className={`rounded-lg px-3 py-2 text-xs font-semibold ${standardSize ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>{standardSize ? '4-6 members' : 'Invalid member count'}</p>
              <p className={`rounded-lg px-3 py-2 text-xs font-semibold ${hasGroupOne ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>Has GROUP_1</p>
              <p className={`rounded-lg px-3 py-2 text-xs font-semibold ${hasGroupTwo ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>Has GROUP_2</p>
              <p className={`rounded-lg px-3 py-2 text-xs font-semibold ${hasLeader ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>Leader selected</p>
            </div>
          </section>

          <section>
            <h4 className="mb-2 text-xs font-bold uppercase tracking-wider text-slate-500">Proposed members ({members.length})</h4>
            <div className="grid gap-2 sm:grid-cols-2">
              {members.map(member => (
                <div key={member._id} className="rounded-xl border border-slate-100 bg-slate-50 px-3 py-2">
                  <p className="text-sm font-semibold text-slate-800">{member.fullName}</p>
                  <p className="text-xs text-slate-500">{member.rollNumber || member.email} · {member.major || 'Missing major'}</p>
                </div>
              ))}
            </div>
          </section>

          <section>
            <h4 className="mb-2 text-xs font-bold uppercase tracking-wider text-slate-500">Decision</h4>
            <div className="grid gap-2 sm:grid-cols-3">
              {decisions.map(option => {
                const Icon = option.icon;
                const selected = decision === option.value;
                return <button key={option.value} type="button" onClick={() => setDecision(option.value)} className={`flex items-center justify-center gap-2 rounded-xl border-2 p-3 text-sm font-semibold ${selected ? option.tone : 'border-slate-200 text-slate-500 hover:bg-slate-50'}`}><Icon className="h-4 w-4" />{option.label}</button>;
              })}
            </div>
            <label className="mt-4 block text-xs font-semibold text-slate-600">Review comment {requiresComment && <span className="text-red-500">*</span>}</label>
            <textarea value={comment} onChange={event => setComment(event.target.value)} maxLength={1000} rows={4} placeholder="3–1000 characters" className="mt-1.5 w-full resize-none rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" />
            {requiresComment && !comment.trim() && <p className="mt-2 flex items-center gap-1.5 text-xs text-amber-700"><AlertTriangle className="h-3.5 w-3.5" />A reason is required for this decision.</p>}
          </section>
        </div>

        <footer className="flex justify-end gap-2 border-t border-slate-100 bg-slate-50 px-5 py-4">
          <button type="button" onClick={onClose} disabled={submitting} className="rounded-xl px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-200">Cancel</button>
          <button type="button" onClick={handleReview} disabled={submitting || (requiresComment && !comment.trim())} className="inline-flex min-w-28 items-center justify-center gap-2 rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">
            {submitting && <Loader2 className="h-4 w-4 animate-spin" />} Confirm
          </button>
        </footer>
      </div>
    </div>
  );
}
