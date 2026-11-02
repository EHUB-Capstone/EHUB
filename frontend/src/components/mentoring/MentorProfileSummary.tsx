import { useState } from 'react';
import { Building2, Download, FileText, Link2 } from 'lucide-react';
import Button from '../ui/Button';
import type { MentorProfile } from '../../types/mentoring';
import { parseMentorCvFormBio, parseMentorProfessionalExperience } from '../../utils/mentorDirectoryBio';

interface MentorProfileSummaryProps {
  profile: MentorProfile;
  onDownload: (kind: 'cv' | 'portfolio') => void;
  downloading: 'cv' | 'portfolio' | null;
}

function ProfileLink({ label, value }: { label: string; value: string | null }) {
  if (!value?.trim()) return null;
  let href: string | undefined;
  try {
    const url = new URL(value);
    if (url.protocol === 'https:') href = url.href;
  } catch { /* Show saved text when it is not a valid HTTPS link. */ }
  return <div className="min-w-0">
    <dt className="text-caption text-slate-500">{label}</dt>
    <dd className="mt-1 break-all text-body">{href
      ? <a href={href} target="_blank" rel="noopener noreferrer" className="inline-flex items-start gap-2 text-primary-700 hover:underline"><Link2 aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0" />{value}</a>
      : value}</dd>
  </div>;
}

export default function MentorProfileSummary({ profile, onDownload, downloading }: MentorProfileSummaryProps) {
  const [bioExpanded, setBioExpanded] = useState(false);
  const bio = profile.bio?.trim() ?? '';
  const parsedBio = parseMentorCvFormBio(bio);
  const workExperience = parseMentorProfessionalExperience(profile.experience);
  const tagGroups = [
    { title: 'Expertise', values: profile.expertise },
    { title: 'Skills', values: profile.technologySkills },
    { title: 'Startup domains', values: profile.startupDomains },
    { title: 'Mentor tags', values: profile.tags },
  ];
  return <div className="space-y-6" aria-label="Saved mentor profile">
    <div className="flex flex-wrap items-center gap-3 rounded-xl bg-slate-50 px-4 py-3 text-body">
      <span className="font-semibold text-slate-900">{profile.mentorType === 'Business' ? 'Business mentor' : profile.mentorType === 'IT' ? 'IT / technical mentor' : 'Mentor type not assigned'}</span>
      {profile.organization?.trim() && <span className="inline-flex items-center gap-1.5 text-slate-600"><Building2 aria-hidden="true" className="h-4 w-4" />{profile.organization}</span>}
    </div>

    {tagGroups.filter(group => group.values.length > 0 || group.title === 'Skills').map(group => <section key={group.title}>
      <h3 className="text-subheading text-secondary">{group.title}</h3>
      {group.values.length > 0 ? <ul className="mt-2 flex flex-wrap gap-2" aria-label={group.title}>{group.values.map(value =>
        <li key={value} className="max-w-full break-words rounded-lg border border-slate-200 bg-slate-50 px-3 py-1.5 text-caption font-medium text-slate-700">{value}</li>)}</ul>
        : <p className="mt-2 text-body text-slate-500">No skills added yet.</p>}
    </section>)}

    {profile.history && <section className="border-t border-slate-200 pt-5" aria-labelledby="mentor-recorded-history">
      <h3 id="mentor-recorded-history" className="text-subheading text-secondary">Recorded mentoring history</h3>
      <dl className="mt-3 grid gap-4 sm:grid-cols-3">
        <div><dt className="text-caption text-slate-500">Completed sessions</dt><dd className="mt-1 text-body font-semibold text-secondary">{profile.history.completedSessionCount}</dd></div>
        <div><dt className="text-caption text-slate-500">Teams coached in completed sessions</dt><dd className="mt-1 text-body font-semibold text-secondary">{profile.history.mentoredTeamCount}</dd></div>
        <div><dt className="text-caption text-slate-500">Projects with recorded achievements</dt><dd className="mt-1 text-body font-semibold text-secondary">{profile.history.achievementCount}</dd></div>
      </dl>
      <p className="mt-3 text-caption text-slate-500">Completed mentoring sessions inform skill matching. Project achievements are counted when recorded during this mentor’s assignment.</p>
    </section>}

    <section className="border-t border-slate-200 pt-5">
      <h3 className="text-subheading text-secondary">Professional bio</h3>
      {parsedBio ? <div className="mt-3 space-y-5">
        {parsedBio.education.length > 0 && <div>
          <h4 className="text-body font-semibold text-slate-800">Education</h4>
          <ol className="mt-3 space-y-3 border-l-2 border-primary-100 pl-4">
            {parsedBio.education.map((entry, index) => <li key={`${entry.period}-${index}`} className="relative rounded-xl border border-slate-200 bg-white p-4 before:absolute before:-left-[1.35rem] before:top-5 before:h-2.5 before:w-2.5 before:rounded-full before:bg-primary">
              <p className="text-caption font-semibold text-primary-700">{entry.period}</p>
              <p className="mt-1 text-body font-semibold text-secondary">{entry.institution}</p>
              {entry.details.length > 0 && <ul className="mt-2 flex flex-wrap gap-2">
                {entry.details.map((detail, detailIndex) => <li key={`${detail}-${detailIndex}`} className="rounded-md bg-slate-50 px-2.5 py-1 text-caption text-slate-600">{detail}</li>)}
              </ul>}
            </li>)}
          </ol>
        </div>}
        {parsedBio.thesis.length > 0 && <div>
          <h4 className="text-body font-semibold text-slate-800">Thesis topic</h4>
          <ul className="mt-2 space-y-2">{parsedBio.thesis.map((detail, index) => <li key={`${detail}-${index}`} className="rounded-lg bg-slate-50 px-3 py-2 text-body-sm leading-6 text-slate-600">{detail}</li>)}</ul>
        </div>}
        {parsedBio.languagesAndCertificates.length > 0 && <div>
          <h4 className="text-body font-semibold text-slate-800">Languages & certificates</h4>
          <ul id="mentor-professional-bio-details" className="mt-2 grid gap-2 sm:grid-cols-2">
            {(bioExpanded ? parsedBio.languagesAndCertificates : parsedBio.languagesAndCertificates.slice(0, 6)).map((detail, index) => <li key={`${detail}-${index}`} className="rounded-lg bg-slate-50 px-3 py-2 text-body-sm leading-6 text-slate-600">{detail}</li>)}
          </ul>
          {parsedBio.languagesAndCertificates.length > 6 && <button type="button" aria-expanded={bioExpanded} aria-controls="mentor-professional-bio-details"
            onClick={() => setBioExpanded(expanded => !expanded)} className="mt-2 text-body-sm font-medium text-primary-700 hover:underline">
            {bioExpanded ? 'Thu gọn' : `Xem thêm ${parsedBio.languagesAndCertificates.length - 6} mục`}
          </button>}
        </div>}
      </div> : <>
        <p id="mentor-professional-bio" className={`mt-2 whitespace-pre-wrap break-words text-body leading-7 text-slate-600 ${bio && !bioExpanded ? 'line-clamp-3' : ''}`}>
          {bio || 'No bio added yet.'}
        </p>
        {bio.length > 180 && <button type="button" aria-expanded={bioExpanded} aria-controls="mentor-professional-bio"
          onClick={() => setBioExpanded(expanded => !expanded)} className="mt-2 text-body-sm font-medium text-primary-700 hover:underline">
          {bioExpanded ? 'Thu gọn' : 'Xem đầy đủ'}
        </button>}
      </>}
    </section>
    <section className="border-t border-slate-200 pt-5">
      <h3 className="text-subheading text-secondary">Work experience</h3>
      {workExperience.length > 0 ? <ul className="mt-3 space-y-2">{workExperience.map((entry, index) =>
        <li key={`${entry}-${index}`} className="rounded-lg bg-slate-50 px-3 py-2 text-body leading-6 text-slate-600">{entry}</li>)}</ul>
        : <p className="mt-2 text-body text-slate-500">No work experience added yet.</p>}
      {profile.experiences.length > 0 && <ul className="mt-4 space-y-3">{profile.experiences.map(entry =>
        <li key={`${entry.kind}:${entry.area}`} className="rounded-xl bg-slate-50 p-4 text-body">
          <p className="font-medium text-slate-900">{entry.area}</p>
          <p className="mt-1 text-slate-500">{[entry.kind, entry.years !== null ? `${entry.years} years` : null, entry.level].filter(Boolean).join(' · ')}</p>
          {entry.notes && <p className="mt-2 whitespace-pre-wrap break-words text-slate-600">{entry.notes}</p>}
        </li>)}</ul>}
    </section>

    {(profile.linkedInUrl || profile.portfolioUrl) && <dl className="grid gap-4 border-t border-slate-200 pt-5 sm:grid-cols-2">
      <ProfileLink label="LinkedIn" value={profile.linkedInUrl} />
      <ProfileLink label="Portfolio website" value={profile.portfolioUrl} />
    </dl>}

    <section className="border-t border-slate-200 pt-5">
      <h3 className="text-subheading text-secondary">Documents</h3>
      <div className="mt-3 space-y-3">{(['cv', 'portfolio'] as const).map(kind => {
        const fileName = kind === 'cv' ? profile.cvFileName : profile.portfolioFileName;
        return <div key={kind} className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200 p-4">
          <FileText aria-hidden="true" className="h-5 w-5 shrink-0 text-primary" />
          <div className="min-w-0 flex-1"><p className="text-body font-medium text-slate-900">{kind === 'cv' ? 'CV / résumé' : 'Portfolio'}</p>
            <p className="mt-0.5 break-all text-caption text-slate-500">{fileName || 'No document uploaded'}</p></div>
          {fileName && <Button variant="outline" size="sm" icon={Download} isLoading={downloading === kind} disabled={downloading !== null}
            aria-label={kind === 'cv' ? 'Download CV' : 'Download portfolio'} onClick={() => onDownload(kind)}>Download</Button>}
        </div>;
      })}</div>
    </section>
  </div>;
}
