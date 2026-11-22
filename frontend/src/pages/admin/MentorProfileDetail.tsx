import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { AlertCircle, ArrowLeft, Loader2, Pencil, RotateCcw, Save, X } from 'lucide-react';
import { mentorProfileApi } from '../../api/mentorProfileApi';
import MentorKindTag from '../../components/admin/MentorKindTag';
import TagInput from '../../components/admin/TagInput';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import type { MentorProfile, MentorProfileStatus, MentorTagSuggestions } from '../../types/mentorProfile';
import { parseApiError } from '../../utils/apiError';
import {
  MENTOR_PROFILE_LIMITS,
  MENTOR_PROFILE_STATUSES,
  addExpertiseTag,
  addTagOfKind,
  hasProfileChanges,
  profileToFormValues,
  toUpdatePayload,
  validateMentorProfileForm,
  type MentorProfileFormErrors,
  type MentorProfileFormValues,
} from '../../utils/mentorProfileForm';
import { useTranslation } from 'react-i18next';
import i18n from '../../i18n';

const statusBadge: Record<MentorProfileStatus, string> = { get Active() { return i18n.t('admin:classManagement.active'); }, get Inactive() { return i18n.t('admin:classManagement.inactive'); }, get Unavailable() { return i18n.t('admin:mentorProfileDetail.atRisk'); } };
const inputClass = 'w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:bg-slate-50';

export default function MentorProfileDetail() {
  const { t } = useTranslation('admin');
  const { profileId = '' } = useParams();
  const [profile, setProfile] = useState<MentorProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [editing, setEditing] = useState(false);
  const [values, setValues] = useState<MentorProfileFormValues | null>(null);
  const [errors, setErrors] = useState<MentorProfileFormErrors>({});
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [conflict, setConflict] = useState(false);
  const [suggestions, setSuggestions] = useState<MentorTagSuggestions | null>(null);

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setLoadError('');
    try {
      const response = await mentorProfileApi.get(profileId, signal);
      setProfile(response.data);
    } catch (error: unknown) {
      if (!signal?.aborted) setLoadError(parseApiError(error, 'The mentor profile could not be loaded.').message);
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }, [profileId]);

  useEffect(() => {
    const controller = new AbortController();
    // oxlint-disable-next-line react/set-state-in-effect -- fetch the profile when the page opens
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  const startEditing = () => {
    if (!profile) return;
    setValues(profileToFormValues(profile));
    setErrors({});
    setSaveError('');
    setConflict(false);
    setEditing(true);
    // Suggestions only help the admin spell a tag like the others; the form works without them.
    mentorProfileApi.getTagSuggestions().then(response => setSuggestions(response.data)).catch(() => setSuggestions(null));
  };

  const cancelEditing = () => {
    if (saving) return;
    setEditing(false);
    setValues(null);
    setErrors({});
    setSaveError('');
    setConflict(false);
  };

  const set = <K extends keyof MentorProfileFormValues>(key: K, value: MentorProfileFormValues[K]) => {
    setValues(current => (current ? { ...current, [key]: value } : current));
    if (errors[key]) setErrors(current => ({ ...current, [key]: undefined }));
  };

  const save = async () => {
    if (!profile || !values || saving) return;
    const found = validateMentorProfileForm(values);
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    setSaving(true);
    setSaveError('');
    try {
      const response = await mentorProfileApi.update(profile.id, toUpdatePayload(values, profile.rowVersion));
      setProfile(response.data);
      setEditing(false);
      setValues(null);
      toast.success(t('mentorProfileDetail.mentorProfileSaved'));
    } catch (error: unknown) {
      // The form stays open with everything the admin typed, so nothing is lost.
      const parsed = parseApiError(error, 'The mentor profile could not be saved.');
      setSaveError(parsed.message);
      setConflict(parsed.code === 'MENTOR_PROFILE_CONFLICT');
    } finally {
      setSaving(false);
    }
  };

  const reloadAfterConflict = async () => {
    await load();
    setEditing(false);
    setValues(null);
    setConflict(false);
    setSaveError('');
  };

  if (loading && !profile) {
    return <div className="mx-auto max-w-5xl space-y-4"><LoadingSkeleton lines={8} /></div>;
  }

  if (loadError || !profile) {
    return (
      <div className="mx-auto flex max-w-xl flex-col items-center gap-3 py-16 text-center">
        <AlertCircle className="h-7 w-7 text-red-500" />
        <p className="text-sm text-red-700">{loadError || t('mentorProfileDetail.theMentorProfileWasNotFound')}</p>
        <div className="flex gap-2">
          <Link to="/admin/users" className="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50">{t('mentorProfileDetail.backToUsers')}</Link>
          <Button size="sm" variant="outline" icon={RotateCcw} onClick={() => void load()}>{t('classManagement.retry')}</Button>
        </div>
      </div>
    );
  }

  const form = editing ? values : null;
  const dirty = form ? hasProfileChanges(profile, form) : false;
  const statusChanged = form !== null && form.status !== 'Active' && profile.activeTeamCount > 0;

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <Link to="/admin/users" className="inline-flex items-center gap-1.5 text-xs font-semibold text-slate-500 hover:text-primary-700">
        <ArrowLeft className="h-3.5 w-3.5" /> {t('mentorProfileDetail.backToUsers')}
      </Link>

      <header className="flex flex-col gap-4 rounded-2xl border border-slate-200/70 bg-white p-5 shadow-sm sm:flex-row sm:items-center sm:justify-between">
        <div className="flex min-w-0 items-center gap-4">
          {profile.avatarUrl ? (
            <img src={profile.avatarUrl} alt="" className="h-14 w-14 shrink-0 rounded-2xl object-cover" />
          ) : (
            <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-primary-100 text-xl font-bold text-primary-700">{profile.fullName.charAt(0).toUpperCase()}</div>
          )}
          <div className="min-w-0">
            <h1 className="truncate text-heading sm:text-display text-secondary tracking-tight">{profile.fullName}</h1>
            <p className="truncate text-sm text-slate-500">{profile.email}{profile.phone ? ` · ${profile.phone}` : ''}</p>
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <MentorKindTag type={profile.mentorType} />
              <Badge variant={statusBadge[profile.status]} size="sm">{profile.status}</Badge>
              <span className="text-xs text-slate-500">{t('mentorProfileDetail.activeTeams', { count: profile.activeTeamCount })}</span>
            </div>
          </div>
        </div>
        {!editing && <Button icon={Pencil} onClick={startEditing}>{t('mentorProfileDetail.editProfile')}</Button>}
      </header>

      {editing && form && (
        <div className="space-y-4">
          {saveError && (
            <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              <span className="flex items-start gap-2"><AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />{saveError}</span>
              {conflict && <Button size="sm" variant="outline" icon={RotateCcw} onClick={() => void reloadAfterConflict()}>{t('mentorProfileDetail.reloadTheLatestProfile')}</Button>}
            </div>
          )}

          <Section title={t('mentorProfileDetail.about')} description={t('mentorProfileDetail.whoTheMentorIsAndWhat')}>
            <TagInput
              label={t('mentorProfileDetail.expertise')}
              value={form.expertise}
              onChange={tags => set('expertise', tags)}
              addTag={addExpertiseTag}
              suggestions={suggestions?.expertise}
              disabled={saving}
              placeholder={t('mentorProfileDetail.eGMarketingFundraisingPitching')}
              hint={t('mentorProfileDetail.whatThisMentorCanCoachPress', { expertiseItems: MENTOR_PROFILE_LIMITS.expertiseItems })}
            />
            <div className="grid gap-4 md:grid-cols-2">
              <TagInput
                label={t('mentorProfileDetail.startupDomain')}
                value={form.startupDomains}
                onChange={tags => set('startupDomains', tags)}
                addTag={addTagOfKind('startup domain')}
                suggestions={suggestions?.startupDomains}
                disabled={saving}
                placeholder={t('mentorProfileDetail.eGFintechEdtechHealthtech')}
                hint={t('mentorProfileDetail.fieldsOfStartupsTheMentorKnows')}
              />
              <TagInput
                label={t('mentorProfileDetail.technologySkills')}
                value={form.technologySkills}
                onChange={tags => set('technologySkills', tags)}
                addTag={addTagOfKind('technology skill')}
                suggestions={suggestions?.technologySkills}
                disabled={saving}
                placeholder={t('mentorProfileDetail.eGReactNetMachineLearning')}
                hint={t('mentorProfileDetail.technologiesTheMentorWorksWith')}
              />
            </div>
            <TagInput
              label={t('mentorProfileDetail.mentorTags')}
              value={form.mentorTags}
              onChange={tags => set('mentorTags', tags)}
              addTag={addTagOfKind('mentor tag')}
              suggestions={suggestions?.mentorTags}
              disabled={saving}
              placeholder={t('mentorProfileDetail.eGAlumniInvestor')}
              hint={t('mentorProfileDetail.freeLabelsToGroupOrFind')}
            />
            <Field label={t('mentorProfileDetail.background')} error={errors.bio} counter={`${form.bio.length}/${MENTOR_PROFILE_LIMITS.bio}`}>
              <textarea rows={5} value={form.bio} disabled={saving} onChange={event => set('bio', event.target.value)} className={inputClass} placeholder={t('mentorProfileDetail.experienceAchievementsAndTheAreasThis')} />
            </Field>
          </Section>

          <Section title={t('mentorProfileDetail.availability')} description={t('mentorProfileDetail.onlyActiveMentorsCanBeAssigned')}>
            <Field label={t('mentorProfileDetail.status')}>
              <select value={form.status} disabled={saving} onChange={event => set('status', event.target.value as MentorProfileStatus)} className={inputClass}>
                {MENTOR_PROFILE_STATUSES.map(option => <option key={option.value} value={option.value}>{option.label} — {option.hint}</option>)}
              </select>
            </Field>
            {statusChanged && (
              <p className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                {t('mentorProfileDetail.statusChangeNotice', { count: profile.activeTeamCount })}
              </p>
            )}
            <Field label={t('mentorProfileDetail.availabilityNote')} error={errors.availabilityNote} counter={`${form.availabilityNote.length}/${MENTOR_PROFILE_LIMITS.availabilityNote}`}>
              <textarea rows={2} value={form.availabilityNote} disabled={saving} onChange={event => set('availabilityNote', event.target.value)} className={inputClass} placeholder={t('mentorProfileDetail.eGWeekdayAfternoonsOnlineOnly')} />
            </Field>
          </Section>

          <Section title={t('mentorProfileDetail.professionalDetails')}>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t('mentorProfileDetail.organization')} error={errors.organization}><input value={form.organization} disabled={saving} onChange={event => set('organization', event.target.value)} className={inputClass} /></Field>
              <Field label={t('mentorProfileDetail.jobTitle')} error={errors.jobTitle}><input value={form.jobTitle} disabled={saving} onChange={event => set('jobTitle', event.target.value)} className={inputClass} /></Field>
              <Field label={t('mentorProfileDetail.department')} error={errors.department}><input value={form.department} disabled={saving} onChange={event => set('department', event.target.value)} className={inputClass} /></Field>
              <Field label={t('mentorProfileDetail.contractType')} error={errors.contractType}><input value={form.contractType} disabled={saving} onChange={event => set('contractType', event.target.value)} className={inputClass} /></Field>
              <Field label={t('mentorProfileDetail.educationLevel')} error={errors.educationLevel}><input value={form.educationLevel} disabled={saving} onChange={event => set('educationLevel', event.target.value)} className={inputClass} /></Field>
              <Field label={t('mentorProfileDetail.dateOfBirth')} error={errors.dateOfBirth}><input type="date" value={form.dateOfBirth} disabled={saving} onChange={event => set('dateOfBirth', event.target.value)} className={inputClass} /></Field>
              <Field label={t('mentorProfileDetail.linkedinUrl')} error={errors.linkedInUrl}><input value={form.linkedInUrl} disabled={saving} onChange={event => set('linkedInUrl', event.target.value)} className={inputClass} placeholder="https://www.linkedin.com/in/..." /></Field>
              <Field label={t('mentorProfileDetail.fptEmail')} error={errors.fptEmail}><input value={form.fptEmail} disabled={saving} onChange={event => set('fptEmail', event.target.value)} className={inputClass} /></Field>
            </div>
            <Field label={t('mentorProfileDetail.address')} error={errors.currentAddress}><input value={form.currentAddress} disabled={saving} onChange={event => set('currentAddress', event.target.value)} className={inputClass} /></Field>
          </Section>

          <div className="sticky bottom-3 flex flex-wrap items-center justify-end gap-2 rounded-2xl border border-slate-200 bg-white/95 p-3 shadow-lg backdrop-blur">
            <p className="mr-auto text-xs text-slate-500">{dirty ? t('mentorProfileDetail.youHaveUnsavedChanges') : t('mentorProfileDetail.noChangesYet')}</p>
            <Button variant="outline" icon={X} onClick={cancelEditing} disabled={saving}>{t('common:actions.cancel')}</Button>
            <Button icon={saving ? Loader2 : Save} onClick={() => void save()} disabled={!dirty || saving} isLoading={saving}>{t('mentorProfileDetail.saveProfile')}</Button>
          </div>
        </div>
      )}

      {!editing && (
        <div className="space-y-4">
          <Section title={t('mentorProfileDetail.about')}>
            <TagRow label={t('mentorProfileDetail.expertise')} tags={profile.expertise} />
            <TagRow label={t('mentorProfileDetail.startupDomain')} tags={profile.startupDomains} />
            <TagRow label={t('mentorProfileDetail.technologySkills')} tags={profile.technologySkills} />
            <TagRow label={t('mentorProfileDetail.mentorTags')} tags={profile.mentorTags} />
            <ReadRow label={t('mentorProfileDetail.background')}>{profile.bio ? <p className="whitespace-pre-wrap text-sm leading-6 text-slate-700">{profile.bio}</p> : <Empty />}</ReadRow>
          </Section>
          <Section title={t('mentorProfileDetail.availability')}>
            <ReadRow label={t('mentorProfileDetail.status')}><Badge variant={statusBadge[profile.status]} size="sm">{profile.status}</Badge></ReadRow>
            <ReadRow label={t('mentorProfileDetail.availabilityNote')}>{profile.availabilityNote ? <p className="whitespace-pre-wrap text-sm text-slate-700">{profile.availabilityNote}</p> : <Empty />}</ReadRow>
          </Section>
          <Section title={t('mentorProfileDetail.professionalDetails')}>
            <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2">
              <Detail label={t('mentorProfileDetail.organization')} value={profile.organization} />
              <Detail label={t('mentorProfileDetail.jobTitle')} value={profile.jobTitle} />
              <Detail label={t('mentorProfileDetail.department')} value={profile.department} />
              <Detail label={t('mentorProfileDetail.contractType')} value={profile.contractType} />
              <Detail label={t('mentorProfileDetail.educationLevel')} value={profile.educationLevel} />
              <Detail label={t('mentorProfileDetail.dateOfBirth')} value={profile.dateOfBirth ? new Date(`${profile.dateOfBirth}T00:00:00`).toLocaleDateString('en-GB') : null} />
              <Detail label={t('mentorProfileDetail.linkedin')} value={profile.linkedInUrl} href={profile.linkedInUrl ?? undefined} />
              <Detail label={t('mentorProfileDetail.fptEmail')} value={profile.fptEmail} />
              <Detail label={t('mentorProfileDetail.address')} value={profile.currentAddress} />
            </dl>
          </Section>
          <p className="text-xs text-slate-400">{t('mentorProfileDetail.nameEmailAndPhoneBelongTo')}</p>
        </div>
      )}
    </div>
  );
}

function Section({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
  return (
    <section className="space-y-4 rounded-2xl border border-slate-200/70 bg-white p-5 shadow-sm">
      <div>
        <h2 className="text-sm font-bold text-slate-900">{title}</h2>
        {description && <p className="mt-0.5 text-xs text-slate-500">{description}</p>}
      </div>
      {children}
    </section>
  );
}

function Field({ label, error, counter, children }: { label: string; error?: string; counter?: string; children: ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 flex items-center justify-between text-xs font-semibold text-slate-700">
        {label}
        {counter && <span className="font-normal text-slate-400">{counter}</span>}
      </span>
      {children}
      {error && <span role="alert" className="mt-1 block text-caption text-red-600">{error}</span>}
    </label>
  );
}

function ReadRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-400">{label}</p>
      {children}
    </div>
  );
}

function TagRow({ label, tags }: { label: string; tags: string[] }) {
  return (
    <ReadRow label={label}>
      {tags.length > 0
        ? <div className="flex flex-wrap gap-1.5">{tags.map(tag => <span key={tag} className="rounded-full border border-primary-100 bg-primary-50 px-2.5 py-0.5 text-xs font-semibold text-primary-700">{tag}</span>)}</div>
        : <Empty />}
    </ReadRow>
  );
}

function Empty() {
  const { t } = useTranslation('admin');
  return <p className="text-sm text-slate-400">{t('mentorProfileDetail.notProvided')}</p>;
}

function Detail({ label, value, href }: { label: string; value?: string | null; href?: string }) {
  const { t } = useTranslation('admin');
  return (
    <div className="min-w-0">
      <dt className="text-xs font-semibold uppercase tracking-wide text-slate-400">{label}</dt>
      <dd className="mt-0.5 truncate text-sm text-slate-700">
        {value ? (href && /^https?:\/\//i.test(href) ? <a href={href} target="_blank" rel="noopener noreferrer" className="text-primary-700 hover:underline">{value}</a> : value) : <span className="text-slate-400">{t('mentorProfileDetail.notProvided')}</span>}
      </dd>
    </div>
  );
}
