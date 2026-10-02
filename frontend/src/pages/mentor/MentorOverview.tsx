import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { mentorSupportApi } from '../../api/mentorSupportApi';
import { teamApi } from '../../api/teamApi';
import type { MentorProfile, MentoringSession } from '../../types/mentoring';
import { unwrapApiData } from '../../utils/classMappers';
import { parseApiError } from '../../utils/apiError';

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
  if (loading) return <main className="p-8">Loading mentor overview…</main>;
  if (error) return <main className="p-8 text-red-700" role="alert">{error}<button type="button" onClick={() => void load(true)} className="ml-3 underline">Retry</button></main>;
  const upcoming = sessions.filter(item => item.status === 'Scheduled' && new Date(item.startAt) >= new Date());
  return <main className="mx-auto max-w-5xl space-y-6 p-4 sm:p-8">
    <header className="flex flex-wrap items-end justify-between gap-3"><div><h1 className="text-2xl font-bold text-slate-900">Mentor overview</h1>
      <p className="text-sm text-slate-600">{profile?.fullName} · {profile?.mentorType === 'IT' ? 'IT mentor' : profile?.mentorType === 'Business' ? 'Business mentor' : 'Complete your mentor type'}</p></div>
      <div className="flex gap-2"><Link to="/mentor/profile" className="rounded-lg border px-4 py-2 text-sm">Edit profile</Link>
        <Link to="/sessions" className="rounded-lg bg-indigo-600 px-4 py-2 text-sm text-white">Mentoring sessions</Link></div></header>
    <div className="grid gap-4 sm:grid-cols-3">
      <div className="rounded-xl border bg-white p-5"><p className="text-sm text-slate-600">Assigned teams</p><p className="text-3xl font-bold">{teams.length}</p></div>
      <div className="rounded-xl border bg-white p-5"><p className="text-sm text-slate-600">Upcoming sessions</p><p className="text-3xl font-bold">{upcoming.length}</p></div>
      <div className="rounded-xl border bg-white p-5"><p className="text-sm text-slate-600">Profile expertise</p><p className="text-3xl font-bold">{profile?.expertise.length ?? 0}</p></div>
    </div>
    <section className="rounded-xl border bg-white p-5"><h2 className="text-lg font-semibold">My teams</h2>
      {teams.length === 0 ? <p className="mt-3 text-sm text-slate-600">No teams are assigned yet.</p> :
        <ul className="mt-3 divide-y">{teams.map(team => <li key={team.id} className="flex flex-wrap justify-between gap-2 py-3 text-sm">
          <div><strong>{team.teamName}</strong>{team.projectName && <span className="ml-2 text-slate-600">{team.projectName}</span>}</div>
          <Link to={`/workspace/teams/${team.id}`} className="text-indigo-700 underline">Open workspace</Link></li>)}</ul>}
    </section>
    <section className="rounded-xl border bg-white p-5"><h2 className="text-lg font-semibold">Next sessions</h2>
      {upcoming.length === 0 ? <p className="mt-3 text-sm text-slate-600">No upcoming sessions.</p> :
        <ul className="mt-3 divide-y">{upcoming.slice(0, 5).map(item => <li key={item.id} className="py-3 text-sm">
          <strong>{item.title}</strong><span className="ml-2 text-slate-600">{new Date(item.startAt).toLocaleString()}</span></li>)}</ul>}
    </section>
  </main>;
}
