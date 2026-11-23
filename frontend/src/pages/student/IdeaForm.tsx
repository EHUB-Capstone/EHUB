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
import { useTranslation } from 'react-i18next';

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
  const { t } = useTranslation('class');
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
        // Keep the empty state below without showing a toast on page entry.
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
    if (!form.startupName || !form.problem) { toast.error(t('ideaForm.nameAndProblemAreRequired')); return; }
    if (!teamId) { toast.error(t('ideaForm.youNeedToBeInA')); return; }

    const isSaving = status === 'DRAFT';
    if (isSaving) setSaving(true); else setSubmitting(true);

    try {
      const payload = { ...form, teamId, status };
      let result;
      if (existingIdea) {
        result = await startupApi.update(existingIdea._id, payload);
        toast.success(isSaving ? t('ideaForm.draftSaved') : t('ideaForm.ideaSubmittedForReview'));
      } else {
        result = await startupApi.create(payload);
        const newIdea = result.data?.startupIdea || result.startupIdea || result.data;
        setExistingIdea(newIdea);
        toast.success(isSaving ? t('ideaForm.draftSaved') : t('ideaForm.ideaSubmittedForReview'));
      }
      if (!isSaving) navigate('/student');
    } catch (err) {
      toast.error(err.message || t('ideaForm.failedToSave'));
    } finally {
      setSaving(false);
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingSkeleton />;
  if (!teamId) return <EmptyState icon={Rocket} title={t('ideaForm.youNeedToBeInA2')} description={t('ideaForm.askYourLecturerToAddYou')} action={{ label: t('ideaForm.backToDashboard'), onClick: () => navigate('/student') }} />;

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
        <div className="flex items-center justify-between mb-1">
          <h1 className="text-heading sm:text-display text-secondary tracking-tight">{existingIdea ? t('ideaForm.editStartupIdea') : t('ideaForm.submitStartupIdea')}</h1>
          {existingIdea && <span className="text-sm bg-amber-100 text-amber-700 px-3 py-1 rounded-full font-medium">{existingIdea.status}</span>}
        </div>
        <p className="text-slate-500">{t('ideaForm.describeYourStartupIdeaInDetail')}</p>
      </motion.div>

      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.07 }} className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6 space-y-5">
        <Field label={t('ideaForm.startupName')} name="startupName" value={form.startupName} onChange={handleChange} placeholder={t('ideaForm.eGEdusparkVn')} />

        <div>
          <label className="block text-sm font-semibold text-slate-700 mb-1.5">{t('ideaForm.currentStage')}</label>
          <div className="flex gap-2 flex-wrap">
            {STAGES.map(s => (
              <button key={s} type="button" onClick={() => setForm(p => ({ ...p, stage: s }))}
                className={`px-4 py-1.5 rounded-lg text-sm font-medium transition-all ${form.stage === s ? 'bg-primary text-white shadow-sm' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}>
                {t(`ideaForm.stages.${s}`)}
              </button>
            ))}
          </div>
        </div>

        <Field label={t('ideaForm.problemStatement')} name="problem" value={form.problem} onChange={handleChange} multiline placeholder={t('ideaForm.whatProblemAreYouSolvingWho')} />
        <Field label={t('ideaForm.targetCustomer')} name="targetCustomer" value={form.targetCustomer} onChange={handleChange} multiline placeholder={t('ideaForm.whoIsYourPrimaryCustomerDemographics')} />
        <Field label={t('ideaForm.proposedSolution')} name="solution" value={form.solution} onChange={handleChange} multiline placeholder={t('ideaForm.howDoesYourProductServiceSolve')} />

        {/* Advanced Section */}
        <button
          type="button"
          onClick={() => setShowAdvanced(!showAdvanced)}
          className="flex items-center gap-2 text-sm text-primary-700 font-medium hover:underline"
        >
          {showAdvanced ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
          {showAdvanced ? t('ideaForm.hide') : t('ideaForm.show')} {t('ideaForm.advancedFieldsBusinessModelTechMarket')}
        </button>

        {showAdvanced && (
          <div className="space-y-5 pt-2 border-t border-slate-100">
            <Field label={t('ideaForm.businessModel')} name="businessModel" value={form.businessModel} onChange={handleChange} multiline placeholder={t('ideaForm.howWillYouMakeMoneySubscription')} />
            <Field label={t('ideaForm.technologyStack')} name="technology" value={form.technology} onChange={handleChange} multiline placeholder={t('ideaForm.whatTechnologiesWillYouUseFrontend')} />
            <Field label={t('ideaForm.marketAnalysis')} name="marketAnalysis" value={form.marketAnalysis} onChange={handleChange} multiline placeholder={t('ideaForm.tamSamSomMarketSizeGrowth')} />
            <Field label={t('ideaForm.competitors')} name="competitors" value={form.competitors} onChange={handleChange} multiline placeholder={t('ideaForm.whoAreYourCompetitorsWhatS')} />
          </div>
        )}
      </motion.div>

      {/* Actions */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.14 }} className="flex gap-3 justify-end">
        <Button variant="ghost" onClick={() => navigate('/student')}>{t('common:actions.cancel')}</Button>
        <Button variant="outline" icon={Save} isLoading={saving} onClick={() => handleSave('DRAFT')}>{t('ideaForm.saveDraft')}</Button>
        <Button variant="primary" icon={Send} isLoading={submitting} onClick={() => handleSave('SUBMITTED')}>{t('ideaForm.submitForReview')}</Button>
      </motion.div>
    </div>
  );
};

export default IdeaForm;
