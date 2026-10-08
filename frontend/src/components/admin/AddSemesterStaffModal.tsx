import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Check, CheckCircle2, CircleAlert, Minus, Search } from 'lucide-react';
import { subjectApi } from '../../api/subjectApi';
import { parseApiError } from '../../utils/apiError';
import {
  getSelectionState,
  keepEligibleSelection,
  setVisibleSelection,
  toggleSelection,
} from '../../utils/mentorBatchAssignment';
import {
  groupCandidates,
  summarizeStaffBatch,
  type MentorKindFilter,
  type StaffBatchSummary,
} from '../../utils/semesterStaffCandidates';
import type {
  AddTeachingStaffBatchResponse,
  SemesterCode,
  TeachingStaffCandidateDto,
} from '../../types/subjects';
import Button from '../ui/Button';
import LoadingSkeleton from '../ui/LoadingSkeleton';
import Modal from '../ui/Modal';
import MentorKindTag from './MentorKindTag';
import MentorTagChips from './MentorTagChips';
import MentorTagFilter from './MentorTagFilter';
import { collectTagOptions, keepKnownTags } from '../../utils/mentorTags';

interface AddSemesterStaffModalProps {
  isOpen: boolean;
  role: TeachingStaffCandidateDto['role'];
  semester: SemesterCode;
  year: number;
  /** Accounts already in this semester's list for the role, whatever their status. */
  existingUserIds: ReadonlySet<string>;
  onClose: () => void;
  /** Called after at least one person was added, so the parent can reload the semester list. */
  onChanged: () => Promise<void> | void;
}

const kindFilters: { value: MentorKindFilter; label: string }[] = [
  { value: 'ALL', label: 'All mentors' },
  { value: 'Enterprise', label: 'Enterprise mentors' },
  { value: 'Academic', label: 'Lecturer mentors' },
];

export default function AddSemesterStaffModal({
  isOpen,
  role,
  semester,
  year,
  existingUserIds,
  onClose,
  onChanged,
}: AddSemesterStaffModalProps) {
  const [candidates, setCandidates] = useState<TeachingStaffCandidateDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadFailed, setLoadFailed] = useState(false);
  const [search, setSearch] = useState('');
  const [mentorType, setMentorType] = useState<MentorKindFilter>('ALL');
  const [tagFilter, setTagFilter] = useState<string[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [summary, setSummary] = useState<StaffBatchSummary | null>(null);
  const mounted = useRef(true);

  const roleNoun = role === 'MENTOR' ? 'mentor' : 'lecturer';
  const semesterLabel = `${semester} ${year}`;

  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  const loadCandidates = useCallback(async () => {
    setLoading(true);
    setLoadFailed(false);
    try {
      const response = await subjectApi.getTeachingStaffCandidates() as { data?: { candidates?: TeachingStaffCandidateDto[] } };
      if (mounted.current) setCandidates(response?.data?.candidates ?? []);
    } catch (error) {
      if (!mounted.current) return;
      setLoadFailed(true);
      setCandidates([]);
      toast.error(parseApiError(error, 'Failed to load eligible lecturers and mentors').message);
    } finally {
      if (mounted.current) setLoading(false);
    }
  }, []);

  // Every time the dialog opens it starts clean and reads the current accounts.
  useEffect(() => {
    if (!isOpen) return;
    setSearch('');
    setMentorType('ALL');
    setSelected([]);
    setSummary(null);
    void loadCandidates();
  }, [isOpen, role, loadCandidates]);

  const tagOptions = useMemo(() => collectTagOptions(candidates.filter(candidate => candidate.role === 'MENTOR')), [candidates]);
  const activeTagFilter = useMemo(() => keepKnownTags(tagFilter, tagOptions), [tagFilter, tagOptions]);
  const groups = useMemo(
    () => groupCandidates(candidates, role, existingUserIds, { search, mentorType, tags: activeTagFilter }),
    [candidates, role, existingUserIds, search, mentorType, activeTagFilter],
  );
  const availableIds = useMemo(() => groups.available.map(item => item.userId), [groups.available]);
  // A tick never survives for an account that is no longer available (for example after a refresh).
  const chosen = useMemo(() => keepEligibleSelection(selected, availableIds), [selected, availableIds]);
  const selectionState = getSelectionState(chosen, availableIds);

  const handleSubmit = async () => {
    if (chosen.length === 0 || submitting) return;
    setSubmitting(true);
    setSummary(null);
    try {
      const response = await subjectApi.addTeachingStaffBatch({ semester, year, role, userIds: chosen }) as { data?: AddTeachingStaffBatchResponse };
      if (!mounted.current || !response?.data) return;

      const nameById = new Map(candidates.map(item => [item.userId, item.name]));
      const result = summarizeStaffBatch(response.data, id => nameById.get(id) ?? 'Unknown account', roleNoun);
      setSummary(result);
      if (result.tone === 'success') toast.success(result.message);
      else if (result.tone === 'error') toast.error(result.message);
      else toast(result.message, { icon: result.tone === 'partial' ? '⚠️' : 'ℹ️' });

      setSelected([]);
      if (response.data.addedCount > 0) await onChanged();
    } catch (error) {
      toast.error(parseApiError(error, `Failed to add ${roleNoun}s to the semester`).message);
    } finally {
      if (mounted.current) setSubmitting(false);
    }
  };

  const submitLabel = chosen.length === 0
    ? `Add to ${semesterLabel}`
    : `Add ${chosen.length} ${roleNoun}${chosen.length === 1 ? '' : 's'}`;
  const hasFilters = Boolean(search.trim()) || (role === 'MENTOR' && (mentorType !== 'ALL' || activeTagFilter.length > 0));

  return (
    <Modal
      isOpen={isOpen}
      onClose={submitting ? () => undefined : onClose}
      title={`Add ${role === 'MENTOR' ? 'mentors' : 'lecturers'} to ${semesterLabel}`}
      size="xl"
      submitText={submitLabel}
      isSubmitting={submitting}
      submitDisabled={chosen.length === 0 || loading}
      onSubmit={handleSubmit}
    >
      <div className="space-y-4">
        <p className="rounded-xl border border-blue-200 bg-blue-50 px-3 py-2 text-xs leading-5 text-blue-700">
          This does not create new accounts. Tick the existing active {roleNoun}s who take part in {semesterLabel}.
          Need a new account?{' '}
          <Link to="/admin/users" className="font-semibold underline">Go to User Management</Link> first.
        </p>

        {summary && (
          <div
            role={summary.tone === 'success' || summary.tone === 'info' ? 'status' : 'alert'}
            className={`rounded-xl px-3 py-2.5 text-xs ${summary.tone === 'success' ? 'bg-green-50 text-green-700' : summary.tone === 'info' ? 'bg-slate-50 text-slate-700' : summary.tone === 'partial' ? 'bg-amber-50 text-amber-800' : 'bg-red-50 text-red-700'}`}
          >
            <p className="flex items-center gap-1.5 font-semibold">
              {summary.tone === 'success' ? <CheckCircle2 className="h-4 w-4 shrink-0" /> : <CircleAlert className="h-4 w-4 shrink-0" />}
              {summary.message}
            </p>
            {summary.problems.length > 0 && (
              <ul className="mt-1.5 list-disc space-y-0.5 pl-6">
                {summary.problems.map(problem => <li key={problem.userId}><strong>{problem.name}</strong>: {problem.message}</li>)}
              </ul>
            )}
          </div>
        )}

        <div className="flex flex-col gap-2 sm:flex-row">
          <div className="relative min-w-0 flex-1">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <input
              type="search"
              value={search}
              onChange={event => setSearch(event.target.value)}
              placeholder={`Search ${roleNoun}s by name, email${role === 'MENTOR' ? ', contract or tag' : ''}...`}
              aria-label={`Search ${roleNoun}s`}
              className="w-full rounded-xl border border-slate-200 bg-white py-2 pl-9 pr-3 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            />
          </div>
          {role === 'MENTOR' && (
            <div role="group" aria-label="Mentor kind" className="inline-flex shrink-0 rounded-xl border border-slate-200 bg-white p-0.5">
              {kindFilters.map(option => (
                <button
                  key={option.value}
                  type="button"
                  aria-pressed={mentorType === option.value}
                  onClick={() => setMentorType(option.value)}
                  className={`rounded-lg px-3 py-1.5 text-xs font-semibold transition-colors ${mentorType === option.value ? 'bg-primary text-white' : 'text-slate-600 hover:text-slate-900'}`}
                >
                  {option.label}
                </button>
              ))}
            </div>
          )}
        </div>

        {role === 'MENTOR' && (
          <MentorTagFilter options={tagOptions} selected={activeTagFilter} onChange={setTagFilter} disabled={submitting} />
        )}

        {loading ? (
          <LoadingSkeleton variant="text" lines={5} />
        ) : loadFailed ? (
          <div className="rounded-xl bg-red-50 px-3 py-4 text-center text-sm text-red-700">
            <p>Could not load the accounts.</p>
            <Button size="sm" variant="outline" className="mt-2" onClick={() => void loadCandidates()}>Try again</Button>
          </div>
        ) : (
          <>
            <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-slate-500" aria-live="polite">
              <span>
                <strong className="text-slate-700">{groups.available.length}</strong> available
                {groups.inSemester.length > 0 && <> · {groups.inSemester.length} already in {semesterLabel}</>}
                {chosen.length > 0 && <> · <strong className="text-primary">{chosen.length} selected</strong></>}
              </span>
              {groups.available.length > 0 && (
                <button
                  type="button"
                  role="checkbox"
                  aria-checked={selectionState === 'all' ? true : selectionState === 'some' ? 'mixed' : false}
                  disabled={submitting}
                  onClick={() => setSelected(current => setVisibleSelection(current, availableIds, selectionState !== 'all'))}
                  className="flex items-center gap-2 rounded-lg px-2 py-1 font-semibold text-slate-600 hover:bg-slate-50 disabled:opacity-60"
                >
                  <span className={`flex h-4 w-4 shrink-0 items-center justify-center rounded border ${selectionState === 'none' ? 'border-slate-300 bg-white' : 'border-primary bg-primary'}`}>
                    {selectionState === 'all' && <Check className="h-3 w-3 text-white" />}
                    {selectionState === 'some' && <Minus className="h-3 w-3 text-white" />}
                  </span>
                  {selectionState === 'all' ? 'Clear selection' : `Select all${hasFilters ? ' shown' : ''} (${groups.available.length})`}
                </button>
              )}
            </div>

            <div className="max-h-[42vh] space-y-1.5 overflow-y-auto rounded-xl border border-slate-100 bg-slate-50/50 p-2">
              {groups.available.length === 0 && groups.inSemester.length === 0 ? (
                <p className="py-8 text-center text-xs text-slate-500">
                  {hasFilters ? `No ${roleNoun} matches the current search.` : `No active ${roleNoun} accounts exist yet.`}
                </p>
              ) : (
                <>
                  {groups.available.length === 0 && (
                    <p className="py-6 text-center text-xs text-slate-500">
                      {hasFilters ? `No more ${roleNoun}s match the current search.` : `Every active ${roleNoun} is already in ${semesterLabel}.`}
                    </p>
                  )}
                  {groups.available.map(candidate => {
                    const isChecked = chosen.includes(candidate.userId);
                    return (
                      <button
                        key={candidate.userId}
                        type="button"
                        role="checkbox"
                        aria-checked={isChecked}
                        disabled={submitting}
                        onClick={() => setSelected(current => toggleSelection(keepEligibleSelection(current, availableIds), candidate.userId))}
                        className={`flex w-full items-center justify-between gap-3 rounded-xl border p-2.5 text-left transition-all disabled:opacity-60 ${isChecked ? 'border-primary/30 bg-primary-50/50 shadow-xs' : 'border-slate-200/60 bg-white hover:bg-slate-50'}`}
                      >
                        <CandidateInfo candidate={candidate} />
                        <span className={`flex h-5 w-5 shrink-0 items-center justify-center rounded border ${isChecked ? 'border-primary bg-primary' : 'border-slate-300 bg-white'}`}>
                          {isChecked && <Check className="h-3 w-3 text-white" />}
                        </span>
                      </button>
                    );
                  })}
                  {groups.inSemester.length > 0 && (
                    <>
                      <p className="px-1 pt-2 text-[11px] font-semibold uppercase tracking-wide text-slate-400">Already in {semesterLabel}</p>
                      {groups.inSemester.map(candidate => (
                        <div key={candidate.userId} className="flex items-center justify-between gap-3 rounded-xl border border-slate-200/60 bg-white p-2.5 opacity-60">
                          <CandidateInfo candidate={candidate} />
                          <span className="shrink-0 rounded-full bg-slate-100 px-2 py-0.5 text-[10px] font-semibold text-slate-600">In semester</span>
                        </div>
                      ))}
                    </>
                  )}
                </>
              )}
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}

function CandidateInfo({ candidate }: { candidate: TeachingStaffCandidateDto }) {
  return (
    <div className="flex min-w-0 items-center gap-2.5">
      <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-slate-100 text-xs font-bold text-slate-600">
        {candidate.name.trim().charAt(0).toUpperCase() || '?'}
      </span>
      <div className="min-w-0">
        <p className="truncate text-xs font-semibold text-slate-800">{candidate.name}</p>
        <p className="truncate text-[10px] text-slate-400">{candidate.email}</p>
        <MentorTagChips tags={candidate.tags} />
      </div>
      {candidate.mentorType && (
        <MentorKindTag type={candidate.mentorType} className="hidden shrink-0 sm:inline-flex">
          {candidate.contractType ? ` · ${candidate.contractType}` : ''}
        </MentorKindTag>
      )}
    </div>
  );
}
