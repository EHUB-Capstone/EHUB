import { useState } from 'react';
import {
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  ChevronDown,
  Clock3,
  GraduationCap,
  Loader2,
  Lock,
  RefreshCw,
  Rocket,
  UserPlus,
} from 'lucide-react';
import { getMajorName, TEAM_MAJOR_GROUPS } from '../../constants/majors';
import { isMissingTeamMajor, isVerifiedEnrollmentMajor } from '../../utils/teamManagement';

export type StudentMajorNextAction = {
  label: string;
  kind: 'workspace' | 'team' | 'review' | 'retry';
  onClick: () => void;
};

interface Props {
  currentMajor?: string | null;
  canEdit: boolean;
  isLocked: boolean;
  updating: boolean;
  lecturerName?: string | null;
  verificationStatus?: string | null;
  nextAction?: StudentMajorNextAction;
  onSave: (majorCode: string) => Promise<boolean>;
}

export default function StudentMajorActionCard({
  currentMajor,
  canEdit,
  isLocked,
  updating,
  lecturerName,
  verificationStatus,
  nextAction,
  onSave,
}: Props) {
  const [editing, setEditing] = useState(false);
  const [draftMajor, setDraftMajor] = useState('');
  const isMissing = isMissingTeamMajor(currentMajor);
  const normalizedMajor = isMissing ? '' : currentMajor?.trim().toUpperCase() || '';
  const majorName = getMajorName(normalizedMajor);
  const isVerified = isVerifiedEnrollmentMajor(verificationStatus);
  const normalizedStatus = verificationStatus?.trim().toUpperCase() || 'UNVERIFIED';

  const openEditor = () => {
    setDraftMajor(normalizedMajor);
    setEditing(true);
  };

  const closeEditor = () => {
    if (updating) return;
    setDraftMajor(normalizedMajor);
    setEditing(false);
  };

  const saveMajor = async () => {
    if (!draftMajor || updating || draftMajor === normalizedMajor) return;
    if (await onSave(draftMajor)) setEditing(false);
  };

  if (isMissing) {
    return (
      <section
        id="major-action-card"
        aria-labelledby="major-action-title"
        className="overflow-hidden rounded-2xl border border-orange-200 bg-orange-50/80 shadow-sm"
      >
        <div className="flex flex-col gap-4 p-4 sm:flex-row sm:items-center sm:justify-between sm:p-5">
          <div className="flex min-w-0 items-start gap-3.5">
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-primary text-white shadow-sm">
              <GraduationCap className="h-5 w-5" />
            </div>
            <div className="min-w-0">
              <span className="inline-flex items-center gap-1 rounded-full border border-orange-200 bg-white px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-orange-700">
                <AlertTriangle className="h-3 w-3" /> Action Required
              </span>
              <h2 id="major-action-title" className="mt-2 text-base font-bold text-slate-900 sm:text-lg">
                Select your major to get started
              </h2>
              <p className="mt-1 max-w-2xl text-sm leading-5 text-slate-600">
                E-HUB uses your major when forming teams and keeps it synchronized with your profile and active classes. Select it before creating or joining a team.
              </p>
            </div>
          </div>

          {!editing && (canEdit || nextAction?.kind === 'workspace') && (
            <div className="flex shrink-0 flex-wrap items-center gap-2">
              {canEdit && (
                <button
                  id="select-major-action"
                  type="button"
                  onClick={openEditor}
                  className="inline-flex min-h-10 shrink-0 items-center justify-center gap-2 rounded-xl border border-orange-200 bg-white px-4 py-2.5 text-sm font-bold text-orange-800 shadow-sm transition hover:bg-orange-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500/20"
                >
                  Select Your Major <ChevronDown className="h-4 w-4" />
                </button>
              )}
              {nextAction?.kind === 'workspace' && (
                <button
                  id="major-next-action"
                  type="button"
                  onClick={nextAction.onClick}
                  className="inline-flex min-h-10 shrink-0 items-center justify-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-sm font-bold text-white shadow-sm transition hover:bg-primary-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 focus-visible:ring-offset-2"
                >
                  <Rocket className="h-4 w-4" />
                  {nextAction.label}
                  <ArrowRight className="h-4 w-4" />
                </button>
              )}
            </div>
          )}
        </div>

        {canEdit && editing && (
          <div className="border-t border-orange-200/80 bg-white/70 px-4 py-4 sm:px-5">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
              <div className="min-w-0 flex-1">
                <label htmlFor="student-major-select" className="mb-1.5 block text-xs font-bold text-slate-700">
                  Your major <span className="text-red-500">*</span>
                </label>
                <select
                  id="student-major-select"
                  value={draftMajor}
                  disabled={updating}
                  onChange={event => setDraftMajor(event.target.value)}
                  className="min-h-11 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-semibold text-slate-700 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15 disabled:cursor-wait disabled:opacity-60"
                >
                  <option value="">Choose your major</option>
                  {TEAM_MAJOR_GROUPS.map(group => (
                    <optgroup key={group.key} label={group.label}>
                      {group.majors.map(major => (
                        <option key={major.code} value={major.code}>{major.code} — {major.name}</option>
                      ))}
                    </optgroup>
                  ))}
                </select>
                <p className="mt-1.5 text-xs text-slate-500">This updates your profile and active class enrollments together.</p>
              </div>
              <div className="flex shrink-0 gap-2">
                <button
                  type="button"
                  onClick={closeEditor}
                  disabled={updating}
                  className="min-h-10 rounded-xl border border-slate-200 bg-white px-3.5 py-2 text-sm font-semibold text-slate-600 transition hover:bg-slate-50 disabled:opacity-50"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={() => void saveMajor()}
                  disabled={!draftMajor || updating}
                  className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-primary px-4 py-2 text-sm font-bold text-white shadow-sm transition hover:bg-primary-600 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {updating && <Loader2 className="h-4 w-4 animate-spin" />}
                  {updating ? 'Saving...' : 'Save Major'}
                </button>
              </div>
            </div>
          </div>
        )}

        {!canEdit && (
          <div className="flex items-start gap-2 border-t border-orange-200/80 bg-white/60 px-4 py-3 text-sm text-orange-800 sm:px-5">
            <Lock className="mt-0.5 h-4 w-4 shrink-0" />
            <p>
              {isLocked
                ? `Major selection is locked in an active class. Contact ${lecturerName || 'your lecturer'} if this information needs to be corrected.`
                : 'Major selection is unavailable while this class is read-only.'}
            </p>
          </div>
        )}
      </section>
    );
  }

  if (isVerified) {
    const NextActionIcon = nextAction?.kind === 'workspace'
      ? Rocket
      : nextAction?.kind === 'retry'
        ? RefreshCw
        : nextAction?.kind === 'team'
          ? UserPlus
          : ArrowRight;

    return (
      <section
        id="major-action-card"
        aria-labelledby="major-status-title"
        className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm sm:p-5"
      >
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex min-w-0 items-start gap-3.5">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-emerald-50 text-emerald-700">
              <CheckCircle2 className="h-5 w-5" />
            </div>
            <div className="min-w-0">
              <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-emerald-700">
                <CheckCircle2 className="h-3 w-3" /> Verified
              </span>
              <h2 id="major-status-title" className="mt-2 text-sm font-bold text-slate-900">Your Major</h2>
              <p className="mt-0.5 text-sm text-slate-700">
                <span className="font-bold">{normalizedMajor}</span>{majorName ? ` — ${majorName}` : ''}
              </p>
              <p className="mt-0.5 text-xs text-slate-500">Matches the latest official class record.</p>
              {isLocked && (
                <p data-testid="major-locked-notice" className="mt-1 flex items-center gap-1 text-xs font-semibold text-slate-500">
                  <Lock className="h-3 w-3" />
                  Your major is verified and updates are locked. Contact {lecturerName || 'your lecturer'} if it needs to be corrected.
                </p>
              )}
            </div>
          </div>

          {nextAction && (
            <button
              id="major-next-action"
              type="button"
              onClick={nextAction.onClick}
              className="inline-flex min-h-10 shrink-0 items-center justify-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-sm font-bold text-white shadow-sm transition hover:bg-primary-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 focus-visible:ring-offset-2"
            >
              <NextActionIcon className="h-4 w-4" />
              {nextAction.label}
              {nextAction.kind === 'workspace' && <ArrowRight className="h-4 w-4" />}
            </button>
          )}
        </div>
      </section>
    );
  }

  const needsReview = ['MISMATCHED', 'MISSING', 'NOTFOUND'].includes(normalizedStatus);
  const statusLabel = needsReview ? 'Needs Review' : 'Awaiting Verification';
  const statusDescription = normalizedStatus === 'MISMATCHED'
    ? 'Your selected major does not match the latest official class record. Review it or contact your lecturer.'
    : needsReview
      ? 'Your major could not be confirmed from the latest official class record. Contact your lecturer if it is correct.'
      : 'Your major has been selected and is waiting for verification against the official class record.';

  return (
    <section
      id="major-action-card"
      aria-labelledby="major-status-title"
      className="overflow-hidden rounded-2xl border border-amber-200 bg-amber-50/60 shadow-sm"
    >
      <div className="flex flex-col gap-4 p-4 sm:flex-row sm:items-center sm:justify-between sm:p-5">
        <div className="flex min-w-0 items-start gap-3.5">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-amber-100 text-amber-700">
            {needsReview ? <AlertTriangle className="h-5 w-5" /> : <Clock3 className="h-5 w-5" />}
          </div>
          <div className="min-w-0">
            <span className="inline-flex rounded-full bg-amber-100 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-amber-800">
              {statusLabel}
            </span>
            <h2 id="major-status-title" className="mt-2 text-sm font-bold text-slate-900">Your Major</h2>
            <p className="mt-0.5 text-sm text-slate-700">
              <span className="font-bold">{normalizedMajor}</span>{majorName ? ` — ${majorName}` : ''}
            </p>
            <p className="mt-0.5 max-w-2xl text-xs leading-5 text-slate-600">{statusDescription}</p>
            {isLocked && (
              <p className="mt-1 flex items-center gap-1 text-xs font-semibold text-slate-500">
                <Lock className="h-3 w-3" /> Changes are locked by an active class.
              </p>
            )}
          </div>
        </div>

        {!editing && (canEdit || nextAction?.kind === 'workspace') && (
          <div className="flex shrink-0 flex-wrap items-center gap-2">
            {canEdit && (
              <button
                type="button"
                onClick={openEditor}
                className="inline-flex min-h-9 shrink-0 items-center justify-center rounded-xl border border-amber-200 bg-white px-3.5 py-2 text-sm font-semibold text-amber-800 transition hover:bg-amber-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500/20"
              >
                Change Major
              </button>
            )}
            {nextAction?.kind === 'workspace' && (
              <button
                id="major-next-action"
                type="button"
                onClick={nextAction.onClick}
                className="inline-flex min-h-9 shrink-0 items-center justify-center gap-2 rounded-xl bg-primary px-3.5 py-2 text-sm font-bold text-white shadow-sm transition hover:bg-primary-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 focus-visible:ring-offset-2"
              >
                <Rocket className="h-4 w-4" />
                {nextAction.label}
                <ArrowRight className="h-4 w-4" />
              </button>
            )}
          </div>
        )}
      </div>

      {canEdit && editing && (
        <div className="border-t border-amber-200/80 bg-white/70 px-4 py-4 sm:px-5">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <div className="min-w-0 flex-1">
              <label htmlFor="student-major-change" className="mb-1.5 block text-xs font-bold text-slate-700">Choose a new major</label>
              <select
                id="student-major-change"
                value={draftMajor}
                disabled={updating}
                onChange={event => setDraftMajor(event.target.value)}
                className="min-h-11 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-semibold text-slate-700 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15 disabled:cursor-wait disabled:opacity-60"
              >
                {TEAM_MAJOR_GROUPS.map(group => (
                  <optgroup key={group.key} label={group.label}>
                    {group.majors.map(major => (
                      <option key={major.code} value={major.code}>{major.code} — {major.name}</option>
                    ))}
                  </optgroup>
                ))}
              </select>
              <p className="mt-1.5 text-xs text-slate-500">Changing this updates your profile and active class enrollments.</p>
            </div>
            <div className="flex shrink-0 gap-2">
              <button type="button" onClick={closeEditor} disabled={updating} className="min-h-10 rounded-xl border border-slate-200 bg-white px-3.5 py-2 text-sm font-semibold text-slate-600 transition hover:bg-slate-50 disabled:opacity-50">
                Cancel
              </button>
              <button
                type="button"
                onClick={() => void saveMajor()}
                disabled={!draftMajor || draftMajor === normalizedMajor || updating}
                className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-primary px-4 py-2 text-sm font-bold text-white shadow-sm transition hover:bg-primary-600 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {updating && <Loader2 className="h-4 w-4 animate-spin" />}
                {updating ? 'Saving...' : 'Save Changes'}
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
