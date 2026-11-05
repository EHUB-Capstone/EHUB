import { Link } from 'react-router-dom';
import { FolderKanban, Pencil } from 'lucide-react';
import Card from '../ui/Card';
import { PROJECT_PROFILE_LABELS } from '../../constants/projectProfile';
import type { ShowcaseProfileDto } from '../../types/showcase';

interface ShowcaseProfileSummaryProps {
  profile: ShowcaseProfileDto;
  /** Where the fields are edited; omitted when the viewer cannot edit them. */
  editPath?: string;
}

function Field({ label, value, empty }: { label: string; value: string | null; empty: string }) {
  return (
    <div>
      <dt className="text-body-sm font-semibold text-secondary">{label}</dt>
      <dd className={value?.trim() ? 'mt-1 whitespace-pre-line text-body text-slate-700' : 'mt-1 text-body text-slate-600 italic'}>
        {value?.trim() ? value : empty}
      </dd>
    </div>
  );
}

/**
 * The Project Profile fields a showcase displays. They are read live from the project and edited on the Project Profile
 * page, which keeps one source of truth with the same field names on both pages.
 */
export default function ShowcaseProfileSummary({ profile, editPath }: ShowcaseProfileSummaryProps) {
  return (
    <Card hover={false} className="space-y-4">
      <Card.Header className="mb-0">
        <Card.Title icon={FolderKanban}>Project Profile</Card.Title>
        {editPath && (
          <Link
            to={editPath}
            className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 text-body-sm font-medium text-slate-700 hover:border-slate-400 hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
          >
            <Pencil className="h-4 w-4" aria-hidden="true" />
            Edit in Project Profile
          </Link>
        )}
      </Card.Header>
      <p className="text-body-sm text-slate-600">
        These fields come straight from your Project Profile, so both pages always match.
        Changes to Project name and Description need your lecturer&apos;s approval.
      </p>
      <dl className="space-y-4">
        <Field label={PROJECT_PROFILE_LABELS.projectName} value={profile.projectName} empty="Not provided" />
        <Field label={PROJECT_PROFILE_LABELS.description} value={profile.description} empty="Not provided" />
        <Field label={PROJECT_PROFILE_LABELS.problem} value={profile.problem} empty="Not filled in yet" />
        <Field label={PROJECT_PROFILE_LABELS.solution} value={profile.solution} empty="Not filled in yet" />
        <Field label={PROJECT_PROFILE_LABELS.targetUsers} value={profile.targetUsers} empty="Not filled in yet" />
      </dl>
    </Card>
  );
}
