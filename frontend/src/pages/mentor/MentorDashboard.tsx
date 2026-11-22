// @ts-nocheck
import { useState, useEffect } from 'react';
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
import ReviewInboxCard from '../../components/workspace/ReviewInboxCard';
import {
  Users, ClipboardList, TrendingUp, CheckCircle, Calendar,
  ArrowRight, Zap, Star, AlertCircle
} from 'lucide-react';
import { useTranslation } from 'react-i18next';

const MentorDashboard = () => {
  const { t } = useTranslation('dashboard');
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const { user } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    dashboardApi.getMentor()
      .then(res => setData(res.data || res))
      .catch(() => undefined)
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <LoadingSkeleton />;
  if (!data) return <EmptyState icon={Users} title={t('common:states.noData')} />;

  const { myTeams, pendingReviews, upcomingSessions, averageScore, taskProgress, recentEvaluations = [], recentSessions = [] } = data;

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="flex flex-col sm:flex-row justify-between items-start sm:items-end gap-3">
        <div>
          <h1 className="text-heading sm:text-display text-secondary tracking-tight">{t('mentorDashboard.welcomeBack', { v1: user?.name?.split(' ')[0] || 'Mentor' })}</h1>
          <p className="text-slate-500 mt-1">{t('mentorDashboard.hereSAnOverviewOfYour')}</p>
        </div>
        <Button variant="primary" size="sm" icon={Calendar} onClick={() => navigate('/sessions')}>{t('mentorDashboard.scheduleSession')}</Button>
      </motion.div>

      <ReviewInboxCard />

      {/* Stats */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.07 }} className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
        <StatCard title={t('mentorDashboard.myTeams')} value={myTeams || 0} icon={Users} color="primary" change="Assigned teams" trend="flat" />
        <StatCard title={t('mentorDashboard.pendingReviews')} value={pendingReviews || 0} icon={ClipboardList} color="warning" change="Need action" trend={pendingReviews > 0 ? 'up' : 'flat'} />
        <StatCard title={t('mentorDashboard.upcomingSessions')} value={upcomingSessions || 0} icon={Calendar} color="secondary" change="Scheduled" trend="up" />
        <StatCard title={t('mentorDashboard.avgRating')} value={averageScore ?? '—'} icon={Star} color="success" change={averageScore == null ? 'Scores not published' : 'Out of 100'} trend="flat" />
      </motion.div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Main Content Area */}
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.14 }} className="lg:col-span-2 space-y-5">
          {/* Recent Evaluations */}
          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
            <div className="flex items-center justify-between mb-5">
              <h3 className="text-lg font-semibold text-slate-900 flex items-center gap-2">
                <CheckCircle className="w-5 h-5 text-emerald-500" />{t('mentorDashboard.recentEvaluations')}
              </h3>
            </div>
            {recentEvaluations.length === 0 ? (
              <EmptyState icon={CheckCircle} title={t('adminDashboard.noEvaluationsYet')} description={t('mentorDashboard.youHavenTEvaluatedAnyTeams')} size="sm" />
            ) : (
              <div className="space-y-3">
                {recentEvaluations.map(evalu => (
                  <div key={evalu._id} className="flex items-center justify-between p-4 rounded-xl border border-slate-100">
                    <div className="min-w-0 flex-1">
                      <h4 className="font-semibold text-slate-900 truncate">{t('mentorDashboard.evaluationFor', { v1: evalu.teamId?.teamName || 'Team' })}</h4>
                      <p className="text-sm text-slate-500">{evalu.totalScore == null ? t('mentorDashboard.scorePendingPublication') : t('mentorDashboard.teamScore', { totalScore: evalu.totalScore })}</p>
                    </div>
                    <div className="flex items-center gap-2 shrink-0 ml-3">
                      <Badge variant="Approved" size="xs">{t('lecturerDashboard.evaluated')}</Badge>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </motion.div>

        {/* Sidebar area */}
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.21 }} className="space-y-5">
          
          {/* Overall Team Task Progress */}
          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
            <h3 className="text-lg font-semibold text-slate-900 mb-4 flex items-center gap-2"><TrendingUp className="w-5 h-5 text-primary" />{t('mentorDashboard.teamProgress')}</h3>
            <div className="space-y-3">
              <div className="flex justify-between text-sm mb-1">
                <span className="font-medium text-slate-700">{t('mentorDashboard.sprintTasks')}</span>
                <span className="font-bold text-primary-700">{taskProgress}%</span>
              </div>
              <ProgressBar value={taskProgress} size="md" />
              <p className="text-xs text-slate-500 mt-2">{t('mentorDashboard.averageTaskCompletionRateAcrossAll')}</p>
            </div>
          </div>

          {/* Recent Sessions */}
          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
            <h3 className="text-lg font-semibold text-slate-900 mb-4 flex items-center gap-2"><Calendar className="w-5 h-5 text-secondary" />{t('mentorDashboard.recentSessions')}</h3>
            {recentSessions.length === 0 ? (
              <EmptyState icon={Calendar} title={t('mentorDashboard.noSessionsYet')} size="sm" />
            ) : (
              <div className="space-y-3">
                {recentSessions.map(s => (
                  <div key={s._id} className="p-3 rounded-xl border border-slate-100">
                    <p className="text-sm font-semibold text-slate-900 truncate">{s.title}</p>
                    <p className="text-xs text-slate-500 mt-0.5">{s.teamId?.teamName || s.teamId?.name || t('mentorDashboard.team')} • {s.meetingDate ? new Date(s.meetingDate).toLocaleDateString() : '—'}</p>
                  </div>
                ))}
              </div>
            )}
          </div>

          <Button variant="outline" size="sm" className="w-full" icon={Zap} onClick={() => navigate('/sessions')}>{t('mentorDashboard.viewAllSessions')}</Button>
        </motion.div>
      </div>
    </div>
  );
};

export default MentorDashboard;
