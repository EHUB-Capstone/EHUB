// @ts-nocheck
import { FileText, ArrowLeft, Calendar, User, ShieldAlert } from 'lucide-react';
import Badge from '../../components/ui/Badge';
import InlineCommentUI from '../../components/workspace/InlineCommentUI';

const statusColors = {
  DRAFT: 'bg-slate-100 text-slate-600 border border-slate-200',
  SUBMITTED: 'bg-blue-50 text-blue-600 border border-blue-200',
  REVIEWED: 'bg-purple-50 text-purple-600 border border-purple-200',
  APPROVED: 'bg-emerald-50 text-emerald-600 border border-emerald-200',
  REJECTED: 'bg-red-50 text-red-600 border border-red-200'
};

export default function ProposalPreview({ proposal, onBack }) {
  if (!proposal) {
    return (
      <div className="bg-white rounded-2xl border border-slate-200/60 p-12 text-center">
        <ShieldAlert className="w-12 h-12 text-slate-300 mx-auto mb-3" />
        <h3 className="text-lg font-bold text-slate-800">No proposal initialized</h3>
        <p className="text-slate-500 text-sm mt-1">This team has not created a proposal draft yet.</p>
        {onBack && (
          <button 
            onClick={onBack}
            className="mt-4 text-sm font-semibold text-primary hover:underline"
          >
            Go Back
          </button>
        )}
      </div>
    );
  }

  const sections = [
    { key: 'problem', label: 'Problem Statement', val: proposal.problem },
    { key: 'solution', label: 'Proposed Solution', val: proposal.solution },
    { key: 'targetCustomers', label: 'Target Customers', val: proposal.targetCustomers },
    { key: 'valueProposition', label: 'Value Proposition', val: proposal.valueProposition },
    { key: 'marketSize', label: 'Market Size & Potential', val: proposal.marketSize },
    { key: 'competitors', label: 'Competitors & Competitive Advantage', val: proposal.competitors },
    { key: 'businessModel', label: 'Business Model', val: proposal.businessModel },
    { key: 'revenueModel', label: 'Revenue Model', val: proposal.revenueModel },
    { key: 'marketingStrategy', label: 'Marketing & Sales Strategy', val: proposal.marketingStrategy },
    { key: 'technology', label: 'Technology Stack', val: proposal.technology },
    { key: 'financialPlan', label: 'Financial Plan', val: proposal.financialPlan },
    { key: 'roadmap', label: 'Product Roadmap & Milestones', val: proposal.roadmap },
    { key: 'teamIntroduction', label: 'Team Introduction & Roles', val: proposal.teamIntroduction },
  ];

  return (
    <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm overflow-hidden max-w-4xl mx-auto">
      {/* Header */}
      <div className="bg-gradient-to-r from-slate-900 to-slate-800 p-6 sm:p-8 text-white relative">
        <div className="flex items-center justify-between flex-wrap gap-4">
          <button
            onClick={onBack}
            className="flex items-center gap-1.5 text-slate-300 hover:text-white transition-all text-xs font-semibold uppercase tracking-wider"
          >
            <ArrowLeft className="w-4 h-4" /> Back to Workspace
          </button>
          <span className={`px-3 py-1 rounded-full text-xs font-bold uppercase tracking-wider ${statusColors[proposal.status]}`}>
            {proposal.status}
          </span>
        </div>

        <div className="mt-6 flex items-start gap-4">
          <div className="w-12 h-12 rounded-2xl bg-white/10 flex items-center justify-center shrink-0 border border-white/10">
            <FileText className="w-6 h-6 text-primary-200" />
          </div>
          <div>
            <h1 className="text-2xl sm:text-3xl font-black">{proposal.startupName || 'Unnamed Startup'}</h1>
            {proposal.tagline && <p className="text-slate-300/80 mt-1 italic text-sm sm:text-base">"{proposal.tagline}"</p>}
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-6 mt-6 pt-6 border-t border-white/10 text-xs text-slate-400">
          <span className="flex items-center gap-1.5"><Calendar className="w-3.5 h-3.5" /> Updated {new Date(proposal.updatedAt).toLocaleString()}</span>
          {proposal.submittedAt && (
            <span className="flex items-center gap-1.5"><Calendar className="w-3.5 h-3.5 text-blue-400" /> Submitted {new Date(proposal.submittedAt).toLocaleDateString()}</span>
          )}
        </div>
      </div>

      {/* Content */}
      <div className="p-6 sm:p-8 space-y-6">
        {sections.map(({ key, label, val }) => (
          <div key={label} className="p-5 rounded-2xl bg-slate-50/50 border border-slate-100 group">
            <div className="flex justify-between items-start mb-2.5">
              <h3 className="text-xs font-bold text-slate-400 uppercase tracking-widest">{label}</h3>
            </div>
            <div className="text-slate-700 leading-relaxed text-sm whitespace-pre-line mb-4">
              {val || <span className="text-slate-300 italic">Not specified yet.</span>}
            </div>
            
            {/* Inline Comment Section */}
            <div className="border-t border-slate-200/50 pt-2 opacity-0 group-hover:opacity-100 transition-opacity focus-within:opacity-100">
              <InlineCommentUI proposalId={proposal._id} sectionKey={key} sectionLabel={label} />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
