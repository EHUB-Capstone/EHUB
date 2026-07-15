// @ts-nocheck
import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { motion } from 'framer-motion';
import { useNavigate } from 'react-router-dom';
import { startupApi } from '../../api/startupApi';
import { dashboardApi } from '../../api/dashboardApi';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import EmptyState from '../../components/ui/EmptyState';
import Button from '../../components/ui/Button';
import { Rocket, Save, Send, ChevronDown, ChevronUp } from 'lucide-react';

const STAGES = ['Idea', 'Research', 'MVP', 'Launch', 'Growth'];

const Field = ({ label, name, value, onChange, multiline = false, placeholder = '' }) => (
  <div>
    <label className="block text-sm font-semibold text-slate-700 mb-1.5">{label}</label>
    {multiline ? (
      <textarea
        name={name}
        value={value}
        onChange={onChange}
        rows={4}
        placeholder={placeholder}
        className="w-full border border-slate-200 rounded-xl px-3 py-2.5 text-sm placeholder:text-slate-300 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all resize-none"
      />
    ) : (
      <input
        type="text"
        name={name}
        value={value}
        onChange={onChange}
        placeholder={placeholder}
        className="w-full border border-slate-200 rounded-xl px-3 py-2.5 text-sm placeholder:text-slate-300 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
      />
    )}
  </div>
);

const IdeaForm = () => {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [existingIdea, setExistingIdea] = useState(null);
  const [teamId, setTeamId] = useState(null);
  const [showAdvanced, setShowAdvanced] = useState(false);

  const [form, setForm] = useState({
    startupName: '', problem: '', targetCustomer: '', solution: '',
    businessModel: '', technology: '', marketAnalysis: '', competitors: '', stage: 'Idea',
  });

  useEffect(() => {
    const loadData = async () => {
      try {
        const res = await dashboardApi.getStudent();
        const dashData = res.data || res;
        if (dashData.team) setTeamId(dashData.team._id);
        if (dashData.startupIdea) {
          setExistingIdea(dashData.startupIdea);
          const { startupName, problem, targetCustomer, solution, businessModel, technology, marketAnalysis, competitors, stage } = dashData.startupIdea;
          setForm({ startupName: startupName || '', problem: problem || '', targetCustomer: targetCustomer || '', solution: solution || '', businessModel: businessModel || '', technology: technology || '', marketAnalysis: marketAnalysis || '', competitors: competitors || '', stage: stage || 'Idea' });
        }
      } catch {
        toast.error('Failed to load data');
      } finally {
        setLoading(false);
      }
    };
    loadData();
  }, []);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm(prev => ({ ...prev, [name]: value }));
  };

  const handleSave = async (status = 'DRAFT') => {
    if (!form.startupName || !form.problem) { toast.error('Name and problem are required'); return; }
    if (!teamId) { toast.error('You need to be in a team first'); return; }

    const isSaving = status === 'DRAFT';
    if (isSaving) setSaving(true); else setSubmitting(true);

    try {
      const payload = { ...form, teamId, status };
      let result;
      if (existingIdea) {
        result = await startupApi.update(existingIdea._id, payload);
        toast.success(isSaving ? 'Draft saved!' : 'Idea submitted for review!');
      } else {
        result = await startupApi.create(payload);
        const newIdea = result.data?.startupIdea || result.startupIdea || result.data;
        setExistingIdea(newIdea);
        toast.success(isSaving ? 'Draft saved!' : 'Idea submitted for review!');
      }
      if (!isSaving) navigate('/student');
    } catch (err) {
      toast.error(err.message || 'Failed to save');
    } finally {
      setSaving(false);
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingSkeleton />;
  if (!teamId) return <EmptyState icon={Rocket} title="You need to be in a team" description="Ask your lecturer to add you to a team before submitting a startup idea" action={{ label: 'Back to Dashboard', onClick: () => navigate('/student') }} />;

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
        <div className="flex items-center justify-between mb-1">
          <h1 className="text-3xl font-bold text-slate-900">{existingIdea ? 'Edit Startup Idea' : 'Submit Startup Idea'}</h1>
          {existingIdea && <span className="text-sm bg-amber-100 text-amber-700 px-3 py-1 rounded-full font-medium">{existingIdea.status}</span>}
        </div>
        <p className="text-slate-500">Describe your startup idea in detail. The more complete, the better the AI analysis.</p>
      </motion.div>

      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.07 }} className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6 space-y-5">
        <Field label="Startup Name *" name="startupName" value={form.startupName} onChange={handleChange} placeholder="e.g. EduSpark VN" />

        <div>
          <label className="block text-sm font-semibold text-slate-700 mb-1.5">Current Stage</label>
          <div className="flex gap-2 flex-wrap">
            {STAGES.map(s => (
              <button key={s} type="button" onClick={() => setForm(p => ({ ...p, stage: s }))}
                className={`px-4 py-1.5 rounded-lg text-sm font-medium transition-all ${form.stage === s ? 'bg-primary text-white shadow-sm' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}>
                {s}
              </button>
            ))}
          </div>
        </div>

        <Field label="Problem Statement *" name="problem" value={form.problem} onChange={handleChange} multiline placeholder="What problem are you solving? Who faces this problem? How severe is it?" />
        <Field label="Target Customer" name="targetCustomer" value={form.targetCustomer} onChange={handleChange} multiline placeholder="Who is your primary customer? Demographics, behaviors, needs..." />
        <Field label="Proposed Solution" name="solution" value={form.solution} onChange={handleChange} multiline placeholder="How does your product/service solve the problem?" />

        {/* Advanced Section */}
        <button
          type="button"
          onClick={() => setShowAdvanced(!showAdvanced)}
          className="flex items-center gap-2 text-sm text-primary font-medium hover:underline"
        >
          {showAdvanced ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
          {showAdvanced ? 'Hide' : 'Show'} Advanced Fields (Business Model, Tech, Market, Competitors)
        </button>

        {showAdvanced && (
          <div className="space-y-5 pt-2 border-t border-slate-100">
            <Field label="Business Model" name="businessModel" value={form.businessModel} onChange={handleChange} multiline placeholder="How will you make money? Subscription, marketplace, B2B, B2C..." />
            <Field label="Technology Stack" name="technology" value={form.technology} onChange={handleChange} multiline placeholder="What technologies will you use? Frontend, backend, AI, etc." />
            <Field label="Market Analysis" name="marketAnalysis" value={form.marketAnalysis} onChange={handleChange} multiline placeholder="TAM, SAM, SOM. Market size, growth rate, opportunity..." />
            <Field label="Competitors" name="competitors" value={form.competitors} onChange={handleChange} multiline placeholder="Who are your competitors? What's your competitive advantage?" />
          </div>
        )}
      </motion.div>

      {/* Actions */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.14 }} className="flex gap-3 justify-end">
        <Button variant="ghost" onClick={() => navigate('/student')}>Cancel</Button>
        <Button variant="outline" icon={Save} isLoading={saving} onClick={() => handleSave('DRAFT')}>Save Draft</Button>
        <Button variant="gradient" icon={Send} isLoading={submitting} onClick={() => handleSave('SUBMITTED')}>Submit for Review</Button>
      </motion.div>
    </div>
  );
};

export default IdeaForm;
