import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import io from 'socket.io-client';
import {
  Activity, AlertTriangle, BarChart3, Brain, Clock, GraduationCap, LogIn,
  RefreshCw, Rocket, ShieldAlert, Sparkles, TrendingUp, Trophy, UserCheck,
  UserPlus, Users, Wifi, WifiOff,
} from 'lucide-react';
import {
  Bar, BarChart, CartesianGrid, Cell, Legend, Line, LineChart, Pie, PieChart,
  ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import { dashboardApi } from '../../api/dashboardApi';
import { trackingApi } from '../../api/trackingApi';
import Button from '../../components/ui/Button';
import EmptyState from '../../components/ui/EmptyState';
import ErrorState from '../../components/ui/ErrorState';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import StatCard from '../../components/ui/StatCard';
import { runtimeConfig } from '../../config/runtimeConfig';

type AnyRecord = Record<string, any>;
const roleColors: Record<string, string> = { ADMIN: '#dc2626', LECTURER: '#7c3aed', MENTOR: '#d97706', STUDENT: '#2563eb' };
const statusColors = ['#1e5e9f', '#ea6a12', '#51b848', '#8b5cf6', '#dc2626', '#64748b'];
const responseData = (response: any) => response?.data?.data ?? response?.data ?? response;
const number = (value: unknown) => Number(value ?? 0);
const formatNumber = (value: unknown) => number(value).toLocaleString();
const initials = (name: string) => name.trim().charAt(0).toUpperCase() || '?';
const ago = (value?: string) => {
  if (!value) return 'unknown';
  const minutes = Math.max(0, Math.floor((Date.now() - new Date(value).getTime()) / 60000));
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  if (minutes < 1440) return `${Math.floor(minutes / 60)}h ago`;
  return `${Math.floor(minutes / 1440)}d ago`;
};

function SectionUnavailable({ title }: { title: string }) {
  return <div className="rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-500">{title}: Data unavailable</div>;
}

function ActivityUser({ user, online }: { user: AnyRecord; online: boolean }) {
  const role = String(user.role ?? 'STUDENT').toUpperCase();
  return <div className={`flex min-w-0 items-center gap-3 rounded-xl border p-3 ${online ? 'border-emerald-100 bg-emerald-50/50' : 'border-slate-100 bg-slate-50'}`}>
    <div className="relative shrink-0">
      {user.avatar ? <img src={user.avatar} alt="" className={`h-9 w-9 rounded-full object-cover ${online ? '' : 'opacity-70'}`} /> : <div className={`flex h-9 w-9 items-center justify-center rounded-full text-sm font-bold text-white ${online ? 'bg-emerald-600' : 'bg-slate-400'}`}>{initials(user.name ?? '')}</div>}
      <span className={`absolute -bottom-0.5 -right-0.5 h-3 w-3 rounded-full border-2 border-white ${online ? 'bg-emerald-500' : 'bg-slate-300'}`} />
    </div>
    <div className="min-w-0 flex-1"><p className="truncate text-sm font-semibold text-slate-800">{user.name ?? 'Unknown user'}</p><p className="truncate text-xs text-slate-500">{online ? user.email : ago(user.lastSeen)}</p></div>
    {online && <span className="rounded-md bg-white px-1.5 py-0.5 text-[10px] font-bold text-slate-600">{role}</span>}
  </div>;
}

export default function AdminDashboard() {
  const navigate = useNavigate();
  const [dashboard, setDashboard] = useState<AnyRecord | null>(null);
  const [dashboardLoading, setDashboardLoading] = useState(true);
  const [dashboardError, setDashboardError] = useState(false);
  const [days, setDays] = useState<7 | 30>(7);
  const [tracking, setTracking] = useState<AnyRecord | null>(null);
  const [trackingLoading, setTrackingLoading] = useState(true);
  const [trackingError, setTrackingError] = useState(false);
  const [online, setOnline] = useState<AnyRecord | null>(null);
  const [onlineLoading, setOnlineLoading] = useState(true);
  const [onlineError, setOnlineError] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [socketConnected, setSocketConnected] = useState(false);
  const onlineLoaderRef = useRef<() => Promise<void>>(async () => {});

  const loadDashboard = useCallback(async () => {
    setDashboardLoading(true); setDashboardError(false);
    try { setDashboard(responseData(await dashboardApi.getAdmin())); } catch { setDashboardError(true); } finally { setDashboardLoading(false); }
  }, []);
  const loadTracking = useCallback(async () => {
    setTrackingLoading(true); setTrackingError(false);
    try { setTracking(responseData(await trackingApi.getAuthStats(days))); } catch { setTrackingError(true); } finally { setTrackingLoading(false); }
  }, [days]);
  const loadOnline = useCallback(async () => {
    setOnlineLoading(true); setOnlineError(false);
    try { setOnline(responseData(await trackingApi.getOnlineUsers())); } catch { setOnlineError(true); } finally { setOnlineLoading(false); }
  }, []);
  onlineLoaderRef.current = loadOnline;

  useEffect(() => { void loadDashboard(); }, [loadDashboard]);
  useEffect(() => { void loadTracking(); }, [loadTracking]);
  useEffect(() => { void loadOnline(); const timer = window.setInterval(() => void loadOnline(), 30000); return () => window.clearInterval(timer); }, [loadOnline]);
  useEffect(() => {
    if (!runtimeConfig.realtime.enabled) {
      setSocketConnected(false);
      return;
    }

    const socket = io(runtimeConfig.realtime.origin, { withCredentials: true, transports: ['websocket', 'polling'], reconnection: true });
    socket.on('connect', () => setSocketConnected(true));
    socket.on('disconnect', () => setSocketConnected(false));
    const refreshPresence = () => void onlineLoaderRef.current();
    socket.on('presence', refreshPresence); socket.on('user_online', refreshPresence); socket.on('user_offline', refreshPresence);
    return () => { socket.disconnect(); };
  }, []);

  const refreshAll = async () => { setRefreshing(true); await Promise.all([loadDashboard(), loadTracking(), loadOnline()]); setRefreshing(false); };
  const stats = dashboard?.stats ?? {};
  const topTeams = Array.isArray(dashboard?.topTeams) ? dashboard.topTeams : [];
  const roleCounts = useMemo(() => {
    const source = new Map((dashboard?.usersByRole ?? []).map((item: AnyRecord) => [String(item.role).toUpperCase(), number(item.count)]));
    return ['ADMIN', 'LECTURER', 'MENTOR', 'STUDENT'].map(role => ({ name: role, value: source.get(role) ?? 0 }));
  }, [dashboard]);
  const trendData = useMemo(() => {
    const byDate = new Map<string, AnyRecord>();
    for (const item of tracking?.loginRate ?? []) byDate.set(item.date, { ...(byDate.get(item.date) ?? {}), date: item.date, Logins: number(item.count) });
    for (const item of tracking?.registerRate ?? []) byDate.set(item.date, { ...(byDate.get(item.date) ?? {}), date: item.date, Registers: number(item.count) });
    return [...byDate.values()].sort((a, b) => a.date.localeCompare(b.date)).map(item => ({ ...item, date: String(item.date).slice(5), Logins: item.Logins ?? 0, Registers: item.Registers ?? 0 }));
  }, [tracking]);
  const statCards = [
    ['Total Users', stats.totalUsers, Users, 'primary', 'Platform users'], ['Classes', stats.totalClasses, GraduationCap, 'cyan', 'All classes'],
    ['Teams', stats.totalTeams, Rocket, 'secondary', 'Startup teams'], ['Sprint Progress', `${number(stats.overallTaskProgress)}%`, Activity, 'success', `${number(stats.completedTasks)} / ${number(stats.totalTasks)} tasks done`],
    ['Ideas', stats.totalIdeas, Brain, 'indigo', 'Registered ideas'], ['Proposals', stats.submittedProposals, Sparkles, 'violet', 'Submitted'],
    ['Evaluations', stats.totalEvaluations, TrendingUp, 'warning', 'Completed'], ['Sessions', stats.totalMentoringSessions, Clock, 'orange', 'Mentoring sessions'],
  ] as const;

  if (dashboardLoading && !dashboard) return <div className="space-y-6"><LoadingSkeleton lines={2} /><div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-4">{Array.from({ length: 8 }).map((_, index) => <LoadingSkeleton key={index} variant="card" />)}</div></div>;
  if (dashboardError && !dashboard) return <ErrorState title="Dashboard unavailable" message="The platform overview could not be loaded." onRetry={() => void loadDashboard()} />;

  return <div className="space-y-6 pb-6">
    <motion.header initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} className="flex flex-col justify-between gap-3 sm:flex-row sm:items-end">
      <div><h1 className="text-2xl font-bold text-slate-900 sm:text-3xl">Admin Overview</h1><p className="mt-1 text-slate-500">Platform analytics and system health</p></div>
      <div className="flex gap-2"><Button aria-label="Refresh dashboard data" variant="outline" icon={RefreshCw} isLoading={refreshing} onClick={() => void refreshAll()}>Refresh</Button><Button variant="outline" icon={BarChart3} onClick={() => navigate('/rankings')}>View Rankings</Button></div>
    </motion.header>

    <section className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-4">{statCards.map(([title, value, icon, color, description], index) => <StatCard key={title} title={title} value={typeof value === 'string' ? value : formatNumber(value)} icon={icon} color={color} change={description} delay={index * 0.04} />)}</section>

    <section className="grid gap-5 lg:grid-cols-3">
      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm lg:col-span-2"><h2 className="mb-5 flex items-center gap-2 text-lg font-bold text-slate-900"><BarChart3 className="h-5 w-5 text-primary" />Top Team Rankings</h2>{topTeams.length === 0 ? <EmptyState icon={Trophy} title="No data yet" description="Complete some evaluations first" size="sm" /> : <ResponsiveContainer width="100%" height={250}><BarChart data={topTeams.slice(0, 8).map((team: AnyRecord, index: number) => ({ name: team.team?.name ?? `Team ${index + 1}`, score: number(team.avgScore) }))}><CartesianGrid stroke="#e2e8f0" strokeDasharray="3 3" /><XAxis dataKey="name" tick={{ fontSize: 11 }} /><YAxis domain={[0, 10]} /><Tooltip formatter={(value: number) => [value.toFixed(2), 'Score']} /><Bar dataKey="score" fill="#1e5e9f" radius={[5, 5, 0, 0]} /></BarChart></ResponsiveContainer>}</div>
      <div className="space-y-5"><div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><h2 className="mb-3 text-base font-bold text-slate-900">Users by Role</h2><div className="flex items-center gap-4"><PieChart width={108} height={108}><Pie data={roleCounts} dataKey="value" cx={50} cy={50} innerRadius={28} outerRadius={46}>{roleCounts.map(item => <Cell key={item.name} fill={roleColors[item.name]} />)}</Pie></PieChart><div className="space-y-1.5">{roleCounts.map(item => <div key={item.name} className="flex items-center gap-2 text-sm"><span className="h-2.5 w-2.5 rounded-full" style={{ background: roleColors[item.name] }} /><span className="text-slate-600">{item.name}</span><b>{formatNumber(item.value)}</b></div>)}</div></div></div>
      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><h2 className="mb-3 text-base font-bold text-slate-900">Ideas by Status</h2>{(dashboard?.ideasByStatus ?? []).length ? <div className="space-y-2">{dashboard.ideasByStatus.map((item: AnyRecord, index: number) => <div key={item.status} className="flex items-center justify-between text-sm"><span className="flex items-center gap-2 text-slate-600"><span className="h-2.5 w-2.5 rounded-full" style={{ background: statusColors[index % statusColors.length] }} />{item.status}</span><b>{formatNumber(item.count)}</b></div>)}</div> : <p className="text-sm text-slate-400">No data yet</p>}</div></div>
    </section>

    <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm"><div className="flex items-center justify-between border-b border-slate-100 p-5"><h2 className="flex items-center gap-2 text-lg font-bold text-slate-900"><Trophy className="h-5 w-5 text-amber-500" />Top Teams Leaderboard</h2><Button variant="ghost-primary" size="sm" onClick={() => navigate('/rankings')}>View All -&gt;</Button></div>{topTeams.length === 0 ? <div className="p-8"><EmptyState icon={Trophy} title="No evaluations yet" description="Evaluations will appear here" size="sm" /></div> : <div className="overflow-x-auto"><table className="min-w-[680px] w-full text-sm"><thead className="bg-slate-50 text-left text-xs uppercase text-slate-400"><tr>{['Rank', 'Team', 'Class', 'Startup', 'Score'].map(header => <th key={header} className="px-5 py-3">{header}</th>)}</tr></thead><tbody>{topTeams.slice(0, 10).map((team: AnyRecord, index: number) => { const score = number(team.avgScore); return <tr key={`${team.team?.name}-${index}`} className="border-t border-slate-100"><td className="px-5 py-3"><span className={`inline-flex h-7 w-7 items-center justify-center rounded-lg font-bold ${index === 0 ? 'bg-amber-100 text-amber-700' : index === 1 ? 'bg-slate-200 text-slate-700' : index === 2 ? 'bg-orange-100 text-orange-700' : 'text-slate-500'}`}>{index + 1}</span></td><td className="px-5 py-3 font-semibold text-slate-900">{team.team?.name ?? '-'}</td><td className="px-5 py-3 text-slate-600">{team.team?.classId?.classCode ?? '-'}</td><td className="px-5 py-3 text-slate-600">{team.startupName ?? '-'}</td><td className="px-5 py-3"><span className={`rounded-lg px-2 py-1 font-bold ${score >= 8 ? 'bg-emerald-100 text-emerald-700' : score >= 6 ? 'bg-amber-100 text-amber-700' : 'bg-slate-100 text-slate-600'}`}>{score.toFixed(2)}</span></td></tr>; })}</tbody></table></div>}</section>

    <section className="space-y-4"><div className="flex flex-wrap items-center justify-between gap-3"><div><h2 className="text-lg font-bold text-slate-900">Auth Analytics</h2><p className="text-sm text-slate-500">Real-time authentication tracking</p></div><div className="flex rounded-xl bg-slate-100 p-1">{([7, 30] as const).map(value => <button key={value} aria-label={`Show last ${value} days`} onClick={() => setDays(value)} className={`rounded-lg px-3 py-1.5 text-sm font-medium ${days === value ? 'bg-white text-primary shadow-sm' : 'text-slate-600'}`}>{value} Days</button>)}</div></div>{trackingLoading && !tracking ? <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-6">{Array.from({ length: 6 }).map((_, index) => <LoadingSkeleton key={index} variant="card" />)}</div> : trackingError ? <SectionUnavailable title="Auth Analytics" /> : <><div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-6">{[['Total Users', tracking?.totalUsers, Users, 'primary', ''], ['Total Registers', tracking?.totalRegisters, UserPlus, 'success', ''], ['Total Logins', tracking?.totalLogins, LogIn, 'violet', ''], ['Failed Logins', tracking?.failedLogins, ShieldAlert, 'danger', ''], ['Today Registers', tracking?.todayRegisters, AlertTriangle, 'warning', `${formatNumber(tracking?.todayLogins)} logins today`], ['Active Today', tracking?.activeUsersToday, UserCheck, 'cyan', '']].map(([title, value, icon, color, hint], index) => <StatCard key={String(title)} title={title} value={formatNumber(value)} icon={icon as any} color={color as any} change={hint as string} delay={index * 0.04} />)}</div><div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><h3 className="mb-4 flex items-center gap-2 font-bold text-slate-900"><TrendingUp className="h-4 w-4 text-primary" />Login &amp; Register Trend <span className="ml-auto text-xs font-normal text-slate-400">Last {days} days</span></h3>{trendData.length ? <ResponsiveContainer width="100%" height={220}><LineChart data={trendData}><CartesianGrid stroke="#e2e8f0" strokeDasharray="3 3" /><XAxis dataKey="date" /><YAxis allowDecimals={false} /><Tooltip /><Legend /><Line type="monotone" dataKey="Logins" stroke="#2563eb" strokeWidth={2} /><Line type="monotone" dataKey="Registers" stroke="#10b981" strokeWidth={2} /></LineChart></ResponsiveContainer> : <p className="py-8 text-center text-sm text-slate-400">No tracking data yet</p>}</div></>}</section>

    <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm"><div className="flex items-center justify-between border-b border-slate-100 p-5"><div><h2 className="flex items-center gap-2 text-lg font-bold text-slate-900"><Wifi className="h-5 w-5 text-emerald-600" />Online Users</h2><p className="text-sm text-slate-500">{formatNumber(online?.onlineCount)} online of {formatNumber(online?.totalUsers)} total · {socketConnected ? 'live updates' : 'refreshes every 30s'}</p></div><Button aria-label="Refresh online users" variant="outline" size="sm" icon={RefreshCw} isLoading={onlineLoading} onClick={() => void loadOnline()}>Refresh</Button></div>{onlineLoading && !online ? <div className="p-5"><LoadingSkeleton variant="table" lines={4} /></div> : onlineError ? <div className="p-5"><SectionUnavailable title="Online Users" /></div> : <div className="space-y-5 p-5">{online?.onlineUsers?.length ? <div><h3 className="mb-3 text-xs font-bold uppercase text-emerald-600">Currently Online</h3><div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">{online.onlineUsers.map((user: AnyRecord) => <ActivityUser key={user.id ?? user._id} user={user} online />)}</div></div> : null}{online?.recentlyActive?.length ? <div><h3 className="mb-3 flex items-center gap-1 text-xs font-bold uppercase text-slate-500"><WifiOff className="h-3 w-3" />Recently Active</h3><div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">{online.recentlyActive.slice(0, 12).map((user: AnyRecord) => <ActivityUser key={user.id ?? user._id} user={user} online={false} />)}</div></div> : null}{!online?.onlineUsers?.length && !online?.recentlyActive?.length ? <EmptyState icon={WifiOff} title="No user activity detected yet" description="" size="sm" /> : null}</div>}</section>
  </div>;
}
