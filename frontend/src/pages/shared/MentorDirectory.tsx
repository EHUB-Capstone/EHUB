import { useEffect, useMemo, useRef, useState } from 'react';
import { mentorSupportApi } from '../../api/mentorSupportApi';
import type { MentorProfile } from '../../types/mentoring';
import { parseApiError } from '../../utils/apiError';
import { useAuth } from '../../hooks/useAuth';
import { mentorSearchText } from '../../utils/mentorProfiles';
import MentorProfileEditor from '../../components/admin/MentorProfileEditor';
import MentorSemesterAvailability from '../../components/admin/MentorSemesterAvailability';
import Button from '../../components/ui/Button';

export default function MentorDirectory() {
  const { user } = useAuth();
  const isAdmin = user?.role?.toUpperCase() === 'ADMIN';
  const [editing, setEditing] = useState<MentorProfile | 'new' | null>(null);
  const [availability, setAvailability] = useState<MentorProfile | null>(null);
  const [notice, setNotice] = useState('');
  const [domain, setDomain] = useState('');
  const [technology, setTechnology] = useState('');
  const [tag, setTag] = useState('');
  const [profiles, setProfiles] = useState<MentorProfile[]>([]);
  const [query, setQuery] = useState('');
  const [type, setType] = useState('All');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const directoryController = useRef<AbortController | null>(null);
  const load = async (retry = false) => {
    directoryController.current?.abort();
    const controller = new AbortController(); directoryController.current = controller;
    if (retry) { setLoading(true); setError(''); }
    try { const items = await mentorSupportApi.getDirectory(controller.signal); if (!controller.signal.aborted) setProfiles(items); }
    catch (cause) { if (!controller.signal.aborted) setError(parseApiError(cause, 'Could not load mentor directory').message); }
    finally { if (!controller.signal.aborted) setLoading(false); }
  };
  useEffect(() => {
    const controller = new AbortController();
    directoryController.current = controller;
    let active = true;
    mentorSupportApi.getDirectory(controller.signal).then(items => { if (active) setProfiles(items); })
      .catch(cause => { if (active) setError(parseApiError(cause, 'Could not load mentor directory').message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; directoryController.current?.abort(); };
  }, []);
  const filtered = useMemo(() => profiles.filter(profile =>
    (type === 'All' || profile.mentorType === type) &&
    `${profile.fullName} ${profile.organization ?? ''} ${mentorSearchText(profile)}`.toLowerCase().includes(query.toLowerCase()) &&
    (!domain || profile.startupDomains.includes(domain) || profile.experiences.some(x => x.kind === 'Startup' && x.area === domain)) &&
    (!technology || profile.technologySkills.includes(technology) || profile.experiences.some(x => x.kind === 'Technology' && x.area === technology)) &&
    (!tag || profile.tags.includes(tag))),
  [profiles, query, type, domain, technology, tag]);

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
      <p className="mt-1 text-sm text-slate-600">Profiles, expertise and mentoring history across semesters.</p>
      {isAdmin && <Button className="mt-3" onClick={() => setEditing('new')}>Create mentor</Button>}</header>
    {notice && <p role="status" className="rounded-lg bg-green-50 p-3 text-green-800">{notice}</p>}
    {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-red-700">{error}<button type="button" onClick={() => void load(true)} className="ml-3 underline">Retry</button></p>}
    <div className="flex flex-wrap gap-3"><input aria-label="Search mentors" value={query} onChange={event => setQuery(event.target.value)} placeholder="Search name or expertise" className="min-w-48 flex-1 rounded-lg border p-2" />
      <select aria-label="Filter mentor type" value={type} onChange={event => setType(event.target.value)} className="rounded-lg border p-2">
        <option value="All">All types</option><option value="Business">Business</option><option value="IT">IT</option><option value="Unspecified">Unspecified</option></select></div>
    <div className="flex flex-wrap gap-3">{[
      { label: 'Startup domain', value: domain, set: setDomain, options: profiles.flatMap(x => [...x.startupDomains, ...x.experiences.filter(e => e.kind === 'Startup').map(e => e.area)]) },
      { label: 'Technology skill', value: technology, set: setTechnology, options: profiles.flatMap(x => [...x.technologySkills, ...x.experiences.filter(e => e.kind === 'Technology').map(e => e.area)]) },
      { label: 'Mentor tag', value: tag, set: setTag, options: profiles.flatMap(x => x.tags) },
    ].map(filter => <label key={filter.label} className="text-sm">{filter.label}<select className="ml-2 rounded-lg border p-2" value={filter.value} onChange={e => filter.set(e.target.value)}>
      <option value="">All</option>{[...new Set(filter.options)].sort().map(value => <option key={value}>{value}</option>)}</select></label>)}</div>
    {loading ? <p>Loading mentors…</p> : filtered.length === 0 ? <p className="rounded-xl border bg-white p-8 text-center text-slate-600">No mentors match your search.</p> :
      <div className="grid gap-4 md:grid-cols-2">{filtered.map(profile => <article key={profile.id} className="space-y-3 rounded-xl border bg-white p-5">
        <div className="flex flex-wrap items-start justify-between gap-2"><div><h2 className="text-lg font-semibold">{profile.fullName}</h2>
          <p className="text-sm text-slate-600">{profile.organization || 'Independent'} · {profile.mentorType}</p></div>
          <span className="rounded-full bg-slate-100 px-2 py-1 text-xs">{profile.status}</span></div>
        <p className="text-sm text-slate-700">{profile.bio || 'No biography yet.'}</p>
        {isAdmin && <p className="text-sm text-slate-500">{profile.email}</p>}
        <div className="flex flex-wrap gap-1">{profile.expertise.map(skill => <span key={skill} className="rounded-full bg-indigo-50 px-2 py-1 text-xs text-indigo-800">{skill}</span>)}</div>
        <dl className="space-y-1 text-sm">{[['Domains', profile.startupDomains], ['Technology', profile.technologySkills], ['Tags', profile.tags]].map(([label, values]) =>
          <div key={String(label)}><dt className="inline font-medium">{label}: </dt><dd className="inline text-slate-600">{(values as string[]).join(', ') || '—'}</dd></div>)}</dl>
        {profile.experiences.length > 0 && <ul className="space-y-1 text-sm text-slate-600">{profile.experiences.map(entry =>
          <li key={`${entry.kind}:${entry.area}`}><span className="font-medium">{entry.kind} · {entry.area}</span>{entry.years !== null && ` · ${entry.years} years`}{entry.level && ` · ${entry.level}`}{entry.notes && <p>{entry.notes}</p>}</li>)}</ul>}
        <dl className="grid grid-cols-2 gap-2 border-t pt-3 text-sm sm:grid-cols-4">
          <div><dt className="text-slate-500">Active teams</dt><dd className="font-semibold">{profile.activeTeamCount}</dd></div>
          <div><dt className="text-slate-500">Assignments</dt><dd className="font-semibold">{profile.totalAssignments}</dd></div>
          <div><dt className="text-slate-500">Sessions</dt><dd className="font-semibold">{profile.totalSessions}</dd></div>
          <div><dt className="text-slate-500">Student rating</dt><dd className="font-semibold">{profile.averageFeedbackRating?.toFixed(1) ?? '—'}</dd></div></dl>
        <div className="flex flex-wrap gap-3 text-sm text-indigo-700">
          {profile.linkedInUrl && <a href={profile.linkedInUrl} target="_blank" rel="noopener noreferrer" className="underline">LinkedIn</a>}
          {profile.portfolioUrl && <a href={profile.portfolioUrl} target="_blank" rel="noopener noreferrer" className="underline">Portfolio link</a>}
          {profile.cvFileName && <button type="button" onClick={() => void download(profile, 'cv')} className="underline">Download CV</button>}
          {profile.portfolioFileName && <button type="button" onClick={() => void download(profile, 'portfolio')} className="underline">Download portfolio</button>}
        </div>
        {isAdmin && <div className="flex gap-2 border-t pt-3"><Button variant="outline" size="sm" onClick={() => setEditing(profile)}>Edit profile</Button><Button variant="outline" size="sm" onClick={() => setAvailability(profile)}>Availability</Button></div>}
      </article>)}</div>}
    {editing && <MentorProfileEditor profile={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} onSaved={saved => {
      setProfiles(items => items.some(x => x.id === saved.id) ? items.map(x => x.id === saved.id ? { ...x, ...saved, activeTeamCount: x.activeTeamCount, totalAssignments: x.totalAssignments, totalSessions: x.totalSessions, averageFeedbackRating: x.averageFeedbackRating } : x) : [...items, saved]);
      setEditing(null); setNotice('Mentor profile saved.');
    }} />}
    {availability && <MentorSemesterAvailability profile={availability} onClose={() => setAvailability(null)} />}
  </main>;
}
