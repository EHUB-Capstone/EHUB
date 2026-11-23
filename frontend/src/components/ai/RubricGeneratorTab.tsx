// @ts-nocheck
import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { BookOpen, Zap, AlertTriangle, CheckSquare, MessageCircle, FileText, ChevronDown } from 'lucide-react';
import toast from 'react-hot-toast';
import { aiApi } from '../../api/aiApi';
import Button from '../ui/Button';
import { useTranslation } from 'react-i18next';
import i18n from '../../i18n';

const FOCUS_OPTIONS = [
  { id: 'feasibility', get label() { return i18n.t('ai:rubricGeneratorTab.feasibilityTinhKhaThi'); } },
  { id: 'market', get label() { return i18n.t('ai:rubricGeneratorTab.marketThiTruong'); } },
  { id: 'technical', get label() { return i18n.t('ai:rubricGeneratorTab.technicalKyThuat'); } },
  { id: 'business', get label() { return i18n.t('ai:rubricGeneratorTab.businessKinhDoanh'); } },
  { id: 'presentation', get label() { return i18n.t('ai:rubricGeneratorTab.presentationTrinhBay'); } }
];

const RubricGeneratorTab = ({ initialData }) => {
  const { t } = useTranslation('ai');
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
      toast.error(t('rubricGeneratorTab.vuiLongDienDuTenGiai'));
      return;
    }
    if (focusAreas.length === 0) {
      toast.error(t('rubricGeneratorTab.vuiLongChonItNhatMot'));
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
        toast.error(t('rubricGeneratorTab.aiDangDungPhanHoiDu'), { duration: 5000 });
      } else {
        toast.success(t('rubricGeneratorTab.taoRubricHoanTat'));
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

  const isFallback = result?.rubric_questions?.length === 1 && 
                     result?.rubric_questions[0]?.category === "Problem Validation" &&
                     result?.rubric_questions[0]?.question?.includes("Sinh viên đã chứng minh vấn đề");

  return (
    <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
      {/* Form Panel */}
      <div className="lg:col-span-4 bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col">
        <div className="p-5 border-b border-slate-100 bg-slate-50 flex items-center justify-between">
           <h2 className="font-semibold text-slate-800 text-lg">{t('rubricGeneratorTab.rubricSetup')}</h2>
           <span className="text-xs font-medium px-2 py-1 bg-blue-100 text-secondary rounded-md">{t('rubricGeneratorTab.input')}</span>
        </div>
        <div className="p-5 flex-1">
          <form onSubmit={handleGenerate} className="space-y-4 flex flex-col h-full">
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('rubricGeneratorTab.tenStartup')}</label>
              <input required name="startup_name" value={formData.startup_name} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('rubricGeneratorTab.eGEdutrack')} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('rubricGeneratorTab.moHinhKinhDoanh')}</label>
              <input required name="business_model" value={formData.business_model} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary" placeholder={t('rubricGeneratorTab.eGB2bSaas')} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-1">{t('rubricGeneratorTab.giaiDoanStage')}</label>
              <select required name="stage" value={formData.stage} onChange={handleChange} className="w-full text-sm border-slate-200 rounded-lg focus:ring-secondary focus:border-secondary">
                <option value="idea">{t('rubricGeneratorTab.ideaYTuong')}</option>
                <option value="prototype">{t('rubricGeneratorTab.prototypeNguyenMau')}</option>
                <option value="mvp">{t('rubricGeneratorTab.mvpSanPhamToiThieu')}</option>
                <option value="pitch">{t('rubricGeneratorTab.pitchDeckDaCoSanPham')}</option>
              </select>
            </div>
            
            <div>
              <label className="block text-xs font-semibold text-slate-600 uppercase tracking-wider mb-2">{t('rubricGeneratorTab.tieuChiDanhGiaFocusAreas')}</label>
              <div className="space-y-2">
                {FOCUS_OPTIONS.map(opt => (
                  <label key={opt.id} className="flex items-center gap-2 cursor-pointer group">
                    <div className={`w-5 h-5 rounded border flex items-center justify-center transition-colors ${focusAreas.includes(opt.id) ? 'bg-secondary border-secondary' : 'border-slate-300 group-hover:border-secondary'}`}>
                      {focusAreas.includes(opt.id) && <CheckSquare className="w-3.5 h-3.5 text-white" />}
                    </div>
                    <span className="text-sm text-slate-700 select-none">{opt.label}</span>
                  </label>
                ))}
              </div>
            </div>
            
            <div className="pt-4 mt-auto">
              <Button type="submit" className="w-full bg-secondary hover:bg-secondary-700 text-white" isLoading={loading} disabled={loading} icon={BookOpen}>
                {t('rubricGeneratorTab.generateRubric')}
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
             <h3 className="text-xl font-semibold text-secondary">{t('rubricGeneratorTab.craftingQuestions')}</h3>
             <p className="text-slate-500 mt-2">{t('rubricGeneratorTab.aiIsGeneratingCustomizedRubricAnd')}</p>
          </div>
        ) : !result ? (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-12 flex flex-col items-center justify-center min-h-[500px]">
            <div className="w-24 h-24 bg-slate-50 rounded-full flex items-center justify-center mb-6">
              <BookOpen className="w-12 h-12 text-slate-300" />
            </div>
            <h3 className="text-xl font-semibold text-slate-700">{t('rubricGeneratorTab.readyForRubric')}</h3>
            <p className="text-slate-500 mt-2 max-w-sm text-center">{t('rubricGeneratorTab.selectYourFocusAreasToGenerate')}</p>
          </div>
        ) : (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-6">
            
            {isFallback && (
               <div className="bg-orange-100 text-orange-800 text-sm px-4 py-3 rounded-xl flex items-center gap-2 font-medium border border-orange-200 shadow-sm">
                 <AlertTriangle className="w-5 h-5 shrink-0" />
                 {t('rubricGeneratorTab.aiDangDungPhanHoiDu2')}
               </div>
            )}
            
            {/* Tips Card */}
            <div className="bg-secondary/5 border border-secondary/20 rounded-2xl p-5 shadow-sm">
               <h3 className="font-bold text-secondary mb-2 flex items-center gap-2"><MessageCircle className="w-5 h-5" /> {t('rubricGeneratorTab.evaluationTipsForMentor')}</h3>
               <p className="text-slate-700 leading-relaxed text-sm">{typeof result.evaluation_tips === 'string' ? result.evaluation_tips : t('rubricGeneratorTab.nA')}</p>
            </div>

            {/* Rubric Questions */}
            <div className="space-y-4">
               {Array.isArray(result.rubric_questions) && result.rubric_questions.length > 0 ? (
                 result.rubric_questions.map((item, idx) => (
                   <div key={idx} className="bg-white border border-slate-200 rounded-xl overflow-hidden shadow-sm">
                     <div className="p-5 border-b border-slate-100">
                        <span className="inline-block px-3 py-1 bg-blue-100 text-secondary text-xs font-bold rounded-md uppercase tracking-wider mb-3">
                           {typeof item.category === 'string' ? item.category : t('rubricGeneratorTab.general')}
                        </span>
                        <h4 className="text-lg font-bold text-slate-800 leading-snug">{typeof item.question === 'string' ? item.question : t('rubricGeneratorTab.nA')}</h4>
                     </div>
                     
                     <div className="bg-slate-50 p-5 space-y-4 border-b border-slate-100">
                        <h5 className="text-xs font-bold text-slate-500 uppercase tracking-wider flex items-center gap-2"><FileText className="w-4 h-4" /> {t('rubricGeneratorTab.scoringGuide')}</h5>
                        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                          <div className="bg-white p-3 rounded-lg border border-green-100 shadow-sm">
                            <span className="block font-black text-success text-xl mb-1">{t('rubricGeneratorTab.5Pts')}</span>
                            <span className="text-xs text-slate-600">{typeof item.scoring_guide?.["5"] === 'string' ? item.scoring_guide["5"] : t('rubricGeneratorTab.nA')}</span>
                          </div>
                          <div className="bg-white p-3 rounded-lg border border-orange-100 shadow-sm">
                            <span className="block font-black text-primary-600 text-xl mb-1">{t('rubricGeneratorTab.3Pts')}</span>
                            <span className="text-xs text-slate-600">{typeof item.scoring_guide?.["3"] === 'string' ? item.scoring_guide["3"] : t('rubricGeneratorTab.nA')}</span>
                          </div>
                          <div className="bg-white p-3 rounded-lg border border-red-100 shadow-sm">
                            <span className="block font-black text-red-500 text-xl mb-1">{t('rubricGeneratorTab.1Pt')}</span>
                            <span className="text-xs text-slate-600">{typeof item.scoring_guide?.["1"] === 'string' ? item.scoring_guide["1"] : t('rubricGeneratorTab.nA')}</span>
                          </div>
                        </div>
                     </div>
                     
                     <div className="p-4 bg-orange-50/50 flex items-start gap-3">
                        <Zap className="w-5 h-5 text-primary-600 shrink-0 mt-0.5" />
                        <div>
                          <span className="block text-xs font-bold text-primary-600 uppercase tracking-wider mb-1">{t('rubricGeneratorTab.followUpQuestion')}</span>
                          <span className="text-sm font-medium text-slate-700 italic">{typeof item.follow_up === 'string' ? item.follow_up : t('rubricGeneratorTab.nA')}</span>
                        </div>
                     </div>
                   </div>
                 ))
               ) : (
                 <div className="bg-slate-50 border border-slate-200 border-dashed rounded-xl p-8 text-center text-slate-500">
                    {t('rubricGeneratorTab.chuaCoRubricQuestions')}
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
