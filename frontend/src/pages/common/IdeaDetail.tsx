// @ts-nocheck
import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { motion } from 'framer-motion';
import { evaluationApi } from '../../api/evaluationApi';
import { startupApi } from '../../api/startupApi';
import { dashboardApi } from '../../api/dashboardApi';
import { useAuth } from '../../hooks/useAuth';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import EmptyState from '../../components/ui/EmptyState';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import { Star, MessageSquare, ClipboardCheck, Award, ChevronDown, ChevronUp } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';

const CRITERIA = [
  { key: 'innovationScore', label: 'Innovation', desc: 'Originality and creativity of the idea' },
  { key: 'feasibilityScore', label: 'Feasibility', desc: 'Technical and business feasibility' },
  { key: 'marketScore', label: 'Market Potential', desc: 'Market size and opportunity' },
  { key: 'technicalScore', label: 'Technical', desc: 'Technology approach and architecture' },
  { key: 'presentationScore', label: 'Presentation', desc: 'Clarity and completeness of proposal' },
];

const ScoreSlider = ({ label, desc, value, onChange, readonly = false }) => (
  <div className="space-y-1.5">
    <div className="flex justify-between items-center">
      <div>
        <span className="text-sm font-semibold text-slate-700">{label}</span>
        <p className="text-xs text-slate-400">{desc}</p>
      </div>
      <div className="text-right">
        <span className={`text-2xl font-black ${value >= 8 ? 'text-green-600' : value >= 6 ? 'text-amber-600' : 'text-red-500'}`}>{Number(value).toFixed(1)}</span>
        <span className="text-slate-400 text-sm">/10</span>
      </div>
    </div>
    <input
      type="range"
      min={0} max={10} step={0.5}
      value={value}
      onChange={onChange}
      disabled={readonly}
      className="w-full h-2 rounded-full appearance-none cursor-pointer accent-primary disabled:opacity-60"
    />
    <div className="flex justify-between text-xs text-slate-300">
      <span>0</span><span>5</span><span>10</span>
    </div>
  </div>
);

const EvaluationPage = () => {
  const { user } = useAuth();
  const [searchParams] = useSearchParams();
  const ideaId = searchParams.get('ideaId');

  const [pendingIdeas, setPendingIdeas] = useState([]);
  const [selectedIdea, setSelectedIdea] = useState(null);
  const [evaluations, setEvaluations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [showHistory, setShowHistory] = useState(false);

  const [scores, setScores] = useState({ innovationScore: 7, feasibilityScore: 7, marketScore: 7, technicalScore: 7, presentationScore: 7 });
  const [comment, setComment] = useState('');

  const totalScore = Object.values(scores).reduce((a, b) => a + Number(b), 0) / Object.keys(scores).length;

  const fetchData = async () => {
    try {
      setLoading(true);
      if (user?.role === 'LECTURER') {
        const res = await dashboardApi.getLecturer();
        const d = res.data || res;
        const ideas = d.pendingIdeas || [];
        setPendingIdeas(ideas);
        if (ideaId) {
          const found = ideas.find(i => i._id === ideaId);
          if (found) selectIdea(found);
          else {
            // Load idea directly
            const ideaRes = await startupApi.getById(ideaId);
            const idea = ideaRes.data?.startupIdea || ideaRes.data;
            if (idea) selectIdea(idea);
          }
        } else if (ideas.length > 0) {
          selectIdea(ideas[0]);
        }
      } else if (user?.role === 'STUDENT') {
        // Student views their own feedback
        const res = await dashboardApi.getStudent();
        const d = res.data || res;
        if (d.startupIdea) {
          setSelectedIdea(d.startupIdea);
          const evalRes = await evaluationApi.getByStartup(d.startupIdea._id);
          const evals = evalRes.data?.evaluations || evalRes.evaluations || evalRes.data || [];
          setEvaluations(Array.isArray(evals) ? evals : []);
        }
      }
    } catch {
      toast.error('Failed to load data');
    } finally {
      setLoading(false);
    }
  };

  const selectIdea = async (idea) => {
    setSelectedIdea(idea);
    try {
      const res = await evaluationApi.getByStartup(idea._id);
      const evals = res.data?.evaluations || res.evaluations || res.data || [];
      setEvaluations(Array.isArray(evals) ? evals : []);
    } catch { /* no-op */ }
  };

  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => { void fetchData(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const handleSubmit = async () => {
    if (!selectedIdea) { toast.error('Select an idea to evaluate'); return; }
    setSaving(true);
    try {
      await evaluationApi.create({
        startupIdeaId: selectedIdea._id,
        ...scores,
        comment,
      });
      toast.success('Evaluation submitted!');
      setComment('');
      // Refresh
      await selectIdea(selectedIdea);
    } catch (err) {
      toast.error(err.message || 'Failed to submit');
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <LoadingSkeleton />;

  const isLecturer = user?.role === 'LECTURER';

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
        <h1 className="text-3xl font-bold text-slate-900">{isLecturer ? 'Evaluate Startup Ideas' : 'Feedback & Evaluation'}</h1>
        <p className="text-slate-500 mt-1">{isLecturer ? `${pendingIdeas.length} ideas pending review` : 'View your lecturer feedback'}</p>
      </motion.div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Left: Idea List (for lecturer) or Idea info (for student) */}
        {isLecturer && (
          <div className="lg:col-span-4">
            <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm overflow-hidden">
              <div className="p-4 border-b border-slate-100 bg-slate-50/60">
                <h3 className="font-semibold text-slate-900">Pending Review</h3>
              </div>
              {pendingIdeas.length === 0 ? (
                <div className="p-6"><EmptyState icon={ClipboardCheck} title="All caught up!" size="sm" /></div>
              ) : (
                <div className="divide-y divide-slate-50">
                  {pendingIdeas.map(idea => (
                    <div
                      key={idea._id}
                      onClick={() => selectIdea(idea)}
                      className={`p-4 cursor-pointer hover:bg-primary-50/30 transition-all ${selectedIdea?._id === idea._id ? 'bg-primary-50/40 border-l-2 border-primary' : ''}`}
                    >
                      <h4 className="font-semibold text-sm text-slate-900">{idea.startupName}</h4>
                      <p className="text-xs text-slate-500 mt-0.5">{idea.teamId?.name} • {idea.teamId?.classId?.name}</p>
                      <Badge variant="Submitted" size="xs" className="mt-1">Submitted</Badge>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}

        {/* Right: Evaluation Form */}
        <div className={isLecturer ? 'lg:col-span-8' : 'lg:col-span-12'}>
          {!selectedIdea ? (
            <EmptyState icon={Star} title="Select an idea to evaluate" />
          ) : (
            <div className="space-y-5">
              {/* Idea Header */}
              <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
                <div className="flex justify-between items-start">
                  <div>
                    <h2 className="text-2xl font-bold text-slate-900">{selectedIdea.startupName}</h2>
                    <p className="text-slate-500 text-sm mt-1">{selectedIdea.teamId?.name} • {selectedIdea.teamId?.classId?.name}</p>
                  </div>
                  <Badge variant={selectedIdea.status} size="sm">{selectedIdea.status}</Badge>
                </div>
                <p className="text-slate-600 text-sm mt-3 leading-relaxed line-clamp-3">{selectedIdea.problem}</p>
              </motion.div>

              {/* Evaluation Form (only for lecturers, or show existing) */}
              {isLecturer && (
                <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.05 }} className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
                  <h3 className="text-lg font-semibold text-slate-900 mb-5 flex items-center gap-2"><Star className="w-5 h-5 text-amber-500" />Score Rubric</h3>
                  <div className="space-y-6">
                    {CRITERIA.map(c => (
                      <ScoreSlider
                        key={c.key}
                        label={c.label}
                        desc={c.desc}
                        value={scores[c.key]}
                        onChange={(e) => setScores(s => ({ ...s, [c.key]: parseFloat(e.target.value) }))}
                      />
                    ))}
                  </div>

                  {/* Total Score Preview */}
                  <div className="mt-6 p-5 rounded-xl bg-gradient-to-r from-primary-50 to-secondary-50 border border-primary-100 flex items-center justify-between">
                    <div>
                      <p className="text-sm text-slate-600 font-medium">Total Score</p>
                      <p className="text-xs text-slate-400">Average of all criteria</p>
                    </div>
                    <div className="text-right">
                      <span className={`text-4xl font-black ${totalScore >= 8 ? 'text-green-600' : totalScore >= 6 ? 'text-amber-600' : 'text-red-500'}`}>{totalScore.toFixed(2)}</span>
                      <span className="text-slate-400">/10</span>
                    </div>
                  </div>

                  <div className="mt-5">
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5 flex items-center gap-2"><MessageSquare className="w-4 h-4" />Comment & Feedback</label>
                    <textarea
                      rows={4}
                      value={comment}
                      onChange={(e) => setComment(e.target.value)}
                      placeholder="Provide detailed feedback to help the team improve their startup idea..."
                      className="w-full border border-slate-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary resize-none"
                    />
                  </div>

                  <div className="mt-5 flex justify-end">
                    <Button variant="gradient" size="md" icon={Award} isLoading={saving} onClick={handleSubmit}>Submit Evaluation</Button>
                  </div>
                </motion.div>
              )}

              {/* Evaluation History */}
              {evaluations.length > 0 && (
                <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }} className="bg-white rounded-2xl border border-slate-200/60 shadow-sm overflow-hidden">
                  <button
                    onClick={() => setShowHistory(!showHistory)}
                    className="w-full p-5 flex items-center justify-between hover:bg-slate-50 transition-colors"
                  >
                    <h3 className="font-semibold text-slate-900 flex items-center gap-2"><ClipboardCheck className="w-5 h-5 text-primary" />Evaluation History ({evaluations.length})</h3>
                    {showHistory ? <ChevronUp className="w-5 h-5 text-slate-400" /> : <ChevronDown className="w-5 h-5 text-slate-400" />}
                  </button>

                  {showHistory && (
                    <div className="p-5 space-y-4 border-t border-slate-100">
                      {evaluations.map(ev => (
                        <div key={ev._id} className="p-4 bg-slate-50 rounded-xl border border-slate-100">
                          <div className="flex justify-between items-center mb-3">
                            <div>
                              <p className="font-semibold text-slate-900">{ev.lecturerId?.name || 'Lecturer'}</p>
                              <p className="text-xs text-slate-400">{ev.createdAt ? new Date(ev.createdAt).toLocaleDateString() : '—'}</p>
                            </div>
                            <span className={`text-2xl font-black ${ev.totalScore >= 8 ? 'text-green-600' : ev.totalScore >= 6 ? 'text-amber-600' : 'text-red-500'}`}>{ev.totalScore?.toFixed(2)}/10</span>
                          </div>
                          <div className="grid grid-cols-5 gap-2 mb-3">
                            {CRITERIA.map(c => (
                              <div key={c.key} className="text-center">
                                <p className="text-xs text-slate-400">{c.label.slice(0, 6)}</p>
                                <p className="font-bold text-sm text-slate-700">{ev[c.key]?.toFixed(1)}</p>
                              </div>
                            ))}
                          </div>
                          {ev.comment && <p className="text-sm text-slate-600 bg-white rounded-lg p-3 border border-slate-100">{ev.comment}</p>}
                        </div>
                      ))}
                    </div>
                  )}
                </motion.div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default EvaluationPage;
