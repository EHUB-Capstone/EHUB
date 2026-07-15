// @ts-nocheck
import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { BookOpen, Zap, AlertTriangle, CheckSquare, MessageCircle, FileText, ChevronDown } from 'lucide-react';
import toast from 'react-hot-toast';
import { aiApi } from '../../api/aiApi';
import Button from '../ui/Button';

const FOCUS_OPTIONS = [
  { id: 'feasibility', label: 'Feasibility (Tính khả thi)' },
  { id: 'market', label: 'Market (Thị trường)' },
  { id: 'technical', label: 'Technical (Kỹ thuật)' },
  { id: 'business', label: 'Business (Kinh doanh)' },
  { id: 'presentation', label: 'Presentation (Trình bày)' }
];

const RubricGeneratorTab = ({ initialData }) => {
  const [formData, setFormData] = useState({
    startup_name: initialData?.startup_name || '',
    stage: 'idea',
    business_model: initialData?.business_model || '',
  });

  const [focusAreas, setFocusAreas] = useState(['feasibility', 'market']);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState(null);

  useEffect(() => {
    setFormData(prev => ({
      ...prev,
      startup_name: prev.startup_name || initialData?.startup_name || '',
      business_model: prev.business_model || initialData?.business_model || '',
    }));
  }, [initialData]);

  const handleChange = (e) => {
    setFormData(prev => ({ ...prev, [e.target.name]: e.target.value }));
  };

  const handleCheckbox = (id) => {
    setFocusAreas(prev => 
      prev.includes(id) ? prev.filter(a => a !== id) : [...prev, id]
    );
  };

  const handleGenerate = async (e) => {
    e.preventDefault();
    if (!formData.startup_name || !formData.stage || !formData.business_model) {
      toast.error('Vui lòng điền đủ Tên, Giai đoạn và Mô hình kinh doanh.');
      return;
    }
    if (focusAreas.length === 0) {
      toast.error('Vui lòng chọn ít nhất một Focus Area.');
      return;
    }

    setLoading(true);
    setResult(null);

    try {
      const res = await aiApi.generateRubric({
        ...formData,
        focus_areas: focusAreas
      });
      setResult(res.data);
      
      const isFallback = res.data?.rubric_questions?.length === 1 && 
                         res.data?.rubric_questions[0]?.category === "Problem Validation" &&
                         res.data?.rubric_questions[0]?.question?.includes("Sinh viên đã chứng minh vấn đề");
                         
      if (isFallback) {
        toast.error('AI đang dùng phản hồi dự phòng. Vui lòng kiểm tra cấu hình Gemini API.', { duration: 5000 });
      } else {
        toast.success('Tạo Rubric hoàn tất!');
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

  const isFallback = result?.rubric_questions?.length === 1 && 
                     result?.rubric_questions[0]?.category === "Problem Validation" &&
                     result?.rubric_questions[0]?.question?.includes("Sinh viên đã chứng minh vấn đề");

  return (
    <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
      {/* Form Panel */}
      <div className="lg:col-span-4 bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col">
        <div className="p-5 border-b border-slate-100 bg-slate-50 flex items-center justify-between">
           <h2 className="font-semibold text-slate-800 text-lg">Rubric Setup</h2>
           <span className="text-xs font-medium px-2 py-1 bg-blue-100 text-[#034EA2] rounded-md">Input</span>
        </div>
        <div className="p-5 flex-1">
          <form onSubmit={handleGenerate} className="space-y-4 flex flex-col h-full">
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Tên Startup *</label>
              <input required name="startup_name" value={formData.startup_name} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="e.g. EduTrack" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Mô hình kinh doanh *</label>
              <input required name="business_model" value={formData.business_model} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="e.g. B2B SaaS" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Giai đoạn (Stage) *</label>
              <select required name="stage" value={formData.stage} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]">
                <option value="idea">Idea (Ý tưởng)</option>
                <option value="prototype">Prototype (Nguyên mẫu)</option>
                <option value="mvp">MVP (Sản phẩm tối thiểu)</option>
                <option value="pitch">Pitch Deck (Đã có sản phẩm & Pitching)</option>
              </select>
            </div>
            
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-2">Tiêu chí đánh giá (Focus Areas) *</label>
              <div className="space-y-2">
                {FOCUS_OPTIONS.map(opt => (
                  <label key={opt.id} className="flex items-center gap-2 cursor-pointer group">
                    <div className={`w-5 h-5 rounded border flex items-center justify-center transition-colors ${focusAreas.includes(opt.id) ? 'bg-[#034EA2] border-[#034EA2]' : 'border-slate-300 group-hover:border-[#034EA2]'}`}>
                      {focusAreas.includes(opt.id) && <CheckSquare className="w-3.5 h-3.5 text-white" />}
                    </div>
                    <span className="text-sm text-slate-700 select-none">{opt.label}</span>
                  </label>
                ))}
              </div>
            </div>
            
            <div className="pt-4 mt-auto">
              <Button type="submit" className="w-full bg-[#034EA2] hover:bg-[#023B7A] text-white" isLoading={loading} disabled={loading} icon={BookOpen}>
                Generate Rubric
              </Button>
            </div>
          </form>
        </div>
      </div>

      {/* Result Panel */}
      <div className="lg:col-span-8">
        {loading ? (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
             <div className="w-16 h-16 border-4 border-[#034EA2]/30 border-t-[#034EA2] rounded-full animate-spin mb-4"></div>
             <h3 className="text-xl font-semibold text-[#034EA2]">Crafting questions...</h3>
             <p className="text-slate-500 mt-2">AI is generating customized rubric and follow-up questions.</p>
          </div>
        ) : !result ? (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
            <div className="w-24 h-24 bg-slate-50 rounded-full flex items-center justify-center mb-6">
              <BookOpen className="w-12 h-12 text-slate-300" />
            </div>
            <h3 className="text-xl font-semibold text-slate-700">Ready for Rubric</h3>
            <p className="text-slate-500 mt-2 max-w-sm text-center">Select your focus areas to generate an expert evaluation rubric.</p>
          </div>
        ) : (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-6">
            
            {isFallback && (
               <div className="bg-orange-100 text-orange-800 text-sm px-4 py-3 rounded-xl flex items-center gap-2 font-medium border border-orange-200 shadow-sm">
                 <AlertTriangle className="w-5 h-5 shrink-0" />
                 ⚠ AI đang dùng phản hồi dự phòng. Vui lòng kiểm tra cấu hình Gemini API hoặc thử lại sau.
               </div>
            )}
            
            {/* Tips Card */}
            <div className="bg-[#034EA2]/5 border border-[#034EA2]/20 rounded-2xl p-5 shadow-sm">
               <h3 className="font-bold text-[#034EA2] mb-2 flex items-center gap-2"><MessageCircle className="w-5 h-5" /> Evaluation Tips for Mentor</h3>
               <p className="text-slate-700 leading-relaxed text-sm">{typeof result.evaluation_tips === 'string' ? result.evaluation_tips : 'N/A'}</p>
            </div>

            {/* Rubric Questions */}
            <div className="space-y-4">
               {Array.isArray(result.rubric_questions) && result.rubric_questions.length > 0 ? (
                 result.rubric_questions.map((item, idx) => (
                   <div key={idx} className="bg-white border border-slate-200 rounded-xl overflow-hidden shadow-sm">
                     <div className="p-5 border-b border-slate-100">
                        <span className="inline-block px-3 py-1 bg-blue-100 text-[#034EA2] text-xs font-bold rounded-md uppercase tracking-wider mb-3">
                           {typeof item.category === 'string' ? item.category : 'General'}
                        </span>
                        <h4 className="text-lg font-bold text-slate-800 leading-snug">{typeof item.question === 'string' ? item.question : 'N/A'}</h4>
                     </div>
                     
                     <div className="bg-slate-50 p-5 space-y-4 border-b border-slate-100">
                        <h5 className="text-xs font-bold text-slate-500 uppercase tracking-wider flex items-center gap-2"><FileText className="w-4 h-4" /> Scoring Guide</h5>
                        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                          <div className="bg-white p-3 rounded-lg border border-green-100 shadow-sm">
                            <span className="block font-black text-[#51B848] text-xl mb-1">5 pts</span>
                            <span className="text-xs text-slate-600">{typeof item.scoring_guide?.["5"] === 'string' ? item.scoring_guide["5"] : 'N/A'}</span>
                          </div>
                          <div className="bg-white p-3 rounded-lg border border-orange-100 shadow-sm">
                            <span className="block font-black text-[#F37021] text-xl mb-1">3 pts</span>
                            <span className="text-xs text-slate-600">{typeof item.scoring_guide?.["3"] === 'string' ? item.scoring_guide["3"] : 'N/A'}</span>
                          </div>
                          <div className="bg-white p-3 rounded-lg border border-red-100 shadow-sm">
                            <span className="block font-black text-red-500 text-xl mb-1">1 pt</span>
                            <span className="text-xs text-slate-600">{typeof item.scoring_guide?.["1"] === 'string' ? item.scoring_guide["1"] : 'N/A'}</span>
                          </div>
                        </div>
                     </div>
                     
                     <div className="p-4 bg-orange-50/50 flex items-start gap-3">
                        <Zap className="w-5 h-5 text-[#F37021] shrink-0 mt-0.5" />
                        <div>
                          <span className="block text-xs font-bold text-[#F37021] uppercase tracking-wider mb-1">Follow-up Question</span>
                          <span className="text-sm font-medium text-slate-700 italic">{typeof item.follow_up === 'string' ? item.follow_up : 'N/A'}</span>
                        </div>
                     </div>
                   </div>
                 ))
               ) : (
                 <div className="bg-slate-50 border border-slate-200 border-dashed rounded-xl p-8 text-center text-slate-500">
                    Chưa có rubric questions.
                 </div>
               )}
            </div>

          </motion.div>
        )}
      </div>
    </div>
  );
};

export default RubricGeneratorTab;
