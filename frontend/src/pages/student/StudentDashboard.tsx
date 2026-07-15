// @ts-nocheck
import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { motion } from 'framer-motion';
import { dashboardApi } from '../../api/dashboardApi';
import { aiApi } from '../../api/aiApi';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import EmptyState from '../../components/ui/EmptyState';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import StatCard from '../../components/ui/StatCard';
import ProgressBar from '../../components/ui/ProgressBar';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import {
  Rocket, Users, Brain, Star, CheckSquare, Calendar, Plus,
  Clock, Zap, MapPin, BookOpen, TrendingUp, AlertTriangle,
} from 'lucide-react';


const statusColor = { DRAFT: 'Draft', SUBMITTED: 'Submitted', REVIEWED: 'Reviewed', APPROVED: 'Approved' };
const milestoneStatus = { TODO: 'bg-slate-100 text-slate-500', IN_PROGRESS: 'bg-blue-100 text-blue-600', DONE: 'bg-green-100 text-green-600', OVERDUE: 'bg-red-100 text-red-600' };

const StudentDashboard = () => {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [analyzing, setAnalyzing] = useState(false);
  const { user } = useAuth();
  const navigate = useNavigate();

  // ── Weekly Tasks Widget state ──────────────────────────────────
  const [selectedRoadmapWeek, setSelectedRoadmapWeek] = useState(() => {
    const stored = localStorage.getItem('selectedRoadmapWeek');
    const n = Number(stored);
    return n >= 1 && n <= 10 ? n : 1;
  });
  const [weeklyStats, setWeeklyStats] = useState(null);
  const [weeklyLoading, setWeeklyLoading] = useState(false);

  const fetchWeeklyStats = async (week) => {
    setWeeklyLoading(true);
    try {
      const res = await dashboardApi.getStudent(week);
      const d = res?.data || res;
      setWeeklyStats(d?.weeklyTasksSummary || null);
    } catch {
      // silently fail — widget is supplemental
    } finally {
      setWeeklyLoading(false);
    }
  };

  const handleRoadmapWeekChange = (w) => {
    const n = Number(w);
    setSelectedRoadmapWeek(n);
    localStorage.setItem('selectedRoadmapWeek', String(n));
    fetchWeeklyStats(n);
  };

  useEffect(() => {
    dashboardApi.getStudent(selectedRoadmapWeek)
      .then(res => {
        const d = res?.data || res;
        setData(d);
        setWeeklyStats(d?.weeklyTasksSummary || null);
      })
      .catch(() => toast.error('Failed to load dashboard'))
      .finally(() => setLoading(false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleAnalyzeAI = async () => {
    if (!data?.startupIdea?._id) { toast.error('Submit your startup idea first'); return; }
    setAnalyzing(true);
    try {
      await aiApi.analyze(data.startupIdea._id);
      toast.success('AI Analysis started! Refreshing...');
      const res = await dashboardApi.getStudent();
      setData(res.data || res);
    } catch (err) {
      toast.error(err.message || 'AI Analysis failed');
    } finally {
      setAnalyzing(false);
    }
  };

  if (loading) return <LoadingSkeleton />;
  if (!data) return <EmptyState icon={Rocket} title="Dashboard unavailable" />;

  const { hasTeam, team = {}, myClass = {}, startupIdea, aiAnalysis, latestEvaluation, milestones = [], mentoringSessions = [], milestoneProgress = {}, roleInTeam } = data;

  return (
    <div className="space-y-6">
      {/* Header */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="flex flex-col sm:flex-row justify-between items-start sm:items-end gap-3">
        <div>
          <h1 className="text-2xl sm:text-3xl font-bold text-slate-900">Welcome, {user?.name?.split(' ')[0] || 'Student'} 🚀</h1>
          <p className="text-slate-500 mt-1">{myClass ? `${myClass.name} • ${myClass.semester}` : 'No class assigned yet'}</p>
        </div>
        {!startupIdea && hasTeam && (
          <Button variant="gradient" size="sm" icon={Plus} onClick={() => navigate('/student/idea/new')}>Submit Idea</Button>
        )}
      </motion.div>

      {!hasTeam ? (
        <EmptyState icon={Users} title="You're not in a team yet" description="Ask your lecturer to add you to a team" />
      ) : (
        <>
          {/* Stats */}
          <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.07 }} className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
            <StatCard title="AI Score" value={aiAnalysis?.aiScore ? `${aiAnalysis.aiScore}/100` : 'N/A'} icon={Brain} color="primary" change={aiAnalysis ? 'Analyzed' : 'Not analyzed'} trend={aiAnalysis?.aiScore >= 70 ? 'up' : 'flat'} />
            <StatCard title="Lecturer Score" value={latestEvaluation?.totalScore ? `${latestEvaluation.totalScore.toFixed(1)}/100` : 'N/A'} icon={Star} color="secondary" change={latestEvaluation ? 'Evaluated' : 'Pending'} trend={latestEvaluation ? 'up' : 'flat'} />
            <StatCard title="Milestones" value={`${milestoneProgress?.done || 0}/${milestoneProgress?.total || 0}`} icon={CheckSquare} color="success" change={`${milestoneProgress?.percentage || 0}% complete`} trend="up" />
            <StatCard title="Sessions" value={mentoringSessions.length || 0} icon={Calendar} color="cyan" change="Mentoring done" trend="flat" />
          </motion.div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Left column */}
            <div className="lg:col-span-2 space-y-5">
              {/* Startup Idea Card */}
              <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.14 }} className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
                <div className="flex items-center justify-between mb-4">
                  <h3 className="text-lg font-semibold text-slate-900 flex items-center gap-2"><Rocket className="w-5 h-5 text-primary" />My Startup Idea</h3>
                  {startupIdea && <Badge variant={statusColor[startupIdea.status]} size="sm">{startupIdea.status}</Badge>}
                </div>

                {!startupIdea ? (
                  <div className="text-center py-6">
                    <Rocket className="w-12 h-12 text-slate-200 mx-auto mb-3" />
                    <p className="text-slate-500 mb-3">No startup idea submitted yet</p>
                    <Button variant="primary" size="sm" icon={Plus} onClick={() => navigate('/student/idea/new')}>Submit Your Idea</Button>
                  </div>
                ) : (
                  <div>
                    <h2 className="text-2xl font-bold text-slate-900">{startupIdea.startupName}</h2>
                    <p className="text-slate-500 mt-1 text-sm leading-relaxed line-clamp-3">{startupIdea.problem}</p>
                    <div className="flex items-center gap-2 mt-4">
                      <Button variant="outline" size="sm" onClick={() => navigate(`/student/idea/${startupIdea._id}`)}>View Details</Button>
                      <Button variant="primary" size="sm" onClick={() => navigate('/student/idea/new')}>Edit Idea</Button>
                      <Button
                        variant="gradient"
                        size="sm"
                        icon={Zap}
                        isLoading={analyzing}
                        onClick={handleAnalyzeAI}
                      >
                        {aiAnalysis ? 'Re-Analyze AI' : 'Analyze with AI'}
                      </Button>
                    </div>
                  </div>
                )}
              </motion.div>

              {/* Milestones */}
              <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.21 }} className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
                <div className="flex items-center justify-between mb-4">
                  <h3 className="text-lg font-semibold text-slate-900 flex items-center gap-2"><CheckSquare className="w-5 h-5 text-green-500" />Milestones</h3>
                  <Button variant="ghost-primary" size="xs" onClick={() => navigate('/executionboard')}>View Kanban</Button>
                </div>
                <div className="mb-4">
                  <ProgressBar value={milestoneProgress?.percentage || 0} showLabel />
                </div>
                {milestones.length === 0 ? (
                  <EmptyState icon={CheckSquare} title="No milestones" size="sm" />
                ) : (
                  <div className="space-y-2">
                    {milestones.slice(0, 5).map(m => (
                      <div key={m._id} className="flex items-center gap-3 p-2.5 rounded-lg hover:bg-slate-50">
                        <div className={`w-2 h-2 rounded-full shrink-0 ${m.status === 'DONE' ? 'bg-green-500' : m.status === 'OVERDUE' ? 'bg-red-500' : m.status === 'IN_PROGRESS' ? 'bg-blue-500' : 'bg-slate-300'}`} />
                        <span className="flex-1 text-sm text-slate-700 truncate">{m.title}</span>
                        <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${milestoneStatus[m.status]}`}>{m.status}</span>
                        {m.dueDate && <span className="text-xs text-slate-400">{new Date(m.dueDate).toLocaleDateString()}</span>}
                      </div>
                    ))}
                  </div>
                )}
              </motion.div>
            </div>

            {/* Right sidebar */}
            <div className="space-y-5">
              {/* Team Card */}
              <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.28 }} className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
                <h3 className="text-lg font-semibold text-slate-900 mb-4 flex items-center gap-2"><Users className="w-5 h-5 text-secondary" />My Team</h3>
                <div className="mb-3">
                  <p className="text-xl font-bold text-slate-900">{team.name}</p>
                  <p className="text-sm text-slate-500">Your role: <span className="font-semibold text-primary">{roleInTeam || 'Member'}</span></p>
                </div>
                <div className="space-y-2.5">
                  {team.members?.map(m => (
                    <div key={m.userId?._id} className="flex items-center gap-3">
                      <div className="w-8 h-8 rounded-xl bg-gradient-to-br from-primary-100 to-primary-200 flex items-center justify-center text-xs font-bold text-primary shrink-0">
                        {m.userId?.name?.charAt(0)}
                      </div>
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-slate-900 truncate">{m.userId?.name}</p>
                        <p className="text-xs text-slate-400">{m.roleInTeam || 'Member'}</p>
                      </div>
                    </div>
                  ))}
                </div>
              </motion.div>

              {/* AI Analysis Preview */}
              {aiAnalysis && (
                <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.35 }} className="bg-gradient-to-br from-primary-600 to-secondary-600 rounded-2xl p-5 text-white">
                  <div className="flex items-center gap-2 mb-3">
                    <Brain className="w-5 h-5" />
                    <h3 className="font-semibold">AI Analysis</h3>
                  </div>
                  <div className="text-5xl font-black mb-1">{aiAnalysis.aiScore}<span className="text-lg font-normal opacity-70">/100</span></div>
                  <p className="text-white/70 text-sm mb-3">AI Feasibility Score</p>
                  <Button variant="outline" size="sm" className="w-full border-white/30 text-white hover:bg-white/10 hover:border-white/50" onClick={() => navigate(`/student/ai-analysis/${startupIdea?._id}`)}>
                    View Full Analysis
                  </Button>
                </motion.div>
              )}

              {/* Feedback Preview */}
              {latestEvaluation && (
                <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.42 }} className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-5">
                  <h3 className="font-semibold text-slate-900 mb-3 flex items-center gap-2"><Star className="w-4 h-4 text-amber-500" />Latest Feedback</h3>
                  <div className="flex items-center gap-2 mb-2">
                    <span className="text-3xl font-black text-slate-900">{latestEvaluation.totalScore?.toFixed(1)}</span>
                    <span className="text-slate-400">/100</span>
                  </div>
                  <p className="text-sm text-slate-500 line-clamp-3">{latestEvaluation.comment}</p>
                  <Button variant="ghost-primary" size="xs" className="mt-2 w-full" onClick={() => navigate('/student/feedback')}>View Full Feedback →</Button>
                </motion.div>
              )}

              {/* Weekly Tasks Widget */}
              <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.56 }}
                className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-5">
                <div className="flex items-center justify-between mb-3">
                  <h3 className="font-semibold text-slate-900 flex items-center gap-2">
                    <MapPin className="w-4 h-4 text-blue-500" />
                    Weekly Roadmap
                  </h3>
                  {/* Week picker */}
                  <div className="flex items-center gap-1.5 bg-slate-50 border border-slate-200 rounded-xl px-2.5 py-1.5">
                    <BookOpen className="w-3 h-3 text-slate-400" />
                    <select
                      value={selectedRoadmapWeek}
                      onChange={e => handleRoadmapWeekChange(e.target.value)}
                      className="text-xs font-semibold text-slate-600 bg-transparent focus:outline-none cursor-pointer"
                    >
                      {Array.from({ length: 10 }, (_, i) => i + 1).map(w => (
                        <option key={w} value={w}>Week {w}</option>
                      ))}
                    </select>
                  </div>
                </div>

                {weeklyLoading ? (
                  <div className="space-y-2">
                    {[1, 2, 3].map(i => (
                      <div key={i} className="h-8 bg-slate-100 rounded-lg animate-pulse" />
                    ))}
                  </div>
                ) : weeklyStats ? (
                  <div className="space-y-2.5">
                    <div className="grid grid-cols-3 gap-2">
                      <div className="bg-blue-50 rounded-xl p-2.5 text-center">
                        <p className="text-lg font-black text-blue-600">{weeklyStats.pending ?? 0}</p>
                        <p className="text-[10px] text-blue-400 font-semibold">Pending</p>
                      </div>
                      <div className="bg-emerald-50 rounded-xl p-2.5 text-center">
                        <p className="text-lg font-black text-emerald-600">{weeklyStats.completed ?? 0}</p>
                        <p className="text-[10px] text-emerald-400 font-semibold">Done</p>
                      </div>
                      <div className={`rounded-xl p-2.5 text-center ${weeklyStats.overdue > 0 ? 'bg-red-50' : 'bg-slate-50'}`}>
                        <p className={`text-lg font-black ${weeklyStats.overdue > 0 ? 'text-red-600' : 'text-slate-400'}`}>
                          {weeklyStats.overdue ?? 0}
                        </p>
                        <p className={`text-[10px] font-semibold ${weeklyStats.overdue > 0 ? 'text-red-400' : 'text-slate-400'}`}>Overdue</p>
                      </div>
                    </div>

                    {weeklyStats.nextDeadline && (
                      <div className="flex items-center gap-2 text-xs text-slate-500 bg-amber-50 border border-amber-100 rounded-lg px-3 py-2">
                        <AlertTriangle className="w-3.5 h-3.5 text-amber-500 shrink-0" />
                        <span>Next deadline: <span className="font-semibold text-amber-700">
                          {new Date(weeklyStats.nextDeadline).toLocaleDateString('en-GB', { day: '2-digit', month: 'short' })}
                        </span></span>
                      </div>
                    )}

                    <Button
                      variant="ghost-primary"
                      size="xs"
                      className="w-full flex items-center gap-1.5 justify-center mt-1"
                      onClick={() => navigate('/student/workspace?tab=roadmap')}
                    >
                      <TrendingUp className="w-3.5 h-3.5" />
                      View Weekly Roadmap →
                    </Button>
                  </div>
                ) : (
                  <div className="text-center py-4">
                    <p className="text-xs text-slate-400">No roadmap data for Week {selectedRoadmapWeek}</p>
                    <Button variant="ghost-primary" size="xs" className="mt-2"
                      onClick={() => navigate('/student/workspace?tab=roadmap')}>
                      Go to Workspace →
                    </Button>
                  </div>
                )}
              </motion.div>

              {/* Sessions */}
              <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.49 }} className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-5">
                <h3 className="font-semibold text-slate-900 mb-3 flex items-center gap-2"><Calendar className="w-4 h-4 text-cyan-500" />Mentoring Sessions</h3>
                {mentoringSessions.length === 0 ? (
                  <p className="text-sm text-slate-400">No sessions scheduled</p>
                ) : (
                  <div className="space-y-2">
                    {mentoringSessions.slice(0, 3).map(s => (
                      <div key={s._id} className="flex items-start gap-2.5 p-2 rounded-lg hover:bg-slate-50">
                        <Clock className="w-4 h-4 text-slate-400 mt-0.5 shrink-0" />
                        <div className="min-w-0">
                          <p className="text-sm font-medium text-slate-900 truncate">{s.title}</p>
                          <p className="text-xs text-slate-400">{s.meetingDate ? new Date(s.meetingDate).toLocaleDateString() : '—'}</p>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </motion.div>
            </div>
          </div>
        </>
      )}
    </div>
  );
};

export default StudentDashboard;
