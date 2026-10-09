// @ts-nocheck
import { useState } from 'react';
import { motion } from 'framer-motion';
import { MessageSquare, Zap, AlertTriangle, Plus, Trash2, Heart, ShieldAlert, BarChart3, AlertCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import { aiApi } from '../../api/aiApi';
import Button from '../ui/Button';

const SentimentAnalysisTab = ({ ideaId }) => {
  const [feedbacks, setFeedbacks] = useState([
    { id: 'fb_1', author_type: 'mentor', content: '' }
  ]);
  
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState(null);

  const handleAddFeedback = () => {
    setFeedbacks(prev => [
      ...prev,
      { id: `fb_${Date.now()}`, author_type: 'mentor', content: '' }
    ]);
  };

  const handleRemoveFeedback = (id) => {
    if (feedbacks.length > 1) {
      setFeedbacks(prev => prev.filter(fb => fb.id !== id));
    }
  };

  const handleChange = (id, field, value) => {
    setFeedbacks(prev => prev.map(fb => fb.id === id ? { ...fb, [field]: value } : fb));
  };

  const handleAnalyze = async (e) => {
    e.preventDefault();
    
    // Validation
    const validFeedbacks = feedbacks.filter(fb => fb.content.trim() !== '');
    if (validFeedbacks.length === 0) {
      toast.error('Vui lòng nhập ít nhất một feedback có nội dung.');
      return;
    }

    setLoading(true);
    setResult(null);

    try {
      const res = await aiApi.analyzeSentiment({
        startup_id: ideaId || 'unknown_idea',
        texts: validFeedbacks
      });
      setResult(res.data);
      
      const isFallback = res.data?.overall_sentiment === "Neutral" && 
                         (!res.data?.results || res.data.results.length === 0) &&
                         res.data?.summary?.includes("Chưa thể phân tích");
                         
      if (isFallback) {
        toast.error('AI đang dùng phản hồi dự phòng. Vui lòng kiểm tra cấu hình Gemini API.', { duration: 5000 });
      } else {
        toast.success('Phân tích cảm xúc hoàn tất!');
      }
    } catch (err) {
      if (err.response?.status === 401 || err.response?.status === 403) {
        toast.error('Phiên đăng nhập đã hết hạn hoặc bạn chưa có quyền sử dụng AI.');
      } else {
        toast.error(err.response?.data?.error || err.message || 'Có lỗi xảy ra khi gọi AI. Vui lòng thử lại.');
      }
    } finally {
      setLoading(false);
    }
  };

  const isFallback = result?.overall_sentiment === "Neutral" && 
                     (!result?.results || result.results.length === 0) &&
                     result?.summary?.includes("Chưa thể phân tích");

  const getSentimentColor = (sentiment) => {
    if (sentiment === 'Positive') return 'text-[#51B848] bg-green-100 border-green-200';
    if (sentiment === 'Negative') return 'text-red-600 bg-red-100 border-red-200';
    if (sentiment === 'Mixed') return 'text-[#F37021] bg-orange-100 border-orange-200';
    return 'text-[#034EA2] bg-blue-100 border-blue-200';
  };

  return (
    <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
      {/* Form Panel */}
      <div className="lg:col-span-5 bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col">
        <div className="p-5 border-b border-slate-100 bg-slate-50 flex items-center justify-between">
           <h2 className="font-semibold text-slate-800 text-lg">Feedback Input</h2>
           <span className="text-xs font-medium px-2 py-1 bg-blue-100 text-[#034EA2] rounded-md">Input</span>
        </div>
        <div className="p-5 flex-1 overflow-y-auto max-h-[600px] no-scrollbar">
          <form onSubmit={handleAnalyze} className="space-y-4 flex flex-col h-full">
            
            <div className="space-y-4">
              {feedbacks.map((fb, index) => (
                <div key={fb.id} className="p-4 border border-slate-200 rounded-xl relative group bg-slate-50">
                  <div className="flex justify-between items-center mb-2">
                    <span className="text-xs font-bold text-slate-500 uppercase tracking-wider">Feedback #{index + 1}</span>
                    {feedbacks.length > 1 && (
                      <button type="button" onClick={() => handleRemoveFeedback(fb.id)} className="text-slate-400 hover:text-red-500 transition-colors">
                        <Trash2 className="w-4 h-4" />
                      </button>
                    )}
                  </div>
                  <div className="space-y-3">
                    <select value={fb.author_type} onChange={(e) => handleChange(fb.id, 'author_type', e.target.value)} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2] bg-white">
                      <option value="mentor">Mentor</option>
                      <option value="student">Student</option>
                      <option value="lecturer">Lecturer</option>
                    </select>
                    <textarea 
                      rows={3} 
                      value={fb.content} 
                      onChange={(e) => handleChange(fb.id, 'content', e.target.value)} 
                      className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2] bg-white" 
                      placeholder="Nhập nội dung feedback (VD: Sản phẩm rất tốt nhưng UX hơi khó dùng...)" 
                    />
                  </div>
                </div>
              ))}
            </div>

            <button type="button" onClick={handleAddFeedback} className="flex items-center justify-center gap-2 w-full py-3 border-2 border-dashed border-slate-300 rounded-xl text-slate-500 hover:text-[#034EA2] hover:border-[#034EA2] hover:bg-blue-50 transition-colors font-medium text-sm">
              <Plus className="w-4 h-4" /> Add Feedback
            </button>
            
            <div className="pt-4 mt-auto">
              <Button type="submit" className="w-full bg-[#034EA2] hover:bg-[#023B7A] text-white" isLoading={loading} disabled={loading} icon={Heart}>
                Analyze Sentiment
              </Button>
            </div>
          </form>
        </div>
      </div>

      {/* Result Panel */}
      <div className="lg:col-span-7">
        {loading ? (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
             <div className="w-16 h-16 border-4 border-[#034EA2]/30 border-t-[#034EA2] rounded-full animate-spin mb-4"></div>
             <h3 className="text-xl font-semibold text-[#034EA2]">Analyzing sentiments...</h3>
             <p className="text-slate-500 mt-2">AI is reading between the lines to gauge user emotions.</p>
          </div>
        ) : !result ? (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
            <div className="w-24 h-24 bg-slate-50 rounded-full flex items-center justify-center mb-6">
              <MessageSquare className="w-12 h-12 text-slate-300" />
            </div>
            <h3 className="text-xl font-semibold text-slate-700">Ready for Sentiment Analysis</h3>
            <p className="text-slate-500 mt-2 max-w-sm text-center">Add user or mentor feedback and let AI detect emotions, concerns, and urgency.</p>
          </div>
        ) : (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-6">
            
            {isFallback && (
               <div className="bg-orange-100 text-orange-800 text-sm px-4 py-3 rounded-xl flex items-center gap-2 font-medium border border-orange-200 shadow-sm">
                 <AlertTriangle className="w-5 h-5 shrink-0" />
                 ⚠ AI đang dùng phản hồi dự phòng. Vui lòng kiểm tra cấu hình Gemini API hoặc thử lại sau.
               </div>
            )}
            
            {/* Overview Card */}
            <div className="bg-white border border-slate-200 rounded-2xl p-6 shadow-sm flex flex-col md:flex-row items-center gap-6">
               <div className="flex flex-col items-center justify-center min-w-[150px]">
                 <p className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-2">Overall Sentiment</p>
                 <div className={`px-4 py-2 rounded-xl font-black text-xl border shadow-sm ${getSentimentColor(result.overall_sentiment)}`}>
                   {typeof result.overall_sentiment === 'string' ? result.overall_sentiment : 'Unknown'}
                 </div>
               </div>
               
               <div className="flex-1 space-y-3 w-full">
                 <div className="flex items-center justify-between mb-1">
                   <span className="text-sm font-semibold text-slate-700 flex items-center gap-2"><BarChart3 className="w-4 h-4" /> AI Confidence Score</span>
                   <span className="text-sm font-bold text-[#034EA2]">{result.sentiment_score !== null && result.sentiment_score !== undefined ? Math.round(result.sentiment_score * 100) : 'N/A'}%</span>
                 </div>
                 <div className="w-full bg-slate-100 rounded-full h-2.5 overflow-hidden">
                   <div className="bg-gradient-to-r from-blue-400 to-[#034EA2] h-2.5 rounded-full" style={{ width: `${result.sentiment_score ? Math.round(result.sentiment_score * 100) : 0}%` }}></div>
                 </div>
                 <p className="text-sm text-slate-600 mt-3 leading-relaxed">{typeof result.summary === 'string' ? result.summary : 'N/A'}</p>
               </div>
            </div>

            {/* Warning Card */}
            {result.action_required && (
              <div className="bg-red-50 border border-red-200 rounded-2xl p-5 shadow-sm">
                <h3 className="font-bold text-red-600 mb-3 flex items-center gap-2"><AlertCircle className="w-5 h-5" /> Action Required / Urgent Issues</h3>
                <ul className="space-y-2">
                   {Array.isArray(result.urgent_issues) && result.urgent_issues.length > 0 ? (
                     result.urgent_issues.map((issue, idx) => (
                       <li key={idx} className="flex items-start gap-2 text-sm text-red-800">
                         <div className="w-1.5 h-1.5 bg-red-500 rounded-full mt-1.5 shrink-0" />
                         <span className="leading-relaxed">{typeof issue === 'string' ? issue : JSON.stringify(issue)}</span>
                       </li>
                     ))
                   ) : (
                     <li className="text-sm text-red-700 italic">Có vấn đề rủi ro nhưng không có chi tiết cụ thể.</li>
                   )}
                </ul>
              </div>
            )}

            {/* Feedback Results */}
            <div className="space-y-4">
              <h3 className="text-lg font-bold text-slate-800 flex items-center gap-2"><MessageSquare className="w-5 h-5 text-[#F37021]" /> Individual Breakdown</h3>
              {Array.isArray(result.results) && result.results.length > 0 ? (
                 result.results.map((item, idx) => (
                   <div key={idx} className="bg-white border border-slate-200 rounded-xl p-5 shadow-sm">
                     <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
                        <span className="text-xs font-bold text-slate-400 uppercase tracking-wider bg-slate-100 px-2 py-1 rounded">ID: {typeof item.id === 'string' ? item.id : 'N/A'}</span>
                        <div className="flex items-center gap-3">
                           <span className="text-xs font-semibold text-slate-500">Conf: {item.confidence !== null && item.confidence !== undefined ? Math.round(item.confidence * 100) : 'N/A'}%</span>
                           <span className={`px-3 py-1 rounded-full text-xs font-bold border ${getSentimentColor(item.sentiment)}`}>
                             {typeof item.sentiment === 'string' ? item.sentiment : 'Unknown'}
                           </span>
                        </div>
                     </div>
                     
                     <div className="space-y-4">
                       {Array.isArray(item.key_phrases) && item.key_phrases.length > 0 && (
                         <div>
                           <p className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">Key Phrases</p>
                           <div className="flex flex-wrap gap-2">
                             {item.key_phrases.map((phrase, pIdx) => (
                               <span key={pIdx} className="px-2.5 py-1 bg-[#034EA2]/10 text-[#034EA2] text-xs font-medium rounded-md">
                                 {typeof phrase === 'string' ? phrase : JSON.stringify(phrase)}
                               </span>
                             ))}
                           </div>
                         </div>
                       )}
                       
                       {Array.isArray(item.concerns) && item.concerns.length > 0 && (
                         <div className="bg-orange-50 p-3 rounded-xl border border-orange-100">
                           <p className="text-xs font-bold text-[#F37021] uppercase tracking-wider mb-2 flex items-center gap-1"><ShieldAlert className="w-3.5 h-3.5" /> Concerns Detected</p>
                           <ul className="space-y-1.5">
                             {item.concerns.map((concern, cIdx) => (
                               <li key={cIdx} className="text-sm text-slate-700 flex items-start gap-2">
                                 <span className="text-orange-400 mt-0.5">•</span>
                                 <span className="leading-relaxed">{typeof concern === 'string' ? concern : JSON.stringify(concern)}</span>
                               </li>
                             ))}
                           </ul>
                         </div>
                       )}
                     </div>
                   </div>
                 ))
              ) : (
                <div className="bg-slate-50 border border-slate-200 border-dashed rounded-xl p-8 text-center text-slate-500">
                   Chưa có dữ liệu breakdown.
                </div>
              )}
            </div>

          </motion.div>
        )}
      </div>
    </div>
  );
};

export default SentimentAnalysisTab;
