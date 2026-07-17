import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { motion } from 'framer-motion';
import { dashboardApi } from '../../api/dashboardApi';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import EmptyState from '../../components/ui/EmptyState';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import StatCard from '../../components/ui/StatCard';
import ProgressBar from '../../components/ui/ProgressBar';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import {
  GraduationCap, Users, ClipboardList, TrendingUp, CheckCircle, Calendar,
  ArrowRight, Zap, BookOpen, AlertCircle
} from 'lucide-react';

const LecturerDashboard = () => {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const { user } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    dashboardApi.getLecturer()
      .then(res => setData(res.data || res))
      .catch(() => toast.error('Failed to load dashboard'))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <LoadingSkeleton />;
  if (!data) return <EmptyState icon={GraduationCap} title="No data found" />;

  const { totalClasses, totalTeams, totalStudents, pendingReviews, myClasses = [], pendingIdeas = [], recentSessions = [], teamRankings = [] } = data;

  const handleReviewIdea = async (idea) => {
    navigate(`/evaluations?ideaId=${idea._id}`);
  };

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="flex flex-col sm:flex-row justify-between items-start sm:items-end gap-3">
        <div>
          <h1 className="text-2xl sm:text-3xl font-bold text-slate-900">Welcome back, {user?.name?.split(' ')[0] || 'Lecturer'} 👋</h1>
          <p className="text-slate-500 mt-1">Here's an overview of your startup classes</p>
        </div>
        <Button variant="gradient" size="sm" icon={Calendar} onClick={() => navigate('/sessions')}>Schedule Session</Button>
      </motion.div>

      {/* Stats */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.07 }} className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
        <StatCard title="Active Classes" value={totalClasses || 0} icon={GraduationCap} color="primary" change="This semester" trend="up" />
        <StatCard title="Mentoring Teams" value={totalTeams || 0} icon={Users} color="secondary" change="All classes" trend="flat" />
        <StatCard title="Pending Reviews" value={pendingReviews || 0} icon={ClipboardList} color="warning" change="Need action" trend={pendingReviews > 0 ? 'up' : 'flat'} />
        <StatCard title="Students" value={totalStudents || 0} icon={BookOpen} color="success" change="Enrolled" trend="flat" />
      </motion.div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Pending Reviews */}
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.14 }} className="lg:col-span-2 space-y-5">
          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
            <div className="flex items-center justify-between mb-5">
              <h3 className="text-lg font-semibold text-slate-900 flex items-center gap-2">
                <AlertCircle className="w-5 h-5 text-amber-500" />Pending Reviews
                {pendingReviews > 0 && <span className="bg-amber-100 text-amber-700 text-xs font-bold px-2 py-0.5 rounded-full">{pendingReviews}</span>}
              </h3>
              <Button variant="ghost-primary" size="xs" onClick={() => navigate('/evaluations')}>View All</Button>
            </div>
            {pendingIdeas.length === 0 ? (
              <EmptyState icon={CheckCircle} title="All caught up!" description="No pending reviews right now" size="sm" />
            ) : (
              <div className="space-y-3">
                {pendingIdeas.slice(0, 5).map(idea => (
                  <div key={idea._id} className="flex items-center justify-between p-4 rounded-xl bg-amber-50/60 border border-amber-100">
                    <div className="min-w-0 flex-1">
                      <h4 className="font-semibold text-slate-900 truncate">{idea.startupName}</h4>
                      <p className="text-sm text-slate-500">{idea.teamId?.name} • {idea.teamId?.classId?.name}</p>
                    </div>
                    <div className="flex items-center gap-2 shrink-0 ml-3">
                      <Badge variant="Submitted" size="xs">Submitted</Badge>
                      <Button variant="primary" size="xs" icon={ArrowRight} onClick={() => handleReviewIdea(idea)}>Review</Button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* My Classes */}
          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
            <h3 className="text-lg font-semibold text-slate-900 mb-4 flex items-center gap-2"><GraduationCap className="w-5 h-5 text-primary" />My Classes</h3>
            {myClasses.length === 0 ? (
              <EmptyState icon={GraduationCap} title="No classes assigned" size="sm" />
            ) : (
              <div className="space-y-3">
                {myClasses.map(cls => (
                  <div key={cls._id} className="flex items-center justify-between p-4 rounded-xl border border-slate-100 hover:border-primary-200 hover:bg-primary-50/30 transition-all cursor-pointer" onClick={() => navigate(`/lecturer/classes/${cls._id}`)}>
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-xl bg-primary-100 flex items-center justify-center font-bold text-primary text-sm">{cls.code?.slice(0, 3)}</div>
                      <div>
                        <p className="font-semibold text-slate-900">{cls.code}</p>
                        <p className="text-sm text-slate-500">{cls.name} • {cls.members?.length || 0} students</p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <Badge variant="Active" size="xs" dot>Active</Badge>
                      <ArrowRight className="w-4 h-4 text-slate-400" />
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </motion.div>

        {/* Team Rankings Sidebar */}
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.21 }} className="space-y-5">
          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
            <h3 className="text-lg font-semibold text-slate-900 mb-4 flex items-center gap-2"><TrendingUp className="w-5 h-5 text-primary" />Team Rankings</h3>
            {teamRankings.length === 0 ? (
              <EmptyState icon={TrendingUp} title="No scores yet" size="sm" />
            ) : (
              <div className="space-y-3">
                {teamRankings.slice(0, 6).map((t, i) => (
                  <div key={t.team?.id} className="flex items-center gap-3">
                    <span className={`w-6 h-6 rounded-lg flex items-center justify-center text-xs font-bold shrink-0 ${i === 0 ? 'bg-amber-100 text-amber-600' : i === 1 ? 'bg-slate-100 text-slate-600' : 'bg-slate-50 text-slate-400'}`}>{i + 1}</span>
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-semibold text-slate-900 truncate">{t.team?.name || '—'}</p>
                      <ProgressBar value={t.avgScore || 0} size="xs" />
                    </div>
                    <span className={`text-sm font-bold shrink-0 ${t.avgScore >= 80 ? 'text-green-600' : t.avgScore >= 60 ? 'text-amber-600' : 'text-slate-400'}`}>
                      {t.avgScore?.toFixed(1) || '—'}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Recent Sessions */}
          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
            <h3 className="text-lg font-semibold text-slate-900 mb-4 flex items-center gap-2"><Calendar className="w-5 h-5 text-secondary" />Recent Sessions</h3>
            {recentSessions.length === 0 ? (
              <EmptyState icon={Calendar} title="No sessions yet" size="sm" />
            ) : (
              <div className="space-y-3">
                {recentSessions.map(s => (
                  <div key={s._id} className="p-3 rounded-xl border border-slate-100">
                    <p className="text-sm font-semibold text-slate-900 truncate">{s.title}</p>
                    <p className="text-xs text-slate-500 mt-0.5">{s.teamId?.name} • {s.meetingDate ? new Date(s.meetingDate).toLocaleDateString() : '—'}</p>
                  </div>
                ))}
              </div>
            )}
          </div>

          <Button variant="outline" size="sm" className="w-full" icon={Zap} onClick={() => navigate('/sessions')}>View All Sessions</Button>
        </motion.div>
      </div>
    </div>
  );
};

export default LecturerDashboard;
