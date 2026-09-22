import { ArrowLeft, Calendar, FileText, MessageSquareText, ShieldAlert } from 'lucide-react';
import type { ProjectProposal, ProjectProposalContent, ProjectProposalStatus } from '../../types/projectProposal';
import { projectProposalFields, projectProposalStatusLabel } from '../../utils/projectProposal';

const statusColors: Record<ProjectProposalStatus, string> = {
  Draft: 'border-slate-200 bg-slate-100 text-slate-700',
  Submitted: 'border-blue-200 bg-blue-50 text-blue-700',
  NeedsRevision: 'border-amber-200 bg-amber-50 text-amber-700',
  Approved: 'border-emerald-200 bg-emerald-50 text-emerald-700',
  Rejected: 'border-red-200 bg-red-50 text-red-700',
  Archived: 'border-slate-200 bg-slate-100 text-slate-500',
};

type Props = {
  proposal: ProjectProposal | null;
  onBack: () => void;
};

function proposalDate(proposal: ProjectProposal): { label: string; value: string } | null {
  if (proposal.approvedAtUtc) return { label: 'Approved', value: proposal.approvedAtUtc };
  if (proposal.rejectedAtUtc) return { label: 'Rejected', value: proposal.rejectedAtUtc };
  if (proposal.submittedAtUtc) return { label: 'Submitted', value: proposal.submittedAtUtc };
  return null;
}

export default function ProposalPreview({ proposal, onBack }: Props) {
  if (!proposal) {
    return (
      <div className="mx-auto max-w-2xl rounded-2xl border border-slate-200/60 bg-white p-12 text-center">
        <ShieldAlert className="mx-auto mb-3 h-12 w-12 text-slate-300" />
        <h1 className="text-lg font-bold text-slate-800">No detailed proposal yet</h1>
        <p className="mt-1 text-sm text-slate-500">This team has not created its detailed project proposal draft.</p>
        <button type="button" onClick={onBack} className="mt-4 text-sm font-semibold text-primary hover:underline">Back to workspace</button>
      </div>
    );
  }

  const relevantDate = proposalDate(proposal);
  const sections = projectProposalFields.filter((field) => !['title', 'startupName', 'tagline'].includes(field.name));

  return (
    <section className="mx-auto max-w-4xl overflow-hidden rounded-2xl border border-slate-200/60 bg-white shadow-sm">
      <header className="bg-gradient-to-r from-slate-900 to-slate-800 p-6 text-white sm:p-8">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <button type="button" onClick={onBack} className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-slate-300 transition hover:text-white">
            <ArrowLeft className="h-4 w-4" /> Back to workspace
          </button>
          <span className={`rounded-full border px-3 py-1 text-xs font-bold ${statusColors[proposal.status]}`}>
            {projectProposalStatusLabel[proposal.status]}
          </span>
        </div>

        <div className="mt-6 flex items-start gap-4">
          <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl border border-white/10 bg-white/10">
            <FileText className="h-6 w-6 text-primary-200" />
          </span>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-400">{proposal.title || 'Untitled proposal'}</p>
            <h1 className="mt-1 text-2xl font-black sm:text-3xl">{proposal.startupName || 'Unnamed startup'}</h1>
            {proposal.tagline && <p className="mt-1 text-sm italic text-slate-300/80 sm:text-base">“{proposal.tagline}”</p>}
          </div>
        </div>

        {relevantDate && (
          <div className="mt-6 flex items-center gap-1.5 border-t border-white/10 pt-5 text-xs text-slate-400">
            <Calendar className="h-3.5 w-3.5" /> {relevantDate.label} {new Date(relevantDate.value).toLocaleString()}
          </div>
        )}
      </header>

      <div className="space-y-5 p-6 sm:p-8">
        {proposal.reviews.length > 0 && (
          <div className="rounded-2xl border border-amber-200 bg-amber-50 p-5">
            <div className="flex items-center gap-2 text-sm font-bold text-amber-900"><MessageSquareText className="h-4 w-4" /> Lecturer feedback</div>
            <div className="mt-3 space-y-3">
              {proposal.reviews.map((review) => (
                <div key={review.id} className="rounded-xl border border-amber-100 bg-white/70 p-3 text-sm text-slate-700">
                  <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
                    <span className="font-bold text-amber-800">{projectProposalStatusLabel[review.toStatus]}</span>
                    <span className="text-slate-400">{new Date(review.occurredAtUtc).toLocaleString()}</span>
                  </div>
                  <p className="mt-2 whitespace-pre-line leading-6">{review.feedback}</p>
                </div>
              ))}
            </div>
          </div>
        )}

        {sections.map((field) => {
          const value = proposal[field.name as keyof ProjectProposalContent];
          return (
            <article key={field.name} className="rounded-2xl border border-slate-100 bg-slate-50/50 p-5">
              <h2 className="text-xs font-bold uppercase tracking-widest text-slate-400">{field.label}</h2>
              <p className="mt-2 whitespace-pre-line text-sm leading-6 text-slate-700">
                {value || <span className="italic text-slate-300">Not specified yet.</span>}
              </p>
            </article>
          );
        })}
      </div>
    </section>
  );
}
