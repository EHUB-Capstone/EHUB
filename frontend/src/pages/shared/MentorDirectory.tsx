import { useEffect, useMemo, useRef, useState } from 'react';
import { mentorSupportApi } from '../../api/mentorSupportApi';
import type { MentorProfile } from '../../types/mentoring';
import { parseApiError } from '../../utils/apiError';
import { useAuth } from '../../hooks/useAuth';
import { mentorSearchText } from '../../utils/mentorProfiles';
import MentorProfileEditor from '../../components/admin/MentorProfileEditor';
import MentorSemesterAvailability from '../../components/admin/MentorSemesterAvailability';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import MentorDetailModal from '../../components/mentor/MentorDetailModal';
import { useTranslation } from 'react-i18next';

export default function MentorDirectory() {
  const { t } = useTranslation('mentoring');
  const { user } = useAuth();
  const isAdmin = user?.role?.toUpperCase() === 'ADMIN';
  const [editing, setEditing] = useState<MentorProfile | 'new' | null>(null);
  const [availability, setAvailability] = useState<MentorProfile | null>(null);
  const [selected, setSelected] = useState<MentorProfile | null>(null);
  const [notice, setNotice] = useState('');
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
    <header><h1 className="text-display text-secondary">{t('mentorDirectory.mentorDirectory')}</h1>
      <p className="mt-1 text-body text-slate-600">{t('mentorDirectory.profilesExpertiseAndMentoringHistoryAcross')}</p>
      {isAdmin && <Button className="mt-3" onClick={() => setEditing('new')}>{t('mentorDirectory.createMentor')}</Button>}</header>
    {notice && <p role="status" className="rounded-lg bg-success-50 p-3 text-body text-success-dark">{notice}</p>}
    {error && <p role="alert" className="rounded-lg bg-danger-50 p-3 text-body text-danger-dark">{error}<button type="button" onClick={() => void load(true)} className="ml-3 underline">{t('mentorProfilePage.retry')}</button></p>}
    <div className="flex flex-wrap gap-3"><input aria-label={t('mentorDirectory.searchMentors')} value={query} onChange={event => setQuery(event.target.value)} placeholder={t('mentorDirectory.searchNameOrExpertise')} className="min-w-48 flex-1 rounded-lg border p-2" />
      <select aria-label={t('mentorDirectory.filterMentorType')} value={type} onChange={event => setType(event.target.value)} className="rounded-lg border p-2">
        <option value="All">{t('mentorDirectory.allTypes')}</option><option value="Business">{t('mentorDirectory.business')}</option><option value="IT">{t('mentorDirectory.it')}</option><option value="Unspecified">{t('mentorDirectory.unspecified')}</option></select></div>
    {loading ? <p>{t('mentorDirectory.loadingMentors')}</p> : filtered.length === 0 ? <p className="rounded-2xl border border-slate-200 bg-white p-8 text-center text-slate-600 shadow-card">{t('mentorDirectory.noMentorsMatchYourSearch')}</p> :
      <>
        <p className="text-body-sm text-slate-500">{t('mentorDirectory.mentorCount', { count: filtered.length })}</p>
        <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{filtered.map(profile =>
          <li key={profile.id}>
            <button type="button" aria-haspopup="dialog" onClick={() => setSelected(profile)}
              className="flex w-full items-center gap-3 rounded-xl border border-slate-200 bg-white p-3 text-left shadow-card transition hover:border-primary-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary">
              <span aria-hidden="true" className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary-100 text-body-sm font-semibold text-primary-700">{profile.fullName.trim().charAt(0).toUpperCase()}</span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-body font-semibold text-secondary">{profile.fullName}</span>
                <span className="mt-1 flex items-center gap-1.5"><Badge variant="neutral">{profile.mentorType}</Badge>
                  {profile.status !== 'Active' && <Badge variant={profile.status}>{profile.status}</Badge>}</span>
              </span>
            </button>
          </li>)}</ul>
      </>}
    {selected && <MentorDetailModal profile={selected} isAdmin={isAdmin} onClose={() => setSelected(null)} onDownload={(p, kind) => void download(p, kind)}
      onEdit={p => { setSelected(null); setEditing(p); }} onAvailability={p => { setSelected(null); setAvailability(p); }} />}
    {editing && <MentorProfileEditor profile={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} onSaved={saved => {
      setProfiles(items => items.some(x => x.id === saved.id) ? items.map(x => x.id === saved.id ? { ...x, ...saved, activeTeamCount: x.activeTeamCount, totalAssignments: x.totalAssignments, totalSessions: x.totalSessions, averageFeedbackRating: x.averageFeedbackRating } : x) : [...items, saved]);
      setEditing(null); setNotice(t('mentorProfilePage.mentorProfileSaved'));
    }} />}
    {availability && <MentorSemesterAvailability profile={availability} onClose={() => setAvailability(null)} />}
  </main>;
}
