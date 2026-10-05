import { useEffect, useMemo, useState } from 'react';
import { Check, CircleAlert, History, RefreshCw, Search, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { subjectApi } from '../../api/subjectApi';
import type {
  MentorCarryoverCommitResult,
  MentorCarryoverPreview,
  SemesterDto,
} from '../../types/subjects';
import { parseApiError } from '../../utils/apiError';
import Badge from '../ui/Badge';
import Button from '../ui/Button';
import EmptyState from '../ui/EmptyState';
import LoadingSkeleton from '../ui/LoadingSkeleton';
import Modal from '../ui/Modal';

interface MentorCarryoverModalProps {
  isOpen: boolean;
  onClose: () => void;
  targetSemester: SemesterDto;
  sourceSemesters: SemesterDto[];
  onCompleted: () => Promise<void>;
}

type MentorTypeFilter = 'ALL' | 'Enterprise' | 'Academic';

function responseData<T>(response: { data?: T } | T): T {
  return ('data' in (response as { data?: T }) ? (response as { data?: T }).data : response) as T;
}

function label(semester: SemesterDto) {
  return `${semester.semester} ${semester.year}`;
}

function initials(name: string) {
  return name.split(' ').filter(Boolean).slice(0, 2).map(part => part[0]).join('').toUpperCase() || '?';
}

export default function MentorCarryoverModal({
  isOpen,
  onClose,
  targetSemester,
  sourceSemesters,
  onCompleted,
}: MentorCarryoverModalProps) {
  const [sourceSemesterId, setSourceSemesterId] = useState('');
  const [preview, setPreview] = useState<MentorCarryoverPreview | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [search, setSearch] = useState('');
  const [typeFilter, setTypeFilter] = useState<MentorTypeFilter>('ALL');
  const [loading, setLoading] = useState(false);
  const [committing, setCommitting] = useState(false);
  const [error, setError] = useState('');
  const [retryNonce, setRetryNonce] = useState(0);

  useEffect(() => {
    if (!isOpen) return;
    setSourceSemesterId(sourceSemesters[0]?.id ?? '');
    setPreview(null);
    setSelectedIds(new Set());
    setSearch('');
    setTypeFilter('ALL');
    setError('');
  }, [isOpen, sourceSemesters]);

  useEffect(() => {
    if (!isOpen || !sourceSemesterId) return;
    let cancelled = false;
    const controller = new AbortController();
    const load = async () => {
      setLoading(true);
      setError('');
      setPreview(null);
      try {
        const data = responseData<MentorCarryoverPreview>(await subjectApi.previewMentorCarryover(
          { sourceSemesterId, targetSemesterId: targetSemester.id },
          controller.signal,
        ));
        if (cancelled) return;
        setPreview(data);
        setSelectedIds(new Set(data.mentors.filter(mentor => mentor.canSelect).map(mentor => mentor.userId)));
      } catch (loadError) {
        if (cancelled) return;
        setError(parseApiError(loadError, 'Failed to load mentors from the source semester').message);
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    void load();
    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [isOpen, retryNonce, sourceSemesterId, targetSemester.id]);

  const visibleMentors = useMemo(() => {
    const query = search.trim().toLocaleLowerCase();
    return (preview?.mentors ?? []).filter(mentor => {
      const matchesType = typeFilter === 'ALL' || mentor.mentorType === typeFilter;
      const matchesSearch = !query || `${mentor.name} ${mentor.email}`.toLocaleLowerCase().includes(query);
      return matchesType && matchesSearch;
    });
  }, [preview, search, typeFilter]);

  const visibleSelectable = visibleMentors.filter(mentor => mentor.canSelect);
  const allVisibleSelected = visibleSelectable.length > 0 && visibleSelectable.every(mentor => selectedIds.has(mentor.userId));
  const selectedMentors = (preview?.mentors ?? []).filter(mentor => selectedIds.has(mentor.userId));
  const selectedEnterprise = selectedMentors.filter(mentor => mentor.mentorType === 'Enterprise').length;
  const selectedAcademic = selectedMentors.filter(mentor => mentor.mentorType === 'Academic').length;

  const toggleMentor = (userId: string) => {
    setSelectedIds(current => {
      const next = new Set(current);
      if (next.has(userId)) next.delete(userId);
      else next.add(userId);
      return next;
    });
  };

  const toggleVisible = () => {
    setSelectedIds(current => {
      const next = new Set(current);
      if (allVisibleSelected) visibleSelectable.forEach(mentor => next.delete(mentor.userId));
      else visibleSelectable.forEach(mentor => next.add(mentor.userId));
      return next;
    });
  };

  const commit = async () => {
    if (!sourceSemesterId || selectedIds.size === 0) return;
    setCommitting(true);
    try {
      const result = responseData<MentorCarryoverCommitResult>(await subjectApi.commitMentorCarryover({
        sourceSemesterId,
        targetSemesterId: targetSemester.id,
        mentorUserIds: [...selectedIds],
      }));
      const changed = result.addedCount + result.reactivatedCount;
      toast.success(changed > 0
        ? `${changed} mentor${changed === 1 ? '' : 's'} added to ${label(targetSemester)}.`
        : `The selected mentors are already available in ${label(targetSemester)}.`);
      await onCompleted();
      onClose();
    } catch (commitError) {
      toast.error(parseApiError(commitError, 'Failed to reuse selected mentors').message);
    } finally {
      setCommitting(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={() => { if (!committing) onClose(); }}
      title={`Reuse mentors for ${label(targetSemester)}`}
      size="xl"
      submitText={`Add ${selectedIds.size} mentor${selectedIds.size === 1 ? '' : 's'}`}
      submitDisabled={selectedIds.size === 0 || loading || Boolean(error)}
      isSubmitting={committing}
      onSubmit={commit}
    >
      <div className="space-y-4">
        <div className="grid gap-3 rounded-2xl border border-slate-200 bg-slate-50 p-4 sm:grid-cols-[1fr_auto_1fr] sm:items-end">
          <label className="text-sm font-semibold text-slate-700">
            Source semester
            <select
              aria-label="Source semester for mentor reuse"
              value={sourceSemesterId}
              onChange={event => setSourceSemesterId(event.target.value)}
              disabled={loading || committing}
              className="mt-1.5 w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary"
            >
              {sourceSemesters.map(semester => <option key={semester.id} value={semester.id}>{label(semester)} · {semester.status}</option>)}
            </select>
          </label>
          <span className="hidden pb-2 text-slate-400 sm:block">→</span>
          <div className="rounded-xl border border-primary-200 bg-primary-50 px-3 py-2.5">
            <p className="text-xs font-semibold uppercase tracking-wide text-primary">Target semester</p>
            <p className="mt-0.5 font-bold text-slate-900">{label(targetSemester)} · {targetSemester.status}</p>
          </div>
        </div>

        {sourceSemesters.length === 0 ? (
          <EmptyState icon={History} title="No earlier semester available" description="Plan or retain an earlier semester before reusing its mentor list." />
        ) : loading ? (
          <LoadingSkeleton variant="text" lines={6} />
        ) : error ? (
          <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <div className="flex items-start gap-2"><CircleAlert className="mt-0.5 h-4 w-4 shrink-0" /><p className="flex-1">{error}</p></div>
            <Button size="sm" variant="outline" icon={RefreshCw} className="mt-3" onClick={() => setRetryNonce(value => value + 1)}>Try again</Button>
          </div>
        ) : preview ? (
          <>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              <Summary label="Source mentors" value={preview.totalCount} />
              <Summary label="Can be selected" value={preview.eligibleCount} tone="success" />
              <Summary label="Already added" value={preview.alreadyAddedCount} />
              <Summary label="Unavailable" value={preview.unavailableCount} tone="warning" />
            </div>

            <div className="flex flex-col gap-2 sm:flex-row">
              <div className="relative min-w-0 flex-1">
                <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                <input value={search} onChange={event => setSearch(event.target.value)} placeholder="Search mentor name or email..." className="w-full rounded-xl border border-slate-200 bg-white py-2.5 pl-9 pr-3 text-sm outline-none focus:border-primary" />
              </div>
              <select aria-label="Filter mentor type" value={typeFilter} onChange={event => setTypeFilter(event.target.value as MentorTypeFilter)} className="rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary">
                <option value="ALL">All mentor types</option>
                <option value="Enterprise">Enterprise</option>
                <option value="Academic">Academic</option>
              </select>
            </div>

            {preview.mentors.length === 0 ? (
              <EmptyState icon={Users} title="No mentors in the source semester" description="Choose another source semester or import mentors there first." />
            ) : (
              <div className="overflow-hidden rounded-2xl border border-slate-200">
                <div className="flex flex-col gap-2 border-b border-slate-200 bg-slate-50 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
                  <label className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-700">
                    <input type="checkbox" checked={allVisibleSelected} disabled={visibleSelectable.length === 0} onChange={toggleVisible} className="h-4 w-4 rounded border-slate-300 text-primary focus:ring-primary/30" />
                    Select all visible ({visibleSelectable.length})
                  </label>
                  <p className="text-xs text-slate-500">Selected <strong className="text-slate-800">{selectedIds.size}</strong> · {selectedEnterprise} Enterprise · {selectedAcademic} Academic</p>
                </div>
                <div className="max-h-[340px] divide-y divide-slate-100 overflow-y-auto">
                  {visibleMentors.map(mentor => (
                    <label key={mentor.userId} className={`flex gap-3 px-4 py-3 ${mentor.canSelect ? 'cursor-pointer hover:bg-slate-50' : 'cursor-not-allowed bg-slate-50/60 opacity-70'}`}>
                      <input type="checkbox" checked={selectedIds.has(mentor.userId)} disabled={!mentor.canSelect} onChange={() => toggleMentor(mentor.userId)} className="mt-2 h-4 w-4 shrink-0 rounded border-slate-300 text-primary focus:ring-primary/30" />
                      {mentor.avatar ? <img src={mentor.avatar} alt="" className="h-9 w-9 rounded-full object-cover" /> : <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-slate-200 text-xs font-bold text-slate-700">{initials(mentor.name)}</span>}
                      <span className="min-w-0 flex-1">
                        <span className="flex flex-wrap items-center gap-2"><strong className="truncate text-sm text-slate-900">{mentor.name}</strong><Badge variant={mentor.mentorType === 'Enterprise' ? 'Improving' : 'Reviewed'}>{mentor.mentorType || 'Unknown type'}</Badge></span>
                        <span className="block truncate text-xs text-slate-500">{mentor.email}</span>
                        <span className={`mt-1 block text-xs ${mentor.canSelect ? 'text-slate-500' : 'text-amber-700'}`}>{mentor.message}</span>
                      </span>
                      {selectedIds.has(mentor.userId) && <Check className="mt-2 h-4 w-4 shrink-0 text-success" />}
                    </label>
                  ))}
                </div>
              </div>
            )}

            <p className="rounded-xl border border-blue-200 bg-blue-50 px-3 py-2 text-xs leading-5 text-blue-700">
              Mentor accounts and profiles are reused. Previous classes, teams, mentoring sessions and assignments are not copied to {label(targetSemester)}.
            </p>
          </>
        ) : null}
      </div>
    </Modal>
  );
}

function Summary({ label: summaryLabel, value, tone = 'neutral' }: { label: string; value: number; tone?: 'neutral' | 'success' | 'warning' }) {
  const color = tone === 'success' ? 'text-success' : tone === 'warning' ? 'text-warning-dark' : 'text-slate-900';
  return <div className="rounded-xl border border-slate-200 bg-white px-3 py-2"><p className={`text-lg font-bold ${color}`}>{value}</p><p className="text-xs text-slate-500">{summaryLabel}</p></div>;
}
