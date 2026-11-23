// @ts-nocheck
import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { Search, Zap, AlertTriangle, Target, ShieldAlert, Crosshair, Users, Map } from 'lucide-react';
import toast from 'react-hot-toast';
import { aiApi } from '../../api/aiApi';
import Button from '../ui/Button';
import { useTranslation } from 'react-i18next';

const SimilarIdeasTab = ({ initialData }) => {
  const { t } = useTranslation('ai');
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
      toast.error(t('similarIdeasTab.vuiLongDienDayDuTat'));
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
        toast.error(t('rubricGeneratorTab.aiDangDungPhanHoiDu'), { duration: 5000 });
      } else {
        toast.success(t('similarIdeasTab.kiemTraYTuongTuongTu'));
      }
    } catch (err) {
      if (err.response?.status === 401 || err.response?.status === 403) {
        toast.error(t('rubricGeneratorTab.phienDangNhapDaHetHan'));
      } else {
        toast.error(err.response?.data?.error || err.message || t('rubricGeneratorTab.coLoiXayRaKhiGoi'));
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
           <h2 className="font-semibold text-slate-800 text-lg">{t('similarIdeasTab.ideaContext')}</h2>
           <span className="text-xs font-medium px-2 py-1 bg-blue-100 text-secondary rounded-md">{t('rubricGeneratorTab.input')}</span>
        </div>
        <div className="p-5 flex-1">
          <form onSubmit={handleCheck} className="space-y-4 flex flex-col h-full">
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('rubricGeneratorTab.tenStartup')}</label>
              <input required name="startup_name" value={formData.startup_name} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('rubricGeneratorTab.eGEdutrack')} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('similarIdeasTab.vanDeProblem')}</label>
              <textarea required rows={2} name="problem" value={formData.problem} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('similarIdeasTab.moTaNoiDauCuaKhach')} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('similarIdeasTab.giaiPhapSolution')}</label>
              <textarea required rows={2} name="solution" value={formData.solution} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('similarIdeasTab.sanPhamCuaBanGiaiQuyet')} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('similarIdeasTab.khachHangMucTieu')}</label>
              <input required name="target_customer" value={formData.target_customer} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('similarIdeasTab.eGSinhVien1824')} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('similarIdeasTab.thiTruong')}</label>
              <input required name="market" value={formData.market} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('similarIdeasTab.eGEdtechVietNam')} />
            </div>
            
            <div className="pt-4 mt-auto">
              <Button type="submit" className="w-full bg-secondary hover:bg-secondary-700 text-white" isLoading={loading} disabled={loading} icon={Search}>
                {t('similarIdeasTab.checkSimilarIdeas')}
              </Button>
            </div>
          </form>
        </div>
      </div>

      {/* Result Panel */}
      <div className="lg:col-span-8">
        {loading ? (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
             <div className="w-16 h-16 border-4 border-secondary/30 border-t-secondary rounded-full animate-spin mb-4"></div>
             <h3 className="text-xl font-semibold text-secondary">{t('similarIdeasTab.searchingMarket')}</h3>
             <p className="text-slate-500 mt-2">{t('similarIdeasTab.aiIsLookingForSimilarProducts')}</p>
          </div>
        ) : !result ? (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
            <div className="w-24 h-24 bg-slate-50 rounded-full flex items-center justify-center mb-6">
              <Search className="w-12 h-12 text-slate-300" />
            </div>
            <h3 className="text-xl font-semibold text-slate-700">{t('similarIdeasTab.readyForDetection')}</h3>
            <p className="text-slate-500 mt-2 max-w-sm text-center">{t('similarIdeasTab.fillInTheDetailsToIdentify')}</p>
          </div>
        ) : (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-6">
            
            {/* Fallback Warning */}
            {isFallback && (
               <div className="bg-orange-100 text-orange-800 text-sm px-4 py-3 rounded-xl flex items-center gap-2 font-medium border border-orange-200 shadow-sm">
                 <AlertTriangle className="w-5 h-5 shrink-0" />
                 {t('rubricGeneratorTab.aiDangDungPhanHoiDu2')}
               </div>
            )}
            
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
               <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6 border-b border-slate-100 pb-4">
                 <h2 className="text-2xl font-bold text-secondary flex items-center gap-2">
                    <Search className="w-6 h-6 text-primary-600" /> {t('similarIdeasTab.similarityReport')}
                 </h2>
                 <div className={`px-4 py-2 rounded-lg flex items-center gap-2 font-bold ${
                    result.similarity_risk === 'Low' ? 'bg-green-100 text-green-700' :
                    result.similarity_risk === 'High' ? 'bg-red-100 text-red-700' : 'bg-orange-100 text-orange-700'
                 }`}>
                    <ShieldAlert className="w-5 h-5" /> 
                    {t('similarIdeasTab.riskLevel')} {typeof result.similarity_risk === 'string' ? result.similarity_risk : t('sentimentAnalysisTab.unknown')}
                 </div>
               </div>

               <h3 className="text-lg font-bold text-slate-800 mb-4 flex items-center gap-2"><Users className="w-5 h-5 text-slate-400" /> {t('similarIdeasTab.knownCompetitors')}</h3>
               
               {Array.isArray(result.known_competitors) && result.known_competitors.length > 0 ? (
                 <div className="space-y-4">
                   {result.known_competitors.map((comp, idx) => (
                     <div key={idx} className="border border-slate-200 rounded-xl overflow-hidden shadow-sm">
                       <div className="bg-slate-50 px-4 py-3 border-b border-slate-200 flex justify-between items-center">
                          <span className="font-bold text-secondary text-lg">{typeof comp.name === 'string' ? comp.name : t('sentimentAnalysisTab.unknown')}</span>
                          <span className="text-sm font-semibold bg-white px-2 py-1 rounded border shadow-sm text-slate-600">
                            {t('similarIdeasTab.similarity', { value: comp.similarity_percent !== null && comp.similarity_percent !== undefined ? `${comp.similarity_percent}%` : t('similarIdeasTab.na') })}
                          </span>
                       </div>
                       <div className="p-4 space-y-3">
                         <p className="text-sm text-slate-700"><span className="font-semibold text-slate-800">{t('similarIdeasTab.moTa')}</span> {typeof comp.description === 'string' ? comp.description : t('rubricGeneratorTab.nA')}</p>
                         <p className="text-sm text-slate-700"><span className="font-semibold text-slate-800">{t('similarIdeasTab.thiTruong2')}</span> {typeof comp.market === 'string' ? comp.market : t('rubricGeneratorTab.nA')}</p>
                         <div className="bg-orange-50 p-3 rounded-lg border border-orange-100 mt-2 text-sm">
                            <span className="font-bold text-primary-600 block mb-1">{t('similarIdeasTab.cachKhacBietDifferentiate')}</span>
                            <span className="text-slate-800">{typeof comp.how_to_differentiate === 'string' ? comp.how_to_differentiate : t('rubricGeneratorTab.nA')}</span>
                         </div>
                       </div>
                     </div>
                   ))}
                 </div>
               ) : (
                 <div className="bg-slate-50 border border-slate-200 border-dashed rounded-xl p-8 text-center text-slate-500">
                    {t('similarIdeasTab.chuaPhatHienDoiThuTuong')}
                 </div>
               )}
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
              <div className="bg-white border border-blue-200 rounded-2xl p-5 shadow-sm">
                 <h4 className="font-bold text-secondary mb-2 flex items-center gap-2"><Target className="w-4 h-4" /> {t('similarIdeasTab.differentiationStrategy')}</h4>
                 <p className="text-sm text-slate-700 leading-relaxed">{typeof result.differentiation_strategy === 'string' ? result.differentiation_strategy : t('rubricGeneratorTab.nA')}</p>
              </div>
              <div className="bg-white border border-green-200 rounded-2xl p-5 shadow-sm">
                 <h4 className="font-bold text-success mb-2 flex items-center gap-2"><Zap className="w-4 h-4" /> {t('similarIdeasTab.uniqueAngle')}</h4>
                 <p className="text-sm text-slate-700 leading-relaxed">{typeof result.unique_angle === 'string' ? result.unique_angle : t('rubricGeneratorTab.nA')}</p>
              </div>
              <div className="bg-white border border-orange-200 rounded-2xl p-5 shadow-sm">
                 <h4 className="font-bold text-primary-600 mb-2 flex items-center gap-2"><Crosshair className="w-4 h-4" /> {t('similarIdeasTab.marketGap')}</h4>
                 <p className="text-sm text-slate-700 leading-relaxed">{typeof result.market_gap === 'string' ? result.market_gap : t('rubricGeneratorTab.nA')}</p>
              </div>
            </div>

          </motion.div>
        )}
      </div>
    </div>
  );
};

export default SimilarIdeasTab;
