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

const ScoreRing = ({ score, label, colorOverride }) => {
  const color = colorOverride || (score >= 80 ? '#51B848' : score >= 60 ? '#F37021' : '#ef4444');
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
      toast.error('Please fill in the startup name, problem, and solution.');
      return;
    }
    setAnalyzing(true);
    setAnalysis(null);
    try {
      const res = await aiApi.analyzeStartup(formData);
      setAnalysis(res.data);
      if (res.data?.overall_score === 50 && res.data?.ai_feedback?.includes("phản hồi dự phòng")) {
        toast.error('AI is using a fallback response. Please check your Gemini API key or try again later.', { duration: 5000 });
      } else {
        toast.success('AI Analysis complete!');
      }
    } catch (err) {
      if (err.response?.status === 401 || err.response?.status === 403) {
        toast.error('Your session has expired or you do not have permission to use AI.');
      } else {
        toast.error(err.response?.data?.error || err.message || 'Analysis failed');
      }
    } finally {
      setAnalyzing(false);
    }
  };

  const handleSuggest = async (e) => {
    e?.preventDefault();
    if (!formData.startup_name) {
      toast.error('Please enter the Startup Name in the Analysis tab first.');
      setActiveTab('analysis');
      return;
    }
    setSuggesting(true);
    setSuggestion(null);
    try {
      const currentProp = `Problem: ${formData.problem || ''}. Solution: ${formData.solution || ''}. Customer: ${formData.target_customer || ''}`;
      if (!formData.problem && !formData.solution) {
        toast.error('Please fill in the Problem/Solution in the Analysis tab first for best suggestion quality.');
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
        toast.error('AI đang dùng phản hồi dự phòng.', { duration: 5000 });
      } else {
        toast.success('AI Suggestion complete!');
      }
    } catch (err) {
      if (err.response?.status === 401 || err.response?.status === 403) {
        toast.error('Your session has expired.');
      } else {
        toast.error(err.response?.data?.error || err.message || 'Suggestion failed');
      }
    } finally {
      setSuggesting(false);
    }
  };

  if (loading) return <LoadingSkeleton />;

  return (
    <div className="space-y-6 max-w-6xl mx-auto">
      {/* Header */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="bg-gradient-to-r from-[#034EA2] to-[#1e88e5] rounded-2xl p-8 text-white shadow-md">
        <div className="flex items-center gap-4">
          <div className="p-4 bg-white/10 rounded-xl">
            <Brain className="w-10 h-10 text-[#F37021]" />
          </div>
          <div>
            <h1 className="text-2xl md:text-3xl font-bold mb-1">Direct AI Startup Analysis</h1>
            <p className="text-white/80 text-sm">Powered by Google Gemini - Get deep insights into your startup</p>
          </div>
        </div>
      </motion.div>

      <div className="flex border-b border-slate-200 overflow-x-auto no-scrollbar bg-white rounded-t-2xl shadow-sm">
        <button
          className={`px-6 py-4 font-semibold text-sm transition-colors relative whitespace-nowrap ${activeTab === 'analysis' ? 'text-[#034EA2]' : 'text-slate-500 hover:text-slate-700'}`}
          onClick={() => setActiveTab('analysis')}
        >
          <div className="flex items-center gap-2"><Brain className="w-4 h-4" /> AI Analysis</div>
          {activeTab === 'analysis' && <motion.div layoutId="tabIndicator" className="absolute bottom-0 left-0 right-0 h-0.5 bg-[#034EA2]" />}
        </button>
        <button
          className={`px-6 py-4 font-semibold text-sm transition-colors relative whitespace-nowrap ${activeTab === 'suggestion' ? 'text-[#F37021]' : 'text-slate-500 hover:text-slate-700'}`}
          onClick={() => setActiveTab('suggestion')}
        >
          <div className="flex items-center gap-2"><Lightbulb className="w-4 h-4" /> AI Suggestion</div>
          {activeTab === 'suggestion' && <motion.div layoutId="tabIndicator" className="absolute bottom-0 left-0 right-0 h-0.5 bg-[#F37021]" />}
        </button>
        <button
          className={`px-6 py-4 font-semibold text-sm transition-colors relative whitespace-nowrap ${activeTab === 'similar' ? 'text-[#034EA2]' : 'text-slate-500 hover:text-slate-700'}`}
          onClick={() => setActiveTab('similar')}
        >
          <div className="flex items-center gap-2"><Search className="w-4 h-4" /> Similar Ideas</div>
          {activeTab === 'similar' && <motion.div layoutId="tabIndicator" className="absolute bottom-0 left-0 right-0 h-0.5 bg-[#034EA2]" />}
        </button>
        <button
          className={`px-6 py-4 font-semibold text-sm transition-colors relative whitespace-nowrap ${activeTab === 'rubric' ? 'text-[#51B848]' : 'text-slate-500 hover:text-slate-700'}`}
          onClick={() => setActiveTab('rubric')}
        >
          <div className="flex items-center gap-2"><BookOpen className="w-4 h-4" /> Rubric Generator</div>
          {activeTab === 'rubric' && <motion.div layoutId="tabIndicator" className="absolute bottom-0 left-0 right-0 h-0.5 bg-[#51B848]" />}
        </button>
        <button
          className={`px-6 py-4 font-semibold text-sm transition-colors relative whitespace-nowrap ${activeTab === 'sentiment' ? 'text-indigo-600' : 'text-slate-500 hover:text-slate-700'}`}
          onClick={() => setActiveTab('sentiment')}
        >
          <div className="flex items-center gap-2"><MessageCircle className="w-4 h-4" /> Sentiment Analysis</div>
          {activeTab === 'sentiment' && <motion.div layoutId="tabIndicator" className="absolute bottom-0 left-0 right-0 h-0.5 bg-indigo-600" />}
        </button>
      </div>

      {activeTab === 'analysis' || activeTab === 'suggestion' ? (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        
        {/* Form Panel */}
        <div className="lg:col-span-4 bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col">
          <div className="p-5 border-b border-slate-100 bg-slate-50 flex items-center justify-between">
             <h2 className="font-semibold text-slate-800 text-lg">{activeTab === 'analysis' ? 'Idea Data' : 'Suggestion Config'}</h2>
             <span className="text-xs font-medium px-2 py-1 bg-blue-100 text-[#034EA2] rounded-md">Input</span>
          </div>
          <div className="p-5 flex-1">
            {activeTab === 'analysis' ? (
              <form onSubmit={handleAnalyze} className="space-y-4 flex flex-col h-full">
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Startup Name *</label>
                  <input required name="startup_name" value={formData.startup_name} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="e.g. EduTrack" />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Problem *</label>
                  <textarea required rows={2} name="problem" value={formData.problem} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="Describe the customer pain point..." />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Solution *</label>
                  <textarea required rows={2} name="solution" value={formData.solution} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="How does your product solve the problem..." />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Target Customer *</label>
                  <input required name="target_customer" value={formData.target_customer} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="e.g. Students aged 18-24" />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Business Model</label>
                  <input name="business_model" value={formData.business_model} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="e.g. B2B SaaS, Freemium" />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Technology</label>
                  <input name="technology" value={formData.technology} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="e.g. React Native, AI, Blockchain" />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Market</label>
                  <input name="market" value={formData.market} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="e.g. EdTech Vietnam" />
                </div>
                
                <div className="pt-4 mt-auto">
                  <Button type="submit" className="w-full bg-[#034EA2] hover:bg-[#023B7A] text-white" isLoading={analyzing} disabled={analyzing} icon={Zap}>
                    Analyze with AI
                  </Button>
                </div>
              </form>
            ) : (
              <form onSubmit={handleSuggest} className="space-y-4 flex flex-col h-full">
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Startup Name</label>
                  <input disabled value={formData.startup_name || '(Empty — please enter in the Analysis tab)'} className="w-full text-sm border-slate-200 bg-slate-50 rounded-lg text-slate-500" />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Current Stage *</label>
                  <select required name="stage" value={suggestData.stage} onChange={handleSuggestChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#F37021] focus:border-[#F37021]">
                    <option value="Idea">Idea (Just an idea)</option>
                    <option value="Prototype">Prototype (Building a draft)</option>
                    <option value="MVP">MVP (Have a sample product)</option>
                    <option value="Growth">Growth (Finding customers)</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Focus Area *</label>
                  <select required name="focus_area" value={suggestData.focus_area} onChange={handleSuggestChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#F37021] focus:border-[#F37021]">
                    <option value="General">General (Overview)</option>
                    <option value="Product">Product (Features)</option>
                    <option value="Market">Market (Customers)</option>
                    <option value="Tech">Tech (Technology / Architecture)</option>
                  </select>
                </div>
                <div className="pt-4 mt-auto">
                  <Button type="submit" className="w-full bg-[#F37021] hover:bg-[#d96015] text-white" isLoading={suggesting} disabled={suggesting} icon={Lightbulb}>
                    Get AI Suggestions
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
                 <div className="w-16 h-16 border-4 border-[#034EA2]/30 border-t-[#034EA2] rounded-full animate-spin mb-4"></div>
                 <h3 className="text-xl font-semibold text-[#034EA2]">AI is analyzing your startup...</h3>
                 <p className="text-slate-500 mt-2">This may take a few seconds.</p>
              </div>
            ) : !analysis ? (
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
                <div className="w-24 h-24 bg-slate-50 rounded-full flex items-center justify-center mb-6">
                  <Brain className="w-12 h-12 text-slate-300" />
                </div>
                <h3 className="text-xl font-semibold text-slate-700">Ready for Analysis</h3>
                <p className="text-slate-500 mt-2 max-w-sm text-center">Fill in your startup details and click "Analyze with AI" to generate insights.</p>
              </div>
            ) : (
            <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-6">
              
              {/* Scores Header */}
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
                <div className="flex flex-col items-center border-b border-slate-100 pb-6 mb-6">
                   <h2 className="text-2xl font-bold text-[#034EA2] mb-1">Analysis Results</h2>
                   {analysis.overall_score === 50 && analysis.ai_feedback?.includes("phản hồi dự phòng") && (
                     <div className="mb-2 bg-orange-100 text-orange-800 text-xs px-3 py-1.5 rounded-md flex items-center gap-1.5 font-medium border border-orange-200">
                       <AlertTriangle className="w-3.5 h-3.5" />
                       AI is using a fallback response.
                     </div>
                   )}
                   <p className="text-slate-500 text-sm">Risk Level: <span className={`font-bold ${analysis.risk_level === 'Low' ? 'text-[#51B848]' : analysis.risk_level === 'High' ? 'text-red-500' : 'text-[#F37021]'}`}>{analysis.risk_level || 'Unknown'}</span></p>
                </div>
                
                <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
                  <ScoreRing score={analysis.overall_score ?? 'N/A'} label="Overall" colorOverride="#034EA2" />
                  <ScoreRing score={analysis.innovation_score ?? 'N/A'} label="Innovation" />
                  <ScoreRing score={analysis.feasibility_score ?? 'N/A'} label="Feasibility" />
                  <ScoreRing score={analysis.market_potential_score ?? 'N/A'} label="Market" />
                  <ScoreRing score={analysis.technical_readiness_score ?? 'N/A'} label="Tech" />
                </div>
              </div>

              {/* Feedback & Recommendation */}
              <div className="bg-gradient-to-r from-blue-50 to-indigo-50 border border-blue-100 rounded-2xl p-6">
                <div className="flex items-start gap-4">
                  <div className="p-3 bg-white rounded-xl shadow-sm"><Lightbulb className="w-6 h-6 text-[#034EA2]" /></div>
                  <div>
                    <h3 className="text-lg font-bold text-[#034EA2] mb-2">AI Feedback & Recommendation</h3>
                    <p className="text-slate-700 leading-relaxed mb-3">{analysis.ai_feedback}</p>
                    <div className="p-4 bg-white/60 rounded-xl border border-blue-100/50">
                      <span className="font-semibold text-slate-800">Next Step: </span>
                      <span className="text-slate-700">{analysis.recommendation}</span>
                    </div>
                  </div>
                </div>
              </div>

              {/* SWOT Grid */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                {/* Strengths */}
                <div className="bg-white rounded-2xl border border-green-200 shadow-sm p-6">
                  <h3 className="text-lg font-bold text-[#51B848] mb-4 flex items-center gap-2"><CheckCircle className="w-5 h-5" />Strengths</h3>
                  <ul className="space-y-3">
                    {Array.isArray(analysis.strengths) && analysis.strengths.map((item, i) => (
                      <li key={i} className="flex items-start gap-3 text-sm text-slate-700">
                        <div className="w-1.5 h-1.5 bg-[#51B848] rounded-full mt-1.5 shrink-0" />
                        <span className="leading-relaxed">{typeof item === 'string' ? item : JSON.stringify(item)}</span>
                      </li>
                    ))}
                  </ul>
                </div>

                {/* Weaknesses */}
                <div className="bg-white rounded-2xl border border-red-200 shadow-sm p-6">
                  <h3 className="text-lg font-bold text-red-500 mb-4 flex items-center gap-2"><XCircle className="w-5 h-5" />Weaknesses</h3>
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
                  <h3 className="text-lg font-bold text-[#034EA2] mb-4 flex items-center gap-2"><TrendingUp className="w-5 h-5" />Opportunities</h3>
                  <ul className="space-y-3">
                    {Array.isArray(analysis.opportunities) && analysis.opportunities.map((item, i) => (
                      <li key={i} className="flex items-start gap-3 text-sm text-slate-700">
                        <div className="w-1.5 h-1.5 bg-[#034EA2] rounded-full mt-1.5 shrink-0" />
                        <span className="leading-relaxed">{typeof item === 'string' ? item : JSON.stringify(item)}</span>
                      </li>
                    ))}
                  </ul>
                </div>

                {/* Threats */}
                <div className="bg-white rounded-2xl border border-orange-200 shadow-sm p-6">
                  <h3 className="text-lg font-bold text-[#F37021] mb-4 flex items-center gap-2"><ShieldAlert className="w-5 h-5" />Threats</h3>
                  <ul className="space-y-3">
                    {Array.isArray(analysis.threats) && analysis.threats.map((item, i) => (
                      <li key={i} className="flex items-start gap-3 text-sm text-slate-700">
                        <div className="w-1.5 h-1.5 bg-[#F37021] rounded-full mt-1.5 shrink-0" />
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
                 <div className="w-16 h-16 border-4 border-[#F37021]/30 border-t-[#F37021] rounded-full animate-spin mb-4"></div>
                 <h3 className="text-xl font-semibold text-[#F37021]">Generating suggestions...</h3>
                 <p className="text-slate-500 mt-2">AI is crafting actionable advice for your startup.</p>
              </div>
            ) : !suggestion ? (
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
                <div className="w-24 h-24 bg-slate-50 rounded-full flex items-center justify-center mb-6">
                  <Lightbulb className="w-12 h-12 text-slate-300" />
                </div>
                <h3 className="text-xl font-semibold text-slate-700">Ready for AI Suggestions</h3>
                <p className="text-slate-500 mt-2 max-w-sm text-center">Choose your stage and focus area, then click "Get AI Suggestions".</p>
              </div>
            ) : (
              <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-6">
                {/* Fallback Warning */}
                {suggestion.suggestions?.[0]?.current_issue?.includes("fallback") && (
                   <div className="bg-orange-100 text-orange-800 text-sm px-4 py-3 rounded-xl flex items-center gap-2 font-medium border border-orange-200 shadow-sm">
                     <AlertTriangle className="w-5 h-5 shrink-0" />
                      AI is using a fallback response. Please check your Gemini API configuration.
                   </div>
                )}
                
                {/* Suggestions List */}
                <div className="space-y-4">
                  <h2 className="text-2xl font-bold text-[#F37021] flex items-center gap-2 mb-4">
                    <Lightbulb className="w-6 h-6" /> Actionable Suggestions
                  </h2>
                  
                  {Array.isArray(suggestion.suggestions) && suggestion.suggestions.map((item, i) => (
                    <div key={i} className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
                      <div className={`px-5 py-3 border-b flex justify-between items-center ${item.priority === 'High' ? 'bg-red-50 border-red-100' : item.priority === 'Low' ? 'bg-green-50 border-green-100' : 'bg-orange-50 border-orange-100'}`}>
                        <div className="flex items-center gap-2 font-semibold text-slate-800">
                           <Target className="w-4 h-4 text-slate-500" /> Area: {typeof item.area === 'string' ? item.area : 'General'}
                        </div>
                        <span className={`text-xs font-bold uppercase tracking-wider px-2 py-1 rounded-md ${item.priority === 'High' ? 'bg-red-100 text-red-700' : item.priority === 'Low' ? 'bg-green-100 text-green-700' : 'bg-orange-100 text-orange-700'}`}>
                          {typeof item.priority === 'string' ? item.priority : 'Medium'} Priority
                        </span>
                      </div>
                      <div className="p-5 space-y-4">
                        <div>
                          <p className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-1">Current Issue</p>
                          <p className="text-slate-700 text-sm leading-relaxed">{typeof item.current_issue === 'string' ? item.current_issue : 'N/A'}</p>
                        </div>
                        <div className="bg-slate-50 p-4 rounded-xl border border-slate-100">
                          <p className="text-xs font-bold text-[#F37021] uppercase tracking-wider mb-1 flex items-center gap-1"><Zap className="w-3.5 h-3.5" /> AI Suggestion</p>
                          <p className="text-slate-800 font-medium leading-relaxed">{typeof item.suggestion === 'string' ? item.suggestion : 'N/A'}</p>
                        </div>
                        <div>
                          <p className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-1">Example / Action</p>
                          <p className="text-slate-600 text-sm leading-relaxed italic border-l-2 border-slate-300 pl-3">{typeof item.example === 'string' ? item.example : 'N/A'}</p>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>

                {/* MVP & Next Steps Grid */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                  {/* MVP */}
                  <div className="bg-gradient-to-br from-[#034EA2] to-blue-700 rounded-2xl p-6 text-white shadow-sm md:col-span-2">
                    <h3 className="text-lg font-bold mb-3 flex items-center gap-2"><Rocket className="w-5 h-5 text-[#F37021]" /> MVP Recommendation</h3>
                    <p className="text-white/90 leading-relaxed text-lg">{suggestion.mvp_recommendation || 'N/A'}</p>
                  </div>

                  {/* Next Steps */}
                  <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
                    <h3 className="text-lg font-bold text-[#034EA2] mb-4 flex items-center gap-2"><LayoutTemplate className="w-5 h-5" /> Next Steps</h3>
                    <ul className="space-y-3">
                      {Array.isArray(suggestion.next_steps) && suggestion.next_steps.map((step, i) => (
                        <li key={i} className="flex items-start gap-3 text-sm text-slate-700">
                          <div className="w-6 h-6 rounded-full bg-blue-100 text-[#034EA2] font-bold flex items-center justify-center shrink-0 text-xs mt-0.5">{i + 1}</div>
                          <span className="leading-relaxed mt-0.5">{typeof step === 'string' ? step : JSON.stringify(step)}</span>
                        </li>
                      ))}
                    </ul>
                  </div>

                  {/* Mentor Questions */}
                  <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
                    <h3 className="text-lg font-bold text-[#F37021] mb-4 flex items-center gap-2"><MessageCircle className="w-5 h-5" /> Mentor Questions</h3>
                    <ul className="space-y-3">
                      {Array.isArray(suggestion.mentor_questions) && suggestion.mentor_questions.map((q, i) => (
                        <li key={i} className="flex items-start gap-3 text-sm text-slate-700">
                          <div className="w-1.5 h-1.5 bg-[#F37021] rounded-full mt-2 shrink-0" />
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
