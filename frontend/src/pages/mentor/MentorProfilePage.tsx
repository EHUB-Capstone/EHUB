import { useEffect, useRef, useState } from 'react';
import { mentorSupportApi } from '../../api/mentorSupportApi';
import type { MentorProfile, MentorProfileDraft } from '../../types/mentoring';
import { parseApiError } from '../../utils/apiError';
import { needsMentorProfileSetup, parseMentorTags, validateMentorMetadata } from '../../utils/mentorProfiles';
import { CheckCircle2, Pencil, Save, X } from 'lucide-react';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import MentorProfileSummary from '../../components/mentoring/MentorProfileSummary';

const emptyProfile: MentorProfileDraft = {
  mentorType: 'Unspecified', expertise: [], bio: '', experience: '', organization: '', linkedInUrl: '', portfolioUrl: '',
  startupDomains: [], technologySkills: [], tags: [], experiences: [],
};

export default function MentorProfilePage({ embedded = false }: { embedded?: boolean }) {
  const [profile, setProfile] = useState<MentorProfile | null>(null);
  const [draft, setDraft] = useState(emptyProfile);
  const [expertiseText, setExpertiseText] = useState('');
  const [skillsText, setSkillsText] = useState('');
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [downloading, setDownloading] = useState<'cv' | 'portfolio' | null>(null);
  const [uploading, setUploading] = useState<'cv' | 'portfolio' | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const uploadController = useRef<AbortController | null>(null);
  const headingRef = useRef<HTMLHeadingElement | null>(null);
  const wasEditing = useRef(false);

  useEffect(() => () => uploadController.current?.abort(), []);
  useEffect(() => {
    if (wasEditing.current && !editing) {
      headingRef.current?.focus({ preventScroll: true });
      headingRef.current?.scrollIntoView({ block: 'start' });
    }
    wasEditing.current = editing;
  }, [editing]);

  const load = async (retry = false) => {
    if (retry) { setLoading(true); setError(''); }
    try {
      const result = await mentorSupportApi.getProfile();
      setProfile(result);
      setDraft(result);
      setExpertiseText(result.expertise.join(', '));
      setSkillsText(result.technologySkills.join(', '));
      setEditing(needsMentorProfileSetup(result));
    } catch (cause) { setError(parseApiError(cause, 'Could not load mentor profile').message); }
    finally { setLoading(false); }
  };
  useEffect(() => {
    let active = true;
    mentorSupportApi.getProfile().then(result => {
      if (!active) return;
      setProfile(result); setDraft(result); setExpertiseText(result.expertise.join(', '));
      setSkillsText(result.technologySkills.join(', '));
      setEditing(needsMentorProfileSetup(result));
    }).catch(cause => { if (active) setError(parseApiError(cause, 'Could not load mentor profile').message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    if (saving || uploadController.current) return;
    setSaving(true); setError(''); setNotice('');
    try {
      const expertise = expertiseText.split(',').map(value => value.trim()).filter(Boolean);
      const payload = { ...draft, expertise, technologySkills: parseMentorTags(skillsText) };
      const validation = validateMentorMetadata(payload);
      if (validation) {
        setError(validation); return;
      }
      const result = await mentorSupportApi.updateProfile(payload);
      setProfile(result); setDraft(result); setExpertiseText(result.expertise.join(', '));
      setSkillsText(result.technologySkills.join(', '));
      setEditing(false); setNotice('Mentor profile saved.');
    } catch (cause) { setError(parseApiError(cause, 'Could not save mentor profile').message); }
    finally { setSaving(false); }
  };

  const upload = async (kind: 'cv' | 'portfolio', file?: File) => {
    if (!file || saving || uploadController.current) return;
    if (file.size === 0 || file.size > 10 * 1024 * 1024 || !/\.(pdf|doc|docx)$/i.test(file.name)) {
      setError('Choose a non-empty PDF, DOC or DOCX document no larger than 10 MB.'); return;
    }
    const controller = new AbortController();
    uploadController.current = controller;
    setUploading(kind); setError(''); setNotice('');
    try {
      const result = await mentorSupportApi.uploadDocument(kind, file, controller.signal);
      if (controller.signal.aborted) return;
      setProfile(result);
      if (kind === 'cv') {
        setDraft(current => ({ ...current, bio: result.bio, experience: result.experience,
          expertise: result.expertise }));
        setExpertiseText(result.expertise.join(', '));
        setNotice('CV uploaded. Education and work experience have been extracted and saved to your profile. Review the fields below and edit anything that needs correcting. Your skills have been kept.');
      } else setNotice('Document uploaded.');
    }
    catch (cause) { if (!controller.signal.aborted) setError(parseApiError(cause, 'Could not upload document').message); }
    finally { if (!controller.signal.aborted) setUploading(null); uploadController.current = null; }
  };

  const download = async (kind: 'cv' | 'portfolio') => {
    if (!profile || downloading) return;
    setDownloading(kind); setError('');
    try {
      const blob = await mentorSupportApi.getDocument(profile.id, kind);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url; link.download = kind === 'cv' ? profile.cvFileName ?? 'CV.pdf' : profile.portfolioFileName ?? 'Portfolio.pdf';
      link.click(); URL.revokeObjectURL(url);
    } catch (cause) { setError(parseApiError(cause, 'Could not download document').message); }
    finally { setDownloading(null); }
  };

  const resetDraft = (edit: boolean) => {
    if (!profile) return;
    setDraft(profile); setExpertiseText(profile.expertise.join(', '));
    setSkillsText(profile.technologySkills.join(', '));
    setError(''); setNotice(''); setEditing(edit);
  };

  if (loading) return <div className="p-6 text-body text-slate-600" role="status">Loading mentor profile…</div>;
  if (!profile) return <div className="p-6 text-body text-danger-dark" role="alert">{error}<button type="button" onClick={() => void load(true)} className="ml-3 underline">Retry</button></div>;

  const fields: Array<{ name: 'organization' | 'bio' | 'experience' | 'linkedInUrl' | 'portfolioUrl'; label: string; multiline?: boolean }> = [
    { name: 'organization', label: 'Organization' }, { name: 'bio', label: 'Professional bio', multiline: true },
    { name: 'experience', label: 'Startup or technical experience', multiline: true },
    { name: 'linkedInUrl', label: 'LinkedIn HTTPS URL' }, { name: 'portfolioUrl', label: 'Portfolio HTTPS URL' },
  ];
  const Container = embedded ? 'section' : 'main';
  const Heading = embedded ? 'h2' : 'h1';
  return <Container aria-label="Mentor profile and CV" className={embedded ? 'space-y-6' : 'mx-auto max-w-3xl space-y-6 p-4 sm:p-8'}>
    <header className="flex flex-wrap items-start justify-between gap-4">
      <div className="min-w-0 flex-1"><Heading ref={headingRef} tabIndex={-1} className={`scroll-mt-24 text-secondary outline-none ${embedded ? 'text-subheading' : 'text-heading tracking-tight sm:text-display'}`}>{embedded ? 'Mentor profile & CV' : 'Mentor profile'}</Heading>
        <p className="mt-1 text-body text-slate-600">{editing ? 'Upload a CV or update your details, then save your profile.' : 'Your professional information, ready for lecturers to view.'}</p>
        {!editing && <Badge variant="success" className="mt-3 gap-1.5"><CheckCircle2 aria-hidden="true" className="h-3.5 w-3.5" />Saved profile</Badge>}
      </div>
      {!editing && <Button icon={Pencil} size="sm" onClick={() => resetDraft(true)}>Edit profile</Button>}
    </header>
    {error && <p role="alert" className="rounded-lg border border-danger-light bg-danger-50 p-3 text-body text-danger-dark">{error}</p>}
    {notice && <p role="status" className="rounded-lg border border-success-light bg-success-50 p-3 text-body text-success-dark">{notice}</p>}
    {!editing ? <MentorProfileSummary profile={profile} onDownload={kind => void download(kind)} downloading={downloading} /> : <>
    <section className="rounded-xl border border-slate-200 bg-slate-50 p-5"><h3 className="text-subheading text-secondary">Documents</h3>
      <p className="mb-4 mt-1 text-body leading-6 text-slate-600">Upload a CV to fill in your bio, experience and expertise automatically. PDF, DOC or DOCX, up to 10 MB. Review the extracted details before saving.</p>
      {(['cv', 'portfolio'] as const).map(kind => <div key={kind} className="flex flex-wrap items-center gap-3 border-t py-3">
        <label htmlFor={`mentor-${kind}-file`} className="min-w-24 text-body font-medium">{kind === 'cv' ? 'CV / résumé' : 'Portfolio'}</label>
        <input id={`mentor-${kind}-file`} type="file" accept=".pdf,.doc,.docx" className="min-w-0 max-w-full text-body text-slate-500 file:mr-3 file:rounded-lg file:border-0 file:bg-primary-50 file:px-3 file:py-2 file:text-body-sm file:font-medium file:text-primary-700 hover:file:bg-primary-100 dark:file:bg-dark-surface dark:file:text-primary-light dark:hover:file:bg-secondary-700" disabled={uploading !== null || saving} onChange={event => { void upload(kind, event.target.files?.[0]); event.target.value = ''; }} />
        {(kind === 'cv' ? profile.cvFileName : profile.portfolioFileName) && <p className="w-full break-all text-caption text-slate-500">Current: {kind === 'cv' ? profile.cvFileName : profile.portfolioFileName}</p>}
      </div>)}
      {uploading && <p role="status">{uploading === 'cv' ? 'Uploading and extracting CV…' : 'Uploading portfolio…'}</p>}
    </section>
    <form onSubmit={save} className={embedded ? 'space-y-4' : 'space-y-4 rounded-xl border border-slate-200 bg-white p-5'}>
      <fieldset disabled={saving || uploading !== null} className="space-y-4">
      <div><label htmlFor="mentor-type" className="block text-body font-medium">Mentor type</label>
        <select disabled title="Managed by the administrator" id="mentor-type" required value={draft.mentorType} onChange={event => setDraft({ ...draft, mentorType: event.target.value as MentorProfile['mentorType'] })} className="mt-1 w-full rounded-lg border p-2">
          <option value="Unspecified">Select type</option><option value="Business">Business mentor</option><option value="IT">IT lecturer or technical mentor</option>
        </select></div>
      <div><label htmlFor="mentor-expertise" className="block text-body font-medium">Expertise (comma separated)</label>
        <input id="mentor-expertise" required maxLength={1600} value={expertiseText} onChange={event => setExpertiseText(event.target.value)} className="mt-1 w-full rounded-lg border p-2" placeholder="Product management, AI, marketing" /></div>
      <div><label htmlFor="mentor-skills" className="block text-body font-medium">Skills (comma separated)</label>
        <input id="mentor-skills" aria-describedby="mentor-skills-help" maxLength={1640} value={skillsText} onChange={event => setSkillsText(event.target.value)} className="mt-1 w-full rounded-lg border p-2" placeholder="Leadership, Python, project planning" />
        <p id="mentor-skills-help" className="mt-1 text-caption text-slate-500">Enter up to 20 distinct skills, each at most 80 characters. Skills are entered manually and kept when you upload a CV.</p></div>
      {fields.map(field => <div key={field.name}><label htmlFor={`mentor-${field.name}`} className="block text-body font-medium">{field.label}</label>
        {field.multiline ? <textarea id={`mentor-${field.name}`} value={draft[field.name] ?? ''} onChange={event => setDraft({ ...draft, [field.name]: event.target.value })} rows={4} className="mt-1 w-full rounded-lg border p-2" />
          : <input id={`mentor-${field.name}`} value={draft[field.name] ?? ''} onChange={event => setDraft({ ...draft, [field.name]: event.target.value })} className="mt-1 w-full rounded-lg border p-2" />}</div>)}
      <div className="flex flex-wrap gap-3 border-t border-slate-200 pt-5">
        <Button disabled={saving} type="submit" icon={Save} isLoading={saving}>{saving ? 'Saving…' : 'Save profile'}</Button>
        {!needsMentorProfileSetup(profile) && <Button variant="outline" icon={X} onClick={() => resetDraft(false)}>Cancel</Button>}
      </div>
      </fieldset>
    </form>
    </>}
  </Container>;
}
