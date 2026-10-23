import { useEffect, useMemo, useRef, useState } from 'react';
import { mentorSupportApi } from '../../api/mentorSupportApi';
import type { MentorProfile } from '../../types/mentoring';
import { parseApiError } from '../../utils/apiError';
import { useAuth } from '../../hooks/useAuth';
import { mentorSearchText } from '../../utils/mentorProfiles';
import { formatMentorDirectoryBio, isMentorCvFormLabel, parseMentorCvFormBio, parseMentorProfessionalExperience } from '../../utils/mentorDirectoryBio';
import MentorProfileEditor from '../../components/admin/MentorProfileEditor';
import MentorSemesterAvailability from '../../components/admin/MentorSemesterAvailability';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';

export default function MentorDirectory() {
  const { user } = useAuth();
  const isAdmin = user?.role?.toUpperCase() === 'ADMIN';
  const [editing, setEditing] = useState<MentorProfile | 'new' | null>(null);
  const [availability, setAvailability] = useState<MentorProfile | null>(null);
  const [notice, setNotice] = useState('');
  const [profiles, setProfiles] = useState<MentorProfile[]>([]);
  const [expandedBioId, setExpandedBioId] = useState<string | null>(null);
  const [expandedExperienceId, setExpandedExperienceId] = useState<string | null>(null);
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
    `${profile.fullName} ${profile.organization ?? ''} ${mentorSearchText(profile)}`.toLowerCase().includes(query.toLowerCase())),
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
  return <main className="mx-auto max-w-6xl space-y-5 p-4 text-body text-slate-700 sm:p-8">
    <header><h1 className="text-display text-secondary">Mentor directory</h1>
      <p className="mt-1 text-body text-slate-600">Profiles, expertise and mentoring history across semesters.</p>
      {isAdmin && <Button className="mt-3" onClick={() => setEditing('new')}>Create mentor</Button>}</header>
    {notice && <p role="status" className="rounded-lg bg-success-50 p-3 text-body text-success-dark">{notice}</p>}
    {error && <p role="alert" className="rounded-lg bg-danger-50 p-3 text-body text-danger-dark">{error}<button type="button" onClick={() => void load(true)} className="ml-3 underline">Retry</button></p>}
    <div className="flex flex-wrap gap-3"><input aria-label="Search mentors" value={query} onChange={event => setQuery(event.target.value)} placeholder="Search name or expertise" className="min-w-48 flex-1 rounded-lg border p-2" />
      <select aria-label="Filter mentor type" value={type} onChange={event => setType(event.target.value)} className="rounded-lg border p-2">
        <option value="All">All types</option><option value="Business">Business</option><option value="IT">IT</option><option value="Unspecified">Unspecified</option></select></div>
    {loading ? <p>Loading mentors…</p> : filtered.length === 0 ? <p className="rounded-2xl border border-slate-200 bg-white p-8 text-center text-slate-600 shadow-card">No mentors match your search.</p> :
      <div className="grid gap-4 md:grid-cols-2">{filtered.map(profile => {
        const bio = formatMentorDirectoryBio(profile.bio);
        const parsedBio = parseMentorCvFormBio(profile.bio);
        const workExperience = parseMentorProfessionalExperience(profile.experience);
        const expanded = expandedBioId === profile.id;
        const experienceExpanded = expandedExperienceId === profile.id;
        const tags = [...new Set([...profile.expertise, ...profile.technologySkills, ...profile.startupDomains, ...profile.tags]
          .filter(value => value.trim() && !isMentorCvFormLabel(value)))];
        return <article key={profile.id} className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-card">
          <div className="flex flex-wrap items-start justify-between gap-2"><div><h2 className="text-heading text-secondary">{profile.fullName}</h2>
            <p className="text-body text-slate-600">{profile.organization || 'Independent'} · {profile.mentorType}</p></div>
            <Badge variant={profile.status}>{profile.status}</Badge></div>
          {parsedBio ? <div className="space-y-3">
            {parsedBio.education.length > 0 && <section aria-label="Education">
              <h3 className="text-overline text-slate-500">Education</h3>
              <ol className="mt-2 space-y-2 border-l-2 border-primary-100 pl-3">
                {parsedBio.education.map((entry, index) => <li key={`${entry.period}-${index}`} className="rounded-lg bg-slate-50 px-3 py-2">
                  <p className="text-caption font-semibold text-primary-700">{entry.period}</p>
                  <p className="mt-0.5 text-body-sm font-semibold text-secondary">{entry.institution}</p>
                  {entry.details.length > 0 && <p className="mt-1 text-caption leading-5 text-slate-600">{entry.details.join(' · ')}</p>}
                </li>)}
              </ol>
            </section>}
            {parsedBio.thesis.length > 0 && <section aria-label="Thesis topic">
              <h3 className="text-overline text-slate-500">Thesis topic</h3>
              <ul className="mt-1 space-y-1">{parsedBio.thesis.map((detail, index) => <li key={`${detail}-${index}`} className="rounded-lg bg-slate-50 px-3 py-2 text-body-sm leading-5 text-slate-600">{detail}</li>)}</ul>
            </section>}
            {parsedBio.languagesAndCertificates.length > 0 && <section aria-label="Languages and certificates">
              <h3 className="text-overline text-slate-500">Languages & certificates</h3>
              <ul id={`mentor-bio-details-${profile.id}`} className="mt-2 space-y-1.5">
                {(expanded ? parsedBio.languagesAndCertificates : parsedBio.languagesAndCertificates.slice(0, 3)).map((detail, index) => <li key={`${detail}-${index}`} className="rounded-lg border border-slate-100 px-3 py-2 text-body-sm leading-5 text-slate-600">{detail}</li>)}
              </ul>
              {parsedBio.languagesAndCertificates.length > 3 && <button type="button" aria-expanded={expanded} aria-controls={`mentor-bio-details-${profile.id}`}
                onClick={() => setExpandedBioId(expanded ? null : profile.id)} className="mt-1 text-caption font-medium text-primary-700 underline">
                {expanded ? 'Thu gọn' : `Xem thêm ${parsedBio.languagesAndCertificates.length - 3} mục`}
              </button>}
            </section>}
          </div> : <section aria-label="Professional bio">
            <h3 className="text-overline text-slate-500">Professional bio</h3>
            <p id={`mentor-bio-${profile.id}`} className={`mt-1 text-body text-slate-700 ${bio && !expanded ? 'line-clamp-3' : ''}`}>{bio || 'No biography yet.'}</p>
            {bio.length > 180 && <button type="button" aria-expanded={expanded} aria-controls={`mentor-bio-${profile.id}`}
              onClick={() => setExpandedBioId(expanded ? null : profile.id)} className="mt-1 text-caption font-medium text-primary-700 underline">
              {expanded ? 'Thu gọn' : 'Xem đầy đủ'}
            </button>}
          </section>}
          {workExperience.length > 0 && <section aria-label="Professional experience">
            <h3 className="text-overline text-slate-500">Professional experience</h3>
            <ul className="mt-1 space-y-1.5">{(experienceExpanded ? workExperience : workExperience.slice(0, 3)).map((entry, index) =>
              <li key={`${entry}-${index}`} className="rounded-lg bg-slate-50 px-3 py-2 text-body-sm leading-5 text-slate-600">{entry}</li>)}</ul>
            {workExperience.length > 3 && <button type="button" aria-expanded={experienceExpanded} onClick={() => setExpandedExperienceId(experienceExpanded ? null : profile.id)} className="mt-1 text-caption font-medium text-primary-700 underline">
              {experienceExpanded ? 'Thu gọn' : `Xem thêm ${workExperience.length - 3} mục`}
            </button>}
          </section>}
          {tags.length > 0 && <section aria-label="Areas and skills">
            <h3 className="text-overline text-slate-500">Areas & skills</h3>
            <div className="mt-1 flex flex-wrap gap-1">{tags.map(skill => <Badge key={skill} variant="neutral">{skill}</Badge>)}</div>
          </section>}
          {profile.experiences.length > 0 && <section aria-label="Structured experience">
            <h3 className="text-overline text-slate-500">Experience</h3>
            <ul className="mt-1 space-y-1 text-body-sm text-slate-600">{profile.experiences.map(entry =>
              <li key={`${entry.kind}:${entry.area}`}><span className="font-medium">{entry.kind} · {entry.area}</span>{entry.years !== null && ` · ${entry.years} years`}{entry.level && ` · ${entry.level}`}{entry.notes && <p className="mt-0.5">{entry.notes}</p>}</li>)}</ul>
          </section>}
          {isAdmin && <p className="text-body text-slate-500">{profile.email}</p>}
          <dl className="grid grid-cols-2 gap-2 border-t pt-3 text-body sm:grid-cols-4">
            <div><dt className="text-slate-500">Active teams</dt><dd className="font-semibold">{profile.activeTeamCount}</dd></div>
            <div><dt className="text-slate-500">Assignments</dt><dd className="font-semibold">{profile.totalAssignments}</dd></div>
            <div><dt className="text-slate-500">Sessions</dt><dd className="font-semibold">{profile.totalSessions}</dd></div>
            <div><dt className="text-slate-500">Student rating</dt><dd className="font-semibold">{profile.averageFeedbackRating?.toFixed(1) ?? '—'}</dd></div></dl>
          <div className="flex flex-wrap gap-3 text-body text-primary-700">
            {profile.linkedInUrl && <a href={profile.linkedInUrl} target="_blank" rel="noopener noreferrer" className="underline">LinkedIn</a>}
            {profile.portfolioUrl && <a href={profile.portfolioUrl} target="_blank" rel="noopener noreferrer" className="underline">Portfolio link</a>}
            {profile.cvFileName && <button type="button" onClick={() => void download(profile, 'cv')} className="underline">Download CV</button>}
            {profile.portfolioFileName && <button type="button" onClick={() => void download(profile, 'portfolio')} className="underline">Download portfolio</button>}
          </div>
          {isAdmin && <div className="flex gap-2 border-t pt-3"><Button variant="outline" size="sm" onClick={() => setEditing(profile)}>Edit profile</Button><Button variant="outline" size="sm" onClick={() => setAvailability(profile)}>Availability</Button></div>}
        </article>;
      })}</div>}
    {editing && <MentorProfileEditor profile={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} onSaved={saved => {
      setProfiles(items => items.some(x => x.id === saved.id) ? items.map(x => x.id === saved.id ? { ...x, ...saved, activeTeamCount: x.activeTeamCount, totalAssignments: x.totalAssignments, totalSessions: x.totalSessions, averageFeedbackRating: x.averageFeedbackRating } : x) : [...items, saved]);
      setEditing(null); setNotice('Mentor profile saved.');
    }} />}
    {availability && <MentorSemesterAvailability profile={availability} onClose={() => setAvailability(null)} />}
  </main>;
}
