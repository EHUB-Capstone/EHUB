import { useEffect, useState } from 'react';
import { mentorSupportApi } from '../../api/mentorSupportApi';
import type { MentorProfile, MentorProfileDraft } from '../../types/mentoring';
import { parseApiError } from '../../utils/apiError';

const emptyProfile: MentorProfileDraft = {
  mentorType: 'Unspecified', expertise: [], bio: '', experience: '', organization: '', linkedInUrl: '', portfolioUrl: '',
  startupDomains: [], technologySkills: [], tags: [], experiences: [],
};

export default function MentorProfilePage() {
  const [profile, setProfile] = useState<MentorProfile | null>(null);
  const [draft, setDraft] = useState(emptyProfile);
  const [expertiseText, setExpertiseText] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState<'cv' | 'portfolio' | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const load = async (retry = false) => {
    if (retry) { setLoading(true); setError(''); }
    try {
      const result = await mentorSupportApi.getProfile();
      setProfile(result);
      setDraft(result);
      setExpertiseText(result.expertise.join(', '));
    } catch (cause) { setError(parseApiError(cause, 'Could not load mentor profile').message); }
    finally { setLoading(false); }
  };
  useEffect(() => {
    let active = true;
    mentorSupportApi.getProfile().then(result => {
      if (!active) return;
      setProfile(result); setDraft(result); setExpertiseText(result.expertise.join(', '));
    }).catch(cause => { if (active) setError(parseApiError(cause, 'Could not load mentor profile').message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    if (saving) return;
    setSaving(true); setError(''); setNotice('');
    try {
      const expertise = expertiseText.split(',').map(value => value.trim()).filter(Boolean);
      if (draft.mentorType === 'Unspecified' || expertise.length === 0 || expertise.length > 20 || expertise.some(skill => skill.length > 80)) {
        setError('Choose a mentor type and 1–20 expertise areas of at most 80 characters each.'); return;
      }
      const result = await mentorSupportApi.updateProfile({ ...draft, expertise });
      setProfile(result); setDraft(result); setNotice('Mentor profile saved.');
    } catch (cause) { setError(parseApiError(cause, 'Could not save mentor profile').message); }
    finally { setSaving(false); }
  };

  const upload = async (kind: 'cv' | 'portfolio', file?: File) => {
    if (!file) return;
    if (file.size > 10 * 1024 * 1024 || !/\.(pdf|docx)$/i.test(file.name)) {
      setError('Choose a PDF or DOCX document no larger than 10 MB.'); return;
    }
    setUploading(kind); setError(''); setNotice('');
    try { setProfile(await mentorSupportApi.uploadDocument(kind, file)); setNotice('Document uploaded.'); }
    catch (cause) { setError(parseApiError(cause, 'Could not upload document').message); }
    finally { setUploading(null); }
  };

  const download = async (kind: 'cv' | 'portfolio') => {
    if (!profile) return;
    try {
      const blob = await mentorSupportApi.getDocument(profile.id, kind);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url; link.download = kind === 'cv' ? profile.cvFileName ?? 'CV.pdf' : profile.portfolioFileName ?? 'Portfolio.pdf';
      link.click(); URL.revokeObjectURL(url);
    } catch (cause) { setError(parseApiError(cause, 'Could not download document').message); }
  };

  if (loading) return <div className="p-6">Loading mentor profile…</div>;
  if (!profile) return <div className="p-6 text-red-700" role="alert">{error}<button type="button" onClick={() => void load(true)} className="ml-3 underline">Retry</button></div>;

  const fields: Array<{ name: 'organization' | 'bio' | 'experience' | 'linkedInUrl' | 'portfolioUrl'; label: string; multiline?: boolean }> = [
    { name: 'organization', label: 'Organization' }, { name: 'bio', label: 'Professional bio', multiline: true },
    { name: 'experience', label: 'Startup or technical experience', multiline: true },
    { name: 'linkedInUrl', label: 'LinkedIn HTTPS URL' }, { name: 'portfolioUrl', label: 'Portfolio HTTPS URL' },
  ];
  return <main className="mx-auto max-w-3xl space-y-6 p-4 sm:p-8">
    <header><h1 className="text-2xl font-bold text-slate-900">Mentor profile</h1>
      <p className="mt-1 text-sm text-slate-600">Keep your expertise current so lecturers can find the right mentor for each project.</p></header>
    {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-red-700">{error}</p>}
    {notice && <p role="status" className="rounded-lg bg-green-50 p-3 text-green-800">{notice}</p>}
    <form onSubmit={save} className="space-y-4 rounded-xl border border-slate-200 bg-white p-5">
      <div><label htmlFor="mentor-type" className="block text-sm font-medium">Mentor type</label>
        <select disabled title="Managed by the administrator" id="mentor-type" required value={draft.mentorType} onChange={event => setDraft({ ...draft, mentorType: event.target.value as MentorProfile['mentorType'] })} className="mt-1 w-full rounded-lg border p-2">
          <option value="Unspecified">Select type</option><option value="Business">Business mentor</option><option value="IT">IT lecturer or technical mentor</option>
        </select></div>
      <div><label htmlFor="mentor-expertise" className="block text-sm font-medium">Expertise (comma separated)</label>
        <input id="mentor-expertise" required maxLength={1600} value={expertiseText} onChange={event => setExpertiseText(event.target.value)} className="mt-1 w-full rounded-lg border p-2" placeholder="Product management, AI, marketing" /></div>
      {fields.map(field => <div key={field.name}><label htmlFor={`mentor-${field.name}`} className="block text-sm font-medium">{field.label}</label>
        {field.multiline ? <textarea id={`mentor-${field.name}`} value={draft[field.name] ?? ''} onChange={event => setDraft({ ...draft, [field.name]: event.target.value })} rows={4} className="mt-1 w-full rounded-lg border p-2" />
          : <input id={`mentor-${field.name}`} value={draft[field.name] ?? ''} onChange={event => setDraft({ ...draft, [field.name]: event.target.value })} className="mt-1 w-full rounded-lg border p-2" />}</div>)}
      <button disabled={saving} type="submit" className="rounded-lg bg-indigo-600 px-4 py-2 text-white disabled:opacity-50">{saving ? 'Saving…' : 'Save profile'}</button>
    </form>
    <section className="rounded-xl border border-slate-200 bg-white p-5"><h2 className="text-lg font-semibold">Documents</h2>
      <p className="mb-4 text-sm text-slate-600">PDF or DOCX, up to 10 MB. Documents are available only to authorized staff.</p>
      {(['cv', 'portfolio'] as const).map(kind => <div key={kind} className="flex flex-wrap items-center gap-3 border-t py-3">
        <label htmlFor={`mentor-${kind}-file`} className="min-w-24 text-sm font-medium">{kind === 'cv' ? 'CV / résumé' : 'Portfolio'}</label>
        <input id={`mentor-${kind}-file`} type="file" accept=".pdf,.docx" disabled={uploading !== null} onChange={event => { void upload(kind, event.target.files?.[0]); event.target.value = ''; }} />
        {(kind === 'cv' ? profile.cvFileName : profile.portfolioFileName) && <button type="button" onClick={() => void download(kind)} className="text-sm text-indigo-700 underline">Download current</button>}
      </div>)}
      {uploading && <p role="status">Uploading {uploading}…</p>}
    </section>
  </main>;
}
