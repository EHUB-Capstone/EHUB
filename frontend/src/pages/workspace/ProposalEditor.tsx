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

export default function ProposalEditor() {
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
      toast.error(err.response?.data?.error || err.message || 'Failed to fetch proposal details');
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
        <p className="text-sm text-slate-400 mt-2 font-medium">Loading proposal details...</p>
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
      toast.error('Startup Name is required.');
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
        toast.success('Proposal draft created!');
      } else {
        // Update
        await workspaceApi.updateProposal(proposal._id, {
          ...form,
          changeNote: form.changeNote || 'Updated proposal draft'
        });
        toast.success('Proposal draft saved!');
      }
      fetchWorkspaceAndProposal();
    } catch (err) {
      console.error(err);
      toast.error(err.response?.data?.error || err.message || 'Failed to save proposal');
    } finally {
      setSaving(false);
    }
  };

  const handleSubmitProposal = async () => {
    if (!proposal) {
      toast.error('Please save your proposal draft first before submitting.');
      return;
    }

    if (!window.confirm('Are you sure you want to submit this proposal? This will lock it into SUBMITTED stage.')) return;

    setSubmitting(true);
    try {
      await workspaceApi.submitProposal(proposal._id, {
        changeNote: form.changeNote || 'Submitted proposal for evaluation'
      });
      toast.success('Proposal submitted successfully!');
      handleBack();
    } catch (err) {
      console.error(err);
      toast.error(err.response?.data?.error || err.message || 'Failed to submit proposal');
    } finally {
      setSubmitting(false);
    }
  };

  const fields = [
    { name: 'startupName', label: 'Startup Name *', type: 'text', placeholder: 'Enter your startup company name' },
    { name: 'tagline', label: 'Tagline', type: 'text', placeholder: 'A short slogan or description of your idea' },
    { name: 'problem', label: 'Problem Statement', type: 'textarea', placeholder: 'What problem are you solving?' },
    { name: 'solution', label: 'Proposed Solution', type: 'textarea', placeholder: 'Describe your product or service and how it resolves the problem.' },
    { name: 'targetCustomers', label: 'Target Customers', type: 'textarea', placeholder: 'Who are your main target audience and customers?' },
    { name: 'valueProposition', label: 'Value Proposition', type: 'textarea', placeholder: 'What unique value do you deliver to customers?' },
    { name: 'marketSize', label: 'Market Size & Potential', type: 'textarea', placeholder: 'TAM, SAM, SOM or general target market size estimation' },
    { name: 'competitors', label: 'Competitors & Competitive Advantage', type: 'textarea', placeholder: 'Who are your direct/indirect competitors, and why are you better?' },
    { name: 'businessModel', label: 'Business Model', type: 'textarea', placeholder: 'How does your business function? Partners, activities, etc.' },
    { name: 'revenueModel', label: 'Revenue Model', type: 'textarea', placeholder: 'How do you plan to make money? Pricing, subscriptions, advertising, etc.' },
    { name: 'marketingStrategy', label: 'Marketing & Sales Strategy', type: 'textarea', placeholder: 'How will you attract, acquire, and retain customers?' },
    { name: 'technology', label: 'Technology Stack', type: 'textarea', placeholder: 'What technologies, frameworks, and architecture will you use?' },
    { name: 'financialPlan', label: 'Financial Plan', type: 'textarea', placeholder: 'Initial costs, break-even analysis, financial predictions' },
    { name: 'roadmap', label: 'Product Roadmap & Milestones', type: 'textarea', placeholder: 'Phases of implementation, launch schedule, future targets' },
    { name: 'teamIntroduction', label: 'Team Introduction & Roles', type: 'textarea', placeholder: 'Introduce key members and their roles/expertise.' },
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
            <h1 className="text-xl font-bold text-slate-900">
              {proposal ? `Edit Proposal: ${proposal.startupName}` : 'Create Startup Proposal'}
            </h1>
            <p className="text-sm text-slate-500 mt-0.5">
              Fill in all sections of your startup proposal below.
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
            Save Draft
          </Button>
          {proposal && (
            <Button 
              variant="gradient" 
              size="sm" 
              icon={Send}
              isLoading={submitting}
              onClick={handleSubmitProposal}
            >
              Submit Proposal
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
            Version Change Note (Optional)
          </label>
          <input 
            type="text"
            placeholder="e.g., Updated target customer segment and tech stack details"
            className="w-full border border-slate-200 rounded-xl px-4 py-2.5 text-slate-800 focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all text-sm bg-white"
            value={form.changeNote}
            onChange={(e) => setForm({ ...form, changeNote: e.target.value })}
          />
          <p className="text-[11px] text-slate-400">
            This comment helps your team keep track of what changed in this save version.
          </p>
        </div>

        {/* Form Actions footer */}
        <div className="pt-6 border-t border-slate-100 flex items-center justify-end gap-3">
          <Button 
            variant="outline"
            onClick={handleBack}
          >
            Cancel
          </Button>
          <Button 
            variant="primary"
            type="submit"
            isLoading={saving}
          >
            Save Draft Version
          </Button>
        </div>
      </form>

    </div>
  );
}
