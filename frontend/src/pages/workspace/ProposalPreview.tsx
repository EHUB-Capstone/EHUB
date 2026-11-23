// @ts-nocheck
import { FileText, ArrowLeft, Calendar, User, ShieldAlert } from 'lucide-react';
import Badge from '../../components/ui/Badge';
import InlineCommentUI from '../../components/workspace/InlineCommentUI';
import { useTranslation } from 'react-i18next';

const statusColors = {
  DRAFT: 'bg-slate-100 text-slate-600 border border-slate-200',
  SUBMITTED: 'bg-blue-50 text-blue-600 border border-blue-200',
  REVIEWED: 'bg-secondary-50 text-secondary border border-secondary-200',
  APPROVED: 'bg-emerald-50 text-emerald-600 border border-emerald-200',
  REJECTED: 'bg-red-50 text-red-600 border border-red-200'
};

export default function ProposalPreview({ proposal, onBack }) {
  const { t } = useTranslation('workspace');
  if (!proposal) {
    return (
      <div className="bg-white rounded-2xl border border-slate-200/60 p-12 text-center">
        <ShieldAlert className="w-12 h-12 text-slate-300 mx-auto mb-3" />
        <h3 className="text-lg font-bold text-slate-800">{t('proposalPreview.noProposalInitialized')}</h3>
        <p className="text-slate-500 text-sm mt-1">{t('proposalPreview.thisTeamHasNotCreatedA')}</p>
        {onBack && (
          <button 
            onClick={onBack}
            className="mt-4 text-sm font-semibold text-primary-700 hover:underline"
          >
            {t('common:actions.goBack')}
          </button>
        )}
      </div>
    );
  }

  const sections = [
    { key: 'problem', label: t('proposalEditor.problemStatement'), val: proposal.problem },
    { key: 'solution', label: t('proposalEditor.proposedSolution'), val: proposal.solution },
    { key: 'targetCustomers', label: t('proposalEditor.targetCustomers'), val: proposal.targetCustomers },
    { key: 'valueProposition', label: t('proposalEditor.valueProposition'), val: proposal.valueProposition },
    { key: 'marketSize', label: t('proposalEditor.marketSizePotential'), val: proposal.marketSize },
    { key: 'competitors', label: t('proposalEditor.competitorsCompetitiveAdvantage'), val: proposal.competitors },
    { key: 'businessModel', label: t('proposalEditor.businessModel'), val: proposal.businessModel },
    { key: 'revenueModel', label: t('proposalEditor.revenueModel'), val: proposal.revenueModel },
    { key: 'marketingStrategy', label: t('proposalEditor.marketingSalesStrategy'), val: proposal.marketingStrategy },
    { key: 'technology', label: t('proposalEditor.technologyStack'), val: proposal.technology },
    { key: 'financialPlan', label: t('proposalEditor.financialPlan'), val: proposal.financialPlan },
    { key: 'roadmap', label: t('proposalEditor.productRoadmapMilestones'), val: proposal.roadmap },
    { key: 'teamIntroduction', label: t('proposalEditor.teamIntroductionRoles'), val: proposal.teamIntroduction },
  ];

  return (
    <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm overflow-hidden max-w-4xl mx-auto">
      {/* Header */}
      <div className="p-6 sm:p-8 text-white relative bg-secondary">
        <div className="flex items-center justify-between flex-wrap gap-4">
          <button
            onClick={onBack}
            className="flex items-center gap-1.5 text-slate-300 hover:text-white transition-all text-xs font-semibold uppercase tracking-wider"
          >
            <ArrowLeft className="w-4 h-4" /> {t('proposalPreview.backToWorkspace')}
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
            <h1 className="text-2xl sm:text-3xl font-black">{proposal.startupName || t('proposalPreview.unnamedStartup')}</h1>
            {proposal.tagline && <p className="text-slate-300/80 mt-1 italic text-sm sm:text-base">"{proposal.tagline}"</p>}
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-6 mt-6 pt-6 border-t border-white/10 text-xs text-slate-400">
          <span className="flex items-center gap-1.5"><Calendar className="w-3.5 h-3.5" /> {t('proposalPreview.updated')} {new Date(proposal.updatedAt).toLocaleString()}</span>
          {proposal.submittedAt && (
            <span className="flex items-center gap-1.5"><Calendar className="w-3.5 h-3.5 text-blue-400" /> {t('proposalPreview.submitted')} {new Date(proposal.submittedAt).toLocaleDateString()}</span>
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
              {val || <span className="text-slate-300 italic">{t('proposalPreview.notSpecifiedYet')}</span>}
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
