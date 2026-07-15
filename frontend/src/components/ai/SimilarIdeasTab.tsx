// @ts-nocheck
import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { Search, Zap, AlertTriangle, Target, ShieldAlert, Crosshair, Users, Map } from 'lucide-react';
import toast from 'react-hot-toast';
import { aiApi } from '../../api/aiApi';
import Button from '../ui/Button';

const SimilarIdeasTab = ({ initialData }) => {
  const [formData, setFormData] = useState({
    startup_name: initialData?.startup_name || '',
    problem: initialData?.problem || '',
    solution: initialData?.solution || '',
    target_customer: initialData?.target_customer || '',
    market: initialData?.market || ''
  });

  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState(null);

  useEffect(() => {
    // Keep it synced if parent changes (initial click)
    setFormData(prev => ({
      ...prev,
      startup_name: prev.startup_name || initialData?.startup_name || '',
      problem: prev.problem || initialData?.problem || '',
      solution: prev.solution || initialData?.solution || '',
      target_customer: prev.target_customer || initialData?.target_customer || '',
      market: prev.market || initialData?.market || ''
    }));
  }, [initialData]);

  const handleChange = (e) => {
    setFormData(prev => ({ ...prev, [e.target.name]: e.target.value }));
  };

  const handleCheck = async (e) => {
    e.preventDefault();
    if (!formData.startup_name || !formData.problem || !formData.solution || !formData.target_customer || !formData.market) {
      toast.error('Vui lòng điền đầy đủ tất cả các trường.');
      return;
    }

    setLoading(true);
    setResult(null);

    try {
      const res = await aiApi.detectSimilarIdea(formData);
      setResult(res.data);
      
      const isFallback = res.data?.similarity_risk === "Medium" && 
                         (!res.data?.known_competitors || res.data.known_competitors.length === 0) &&
                         res.data?.differentiation_strategy?.includes("Cần nghiên cứu thêm");
                         
      if (isFallback) {
        toast.error('AI đang dùng phản hồi dự phòng. Vui lòng kiểm tra cấu hình Gemini API.', { duration: 5000 });
      } else {
        toast.success('Kiểm tra ý tưởng tương tự hoàn tất!');
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

  const isFallback = result?.similarity_risk === "Medium" && 
                     (!result?.known_competitors || result.known_competitors.length === 0) &&
                     result?.differentiation_strategy?.includes("Cần nghiên cứu thêm");

  return (
    <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
      {/* Form Panel */}
      <div className="lg:col-span-4 bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col">
        <div className="p-5 border-b border-slate-100 bg-slate-50 flex items-center justify-between">
           <h2 className="font-semibold text-slate-800 text-lg">Idea Context</h2>
           <span className="text-xs font-medium px-2 py-1 bg-blue-100 text-[#034EA2] rounded-md">Input</span>
        </div>
        <div className="p-5 flex-1">
          <form onSubmit={handleCheck} className="space-y-4 flex flex-col h-full">
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Tên Startup *</label>
              <input required name="startup_name" value={formData.startup_name} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="e.g. EduTrack" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Vấn đề (Problem) *</label>
              <textarea required rows={2} name="problem" value={formData.problem} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="Mô tả nỗi đau của khách hàng..." />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Giải pháp (Solution) *</label>
              <textarea required rows={2} name="solution" value={formData.solution} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="Sản phẩm của bạn giải quyết vấn đề thế nào..." />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Khách hàng mục tiêu *</label>
              <input required name="target_customer" value={formData.target_customer} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="e.g. Sinh viên 18-24 tuổi" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">Thị trường *</label>
              <input required name="market" value={formData.market} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-[#034EA2] focus:border-[#034EA2]" placeholder="e.g. EdTech Việt Nam" />
            </div>
            
            <div className="pt-4 mt-auto">
              <Button type="submit" className="w-full bg-[#034EA2] hover:bg-[#023B7A] text-white" isLoading={loading} disabled={loading} icon={Search}>
                Check Similar Ideas
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
             <h3 className="text-xl font-semibold text-[#034EA2]">Searching market...</h3>
             <p className="text-slate-500 mt-2">AI is looking for similar products and competitors.</p>
          </div>
        ) : !result ? (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
            <div className="w-24 h-24 bg-slate-50 rounded-full flex items-center justify-center mb-6">
              <Search className="w-12 h-12 text-slate-300" />
            </div>
            <h3 className="text-xl font-semibold text-slate-700">Ready for Detection</h3>
            <p className="text-slate-500 mt-2 max-w-sm text-center">Fill in the details to identify potential competitors and market gaps.</p>
          </div>
        ) : (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-6">
            
            {/* Fallback Warning */}
            {isFallback && (
               <div className="bg-orange-100 text-orange-800 text-sm px-4 py-3 rounded-xl flex items-center gap-2 font-medium border border-orange-200 shadow-sm">
                 <AlertTriangle className="w-5 h-5 shrink-0" />
                 ⚠ AI đang dùng phản hồi dự phòng. Vui lòng kiểm tra cấu hình Gemini API hoặc thử lại sau.
               </div>
            )}
            
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
               <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6 border-b border-slate-100 pb-4">
                 <h2 className="text-2xl font-bold text-[#034EA2] flex items-center gap-2">
                    <Search className="w-6 h-6 text-[#F37021]" /> Similarity Report
                 </h2>
                 <div className={`px-4 py-2 rounded-lg flex items-center gap-2 font-bold ${
                    result.similarity_risk === 'Low' ? 'bg-green-100 text-green-700' :
                    result.similarity_risk === 'High' ? 'bg-red-100 text-red-700' : 'bg-orange-100 text-orange-700'
                 }`}>
                    <ShieldAlert className="w-5 h-5" /> 
                    Risk Level: {typeof result.similarity_risk === 'string' ? result.similarity_risk : 'Unknown'}
                 </div>
               </div>

               <h3 className="text-lg font-bold text-slate-800 mb-4 flex items-center gap-2"><Users className="w-5 h-5 text-slate-400" /> Known Competitors</h3>
               
               {Array.isArray(result.known_competitors) && result.known_competitors.length > 0 ? (
                 <div className="space-y-4">
                   {result.known_competitors.map((comp, idx) => (
                     <div key={idx} className="border border-slate-200 rounded-xl overflow-hidden shadow-sm">
                       <div className="bg-slate-50 px-4 py-3 border-b border-slate-200 flex justify-between items-center">
                          <span className="font-bold text-[#034EA2] text-lg">{typeof comp.name === 'string' ? comp.name : 'Unknown'}</span>
                          <span className="text-sm font-semibold bg-white px-2 py-1 rounded border shadow-sm text-slate-600">
                            Similarity: {comp.similarity_percent !== null && comp.similarity_percent !== undefined ? comp.similarity_percent : 'N/A'}%
                          </span>
                       </div>
                       <div className="p-4 space-y-3">
                         <p className="text-sm text-slate-700"><span className="font-semibold text-slate-800">Mô tả:</span> {typeof comp.description === 'string' ? comp.description : 'N/A'}</p>
                         <p className="text-sm text-slate-700"><span className="font-semibold text-slate-800">Thị trường:</span> {typeof comp.market === 'string' ? comp.market : 'N/A'}</p>
                         <div className="bg-orange-50 p-3 rounded-lg border border-orange-100 mt-2 text-sm">
                            <span className="font-bold text-[#F37021] block mb-1">Cách khác biệt (Differentiate):</span>
                            <span className="text-slate-800">{typeof comp.how_to_differentiate === 'string' ? comp.how_to_differentiate : 'N/A'}</span>
                         </div>
                       </div>
                     </div>
                   ))}
                 </div>
               ) : (
                 <div className="bg-slate-50 border border-slate-200 border-dashed rounded-xl p-8 text-center text-slate-500">
                    Chưa phát hiện đối thủ tương tự rõ ràng.
                 </div>
               )}
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
              <div className="bg-white border border-blue-200 rounded-2xl p-5 shadow-sm">
                 <h4 className="font-bold text-[#034EA2] mb-2 flex items-center gap-2"><Target className="w-4 h-4" /> Differentiation Strategy</h4>
                 <p className="text-sm text-slate-700 leading-relaxed">{typeof result.differentiation_strategy === 'string' ? result.differentiation_strategy : 'N/A'}</p>
              </div>
              <div className="bg-white border border-green-200 rounded-2xl p-5 shadow-sm">
                 <h4 className="font-bold text-[#51B848] mb-2 flex items-center gap-2"><Zap className="w-4 h-4" /> Unique Angle</h4>
                 <p className="text-sm text-slate-700 leading-relaxed">{typeof result.unique_angle === 'string' ? result.unique_angle : 'N/A'}</p>
              </div>
              <div className="bg-white border border-orange-200 rounded-2xl p-5 shadow-sm">
                 <h4 className="font-bold text-[#F37021] mb-2 flex items-center gap-2"><Crosshair className="w-4 h-4" /> Market Gap</h4>
                 <p className="text-sm text-slate-700 leading-relaxed">{typeof result.market_gap === 'string' ? result.market_gap : 'N/A'}</p>
              </div>
            </div>

          </motion.div>
        )}
      </div>
    </div>
  );
};

export default SimilarIdeasTab;
