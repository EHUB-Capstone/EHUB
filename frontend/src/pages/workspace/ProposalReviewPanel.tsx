import { useState } from 'react';
import { CheckCircle2, Loader2, RotateCcw, XCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import { workspaceApi } from '../../api/workspaceApi';
import type { ProjectProposal, ProjectProposalReviewDecision } from '../../types/projectProposal';
import { parseApiError } from '../../utils/apiError';
import { unwrapApiData } from '../../utils/classMappers';

type Props = {
  proposal: ProjectProposal;
  onReviewed: (proposal: ProjectProposal) => void;
};

const decisions: Array<{ value: ProjectProposalReviewDecision; label: string; description: string; icon: typeof CheckCircle2 }> = [
  { value: 'Approved', label: 'Approve', description: 'Accept the submitted proposal.', icon: CheckCircle2 },
  { value: 'NeedsRevision', label: 'Request revision', description: 'Return it to the team with actionable feedback.', icon: RotateCcw },
  { value: 'Rejected', label: 'Reject', description: 'Close this proposal with a clear reason.', icon: XCircle },
];

export default function ProposalReviewPanel({ proposal, onReviewed }: Props) {
  const [decision, setDecision] = useState<ProjectProposalReviewDecision>('NeedsRevision');
  const [feedback, setFeedback] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const submitReview = async () => {
    const normalizedFeedback = feedback.trim();
    if (normalizedFeedback.length < 3 || normalizedFeedback.length > 1000) {
      toast.error('Review feedback must contain between 3 and 1000 characters.');
      return;
    }
    if (!window.confirm(`Confirm decision: ${decisions.find((item) => item.value === decision)?.label}?`)) return;

    setSubmitting(true);
    try {
      const response = await workspaceApi.reviewProposal(proposal.id, {
        decision,
        feedback: normalizedFeedback,
        rowVersion: proposal.rowVersion,
      });
      const updated = unwrapApiData<ProjectProposal>(response);
      if (!updated) throw new Error('The server did not return the reviewed proposal.');
      onReviewed(updated);
      setFeedback('');
      toast.success('Proposal review recorded successfully.');
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to review this proposal. Refresh and try again.').message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section className="mx-auto max-w-4xl rounded-2xl border border-blue-200 bg-blue-50/60 p-5">
      <h2 className="font-bold text-slate-900">Lecturer decision</h2>
      <p className="mt-1 text-sm text-slate-600">The submitted version is immutable. Your decision and feedback will be recorded in the review history.</p>
      <div className="mt-4 grid gap-2 sm:grid-cols-3">
        {decisions.map((item) => {
          const Icon = item.icon;
          return (
            <button key={item.value} type="button" onClick={() => setDecision(item.value)} disabled={submitting} className={`rounded-xl border p-3 text-left transition ${decision === item.value ? 'border-primary bg-white ring-2 ring-primary/10' : 'border-blue-100 bg-white/60 hover:bg-white'}`}>
              <span className="flex items-center gap-2 text-sm font-bold text-slate-800"><Icon className="h-4 w-4" /> {item.label}</span>
              <span className="mt-1 block text-xs leading-5 text-slate-500">{item.description}</span>
            </button>
          );
        })}
      </div>
      <label htmlFor="proposal-review-feedback" className="mt-4 block text-sm font-semibold text-slate-700">Feedback <span className="text-red-500">*</span></label>
      <textarea id="proposal-review-feedback" value={feedback} onChange={(event) => setFeedback(event.target.value)} disabled={submitting} maxLength={1000} rows={4} className="mt-1.5 w-full resize-y rounded-xl border border-blue-100 bg-white px-3 py-2.5 text-sm leading-6 outline-none focus:border-primary focus:ring-2 focus:ring-primary/15" placeholder="Explain the decision and provide actionable guidance to the team." />
      <div className="mt-1 flex justify-between text-xs text-slate-400"><span>Minimum 3 characters</span><span>{feedback.length}/1000</span></div>
      <div className="mt-4 flex justify-end">
        <button type="button" onClick={() => void submitReview()} disabled={submitting || feedback.trim().length < 3} className="inline-flex items-center gap-2 rounded-xl bg-primary px-5 py-2.5 text-sm font-bold text-white disabled:cursor-not-allowed disabled:opacity-50">
          {submitting && <Loader2 className="h-4 w-4 animate-spin" />} Record decision
        </button>
      </div>
    </section>
  );
}
