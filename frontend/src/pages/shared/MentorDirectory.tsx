import { useEffect, useMemo, useState } from 'react';
import { mentorSupportApi } from '../../api/mentorSupportApi';
import type { MentorProfile } from '../../types/mentoring';
import { parseApiError } from '../../utils/apiError';

export default function MentorDirectory() {
  const [profiles, setProfiles] = useState<MentorProfile[]>([]);
  const [query, setQuery] = useState('');
  const [type, setType] = useState('All');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const load = async (retry = false) => {
    if (retry) { setLoading(true); setError(''); }
    try { setProfiles(await mentorSupportApi.getDirectory()); }
    catch (cause) { setError(parseApiError(cause, 'Could not load mentor directory').message); }
    finally { setLoading(false); }
  };
  useEffect(() => {
    let active = true;
    mentorSupportApi.getDirectory().then(items => { if (active) setProfiles(items); })
      .catch(cause => { if (active) setError(parseApiError(cause, 'Could not load mentor directory').message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);
  const filtered = useMemo(() => profiles.filter(profile =>
    (type === 'All' || profile.mentorType === type) &&
    `${profile.fullName} ${profile.organization ?? ''} ${profile.expertise.join(' ')}`.toLowerCase().includes(query.toLowerCase())),
  [profiles, query, type]);

  const download = async (profile: MentorProfile, kind: 'cv' | 'portfolio') => {
    try {
      const blob = await mentorSupportApi.getDocument(profile.id, kind);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url; link.download = kind === 'cv' ? profile.cvFileName ?? 'CV.pdf' : profile.portfolioFileName ?? 'Portfolio.pdf';
      link.click(); URL.revokeObjectURL(url);
    } catch (cause) { setError(parseApiError(cause, 'Could not download document').message); }
  };
  return <main className="mx-auto max-w-6xl space-y-5 p-4 sm:p-8">
    <header><h1 className="text-2xl font-bold text-slate-900">Mentor directory</h1>
      <p className="mt-1 text-sm text-slate-600">Profiles, capacity and mentoring history across semesters.</p></header>
    {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-red-700">{error}<button type="button" onClick={() => void load(true)} className="ml-3 underline">Retry</button></p>}
    <div className="flex flex-wrap gap-3"><input aria-label="Search mentors" value={query} onChange={event => setQuery(event.target.value)} placeholder="Search name or expertise" className="min-w-48 flex-1 rounded-lg border p-2" />
      <select aria-label="Filter mentor type" value={type} onChange={event => setType(event.target.value)} className="rounded-lg border p-2">
        <option value="All">All types</option><option value="Business">Business</option><option value="IT">IT</option><option value="Unspecified">Unspecified</option></select></div>
    {loading ? <p>Loading mentors…</p> : filtered.length === 0 ? <p className="rounded-xl border bg-white p-8 text-center text-slate-600">No mentors match your search.</p> :
      <div className="grid gap-4 md:grid-cols-2">{filtered.map(profile => <article key={profile.id} className="space-y-3 rounded-xl border bg-white p-5">
        <div className="flex flex-wrap items-start justify-between gap-2"><div><h2 className="text-lg font-semibold">{profile.fullName}</h2>
          <p className="text-sm text-slate-600">{profile.organization || 'Independent'} · {profile.mentorType}</p></div>
          <span className="rounded-full bg-slate-100 px-2 py-1 text-xs">{profile.status}</span></div>
        <p className="text-sm text-slate-700">{profile.bio || 'No biography yet.'}</p>
        <div className="flex flex-wrap gap-1">{profile.expertise.map(skill => <span key={skill} className="rounded-full bg-indigo-50 px-2 py-1 text-xs text-indigo-800">{skill}</span>)}</div>
        <dl className="grid grid-cols-2 gap-2 border-t pt-3 text-sm sm:grid-cols-4">
          <div><dt className="text-slate-500">Active teams</dt><dd className="font-semibold">{profile.activeTeamCount}/{profile.maxTeams}</dd></div>
          <div><dt className="text-slate-500">Assignments</dt><dd className="font-semibold">{profile.totalAssignments}</dd></div>
          <div><dt className="text-slate-500">Sessions</dt><dd className="font-semibold">{profile.totalSessions}</dd></div>
          <div><dt className="text-slate-500">Student rating</dt><dd className="font-semibold">{profile.averageFeedbackRating?.toFixed(1) ?? '—'}</dd></div></dl>
        <div className="flex flex-wrap gap-3 text-sm text-indigo-700">
          {profile.linkedInUrl && <a href={profile.linkedInUrl} target="_blank" rel="noopener noreferrer" className="underline">LinkedIn</a>}
          {profile.portfolioUrl && <a href={profile.portfolioUrl} target="_blank" rel="noopener noreferrer" className="underline">Portfolio link</a>}
          {profile.cvFileName && <button type="button" onClick={() => void download(profile, 'cv')} className="underline">Download CV</button>}
          {profile.portfolioFileName && <button type="button" onClick={() => void download(profile, 'portfolio')} className="underline">Download portfolio</button>}
        </div>
      </article>)}</div>}
  </main>;
}
