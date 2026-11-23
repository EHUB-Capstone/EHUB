// @ts-nocheck
// frontend/src/pages/workspace/ProposalEditor.jsx
import { useCallback, useEffect, useState } from 'react';
import { useParams, useNavigate, useSearchParams } from 'react-router-dom';
import { ArrowLeft, Loader2, Save, Send } from 'lucide-react';
import toast from 'react-hot-toast';
import { workspaceApi } from '../../api/workspaceApi';
import { teamWorkspaceApi } from '../../api/teamWorkspaceApi';
import { useAuth } from '../../hooks/useAuth';
import Button from '../../components/ui/Button';
import ProposalPreview from './ProposalPreview';
import { useTranslation } from 'react-i18next';

export default function ProposalEditor() {
  const { t } = useTranslation('workspace');
  const { teamId } = useParams();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { user } = useAuth();
  
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [workspaceData, setWorkspaceData] = useState(null);
  const [accessMode, setAccessMode] = useState(null);
  
  const [form, setForm] = useState({
    title: '',
    startupName: '',
    tagline: '',
    problem: '',
    solution: '',
    targetCustomers: '',
    valueProposition: '',
    marketSize: '',
    competitors: '',
    businessModel: '',
    revenueModel: '',
    marketingStrategy: '',
    technology: '',
    financialPlan: '',
    roadmap: '',
    teamIntroduction: '',
    changeNote: ''
  });

  const isPreviewMode = searchParams.get('preview') === 'true';

  const fetchWorkspaceAndProposal = useCallback(async () => {
    try {
      setLoading(true);
      let contextRes;
      if (teamId) {
        contextRes = await teamWorkspaceApi.getWorkspaceContext(teamId);
      } else {
        contextRes = await teamWorkspaceApi.getCurrentWorkspace();
      }

      const context = contextRes?.data || null;
      setAccessMode(context?.accessMode || context?.selectedWorkspace?.accessMode || null);
      const resolvedTeamId = context?.selectedWorkspace?.teamId || teamId;
      const res = resolvedTeamId
        ? await workspaceApi.getTeamWorkspace(resolvedTeamId)
        : await workspaceApi.getMyWorkspace();

      if (res.data) {
        setWorkspaceData(res.data);
        if (res.data.proposal) {
          const prop = res.data.proposal;
          setForm({
            title: prop.title || '',
            startupName: prop.startupName || '',
            tagline: prop.tagline || '',
            problem: prop.problem || '',
            solution: prop.solution || '',
            targetCustomers: prop.targetCustomers || '',
            valueProposition: prop.valueProposition || '',
            marketSize: prop.marketSize || '',
            competitors: prop.competitors || '',
            businessModel: prop.businessModel || '',
            revenueModel: prop.revenueModel || '',
            marketingStrategy: prop.marketingStrategy || '',
            technology: prop.technology || '',
            financialPlan: prop.financialPlan || '',
            roadmap: prop.roadmap || '',
            teamIntroduction: prop.teamIntroduction || '',
            changeNote: ''
          });
        }
      }
    } catch (err) {
      console.error(err);
      toast.error(err.response?.data?.error || err.message || t('proposalEditor.failedToFetchProposalDetails'));
    } finally {
      setLoading(false);
    }
  }, [teamId]);

  useEffect(() => {
    // Fetching proposal/workspace context is the side effect owned by this page.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchWorkspaceAndProposal();
  }, [fetchWorkspaceAndProposal]);

  const handleBack = () => {
    if (teamId) {
      navigate(`/workspace/teams/${teamId}`);
    } else {
      navigate(`/student/workspace`);
    }
  };

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh]">
        <Loader2 className="w-10 h-10 text-primary animate-spin" />
        <p className="text-sm text-slate-400 mt-2 font-medium">{t('proposalEditor.loadingProposalDetails')}</p>
      </div>
    );
  }

  const members = workspaceData?.members || [];
  const proposal = workspaceData?.proposal || null;
  
  // Only students of the team or Admin can edit/create
  const isStudentMember = members.some(m => m.userId?._id === user?._id);
  const isReadOnly = accessMode === 'READ_ONLY' || !user || (user.role !== 'ADMIN' && !isStudentMember);

  // If preview mode or read-only, render ProposalPreview
  if (isPreviewMode || isReadOnly) {
    return <ProposalPreview proposal={proposal} onBack={handleBack} />;
  }

  const handleSave = async (e) => {
    e.preventDefault();
    if (!form.startupName) {
      toast.error(t('proposalEditor.startupNameIsRequired'));
      return;
    }

    setSaving(true);
    try {
      if (!proposal) {
        // Create
        const targetTeamId = teamId || workspaceData?.team?._id;
        await workspaceApi.createProposal(targetTeamId, {
          ...form,
          changeNote: form.changeNote || 'Initial proposal draft'
        });
        toast.success(t('proposalEditor.proposalDraftCreated'));
      } else {
        // Update
        await workspaceApi.updateProposal(proposal._id, {
          ...form,
          changeNote: form.changeNote || 'Updated proposal draft'
        });
        toast.success(t('proposalEditor.proposalDraftSaved'));
      }
      fetchWorkspaceAndProposal();
    } catch (err) {
      console.error(err);
      toast.error(err.response?.data?.error || err.message || t('proposalEditor.failedToSaveProposal'));
    } finally {
      setSaving(false);
    }
  };

  const handleSubmitProposal = async () => {
    if (!proposal) {
      toast.error(t('proposalEditor.pleaseSaveYourProposalDraftFirst'));
      return;
    }

    if (!window.confirm(t('proposalEditor.areYouSureYouWantTo'))) return;

    setSubmitting(true);
    try {
      await workspaceApi.submitProposal(proposal._id, {
        changeNote: form.changeNote || 'Submitted proposal for evaluation'
      });
      toast.success(t('proposalEditor.proposalSubmittedSuccessfully'));
      handleBack();
    } catch (err) {
      console.error(err);
      toast.error(err.response?.data?.error || err.message || t('proposalEditor.failedToSubmitProposal'));
    } finally {
      setSubmitting(false);
    }
  };

  const fields = [
    { name: 'startupName', label: t('proposalEditor.startupName'), type: 'text', placeholder: t('proposalEditor.enterYourStartupCompanyName') },
    { name: 'tagline', label: t('proposalEditor.tagline'), type: 'text', placeholder: t('proposalEditor.aShortSloganOrDescriptionOf') },
    { name: 'problem', label: t('proposalEditor.problemStatement'), type: 'textarea', placeholder: t('proposalEditor.whatProblemAreYouSolving') },
    { name: 'solution', label: t('proposalEditor.proposedSolution'), type: 'textarea', placeholder: t('proposalEditor.describeYourProductOrServiceAnd') },
    { name: 'targetCustomers', label: t('proposalEditor.targetCustomers'), type: 'textarea', placeholder: t('proposalEditor.whoAreYourMainTargetAudience') },
    { name: 'valueProposition', label: t('proposalEditor.valueProposition'), type: 'textarea', placeholder: t('proposalEditor.whatUniqueValueDoYouDeliver') },
    { name: 'marketSize', label: t('proposalEditor.marketSizePotential'), type: 'textarea', placeholder: t('proposalEditor.tamSamSomOrGeneralTarget') },
    { name: 'competitors', label: t('proposalEditor.competitorsCompetitiveAdvantage'), type: 'textarea', placeholder: t('proposalEditor.whoAreYourDirectIndirectCompetitors') },
    { name: 'businessModel', label: t('proposalEditor.businessModel'), type: 'textarea', placeholder: t('proposalEditor.howDoesYourBusinessFunctionPartners') },
    { name: 'revenueModel', label: t('proposalEditor.revenueModel'), type: 'textarea', placeholder: t('proposalEditor.howDoYouPlanToMake') },
    { name: 'marketingStrategy', label: t('proposalEditor.marketingSalesStrategy'), type: 'textarea', placeholder: t('proposalEditor.howWillYouAttractAcquireAnd') },
    { name: 'technology', label: t('proposalEditor.technologyStack'), type: 'textarea', placeholder: t('proposalEditor.whatTechnologiesFrameworksAndArchitectureWill') },
    { name: 'financialPlan', label: t('proposalEditor.financialPlan'), type: 'textarea', placeholder: t('proposalEditor.initialCostsBreakEvenAnalysisFinancial') },
    { name: 'roadmap', label: t('proposalEditor.productRoadmapMilestones'), type: 'textarea', placeholder: t('proposalEditor.phasesOfImplementationLaunchScheduleFuture') },
    { name: 'teamIntroduction', label: t('proposalEditor.teamIntroductionRoles'), type: 'textarea', placeholder: t('proposalEditor.introduceKeyMembersAndTheirRoles') },
  ];

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      
      {/* Editor Header */}
      <div className="bg-white rounded-2xl border border-slate-200/60 p-6 shadow-sm flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div className="flex items-center gap-3">
          <button
            onClick={handleBack}
            className="w-10 h-10 border border-slate-200 rounded-xl flex items-center justify-center text-slate-500 hover:text-slate-800 transition-all hover:bg-slate-50"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <div>
            <h1 className="text-heading sm:text-display text-secondary tracking-tight">
              {proposal ? t('proposalEditor.editProposal', { startupName: proposal.startupName }) : t('proposalEditor.createStartupProposal')}
            </h1>
            <p className="text-sm text-slate-500 mt-0.5">
              {t('proposalEditor.fillInAllSectionsOfYour')}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 w-full sm:w-auto justify-end">
          <Button 
            variant="outline" 
            size="sm" 
            icon={Save}
            isLoading={saving}
            onClick={handleSave}
          >
            {t('proposalEditor.saveDraft')}
          </Button>
          {proposal && (
            <Button 
              variant="primary" 
              size="sm" 
              icon={Send}
              isLoading={submitting}
              onClick={handleSubmitProposal}
            >
              {t('proposalEditor.submitProposal')}
            </Button>
          )}
        </div>
      </div>

      {/* Editor Form */}
      <form onSubmit={handleSave} className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6 sm:p-8 space-y-6">
        
        {/* Fields list */}
        <div className="grid grid-cols-1 gap-6">
          {fields.map((field) => (
            <div key={field.name} className="space-y-1.5">
              <label className="block text-sm font-bold text-slate-700">
                {field.label}
              </label>
              
              {field.type === 'text' ? (
                <input 
                  type="text"
                  placeholder={field.placeholder}
                  className="w-full border border-slate-200 rounded-xl px-4 py-2.5 text-slate-800 focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all text-sm bg-white"
                  value={form[field.name]}
                  onChange={(e) => setForm({ ...form, [field.name]: e.target.value })}
                />
              ) : (
                <textarea 
                  rows={4}
                  placeholder={field.placeholder}
                  className="w-full border border-slate-200 rounded-xl px-4 py-2.5 text-slate-800 focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all text-sm bg-white whitespace-pre-line leading-relaxed"
                  value={form[field.name]}
                  onChange={(e) => setForm({ ...form, [field.name]: e.target.value })}
                />
              )}
            </div>
          ))}
        </div>

        {/* Change Note */}
        <div className="pt-6 border-t border-slate-100 space-y-1.5">
          <label className="block text-sm font-bold text-slate-700">
            {t('proposalEditor.versionChangeNoteOptional')}
          </label>
          <input 
            type="text"
            placeholder={t('proposalEditor.eGUpdatedTargetCustomerSegment')}
            className="w-full border border-slate-200 rounded-xl px-4 py-2.5 text-slate-800 focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all text-sm bg-white"
            value={form.changeNote}
            onChange={(e) => setForm({ ...form, changeNote: e.target.value })}
          />
          <p className="text-caption text-slate-400">
            {t('proposalEditor.thisCommentHelpsYourTeamKeep')}
          </p>
        </div>

        {/* Form Actions footer */}
        <div className="pt-6 border-t border-slate-100 flex items-center justify-end gap-3">
          <Button 
            variant="outline"
            onClick={handleBack}
          >
            {t('common:actions.cancel')}
          </Button>
          <Button 
            variant="primary"
            type="submit"
            isLoading={saving}
          >
            {t('proposalEditor.saveDraftVersion')}
          </Button>
        </div>
      </form>

    </div>
  );
}
