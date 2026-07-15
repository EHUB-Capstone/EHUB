// @ts-nocheck
import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { motion } from 'framer-motion';
import { Users, GraduationCap, Rocket, Brain, TrendingUp, Trophy, BarChart3, Activity, UserCheck, LogIn, AlertTriangle, UserPlus, ShieldAlert, Zap, Wifi, WifiOff, Clock } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, PieChart, Pie, Cell, LineChart, Line, Legend } from 'recharts';
import { dashboardApi } from '../../api/dashboardApi';
import { trackingApi } from '../../api/trackingApi';
import StatCard from '../../components/ui/StatCard';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import EmptyState from '../../components/ui/EmptyState';
import Button from '../../components/ui/Button';
import { useNavigate } from 'react-router-dom';

const COLORS = ['#034EA2', '#F37021', '#51B848', '#3371b8', '#f58f4d'];

const AdminDashboard = () => {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [trackingData, setTrackingData] = useState(null);
  const [trackingDays, setTrackingDays] = useState(7);
  const [onlineData, setOnlineData] = useState(null);
  const navigate = useNavigate();

  useEffect(() => {
    const fetchData = async () => {
      try {
        const res = await dashboardApi.getAdmin();
        setData(res.data || res);
      } catch (err) {
        setError(err.message || 'Failed to load dashboard');
        toast.error('Could not load dashboard data');
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  useEffect(() => {
    const fetchTracking = async () => {
      try {
        const res = await trackingApi.getAuthStats(trackingDays);
        setTrackingData(res.data || res);
      } catch (err) {
        // Tracking failure không làm vỡ dashboard
        console.warn('Could not load tracking data:', err.message);
      }
    };
    fetchTracking();
  }, [trackingDays]);

  // Fetch online users (auto-refresh every 30s)
  useEffect(() => {
    const fetchOnline = async () => {
      try {
        const res = await trackingApi.getOnlineUsers();
        setOnlineData(res.data || res);
      } catch (err) {
        console.warn('Could not load online users:', err.message);
      }
    };
    fetchOnline();
    const interval = setInterval(fetchOnline, 30000);
    return () => clearInterval(interval);
  }, []);

  if (loading) return <LoadingSkeleton />;
  if (error) return <EmptyState icon={Activity} title="Error Loading Dashboard" description={error} action={{ label: 'Retry', onClick: () => window.location.reload() }} />;
  if (!data) return null;

  const { stats = {}, usersByRole = [], ideasByStatus = [], topTeams = [] } = data;

  const roleChartData = usersByRole.map(r => ({ name: r.role, value: r.count }));
  const statusChartData = ideasByStatus.map(s => ({ name: s.status, value: s.count }));

  const scoreBarData = topTeams.slice(0, 8).map((t, i) => ({
    name: t.team?.name || `Team ${i + 1}`,
    score: t.avgScore || 0,
  }));

  const fade = (i = 0) => ({ initial: { opacity: 0, y: 20 }, animate: { opacity: 1, y: 0 }, transition: { delay: i * 0.07 } });

  return (
    <div className="space-y-6">
      {/* Header */}
      <motion.div {...fade(0)} className="flex flex-col sm:flex-row justify-between items-start sm:items-end gap-3">
        <div>
          <h1 className="text-2xl sm:text-3xl font-bold text-slate-900">Admin Overview</h1>
          <p className="text-slate-500 mt-1">Platform analytics and system health</p>
        </div>
        <Button variant="outline" size="sm" icon={BarChart3} onClick={() => navigate('/rankings')}>View Rankings</Button>
      </motion.div>

      {/* Stat Cards */}
      <motion.div {...fade(1)} className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3 sm:gap-4">
        <StatCard title="Total Users" value={(stats.totalUsers || 0).toLocaleString()} icon={Users} color="primary" change="Platform users" trend="up" />
        <StatCard title="Classes" value={stats.totalClasses || 0} icon={GraduationCap} color="cyan" change="Active semester" trend="up" />
        <StatCard title="Teams" value={stats.totalTeams || 0} icon={Rocket} color="secondary" change="Startup teams" trend="up" />
        <StatCard title="Sprint Progress" value={`${stats.overallTaskProgress || 0}%`} icon={Activity} color="success" change={`${stats.completedTasks || 0} / ${stats.totalTasks || 0} tasks done`} trend="up" />
        <StatCard title="Ideas" value={stats.totalIdeas || 0} icon={Brain} color="indigo" change="Registered ideas" trend="up" />
        <StatCard title="Proposals" value={stats.submittedProposals || 0} icon={Brain} color="violet" change="Submitted" trend="up" />
        <StatCard title="Evaluations" value={stats.totalEvaluations || 0} icon={TrendingUp} color="warning" change="Completed" trend="up" />
        <StatCard title="Sessions" value={stats.totalMentoringSessions || 0} icon={TrendingUp} color="orange" change="Mentoring sessions" trend="up" />
      </motion.div>

      {/* Charts Row */}
      <motion.div {...fade(2)} className="grid grid-cols-1 lg:grid-cols-3 gap-4 sm:gap-6">
        {/* Bar chart */}
        <div className="lg:col-span-2 bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
          <h3 className="text-lg font-semibold text-slate-900 mb-5 flex items-center gap-2"><BarChart3 className="w-5 h-5 text-primary" />Top Team Rankings</h3>
          {scoreBarData.length === 0 ? (
            <EmptyState icon={Trophy} title="No data yet" description="Complete some evaluations first" size="sm" />
          ) : (
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={scoreBarData} barSize={32}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                <YAxis domain={[0, 10]} tick={{ fontSize: 11 }} />
                <Tooltip formatter={(v) => [typeof v === 'number' ? v.toFixed(2) : v, 'Score']} />
                <Bar dataKey="score" radius={[6, 6, 0, 0]} fill="#034EA2" />
              </BarChart>
            </ResponsiveContainer>
          )}
        </div>

        {/* Pie charts */}
        <div className="space-y-5">
          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-5">
            <h3 className="text-base font-semibold text-slate-900 mb-3">Users by Role</h3>
            {roleChartData.length > 0 ? (
              <div className="flex items-center gap-4">
                <PieChart width={100} height={100}>
                  <Pie data={roleChartData} cx={45} cy={45} innerRadius={25} outerRadius={45} dataKey="value">
                    {roleChartData.map((_, i) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}
                  </Pie>
                </PieChart>
                <div className="space-y-1.5">
                  {roleChartData.map((r, i) => (
                    <div key={r.name} className="flex items-center gap-2 text-sm">
                      <div className="w-2.5 h-2.5 rounded-full" style={{ background: COLORS[i % COLORS.length] }} />
                      <span className="text-slate-600">{r.name}</span>
                      <span className="font-bold text-slate-900">{r.value}</span>
                    </div>
                  ))}
                </div>
              </div>
            ) : <p className="text-slate-400 text-sm">No data</p>}
          </div>

          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-5">
            <h3 className="text-base font-semibold text-slate-900 mb-3">Ideas by Status</h3>
            {statusChartData.length > 0 ? (
              <div className="space-y-2">
                {statusChartData.map((s, i) => (
                  <div key={s.name} className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <div className="w-2.5 h-2.5 rounded-full" style={{ background: COLORS[i % COLORS.length] }} />
                      <span className="text-sm text-slate-600">{s.name}</span>
                    </div>
                    <span className="font-bold text-slate-900 text-sm">{s.value}</span>
                  </div>
                ))}
              </div>
            ) : <p className="text-slate-400 text-sm">No data</p>}
          </div>
        </div>
      </motion.div>

      {/* Top Teams Table */}
      <motion.div {...fade(3)} className="bg-white rounded-2xl border border-slate-200/60 shadow-sm overflow-hidden">
        <div className="p-6 flex items-center justify-between border-b border-slate-100">
          <h3 className="text-lg font-semibold text-slate-900 flex items-center gap-2"><Trophy className="w-5 h-5 text-amber-500" />Top Teams Leaderboard</h3>
          <Button variant="ghost-primary" size="xs" onClick={() => navigate('/rankings')}>View All →</Button>
        </div>
        {topTeams.length === 0 ? (
          <div className="p-8"><EmptyState icon={Trophy} title="No evaluations yet" description="Evaluations will appear here" size="sm" /></div>
        ) : (
          <table className="w-full">
            <thead>
              <tr className="bg-slate-50 text-xs text-slate-400 uppercase">
                <th className="px-6 py-3 text-left">Rank</th>
                <th className="px-6 py-3 text-left">Team</th>
                <th className="px-6 py-3 text-left">Class</th>
                <th className="px-6 py-3 text-left">Startup</th>
                <th className="px-6 py-3 text-right">Score</th>
              </tr>
            </thead>
            <tbody>
              {topTeams.slice(0, 10).map((t, i) => (
                <tr key={i} className="border-t border-slate-50 hover:bg-primary-50/20 transition-colors">
                  <td className="px-6 py-3.5">
                    <span className={`inline-flex w-7 h-7 items-center justify-center rounded-lg text-sm font-bold ${i === 0 ? 'bg-amber-100 text-amber-600' : i === 1 ? 'bg-slate-100 text-slate-600' : i === 2 ? 'bg-orange-100 text-orange-600' : 'text-slate-500'}`}>
                      {i + 1}
                    </span>
                  </td>
                  <td className="px-6 py-3.5 font-semibold text-slate-900">{t.team?.name || '—'}</td>
                  <td className="px-6 py-3.5 text-slate-500 text-sm">{t.team?.classId?.code || t.team?.classId?.classCode || t.team?.classId?.name || '—'}</td>
                  <td className="px-6 py-3.5 text-slate-700 text-sm">{t.startupName || '—'}</td>
                  <td className="px-6 py-3.5 text-right">
                    <span className={`inline-flex px-2.5 py-1 rounded-lg text-sm font-bold ${t.avgScore >= 8 ? 'bg-green-100 text-green-700' : t.avgScore >= 6 ? 'bg-amber-100 text-amber-700' : 'bg-slate-100 text-slate-600'}`}>
                      {t.avgScore?.toFixed(2) || '—'}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </motion.div>
      {/* Auth Analytics Section */}
      {trackingData && (
        <motion.div {...fade(4)} className="space-y-4">
          {/* Section Header */}
          <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
            <div className="flex items-center gap-2">
              <div className="w-8 h-8 rounded-lg bg-indigo-100 flex items-center justify-center">
                <Zap className="w-4 h-4 text-indigo-600" />
              </div>
              <div>
                <h2 className="text-lg font-bold text-slate-900">Auth Analytics</h2>
                <p className="text-xs text-slate-500">Real-time authentication tracking</p>
              </div>
            </div>
            <div className="flex gap-2">
              <button
                onClick={() => setTrackingDays(7)}
                className={`px-3 py-1.5 text-xs font-medium rounded-lg transition-colors ${
                  trackingDays === 7
                    ? 'bg-indigo-600 text-white'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                }`}
              >
                7 Days
              </button>
              <button
                onClick={() => setTrackingDays(30)}
                className={`px-3 py-1.5 text-xs font-medium rounded-lg transition-colors ${
                  trackingDays === 30
                    ? 'bg-indigo-600 text-white'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                }`}
              >
                30 Days
              </button>
            </div>
          </div>

          {/* Tracking Stat Cards */}
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
            <div className="bg-gradient-to-br from-blue-50 to-blue-100/50 rounded-2xl border border-blue-200/60 p-4">
              <div className="flex items-center gap-2 mb-2">
                <Users className="w-4 h-4 text-blue-600" />
                <span className="text-xs text-blue-600 font-medium">Total Users</span>
              </div>
              <p className="text-2xl font-bold text-blue-700">{(trackingData.totalUsers || 0).toLocaleString()}</p>
            </div>
            <div className="bg-gradient-to-br from-emerald-50 to-emerald-100/50 rounded-2xl border border-emerald-200/60 p-4">
              <div className="flex items-center gap-2 mb-2">
                <UserPlus className="w-4 h-4 text-emerald-600" />
                <span className="text-xs text-emerald-600 font-medium">Total Registers</span>
              </div>
              <p className="text-2xl font-bold text-emerald-700">{(trackingData.totalRegisters || 0).toLocaleString()}</p>
            </div>
            <div className="bg-gradient-to-br from-violet-50 to-violet-100/50 rounded-2xl border border-violet-200/60 p-4">
              <div className="flex items-center gap-2 mb-2">
                <LogIn className="w-4 h-4 text-violet-600" />
                <span className="text-xs text-violet-600 font-medium">Total Logins</span>
              </div>
              <p className="text-2xl font-bold text-violet-700">{(trackingData.totalLogins || 0).toLocaleString()}</p>
            </div>
            <div className="bg-gradient-to-br from-red-50 to-red-100/50 rounded-2xl border border-red-200/60 p-4">
              <div className="flex items-center gap-2 mb-2">
                <ShieldAlert className="w-4 h-4 text-red-500" />
                <span className="text-xs text-red-500 font-medium">Failed Logins</span>
              </div>
              <p className="text-2xl font-bold text-red-600">{(trackingData.failedLogins || 0).toLocaleString()}</p>
            </div>
            <div className="bg-gradient-to-br from-amber-50 to-amber-100/50 rounded-2xl border border-amber-200/60 p-4">
              <div className="flex items-center gap-2 mb-2">
                <AlertTriangle className="w-4 h-4 text-amber-500" />
                <span className="text-xs text-amber-500 font-medium">Today Registers</span>
              </div>
              <p className="text-2xl font-bold text-amber-600">{trackingData.todayRegisters || 0}</p>
              <p className="text-xs text-amber-500 mt-1">{trackingData.todayLogins || 0} logins today</p>
            </div>
            <div className="bg-gradient-to-br from-teal-50 to-teal-100/50 rounded-2xl border border-teal-200/60 p-4">
              <div className="flex items-center gap-2 mb-2">
                <UserCheck className="w-4 h-4 text-teal-600" />
                <span className="text-xs text-teal-600 font-medium">Active Today</span>
              </div>
              <p className="text-2xl font-bold text-teal-700">{trackingData.activeUsersToday || 0}</p>
              <p className="text-xs text-teal-500 mt-1">unique users</p>
            </div>
          </div>

          {/* Login & Register Line Chart */}
          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
            <h3 className="text-base font-semibold text-slate-900 mb-4 flex items-center gap-2">
              <TrendingUp className="w-4 h-4 text-indigo-600" />
              Login &amp; Register Trend
              <span className="ml-auto text-xs text-slate-400 font-normal">Last {trackingDays} days</span>
            </h3>
            {(trackingData.loginRate?.length > 0 || trackingData.registerRate?.length > 0) ? (
              <ResponsiveContainer width="100%" height={200}>
                <LineChart
                  data={trackingData.loginRate?.map((item, i) => ({
                    date: item.date.slice(5), // MM-DD
                    Logins: item.count,
                    Registers: trackingData.registerRate?.[i]?.count || 0,
                  }))}
                >
                  <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                  <XAxis dataKey="date" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
                  <Tooltip />
                  <Legend />
                  <Line type="monotone" dataKey="Logins" stroke="#6366f1" strokeWidth={2} dot={{ r: 3 }} />
                  <Line type="monotone" dataKey="Registers" stroke="#10b981" strokeWidth={2} dot={{ r: 3 }} />
                </LineChart>
              </ResponsiveContainer>
            ) : (
              <p className="text-slate-400 text-sm text-center py-8">No tracking data yet</p>
            )}
          </div>
        </motion.div>
      )}

      {/* Online Users Section */}
      {onlineData && (
        <motion.div {...fade(5)} className="bg-white rounded-2xl border border-slate-200/60 shadow-sm overflow-hidden">
          {/* Header */}
          <div className="p-6 border-b border-slate-100 flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-emerald-100 flex items-center justify-center">
                <Wifi className="w-5 h-5 text-emerald-600" />
              </div>
              <div>
                <h3 className="text-lg font-bold text-slate-900">Online Users</h3>
                <p className="text-xs text-slate-500">
                  {onlineData.onlineCount} online of {onlineData.totalUsers} total · auto-refreshes every 30s
                </p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <span className="relative flex h-3 w-3">
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
                <span className="relative inline-flex rounded-full h-3 w-3 bg-emerald-500"></span>
              </span>
              <span className="text-sm font-bold text-emerald-600">{onlineData.onlineCount} Online</span>
            </div>
          </div>

          <div className="p-6">
            {/* Online Users List */}
            {onlineData.onlineUsers?.length > 0 && (
              <div className="mb-6">
                <h4 className="text-xs font-bold text-emerald-600 uppercase tracking-wider mb-3 flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
                  Currently Online ({onlineData.onlineUsers.length})
                </h4>
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2">
                  {onlineData.onlineUsers.map(u => (
                    <div key={u._id} className="flex items-center gap-3 p-3 rounded-xl bg-emerald-50/50 border border-emerald-100/60 hover:bg-emerald-50 transition-colors">
                      <div className="relative shrink-0">
                        {u.avatar ? (
                          <img src={u.avatar} alt={u.name} className="w-9 h-9 rounded-full border border-emerald-200 object-cover" />
                        ) : (
                          <div className="w-9 h-9 rounded-full bg-gradient-to-br from-emerald-400 to-emerald-600 flex items-center justify-center text-white text-sm font-bold">
                            {(u.name || '?')[0].toUpperCase()}
                          </div>
                        )}
                        <span className="absolute -bottom-0.5 -right-0.5 w-3 h-3 bg-emerald-500 border-2 border-white rounded-full"></span>
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-semibold text-slate-800 truncate">{u.name}</p>
                        <div className="flex items-center gap-1.5">
                          <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded-md border uppercase tracking-wider ${
                            u.role === 'ADMIN' ? 'bg-red-50 text-red-600 border-red-200' :
                            u.role === 'LECTURER' ? 'bg-purple-50 text-purple-600 border-purple-200' :
                            u.role === 'MENTOR' ? 'bg-amber-50 text-amber-600 border-amber-200' :
                            'bg-blue-50 text-blue-600 border-blue-200'
                          }`}>{u.role}</span>
                          <span className="text-[10px] text-slate-400 truncate">{u.email}</span>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Recently Active Users */}
            {onlineData.recentlyActive?.length > 0 && (
              <div>
                <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-3 flex items-center gap-1.5">
                  <WifiOff className="w-3 h-3" />
                  Recently Active ({onlineData.recentlyActive.length})
                </h4>
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2">
                  {onlineData.recentlyActive.slice(0, 12).map(u => (
                    <div key={u._id} className="flex items-center gap-3 p-3 rounded-xl bg-slate-50/50 border border-slate-100/60 hover:bg-slate-50 transition-colors">
                      <div className="relative shrink-0">
                        {u.avatar ? (
                          <img src={u.avatar} alt={u.name} className="w-9 h-9 rounded-full border border-slate-200 object-cover opacity-70" />
                        ) : (
                          <div className="w-9 h-9 rounded-full bg-gradient-to-br from-slate-300 to-slate-400 flex items-center justify-center text-white text-sm font-bold">
                            {(u.name || '?')[0].toUpperCase()}
                          </div>
                        )}
                        <span className="absolute -bottom-0.5 -right-0.5 w-3 h-3 bg-slate-300 border-2 border-white rounded-full"></span>
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-medium text-slate-600 truncate">{u.name}</p>
                        <div className="flex items-center gap-1">
                          <Clock className="w-3 h-3 text-slate-400" />
                          <span className="text-[10px] text-slate-400">
                            {u.lastSeen ? (() => {
                              const diff = Date.now() - new Date(u.lastSeen).getTime();
                              const mins = Math.floor(diff / 60000);
                              if (mins < 1) return 'just now';
                              if (mins < 60) return `${mins}m ago`;
                              const hrs = Math.floor(mins / 60);
                              if (hrs < 24) return `${hrs}h ago`;
                              return `${Math.floor(hrs / 24)}d ago`;
                            })() : 'unknown'}
                          </span>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Empty State */}
            {(!onlineData.onlineUsers?.length && !onlineData.recentlyActive?.length) && (
              <div className="text-center py-8">
                <WifiOff className="w-10 h-10 text-slate-300 mx-auto mb-2" />
                <p className="text-sm text-slate-400">No user activity detected yet</p>
              </div>
            )}
          </div>
        </motion.div>
      )}
    </div>
  );
};

export default AdminDashboard;
