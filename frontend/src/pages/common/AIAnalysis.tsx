// @ts-nocheck
import { useState, useEffect } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { motion } from 'framer-motion';
import { aiApi } from '../../api/aiApi';
import { startupApi } from '../../api/startupApi';
import { dashboardApi } from '../../api/dashboardApi';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import EmptyState from '../../components/ui/EmptyState';
import Button from '../../components/ui/Button';
import { Brain, Zap, AlertTriangle, TrendingUp, ShieldAlert, Lightbulb, Link2, CheckCircle, XCircle, LayoutTemplate, MessageCircle, Target, Rocket, Search, BookOpen } from 'lucide-react';
import SimilarIdeasTab from '../../components/ai/SimilarIdeasTab';
import RubricGeneratorTab from '../../components/ai/RubricGeneratorTab';
import SentimentAnalysisTab from '../../components/ai/SentimentAnalysisTab';
import { useTranslation } from 'react-i18next';

const ScoreRing = ({ score, label, colorOverride }) => {
  const color = colorOverride || (score >= 80 ? '#16A34A' : score >= 60 ? '#F56A2A' : '#DC2626');
  const circumference = 2 * Math.PI * 35;
  const dash = (score / 100) * circumference;

  return (
    <div className="flex flex-col items-center">
      <div className="relative w-24 h-24 mb-2">
        <svg className="w-full h-full -rotate-90" viewBox="0 0 100 100">
          <circle cx="50" cy="50" r="35" fill="none" stroke="#f1f5f9" strokeWidth="8" />
          <circle cx="50" cy="50" r="35" fill="none" stroke={color} strokeWidth="8" strokeDasharray={`${dash} ${circumference}`} strokeLinecap="round" style={{ transition: 'stroke-dasharray 1s ease' }} />
        </svg>
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <span className="text-2xl font-black text-slate-900">{score}</span>
        </div>
      </div>
      <span className="text-xs font-semibold text-slate-500 text-center uppercase tracking-wider">{label}</span>
    </div>
  );
};

const AIAnalysis = () => {
  const { t } = useTranslation('ai');
  const { startupIdeaId } = useParams();
  const [searchParams] = useSearchParams();
  const ideaId = startupIdeaId || searchParams.get('ideaId');

  const [formData, setFormData] = useState({
    startup_name: '',
    problem: '',
    solution: '',
    target_customer: '',
    business_model: '',
    technology: '',
    market: ''
  });

  const [analysis, setAnalysis] = useState(null);
  const [loading, setLoading] = useState(true);
  const [analyzing, setAnalyzing] = useState(false);

  // Suggestion State
  const [activeTab, setActiveTab] = useState('analysis');
  const [suggestData, setSuggestData] = useState({ stage: 'Idea', focus_area: 'General' });
  const [suggestion, setSuggestion] = useState(null);
  const [suggesting, setSuggesting] = useState(false);

  useEffect(() => {
    const loadData = async () => {
      try {
        let loadedIdea = null;
        if (ideaId) {
          const ideaRes = await startupApi.getById(ideaId);
          loadedIdea = ideaRes.data?.startupIdea || ideaRes.startupIdea || ideaRes.data;
        } else {
          // If no id, attempt to fetch from dashboard
          try {
             const dashRes = await dashboardApi.getStudent();
             const dashData = dashRes.data || dashRes;
             loadedIdea = dashData.startupIdea;
          } catch(e) {
             // Ignore dashboard load error
          }
        }
        
        if (loadedIdea) {
          setFormData({
            startup_name: loadedIdea.startupName || '',
            problem: loadedIdea.problem || '',
            solution: loadedIdea.solution || '',
            target_customer: loadedIdea.targetCustomer || '',
            business_model: loadedIdea.businessModel || '',
            technology: loadedIdea.technology || '',
            market: loadedIdea.marketAnalysis || ''
          });
        }
      } catch (err) {
        // Do nothing if idea cannot be loaded, just show empty form
      } finally {
        setLoading(false);
      }
    };
    loadData();
  }, [ideaId]);

  const handleChange = (e) => {
    setFormData(prev => ({ ...prev, [e.target.name]: e.target.value }));
  };

  const handleSuggestChange = (e) => {
    setSuggestData(prev => ({ ...prev, [e.target.name]: e.target.value }));
  };

  const handleAnalyze = async (e) => {
    e?.preventDefault();
    if (!formData.startup_name || !formData.problem || !formData.solution) {
      toast.error(t('aIAnalysis.pleaseFillInTheStartupName'));
      return;
    }
    setAnalyzing(true);
    setAnalysis(null);
    try {
      const res = await aiApi.analyzeStartup(formData);
      setAnalysis(res.data);
      if (res.data?.overall_score === 50 && res.data?.ai_feedback?.includes("phản hồi dự phòng")) {
        toast.error(t('aIAnalysis.aiIsUsingAFallbackResponse'), { duration: 5000 });
      } else {
        toast.success(t('aIAnalysis.aiAnalysisComplete'));
      }
    } catch (err) {
      if (err.response?.status === 401 || err.response?.status === 403) {
        toast.error(t('aIAnalysis.yourSessionHasExpiredOrYou'));
      } else {
        toast.error(err.response?.data?.error || err.message || t('aIAnalysis.analysisFailed'));
      }
    } finally {
      setAnalyzing(false);
    }
  };

  const handleSuggest = async (e) => {
    e?.preventDefault();
    if (!formData.startup_name) {
      toast.error(t('aIAnalysis.pleaseEnterTheStartupNameIn'));
      setActiveTab('analysis');
      return;
    }
    setSuggesting(true);
    setSuggestion(null);
    try {
      const currentProp = `Problem: ${formData.problem || ''}. Solution: ${formData.solution || ''}. Customer: ${formData.target_customer || ''}`;
      if (!formData.problem && !formData.solution) {
        toast.error(t('aIAnalysis.pleaseFillInTheProblemSolution'));
      }
      
      const payload = {
        startup_name: formData.startup_name,
        current_proposal: currentProp.trim().length > 20 ? currentProp : "Developing a new startup idea",
        stage: suggestData.stage,
        focus_area: suggestData.focus_area
      };
      const res = await aiApi.suggestStartup(payload);
      setSuggestion(res.data);
      if (res.data?.suggestions?.[0]?.current_issue?.includes("dự phòng")) {
        toast.error(t('aIAnalysis.aiDangDungPhanHoiDu'), { duration: 5000 });
      } else {
        toast.success(t('aIAnalysis.aiSuggestionComplete'));
      }
    } catch (err) {
      if (err.response?.status === 401 || err.response?.status === 403) {
        toast.error(t('aIAnalysis.yourSessionHasExpired'));
      } else {
        toast.error(err.response?.data?.error || err.message || t('aIAnalysis.suggestionFailed'));
      }
    } finally {
      setSuggesting(false);
    }
  };

  if (loading) return <LoadingSkeleton />;

  return (
    <div className="space-y-6 max-w-6xl mx-auto">
      {/* Header */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="rounded-2xl p-8 text-white shadow-md bg-secondary">
        <div className="flex items-center gap-4">
          <div className="p-4 bg-white/10 rounded-xl">
            <Brain className="w-10 h-10 text-primary-600" />
          </div>
          <div>
            <h1 className="text-2xl md:text-3xl font-bold mb-1">{t('aIAnalysis.directAiStartupAnalysis')}</h1>
            <p className="text-white/80 text-sm">{t('aIAnalysis.poweredByGoogleGeminiGetDeep')}</p>
          </div>
        </div>
      </motion.div>

      <div className="flex border-b border-slate-200 overflow-x-auto no-scrollbar bg-white rounded-t-2xl shadow-sm">
        <button
          className={`px-6 py-4 font-semibold text-sm transition-colors relative whitespace-nowrap ${activeTab === 'analysis' ? 'text-secondary' : 'text-slate-500 hover:text-slate-700'}`}
          onClick={() => setActiveTab('analysis')}
        >
          <div className="flex items-center gap-2"><Brain className="w-4 h-4" /> {t('aIAnalysis.aiAnalysis')}</div>
          {activeTab === 'analysis' && <motion.div layoutId="tabIndicator" className="absolute bottom-0 left-0 right-0 h-0.5 bg-secondary" />}
        </button>
        <button
          className={`px-6 py-4 font-semibold text-sm transition-colors relative whitespace-nowrap ${activeTab === 'suggestion' ? 'text-primary-600' : 'text-slate-500 hover:text-slate-700'}`}
          onClick={() => setActiveTab('suggestion')}
        >
          <div className="flex items-center gap-2"><Lightbulb className="w-4 h-4" /> {t('aIAnalysis.aiSuggestion')}</div>
          {activeTab === 'suggestion' && <motion.div layoutId="tabIndicator" className="absolute bottom-0 left-0 right-0 h-0.5 bg-primary" />}
        </button>
        <button
          className={`px-6 py-4 font-semibold text-sm transition-colors relative whitespace-nowrap ${activeTab === 'similar' ? 'text-secondary' : 'text-slate-500 hover:text-slate-700'}`}
          onClick={() => setActiveTab('similar')}
        >
          <div className="flex items-center gap-2"><Search className="w-4 h-4" /> {t('aIAnalysis.similarIdeas')}</div>
          {activeTab === 'similar' && <motion.div layoutId="tabIndicator" className="absolute bottom-0 left-0 right-0 h-0.5 bg-secondary" />}
        </button>
        <button
          className={`px-6 py-4 font-semibold text-sm transition-colors relative whitespace-nowrap ${activeTab === 'rubric' ? 'text-success' : 'text-slate-500 hover:text-slate-700'}`}
          onClick={() => setActiveTab('rubric')}
        >
          <div className="flex items-center gap-2"><BookOpen className="w-4 h-4" /> {t('aIAnalysis.rubricGenerator')}</div>
          {activeTab === 'rubric' && <motion.div layoutId="tabIndicator" className="absolute bottom-0 left-0 right-0 h-0.5 bg-success" />}
        </button>
        <button
          className={`px-6 py-4 font-semibold text-sm transition-colors relative whitespace-nowrap ${activeTab === 'sentiment' ? 'text-secondary' : 'text-slate-500 hover:text-slate-700'}`}
          onClick={() => setActiveTab('sentiment')}
        >
          <div className="flex items-center gap-2"><MessageCircle className="w-4 h-4" /> {t('aIAnalysis.sentimentAnalysis')}</div>
          {activeTab === 'sentiment' && <motion.div layoutId="tabIndicator" className="absolute bottom-0 left-0 right-0 h-0.5 bg-secondary-700" />}
        </button>
      </div>

      {activeTab === 'analysis' || activeTab === 'suggestion' ? (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        
        {/* Form Panel */}
        <div className="lg:col-span-4 bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col">
          <div className="p-5 border-b border-slate-100 bg-slate-50 flex items-center justify-between">
             <h2 className="font-semibold text-slate-800 text-lg">{activeTab === 'analysis' ? t('aIAnalysis.ideaData') : t('aIAnalysis.suggestionConfig')}</h2>
             <span className="text-xs font-medium px-2 py-1 bg-blue-100 text-secondary rounded-md">{t('rubricGeneratorTab.input')}</span>
          </div>
          <div className="p-5 flex-1">
            {activeTab === 'analysis' ? (
              <form onSubmit={handleAnalyze} className="space-y-4 flex flex-col h-full">
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('aIAnalysis.startupName')}</label>
                  <input required name="startup_name" value={formData.startup_name} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('rubricGeneratorTab.eGEdutrack')} />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('aIAnalysis.problem')}</label>
                  <textarea required rows={2} name="problem" value={formData.problem} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('aIAnalysis.describeTheCustomerPainPoint')} />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('aIAnalysis.solution')}</label>
                  <textarea required rows={2} name="solution" value={formData.solution} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('aIAnalysis.howDoesYourProductSolveThe')} />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('aIAnalysis.targetCustomer')}</label>
                  <input required name="target_customer" value={formData.target_customer} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('aIAnalysis.eGStudentsAged1824')} />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('aIAnalysis.businessModel')}</label>
                  <input name="business_model" value={formData.business_model} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('aIAnalysis.eGB2bSaasFreemium')} />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('aIAnalysis.technology')}</label>
                  <input name="technology" value={formData.technology} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('aIAnalysis.eGReactNativeAiBlockchain')} />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('aIAnalysis.market')}</label>
                  <input name="market" value={formData.market} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('aIAnalysis.eGEdtechVietnam')} />
                </div>
                
                <div className="pt-4 mt-auto">
                  <Button type="submit" className="w-full bg-secondary hover:bg-secondary-700 text-white" isLoading={analyzing} disabled={analyzing} icon={Zap}>
                    {t('aIAnalysis.analyzeWithAi')}
                  </Button>
                </div>
              </form>
            ) : (
              <form onSubmit={handleSuggest} className="space-y-4 flex flex-col h-full">
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('aIAnalysis.startupName2')}</label>
                  <input disabled value={formData.startup_name || '(Empty — please enter in the Analysis tab)'} className="w-full text-sm border-slate-200 bg-slate-50 rounded-lg text-slate-500" />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('aIAnalysis.currentStage')}</label>
                  <select required name="stage" value={suggestData.stage} onChange={handleSuggestChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-primary focus:border-primary">
                    <option value="Idea">{t('aIAnalysis.ideaJustAnIdea')}</option>
                    <option value="Prototype">{t('aIAnalysis.prototypeBuildingADraft')}</option>
                    <option value="MVP">{t('aIAnalysis.mvpHaveASampleProduct')}</option>
                    <option value="Growth">{t('aIAnalysis.growthFindingCustomers')}</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('aIAnalysis.focusArea')}</label>
                  <select required name="focus_area" value={suggestData.focus_area} onChange={handleSuggestChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-primary focus:border-primary">
                    <option value="General">{t('aIAnalysis.generalOverview')}</option>
                    <option value="Product">{t('aIAnalysis.productFeatures')}</option>
                    <option value="Market">{t('aIAnalysis.marketCustomers')}</option>
                    <option value="Tech">{t('aIAnalysis.techTechnologyArchitecture')}</option>
                  </select>
                </div>
                <div className="pt-4 mt-auto">
                  <Button type="submit" className="w-full bg-primary hover:bg-primary-600 text-white" isLoading={suggesting} disabled={suggesting} icon={Lightbulb}>
                    {t('aIAnalysis.getAiSuggestions')}
                  </Button>
                </div>
              </form>
            )}
          </div>
        </div>

        {/* Result Panel */}
        <div className="lg:col-span-8">
          {activeTab === 'analysis' && (
            analyzing ? (
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
                 <div className="w-16 h-16 border-4 border-secondary/30 border-t-secondary rounded-full animate-spin mb-4"></div>
                 <h3 className="text-xl font-semibold text-secondary">{t('aIAnalysis.aiIsAnalyzingYourStartup')}</h3>
                 <p className="text-slate-500 mt-2">{t('aIAnalysis.thisMayTakeAFewSeconds')}</p>
              </div>
            ) : !analysis ? (
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
                <div className="w-24 h-24 bg-slate-50 rounded-full flex items-center justify-center mb-6">
                  <Brain className="w-12 h-12 text-slate-300" />
                </div>
                <h3 className="text-xl font-semibold text-slate-700">{t('aIAnalysis.readyForAnalysis')}</h3>
                <p className="text-slate-500 mt-2 max-w-sm text-center">{t('aIAnalysis.fillInYourStartupDetailsAnd')}</p>
              </div>
            ) : (
            <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-6">
              
              {/* Scores Header */}
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
                <div className="flex flex-col items-center border-b border-slate-100 pb-6 mb-6">
                   <h2 className="text-2xl font-bold text-secondary mb-1">{t('aIAnalysis.analysisResults')}</h2>
                   {analysis.overall_score === 50 && analysis.ai_feedback?.includes("phản hồi dự phòng") && (
                     <div className="mb-2 bg-orange-100 text-orange-800 text-xs px-3 py-1.5 rounded-md flex items-center gap-1.5 font-medium border border-orange-200">
                       <AlertTriangle className="w-3.5 h-3.5" />
                       {t('aIAnalysis.aiIsUsingAFallbackResponse2')}
                     </div>
                   )}
                   <p className="text-slate-500 text-sm">{t('similarIdeasTab.riskLevel')} <span className={`font-bold ${analysis.risk_level === 'Low' ? 'text-success' : analysis.risk_level === 'High' ? 'text-red-500' : 'text-primary-600'}`}>{analysis.risk_level || t('sentimentAnalysisTab.unknown')}</span></p>
                </div>
                
                <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
                  <ScoreRing score={analysis.overall_score ?? 'N/A'} label={t('aIAnalysis.overall')} colorOverride="#17365D" />
                  <ScoreRing score={analysis.innovation_score ?? 'N/A'} label={t('aIAnalysis.innovation')} />
                  <ScoreRing score={analysis.feasibility_score ?? 'N/A'} label={t('aIAnalysis.feasibility')} />
                  <ScoreRing score={analysis.market_potential_score ?? 'N/A'} label={t('aIAnalysis.market')} />
                  <ScoreRing score={analysis.technical_readiness_score ?? 'N/A'} label={t('aIAnalysis.tech')} />
                </div>
              </div>

              {/* Feedback & Recommendation */}
              <div className="border border-blue-100 rounded-2xl p-6 bg-secondary">
                <div className="flex items-start gap-4">
                  <div className="p-3 bg-white rounded-xl shadow-sm"><Lightbulb className="w-6 h-6 text-secondary" /></div>
                  <div>
                    <h3 className="text-lg font-bold text-secondary mb-2">{t('aIAnalysis.aiFeedbackRecommendation')}</h3>
                    <p className="text-slate-700 leading-relaxed mb-3">{analysis.ai_feedback}</p>
                    <div className="p-4 bg-white/60 rounded-xl border border-blue-100/50">
                      <span className="font-semibold text-slate-800">{t('aIAnalysis.nextStep')} </span>
                      <span className="text-slate-700">{analysis.recommendation}</span>
                    </div>
                  </div>
                </div>
              </div>

              {/* SWOT Grid */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                {/* Strengths */}
                <div className="bg-white rounded-2xl border border-green-200 shadow-sm p-6">
                  <h3 className="text-lg font-bold text-success mb-4 flex items-center gap-2"><CheckCircle className="w-5 h-5" />{t('aIAnalysis.strengths')}</h3>
                  <ul className="space-y-3">
                    {Array.isArray(analysis.strengths) && analysis.strengths.map((item, i) => (
                      <li key={i} className="flex items-start gap-3 text-sm text-slate-700">
                        <div className="w-1.5 h-1.5 bg-success rounded-full mt-1.5 shrink-0" />
                        <span className="leading-relaxed">{typeof item === 'string' ? item : JSON.stringify(item)}</span>
                      </li>
                    ))}
                  </ul>
                </div>

                {/* Weaknesses */}
                <div className="bg-white rounded-2xl border border-red-200 shadow-sm p-6">
                  <h3 className="text-lg font-bold text-red-500 mb-4 flex items-center gap-2"><XCircle className="w-5 h-5" />{t('aIAnalysis.weaknesses')}</h3>
                  <ul className="space-y-3">
                    {Array.isArray(analysis.weaknesses) && analysis.weaknesses.map((item, i) => (
                      <li key={i} className="flex items-start gap-3 text-sm text-slate-700">
                        <div className="w-1.5 h-1.5 bg-red-500 rounded-full mt-1.5 shrink-0" />
                        <span className="leading-relaxed">{typeof item === 'string' ? item : JSON.stringify(item)}</span>
                      </li>
                    ))}
                  </ul>
                </div>

                {/* Opportunities */}
                <div className="bg-white rounded-2xl border border-blue-200 shadow-sm p-6">
                  <h3 className="text-lg font-bold text-secondary mb-4 flex items-center gap-2"><TrendingUp className="w-5 h-5" />{t('aIAnalysis.opportunities')}</h3>
                  <ul className="space-y-3">
                    {Array.isArray(analysis.opportunities) && analysis.opportunities.map((item, i) => (
                      <li key={i} className="flex items-start gap-3 text-sm text-slate-700">
                        <div className="w-1.5 h-1.5 bg-secondary rounded-full mt-1.5 shrink-0" />
                        <span className="leading-relaxed">{typeof item === 'string' ? item : JSON.stringify(item)}</span>
                      </li>
                    ))}
                  </ul>
                </div>

                {/* Threats */}
                <div className="bg-white rounded-2xl border border-orange-200 shadow-sm p-6">
                  <h3 className="text-lg font-bold text-primary-600 mb-4 flex items-center gap-2"><ShieldAlert className="w-5 h-5" />{t('aIAnalysis.threats')}</h3>
                  <ul className="space-y-3">
                    {Array.isArray(analysis.threats) && analysis.threats.map((item, i) => (
                      <li key={i} className="flex items-start gap-3 text-sm text-slate-700">
                        <div className="w-1.5 h-1.5 bg-primary rounded-full mt-1.5 shrink-0" />
                        <span className="leading-relaxed">{typeof item === 'string' ? item : JSON.stringify(item)}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>

            </motion.div>
            )
          )}

          {activeTab === 'suggestion' && (
            suggesting ? (
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
                 <div className="w-16 h-16 border-4 border-primary/30 border-t-primary rounded-full animate-spin mb-4"></div>
                 <h3 className="text-xl font-semibold text-primary-600">{t('aIAnalysis.generatingSuggestions')}</h3>
                 <p className="text-slate-500 mt-2">{t('aIAnalysis.aiIsCraftingActionableAdviceFor')}</p>
              </div>
            ) : !suggestion ? (
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
                <div className="w-24 h-24 bg-slate-50 rounded-full flex items-center justify-center mb-6">
                  <Lightbulb className="w-12 h-12 text-slate-300" />
                </div>
                <h3 className="text-xl font-semibold text-slate-700">{t('aIAnalysis.readyForAiSuggestions')}</h3>
                <p className="text-slate-500 mt-2 max-w-sm text-center">{t('aIAnalysis.chooseYourStageAndFocusArea')}</p>
              </div>
            ) : (
              <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-6">
                {/* Fallback Warning */}
                {suggestion.suggestions?.[0]?.current_issue?.includes("fallback") && (
                   <div className="bg-orange-100 text-orange-800 text-sm px-4 py-3 rounded-xl flex items-center gap-2 font-medium border border-orange-200 shadow-sm">
                     <AlertTriangle className="w-5 h-5 shrink-0" />
                      {t('aIAnalysis.aiIsUsingAFallbackResponse3')}
                   </div>
                )}
                
                {/* Suggestions List */}
                <div className="space-y-4">
                  <h2 className="text-2xl font-bold text-primary-600 flex items-center gap-2 mb-4">
                    <Lightbulb className="w-6 h-6" /> {t('aIAnalysis.actionableSuggestions')}
                  </h2>
                  
                  {Array.isArray(suggestion.suggestions) && suggestion.suggestions.map((item, i) => (
                    <div key={i} className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
                      <div className={`px-5 py-3 border-b flex justify-between items-center ${item.priority === 'High' ? 'bg-red-50 border-red-100' : item.priority === 'Low' ? 'bg-green-50 border-green-100' : 'bg-orange-50 border-orange-100'}`}>
                        <div className="flex items-center gap-2 font-semibold text-slate-800">
                           <Target className="w-4 h-4 text-slate-500" /> {t('aIAnalysis.area')} {typeof item.area === 'string' ? item.area : t('rubricGeneratorTab.general')}
                        </div>
                        <span className={`text-xs font-bold uppercase tracking-wider px-2 py-1 rounded-md ${item.priority === 'High' ? 'bg-red-100 text-red-700' : item.priority === 'Low' ? 'bg-green-100 text-green-700' : 'bg-orange-100 text-orange-700'}`}>
                          {t('aiAnalysis.priority', { level: t(`aiAnalysis.priorities.${typeof item.priority === 'string' ? item.priority : 'Medium'}`, { defaultValue: typeof item.priority === 'string' ? item.priority : 'Medium' }) })}
                        </span>
                      </div>
                      <div className="p-5 space-y-4">
                        <div>
                          <p className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-1">{t('aIAnalysis.currentIssue')}</p>
                          <p className="text-slate-700 text-sm leading-relaxed">{typeof item.current_issue === 'string' ? item.current_issue : t('rubricGeneratorTab.nA')}</p>
                        </div>
                        <div className="bg-slate-50 p-4 rounded-xl border border-slate-100">
                          <p className="text-xs font-bold text-primary-600 uppercase tracking-wider mb-1 flex items-center gap-1"><Zap className="w-3.5 h-3.5" /> {t('aIAnalysis.aiSuggestion')}</p>
                          <p className="text-slate-800 font-medium leading-relaxed">{typeof item.suggestion === 'string' ? item.suggestion : t('rubricGeneratorTab.nA')}</p>
                        </div>
                        <div>
                          <p className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-1">{t('aIAnalysis.exampleAction')}</p>
                          <p className="text-slate-600 text-sm leading-relaxed italic border-l-2 border-slate-300 pl-3">{typeof item.example === 'string' ? item.example : t('rubricGeneratorTab.nA')}</p>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>

                {/* MVP & Next Steps Grid */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                  {/* MVP */}
                  <div className="rounded-2xl p-6 text-white shadow-sm md:col-span-2 bg-secondary">
                    <h3 className="text-lg font-bold mb-3 flex items-center gap-2"><Rocket className="w-5 h-5 text-primary-600" /> {t('aIAnalysis.mvpRecommendation')}</h3>
                    <p className="text-white/90 leading-relaxed text-lg">{suggestion.mvp_recommendation || t('rubricGeneratorTab.nA')}</p>
                  </div>

                  {/* Next Steps */}
                  <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
                    <h3 className="text-lg font-bold text-secondary mb-4 flex items-center gap-2"><LayoutTemplate className="w-5 h-5" /> {t('aIAnalysis.nextSteps')}</h3>
                    <ul className="space-y-3">
                      {Array.isArray(suggestion.next_steps) && suggestion.next_steps.map((step, i) => (
                        <li key={i} className="flex items-start gap-3 text-sm text-slate-700">
                          <div className="w-6 h-6 rounded-full bg-blue-100 text-secondary font-bold flex items-center justify-center shrink-0 text-xs mt-0.5">{i + 1}</div>
                          <span className="leading-relaxed mt-0.5">{typeof step === 'string' ? step : JSON.stringify(step)}</span>
                        </li>
                      ))}
                    </ul>
                  </div>

                  {/* Mentor Questions */}
                  <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
                    <h3 className="text-lg font-bold text-primary-600 mb-4 flex items-center gap-2"><MessageCircle className="w-5 h-5" /> {t('aIAnalysis.mentorQuestions')}</h3>
                    <ul className="space-y-3">
                      {Array.isArray(suggestion.mentor_questions) && suggestion.mentor_questions.map((q, i) => (
                        <li key={i} className="flex items-start gap-3 text-sm text-slate-700">
                          <div className="w-1.5 h-1.5 bg-primary rounded-full mt-2 shrink-0" />
                          <span className="leading-relaxed italic">{typeof q === 'string' ? q : JSON.stringify(q)}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                </div>

              </motion.div>
            )
          )}
        </div>
      </div>
      ) : null}

      {activeTab === 'similar' && <SimilarIdeasTab initialData={formData} />}
      {activeTab === 'rubric' && <RubricGeneratorTab initialData={formData} />}
      {activeTab === 'sentiment' && <SentimentAnalysisTab ideaId={ideaId} />}
    </div>
  );
};

export default AIAnalysis;
