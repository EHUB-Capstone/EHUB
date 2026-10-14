import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { mentorSupportApi } from '../../api/mentorSupportApi';
import { teamApi } from '../../api/teamApi';
import type { MentorProfile, MentoringSession } from '../../types/mentoring';
import { unwrapApiData } from '../../utils/classMappers';
import { parseApiError } from '../../utils/apiError';
import Card from '../../components/ui/Card';

interface TeamOverview { id: string; teamName: string; projectName?: string }

export default function MentorOverview() {
  const [profile, setProfile] = useState<MentorProfile | null>(null);
  const [teams, setTeams] = useState<TeamOverview[]>([]);
  const [sessions, setSessions] = useState<MentoringSession[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const load = async (retry = false) => {
    if (retry) { setLoading(true); setError(''); }
    try {
      const [mentor, rawTeams, meetings] = await Promise.all([mentorSupportApi.getProfile(), teamApi.getAll({}), mentorSupportApi.getSessions()]);
      const list = unwrapApiData<unknown>(rawTeams);
      setProfile(mentor);
      setTeams(Array.isArray(list) ? list.map((item: Record<string, unknown>) => ({ id: String(item.id),
        teamName: String(item.teamName ?? 'Team'), projectName: typeof item.projectName === 'string' ? item.projectName : undefined })) : []);
      setSessions(meetings);
    } catch (cause) { setError(parseApiError(cause, 'Could not load mentor overview').message); }
    finally { setLoading(false); }
  };
  useEffect(() => {
    let active = true;
    Promise.all([mentorSupportApi.getProfile(), teamApi.getAll({}), mentorSupportApi.getSessions()])
      .then(([mentor, rawTeams, meetings]) => {
        if (!active) return;
        const list = unwrapApiData<unknown>(rawTeams);
        setProfile(mentor);
        setTeams(Array.isArray(list) ? list.map((item: Record<string, unknown>) => ({ id: String(item.id),
          teamName: String(item.teamName ?? 'Team'), projectName: typeof item.projectName === 'string' ? item.projectName : undefined })) : []);
        setSessions(meetings);
      }).catch(cause => { if (active) setError(parseApiError(cause, 'Could not load mentor overview').message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);
  if (loading) return <main className="p-8 text-body text-slate-600">Loading mentor overview…</main>;
  if (error) return <main className="p-8 text-body text-danger-dark" role="alert">{error}<button type="button" onClick={() => void load(true)} className="ml-3 underline">Retry</button></main>;
  const upcoming = sessions.filter(item => item.status === 'Scheduled' && new Date(item.startAt) >= new Date());
  return <main className="mx-auto max-w-5xl space-y-6 p-4 text-body text-slate-700 sm:p-8">
    <header className="flex flex-wrap items-end justify-between gap-3"><div><h1 className="text-display text-secondary">Mentor overview</h1>
      <p className="text-body text-slate-600">{profile?.fullName} · {profile?.mentorType === 'IT' ? 'IT mentor' : profile?.mentorType === 'Business' ? 'Business mentor' : 'Complete your mentor type'}</p></div>
      <div className="flex gap-2"><Link to="/profile" className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-body font-medium text-slate-700 transition-colors hover:border-slate-400 hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 focus-visible:ring-offset-2">Edit profile</Link>
        <Link to="/sessions" className="rounded-lg bg-primary px-4 py-2 text-body font-medium text-white shadow-sm transition-colors hover:bg-primary-600 active:bg-primary-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 focus-visible:ring-offset-2">Mentoring sessions</Link></div></header>
    <div className="grid gap-4 sm:grid-cols-3">
      <Card hover={false} padding="p-5" className="space-y-1"><p className="text-body text-slate-600">Assigned teams</p><p className="text-display text-secondary">{teams.length}</p></Card>
      <Card hover={false} padding="p-5" className="space-y-1"><p className="text-body text-slate-600">Upcoming sessions</p><p className="text-display text-secondary">{upcoming.length}</p></Card>
      <Card hover={false} padding="p-5" className="space-y-1"><p className="text-body text-slate-600">Profile expertise</p><p className="text-display text-secondary">{profile?.expertise.length ?? 0}</p></Card>
    </div>
    <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-card"><h2 className="text-heading text-secondary">My teams</h2>
      {teams.length === 0 ? <p className="mt-3 text-body text-slate-600">No teams are assigned yet.</p> :
        <ul className="mt-3 divide-y divide-slate-200">{teams.map(team => <li key={team.id} className="flex flex-wrap justify-between gap-2 py-3 text-body">
          <div><strong>{team.teamName}</strong>{team.projectName && <span className="ml-2 text-slate-600">{team.projectName}</span>}</div>
          <Link to={`/workspace/teams/${team.id}`} className="text-primary-700 underline">Open workspace</Link></li>)}</ul>}
    </section>
    <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-card"><h2 className="text-heading text-secondary">Next sessions</h2>
      {upcoming.length === 0 ? <p className="mt-3 text-body text-slate-600">No upcoming sessions.</p> :
        <ul className="mt-3 divide-y divide-slate-200">{upcoming.slice(0, 5).map(item => <li key={item.id} className="py-3 text-body">
          <strong>{item.title}</strong><span className="ml-2 text-slate-600">{new Date(item.startAt).toLocaleString()}</span></li>)}</ul>}
    </section>
  </main>;
}
