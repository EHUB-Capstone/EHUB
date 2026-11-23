import type { MentorProfile } from '../../types/mentoring';
import { isMentorCvFormLabel, formatMentorDirectoryBio, parseMentorCvFormBio, parseMentorProfessionalExperience } from '../../utils/mentorDirectoryBio';
import Modal from '../ui/Modal';
import Badge from '../ui/Badge';
import Button from '../ui/Button';
import { useTranslation } from 'react-i18next';

interface Props {
  profile: MentorProfile;
  isAdmin: boolean;
  onClose: () => void;
  onDownload: (profile: MentorProfile, kind: 'cv' | 'portfolio') => void;
  onEdit: (profile: MentorProfile) => void;
  onAvailability: (profile: MentorProfile) => void;
}

export default function MentorDetailModal({ profile, isAdmin, onClose, onDownload, onEdit, onAvailability }: Props) {
  const { t } = useTranslation('mentoring');
  const bio = formatMentorDirectoryBio(profile.bio);
  const parsedBio = parseMentorCvFormBio(profile.bio);
  const workExperience = parseMentorProfessionalExperience(profile.experience);
  const tags = [...new Set([...profile.expertise, ...profile.technologySkills, ...profile.startupDomains, ...profile.tags]
    .filter(value => value.trim() && !isMentorCvFormLabel(value)))];

  return <Modal isOpen onClose={onClose} title={profile.fullName} size="xl">
    <div className="space-y-4 text-body text-slate-700">
      <div className="flex flex-wrap items-center gap-2">
        <Badge variant="neutral">{profile.mentorType}</Badge>
        <Badge variant={profile.status}>{profile.status}</Badge>
        <span className="text-body text-slate-600">{profile.organization || t('mentorDetailModal.independent')}</span>
      </div>
      {isAdmin && <p className="text-body text-slate-500">{profile.email}</p>}

      {parsedBio ? <>
        {parsedBio.education.length > 0 && <section aria-label={t('mentorDetailModal.education')}>
          <h3 className="text-overline text-slate-500">{t('mentorDetailModal.education')}</h3>
          <ol className="mt-2 space-y-2 border-l-2 border-primary-100 pl-3">
            {parsedBio.education.map((entry, index) => <li key={`${entry.period}-${index}`} className="rounded-lg bg-slate-50 px-3 py-2">
              <p className="text-caption font-semibold text-primary-700">{entry.period}</p>
              <p className="mt-0.5 text-body-sm font-semibold text-secondary">{entry.institution}</p>
              {entry.details.length > 0 && <p className="mt-1 text-caption leading-5 text-slate-600">{entry.details.join(' · ')}</p>}
            </li>)}
          </ol>
        </section>}
        {parsedBio.thesis.length > 0 && <section aria-label={t('mentorDetailModal.thesisTopic')}>
          <h3 className="text-overline text-slate-500">{t('mentorDetailModal.thesisTopic')}</h3>
          <ul className="mt-1 space-y-1">{parsedBio.thesis.map((detail, index) => <li key={`${detail}-${index}`} className="rounded-lg bg-slate-50 px-3 py-2 text-body-sm leading-5 text-slate-600">{detail}</li>)}</ul>
        </section>}
        {parsedBio.languagesAndCertificates.length > 0 && <section aria-label={t('mentorDetailModal.languagesAndCertificates')}>
          <h3 className="text-overline text-slate-500">{t('mentorDetailModal.languagesCertificates')}</h3>
          <ul className="mt-2 grid gap-1.5 sm:grid-cols-2">{parsedBio.languagesAndCertificates.map((detail, index) =>
            <li key={`${detail}-${index}`} className="rounded-lg border border-slate-100 px-3 py-2 text-body-sm leading-5 text-slate-600">{detail}</li>)}</ul>
        </section>}
      </> : <section aria-label={t('mentorDetailModal.professionalBio')}>
        <h3 className="text-overline text-slate-500">{t('mentorDetailModal.professionalBio')}</h3>
        <p className="mt-1 whitespace-pre-line text-body text-slate-700">{bio || t('mentorDetailModal.noBiographyYet')}</p>
      </section>}

      {workExperience.length > 0 && <section aria-label={t('mentorDetailModal.professionalExperience')}>
        <h3 className="text-overline text-slate-500">{t('mentorDetailModal.professionalExperience')}</h3>
        <ul className="mt-1 space-y-1.5">{workExperience.map((entry, index) =>
          <li key={`${entry}-${index}`} className="rounded-lg bg-slate-50 px-3 py-2 text-body-sm leading-5 text-slate-600">{entry}</li>)}</ul>
      </section>}
      {tags.length > 0 && <section aria-label={t('mentorDetailModal.areasAndSkills')}>
        <h3 className="text-overline text-slate-500">{t('mentorDetailModal.areasSkills')}</h3>
        <div className="mt-1 flex flex-wrap gap-1">{tags.map(skill => <Badge key={skill} variant="neutral">{skill}</Badge>)}</div>
      </section>}
      {profile.experiences.length > 0 && <section aria-label={t('mentorDetailModal.structuredExperience')}>
        <h3 className="text-overline text-slate-500">{t('mentorDetailModal.experience')}</h3>
        <ul className="mt-1 space-y-1 text-body-sm text-slate-600">{profile.experiences.map(entry =>
          <li key={`${entry.kind}:${entry.area}`}><span className="font-medium">{entry.kind} · {entry.area}</span>{entry.years !== null && t('mentorDetailModal.years', { years: entry.years })}{entry.level && ` · ${entry.level}`}{entry.notes && <p className="mt-0.5">{entry.notes}</p>}</li>)}</ul>
      </section>}

      <dl className="grid grid-cols-2 gap-2 border-t border-slate-200 pt-3 text-body sm:grid-cols-4">
        <div><dt className="text-slate-500">{t('mentorDetailModal.activeTeams')}</dt><dd className="font-semibold">{profile.activeTeamCount}</dd></div>
        <div><dt className="text-slate-500">{t('mentorDetailModal.assignments')}</dt><dd className="font-semibold">{profile.totalAssignments}</dd></div>
        <div><dt className="text-slate-500">{t('mentorDetailModal.sessions')}</dt><dd className="font-semibold">{profile.totalSessions}</dd></div>
        <div><dt className="text-slate-500">{t('mentorDetailModal.studentRating')}</dt><dd className="font-semibold">{profile.averageFeedbackRating?.toFixed(1) ?? '—'}</dd></div>
      </dl>
      <div className="flex flex-wrap gap-3 text-body text-primary-700">
        {profile.linkedInUrl && <a href={profile.linkedInUrl} target="_blank" rel="noopener noreferrer" className="underline">{t('mentorDetailModal.linkedin')}</a>}
        {profile.portfolioUrl && <a href={profile.portfolioUrl} target="_blank" rel="noopener noreferrer" className="underline">{t('mentorDetailModal.portfolioLink')}</a>}
        {profile.cvFileName && <button type="button" onClick={() => onDownload(profile, 'cv')} className="underline">{t('mentorDetailModal.downloadCv')}</button>}
        {profile.portfolioFileName && <button type="button" onClick={() => onDownload(profile, 'portfolio')} className="underline">{t('mentorDetailModal.downloadPortfolio')}</button>}
      </div>
      {isAdmin && <div className="flex flex-wrap gap-2 border-t border-slate-200 pt-3">
        <Button variant="outline" size="sm" onClick={() => onEdit(profile)}>{t('mentorDetailModal.editProfile')}</Button>
        <Button variant="outline" size="sm" onClick={() => onAvailability(profile)}>{t('mentorDetailModal.availability')}</Button>
      </div>}
    </div>
  </Modal>;
}
