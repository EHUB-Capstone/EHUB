import { useEffect, useRef, useState } from 'react';
import type { MentorProfile, MentorProfileDraft } from '../../types/mentoring';
import { mentorSupportApi } from '../../api/mentorSupportApi';
import { parseApiError } from '../../utils/apiError';
import { parseMentorTags, validateMentorMetadata } from '../../utils/mentorProfiles';
import Button from '../ui/Button';
import Modal from '../ui/Modal';

interface Props { profile?: MentorProfile; onClose: () => void; onSaved: (profile: MentorProfile) => void }
const inputClass = 'mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 text-sm';
const empty: MentorProfileDraft = { mentorType: 'Business', expertise: [], startupDomains: [], technologySkills: [], tags: [],
  experiences: [], bio: '', experience: '', organization: '', linkedInUrl: '', portfolioUrl: '' };
const tagFields = [ ['expertise', 'Expertise'], ['startupDomains', 'Startup domains'],
  ['technologySkills', 'Technology skills'], ['tags', 'Mentor tags'] ] as const;

export default function MentorProfileEditor({ profile, onClose, onSaved }: Props) {
  const [name, setName] = useState(profile?.fullName ?? '');
  const [email, setEmail] = useState(profile?.email ?? '');
  const [password, setPassword] = useState('');
  const [draft, setDraft] = useState<MentorProfileDraft>(profile ?? empty);
  const [texts, setTexts] = useState(Object.fromEntries(tagFields.map(([key]) => [key, profile?.[key].join(', ') ?? ''])) as Record<typeof tagFields[number][0], string>);
  const [saving, setSaving] = useState(false);
  const busy = useRef(false);
  const requestController = useRef<AbortController | null>(null);
  useEffect(() => () => requestController.current?.abort(), []);
  const [error, setError] = useState('');
  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    if (busy.current) return;
    const payload: MentorProfileDraft = { ...draft, expertise: parseMentorTags(texts.expertise),
      startupDomains: parseMentorTags(texts.startupDomains), technologySkills: parseMentorTags(texts.technologySkills), tags: parseMentorTags(texts.tags) };
    const validation = validateMentorMetadata(payload);
    if (validation) { setError(validation); return; }
    busy.current = true; setSaving(true); setError('');
    const controller = new AbortController(); requestController.current = controller;
    try {
      const request = { fullName: name, email, profile: payload, ...(!profile ? { temporaryPassword: password } : {}) };
      const result = profile ? await mentorSupportApi.updateManagedProfile(profile.id, request, controller.signal) : await mentorSupportApi.createManagedProfile(request, controller.signal);
      if (!controller.signal.aborted) onSaved(result);
    } catch (cause) {
      if (!controller.signal.aborted) {
        const parsed = parseApiError(cause, 'Could not save mentor profile.');
        setError(Object.values(parsed.fieldErrors).join(' ') || parsed.message);
      }
    }
    finally { busy.current = false; if (!controller.signal.aborted) setSaving(false); }
  };
  return <Modal isOpen onClose={() => { if (!busy.current) onClose(); }} title={profile ? 'Edit mentor profile' : 'Create mentor profile'} size="xl">
    <form onSubmit={save} className="space-y-5">
      {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-700">{error}</p>}
      <fieldset disabled={saving} className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="text-sm font-medium">Full name<input required maxLength={100} value={name} onChange={e => setName(e.target.value)} className={inputClass} /></label>
          <label className="text-sm font-medium">Email<input required type="email" maxLength={320} value={email} onChange={e => setEmail(e.target.value)} className={inputClass} /></label>
          {!profile && <label className="text-sm font-medium">Temporary password<input required type="password" autoComplete="new-password" minLength={6} maxLength={100} value={password} onChange={e => setPassword(e.target.value)} className={inputClass} /></label>}
          <label className="text-sm font-medium">Mentor type<select value={draft.mentorType} onChange={e => setDraft({ ...draft, mentorType: e.target.value as MentorProfile['mentorType'] })} className={inputClass}>
            <option value="Business">Business</option><option value="IT">IT</option></select></label>
          <label className="text-sm font-medium">Organization<input maxLength={200} value={draft.organization ?? ''} onChange={e => setDraft({ ...draft, organization: e.target.value })} className={inputClass} /></label>
        </div>
        <label className="block text-sm font-medium">Professional background<textarea required maxLength={2000} rows={3} value={draft.bio ?? ''} onChange={e => setDraft({ ...draft, bio: e.target.value })} className={inputClass} /></label>
        <div className="grid gap-4 sm:grid-cols-2">{tagFields.map(([key, label]) => <label key={key} className="text-sm font-medium">{label} (comma separated)
          <input required={key === 'expertise'} maxLength={1620} value={texts[key]} onChange={e => setTexts({ ...texts, [key]: e.target.value })} className={inputClass} />
        </label>)}</div>
        <section className="space-y-3 rounded-xl border border-slate-200 p-4">
          <h3 className="font-semibold">Startup and technology experience</h3>
          <p className="text-sm text-slate-500">Add a domain or skill; years, level and notes are optional.</p>
          {draft.experiences.map((entry, index) => <fieldset key={index} className="space-y-2 rounded-lg bg-slate-50 p-3">
            <legend className="text-sm font-medium">Experience {index + 1}</legend>
            <div className="grid gap-3 sm:grid-cols-2">
              <label className="text-sm">Type<select className={inputClass} value={entry.kind} onChange={e => setDraft({ ...draft, experiences: draft.experiences.map((x, i) => i === index ? { ...x, kind: e.target.value as 'Startup' | 'Technology' } : x) })}><option>Startup</option><option>Technology</option></select></label>
              <label className="text-sm">Domain / technology / skill<input required maxLength={80} className={inputClass} value={entry.area} onChange={e => setDraft({ ...draft, experiences: draft.experiences.map((x, i) => i === index ? { ...x, area: e.target.value } : x) })} /></label>
              <label className="text-sm">Years<input type="number" min={0} max={80} step={0.1} className={inputClass} value={entry.years ?? ''} onChange={e => setDraft({ ...draft, experiences: draft.experiences.map((x, i) => i === index ? { ...x, years: e.target.value === '' ? null : Number(e.target.value) } : x) })} /></label>
              <label className="text-sm">Level<input maxLength={40} className={inputClass} value={entry.level ?? ''} onChange={e => setDraft({ ...draft, experiences: draft.experiences.map((x, i) => i === index ? { ...x, level: e.target.value } : x) })} placeholder="e.g. Advanced" /></label>
            </div>
            <label className="block text-sm">Notes<textarea rows={2} maxLength={1000} className={inputClass} value={entry.notes ?? ''} onChange={e => setDraft({ ...draft, experiences: draft.experiences.map((x, i) => i === index ? { ...x, notes: e.target.value } : x) })} /></label>
            <Button variant="ghost" size="sm" onClick={() => setDraft({ ...draft, experiences: draft.experiences.filter((_, i) => i !== index) })}>Remove experience</Button>
          </fieldset>)}
          <Button variant="outline" disabled={draft.experiences.length >= 40} onClick={() => setDraft({ ...draft, experiences: [...draft.experiences, { kind: 'Startup', area: '', years: null, level: null, notes: null }] })}>Add experience</Button>
        </section>
        <label className="block text-sm font-medium">Additional experience<textarea maxLength={4000} rows={2} value={draft.experience ?? ''} onChange={e => setDraft({ ...draft, experience: e.target.value })} className={inputClass} /></label>
        <div className="grid gap-4 sm:grid-cols-2">{(['linkedInUrl', 'portfolioUrl'] as const).map(key => <label key={key} className="text-sm font-medium">{key === 'linkedInUrl' ? 'LinkedIn HTTPS URL' : 'Portfolio HTTPS URL'}
          <input type="url" pattern="https://.*" maxLength={key === 'linkedInUrl' ? 500 : 1000} value={draft[key] ?? ''} onChange={e => setDraft({ ...draft, [key]: e.target.value })} className={inputClass} />
        </label>)}</div>
      </fieldset>
      <p className="text-sm text-slate-500">Manage availability by semester using the Availability button after saving the profile.</p>
      <div className="flex justify-end gap-3"><Button variant="outline" disabled={saving} onClick={onClose}>Cancel</Button><Button type="submit" isLoading={saving}>Save profile</Button></div>
    </form>
  </Modal>;
}
