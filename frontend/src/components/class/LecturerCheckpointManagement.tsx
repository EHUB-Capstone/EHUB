import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  AlertCircle,
  CalendarClock,
  ChevronDown,
  Download,
  FileText,
  Loader2,
  RotateCcw,
  ExternalLink,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { lecturerCheckpointApi } from '../../api/lecturerCheckpointApi';
import { checkpointApi } from '../../api/checkpointApi';
import type { LecturerCheckpointOverview } from '../../types/lecturerCheckpoints';
import { unwrapApiData } from '../../utils/classMappers';
import { parseApiError } from '../../utils/apiError';
import {
  formatLecturerCheckpointName,
  groupCheckpointSchedules,
  type CheckpointScheduleGroup,
} from '../../utils/lecturerCheckpointSchedules';
import {
  checkpointDateRangeError,
  formatCheckpointDateTime,
  parseCheckpointLocalDateTime,
} from '../../utils/checkpointDateTime';
import CheckpointDateTimeField from './CheckpointDateTimeField';
import Modal from '../ui/Modal';

interface Props {
  semester: string;
  year: string;
  subjectCode: string;
  classStatus: string;
  search: string;
  initialClassId?: string;
  initialCheckpointNumber?: number;
  onSelectionChange?: (classId: string, checkpointNumber: number | null) => void;
}

const emptyOverview: LecturerCheckpointOverview = {
  serverTimeUtc: '',
  classes: [],
  checkpoints: [],
  schedules: [],
  submissions: [],
};

const statusStyles: Record<string, string> = {
  NotScheduled: 'bg-slate-100 text-slate-600',
  Upcoming: 'bg-blue-50 text-blue-700',
  Open: 'bg-emerald-50 text-emerald-700',
  Pending: 'bg-amber-50 text-amber-700',
  Draft: 'bg-amber-50 text-amber-700',
  Submitted: 'bg-violet-50 text-violet-700',
  Closed: 'bg-red-50 text-red-700',
  Mixed: 'bg-amber-50 text-amber-700',
};

const formatDateTime = formatCheckpointDateTime;

const toLocalInput = (value: string | Date) => {
  const date = typeof value === 'string' ? new Date(value) : value;
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
};

export default function LecturerCheckpointManagement({
  semester,
  year,
  subjectCode,
  classStatus,
  search,
  initialClassId = '',
  initialCheckpointNumber,
  onSelectionChange,
}: Props) {
  const [overview, setOverview] = useState<LecturerCheckpointOverview>(emptyOverview);
  const [selectedClassId, setSelectedClassId] = useState(initialClassId);
  const [selectedCheckpointNumber, setSelectedCheckpointNumber] = useState<number | null>(
    initialCheckpointNumber ?? null,
  );
  const [loading, setLoading] = useState(true);
  const [schedulesExpanded, setSchedulesExpanded] = useState(false);
  const [error, setError] = useState('');
  const [editing, setEditing] = useState<CheckpointScheduleGroup | null>(null);
  const [confirmingBulk, setConfirmingBulk] = useState(false);
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [saving, setSaving] = useState(false);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  useEffect(() => {
    setSelectedClassId(initialClassId);
  }, [initialClassId]);

  useEffect(() => {
    setSelectedCheckpointNumber(initialCheckpointNumber ?? null);
  }, [initialCheckpointNumber]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const response = await lecturerCheckpointApi.getOverview({
        semester: semester || undefined,
        year: year ? Number(year) : undefined,
        subjectCode: subjectCode || undefined,
        status: classStatus || undefined,
        search: search || undefined,
        classId: selectedClassId || undefined,
        checkpointNumber: selectedCheckpointNumber ?? undefined,
      });
      setOverview(unwrapApiData<LecturerCheckpointOverview>(response));
    } catch (requestError) {
      setError(parseApiError(requestError, 'Unable to load checkpoint management data.').message);
    } finally {
      setLoading(false);
    }
  }, [classStatus, search, selectedCheckpointNumber, selectedClassId, semester, subjectCode, year]);

  useEffect(() => {
    void load();
  }, [load]);

  const checkpointOptions = useMemo(() => Array.from(
    new Set(overview.checkpoints.map(item => item.number)),
  ).sort((left, right) => left - right), [overview.checkpoints]);
  const scheduleGroups = useMemo(() => groupCheckpointSchedules(overview.schedules), [overview.schedules]);
  const parsedStart = parseCheckpointLocalDateTime(startDate);
  const parsedEnd = parseCheckpointLocalDateTime(endDate);
  const rangeError = checkpointDateRangeError(startDate, endDate);
  const reopenError = editing?.allClosed && parsedEnd && parsedEnd <= new Date()
    ? 'End date and time must be in the future to reopen this checkpoint.' : '';
  const datesValid = Boolean(parsedStart && parsedEnd && !rangeError && !reopenError);

  const openEditor = (group: CheckpointScheduleGroup) => {
    setEditing(group);
    setConfirmingBulk(false);
    setStartDate(group.startDateUtc ? toLocalInput(group.startDateUtc) : '');
    setEndDate(group.endDateUtc ? toLocalInput(group.endDateUtc) : '');
  };

  const saveSchedule = async () => {
    const start = parseCheckpointLocalDateTime(startDate);
    const end = parseCheckpointLocalDateTime(endDate);
    if (!editing || !start || !end || rangeError || reopenError) {
      toast.error(rangeError || reopenError || 'Enter valid start and end dates with 24-hour times.');
      return;
    }

    if (!selectedClassId && !confirmingBulk) {
      setConfirmingBulk(true);
      return;
    }

    setSaving(true);
    try {
      if (selectedClassId) {
        await lecturerCheckpointApi.saveSchedule(editing.schedules[0].classId, editing.checkpointId, {
          startDateUtc: start.toISOString(),
          endDateUtc: end.toISOString(),
        });
      } else {
        await lecturerCheckpointApi.saveBulkSchedule({
          checkpointNumber: editing.checkpointNumber,
          semester: semester || undefined,
          year: year ? Number(year) : undefined,
          subjectCode: subjectCode || undefined,
          status: classStatus || undefined,
          search: search || undefined,
          expectedClassIds: editing.schedules.map(item => item.classId),
          startDateUtc: start.toISOString(),
          endDateUtc: end.toISOString(),
        });
      }
      toast.success(selectedClassId
        ? editing.allClosed ? 'Checkpoint reopened successfully.' : 'Checkpoint schedule saved.'
        : `Schedule applied to ${editing.schedules.length} ${editing.schedules.length === 1 ? 'class' : 'classes'}.`);
      setEditing(null);
      setConfirmingBulk(false);
      await load();
    } catch (requestError) {
      toast.error(parseApiError(requestError, 'Unable to save checkpoint schedule.').message);
    } finally {
      setSaving(false);
    }
  };

  const downloadEarliest = async (
    teamId: string,
    checkpointNumber: number,
    fileId: string,
    fileName: string,
  ) => {
    setDownloadingId(fileId);
    try {
      await checkpointApi.downloadFile(teamId, checkpointNumber, fileId, fileName);
    } catch (requestError) {
      toast.error(parseApiError(requestError, 'Unable to download the submitted file.').message);
    } finally {
      setDownloadingId(null);
    }
  };

  if (loading && overview.classes.length === 0) {
    return <div className="flex min-h-64 items-center justify-center rounded-2xl border border-slate-200 bg-white"><Loader2 className="h-7 w-7 animate-spin text-primary" /></div>;
  }

  if (error) {
    return (
      <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-center">
        <AlertCircle className="mx-auto h-7 w-7 text-red-500" />
        <p className="mt-2 text-sm font-semibold text-red-800">Unable to load checkpoints</p>
        <p className="mt-1 text-sm text-red-600">{error}</p>
        <button type="button" onClick={() => void load()} className="mt-3 rounded-lg border border-red-300 bg-white px-3 py-1.5 text-xs font-semibold text-red-700">Retry</button>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <section className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <div className="grid gap-3 md:grid-cols-2">
          <label className="space-y-1.5 text-sm font-semibold text-slate-700">
            <span>Class</span>
            <select
              value={selectedClassId}
              onChange={(event) => {
                const value = event.target.value;
                setSelectedClassId(value);
                onSelectionChange?.(value, selectedCheckpointNumber);
              }}
              className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            >
              <option value="">All Classes</option>
              {overview.classes.map(item => (
                <option key={item.id} value={item.id}>{item.classCode} - {item.subjectName}</option>
              ))}
            </select>
          </label>
          <label className="space-y-1.5 text-sm font-semibold text-slate-700">
            <span>Checkpoint</span>
            <select
              value={selectedCheckpointNumber ?? ''}
              onChange={(event) => {
                const value = event.target.value ? Number(event.target.value) : null;
                setSelectedCheckpointNumber(value);
                onSelectionChange?.(selectedClassId, value);
              }}
              className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            >
              <option value="">All Checkpoints</option>
              {checkpointOptions.map(number => (
                <option key={number} value={number}>{formatLecturerCheckpointName(number)}</option>
              ))}
            </select>
          </label>
        </div>
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className={`flex items-center justify-between gap-3 px-5 py-4 ${schedulesExpanded ? 'border-b border-slate-100' : ''}`}>
          <div>
            <h2 className="font-bold text-slate-900">Checkpoint schedules</h2>
            <p className="mt-0.5 text-xs text-slate-500">Configure Admin-defined checkpoints for the classes you manage.</p>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            {loading && <Loader2 className="h-4 w-4 animate-spin text-primary" />}
            <button
              type="button"
              aria-label={schedulesExpanded ? 'Collapse checkpoint schedules' : 'Expand checkpoint schedules'}
              aria-expanded={schedulesExpanded}
              aria-controls="lecturer-checkpoint-schedules"
              onClick={() => setSchedulesExpanded(current => !current)}
              className="inline-flex h-8 w-8 items-center justify-center rounded-lg border border-slate-200 text-slate-600 transition hover:border-primary hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30"
            >
              <ChevronDown className={`h-4 w-4 transition-transform ${schedulesExpanded ? 'rotate-180' : ''}`} />
            </button>
          </div>
        </div>
        <div id="lecturer-checkpoint-schedules" hidden={!schedulesExpanded}>
            {scheduleGroups.length === 0 ? (
              <p className="p-8 text-center text-sm text-slate-500">No checkpoint definitions match the selected classes.</p>
            ) : (
              <div className="space-y-3 p-4">
            {scheduleGroups.map(group => (
              <article key={group.checkpointNumber} className="rounded-xl border border-slate-200 p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-xs font-bold uppercase tracking-wide text-primary">
                      {selectedClassId ? group.schedules[0].classCode : 'All Classes'}
                    </p>
                    <h3 className="mt-1 font-bold text-slate-900">{formatLecturerCheckpointName(group.checkpointNumber)}</h3>
                  </div>
                  <span className={`rounded-full px-2.5 py-1 text-xs font-bold ${statusStyles[group.status] || statusStyles.NotScheduled}`}>
                    {group.status}
                  </span>
                </div>
                {!selectedClassId && (
                  <div className="mt-3 text-xs text-slate-600">
                    <p className="font-semibold text-slate-700">Applies to {group.schedules.length} {group.schedules.length === 1 ? 'class' : 'classes'}</p>
                    <p className="mt-1">{group.schedules.slice(0, 6).map(item => item.classCode).join(' · ')}{group.schedules.length > 6 ? ' · …' : ''}</p>
                    {group.schedules.length > 6 && (
                      <details className="mt-1">
                        <summary className="cursor-pointer font-semibold text-primary">View all classes</summary>
                        <p className="mt-1 max-h-24 overflow-y-auto">{group.schedules.map(item => item.classCode).join(' · ')}</p>
                      </details>
                    )}
                  </div>
                )}
                {group.uniform ? (
                  <div className="mt-4 grid gap-2 text-xs text-slate-600 sm:grid-cols-2">
                    <div className="rounded-lg bg-slate-50 p-2.5"><span className="block font-semibold text-slate-400">Starts</span>{formatDateTime(group.startDateUtc)}</div>
                    <div className="rounded-lg bg-slate-50 p-2.5"><span className="block font-semibold text-slate-400">Ends</span>{formatDateTime(group.endDateUtc)}</div>
                  </div>
                ) : (
                  <p className="mt-4 rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs font-semibold text-amber-800">
                    Schedules differ across selected classes. Applying one schedule will replace their current windows.
                  </p>
                )}
                <button
                  type="button"
                  onClick={() => openEditor(group)}
                  className="mt-4 inline-flex items-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-xs font-bold text-slate-700 hover:border-primary hover:text-primary"
                >
                  {group.allClosed ? <RotateCcw className="h-3.5 w-3.5" /> : <CalendarClock className="h-3.5 w-3.5" />}
                  {group.allClosed ? 'Reopen Checkpoint' : !group.uniform ? 'Apply one schedule to all classes' : group.schedules.some(item => item.id) ? 'Edit schedule' : 'Set schedule'}
                </button>
              </article>
            ))}
              </div>
            )}
        </div>
      </section>

      <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-100 px-5 py-4">
          <h2 className="font-bold text-slate-900">Team submissions</h2>
          <p className="mt-0.5 text-xs text-slate-500">Latest activity and the earliest submitted file for each team and checkpoint.</p>
        </div>
        {overview.submissions.length === 0 ? (
          <p className="p-8 text-center text-sm text-slate-500">No teams match the current filters.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-200 text-sm">
              <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">Class</th><th className="px-4 py-3">Team / Group</th><th className="px-4 py-3">Checkpoint</th><th className="px-4 py-3">Status</th><th className="px-4 py-3">Latest Submission</th><th className="px-4 py-3">Earliest Submitted File</th><th className="px-4 py-3">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {overview.submissions.map(item => (
                  <tr key={`${item.teamId}-${item.checkpointId}`} className="hover:bg-slate-50/70">
                    <td className="whitespace-nowrap px-4 py-3 font-semibold text-slate-800">{item.classCode}</td>
                    <td className="px-4 py-3 text-slate-700">{item.teamName}</td>
                    <td className="min-w-52 px-4 py-3 text-slate-700">{formatLecturerCheckpointName(item.checkpointNumber)}</td>
                    <td className="px-4 py-3"><span className={`rounded-full px-2.5 py-1 text-xs font-bold ${statusStyles[item.status] || statusStyles.NotScheduled}`}>{item.status}</span></td>
                    <td className="whitespace-nowrap px-4 py-3 text-slate-600">{formatDateTime(item.latestSubmissionAtUtc)}</td>
                    <td className="max-w-64 px-4 py-3 text-slate-600">
                      {item.earliestSubmittedFile ? (
                        <div>
                          <span className="block truncate font-medium" title={item.earliestSubmittedFile.originalName}>{item.earliestSubmittedFile.originalName}</span>
                          <span className="block text-xs text-slate-400">{formatDateTime(item.earliestSubmittedFile.uploadedAtUtc)}</span>
                        </div>
                      ) : '—'}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <Link
                          to={`/workspace/teams/${item.teamId}`}
                          className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 px-2.5 py-1.5 text-xs font-semibold text-slate-700 hover:border-primary hover:text-primary"
                        >
                          <ExternalLink className="h-3.5 w-3.5" /> View
                        </Link>
                      {item.earliestSubmittedFile ? (
                        <button
                          type="button"
                          disabled={downloadingId === item.earliestSubmittedFile.id}
                          onClick={() => void downloadEarliest(item.teamId, item.checkpointNumber, item.earliestSubmittedFile!.id, item.earliestSubmittedFile!.originalName)}
                          className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 px-2.5 py-1.5 text-xs font-semibold text-slate-700 hover:border-primary hover:text-primary disabled:opacity-50"
                        >
                          {downloadingId === item.earliestSubmittedFile.id ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Download className="h-3.5 w-3.5" />}
                          Download
                        </button>
                      ) : <FileText className="h-4 w-4 text-slate-300" />}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <Modal
        isOpen={Boolean(editing)}
        onClose={() => { setEditing(null); setConfirmingBulk(false); }}
        title={confirmingBulk ? 'Apply Checkpoint Schedule' : editing?.allClosed ? `Reopen ${formatLecturerCheckpointName(editing.checkpointNumber)}` : `Schedule ${editing ? formatLecturerCheckpointName(editing.checkpointNumber) : 'checkpoint'}`}
        onSubmit={saveSchedule}
        submitText={confirmingBulk
          ? `${editing?.allClosed ? 'Reopen' : 'Apply'} to ${editing?.schedules.length || 0} ${(editing?.schedules.length || 0) === 1 ? 'Class' : 'Classes'}`
          : !selectedClassId ? `Review schedule for ${editing?.schedules.length || 0} ${(editing?.schedules.length || 0) === 1 ? 'class' : 'classes'}`
            : editing?.allClosed ? 'Reopen Checkpoint' : 'Save schedule'}
        isSubmitting={saving}
        submitDisabled={!datesValid}
      >
        <div className="space-y-4">
          {editing && !selectedClassId && (
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3 text-sm text-slate-700">
              <p className="font-semibold">{formatLecturerCheckpointName(editing.checkpointNumber)}</p>
              <p className="mt-1">This schedule will be applied to {editing.schedules.length} {editing.schedules.length === 1 ? 'class' : 'classes'}:</p>
              <p className="mt-1 max-h-24 overflow-y-auto text-xs">{editing.schedules.map(item => item.classCode).join(' · ')}</p>
            </div>
          )}
          {editing?.allClosed && (
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
              {editing.uniform
                ? <p>Current schedule: {formatDateTime(editing.startDateUtc)} → {formatDateTime(editing.endDateUtc)}</p>
                : <p>Current schedules differ across the selected classes.</p>}
              <p className="mt-1">Existing submissions and history will be preserved. Only the availability window changes.</p>
            </div>
          )}
          {editing && !editing.uniform && (
            <div className="max-h-28 space-y-1 overflow-y-auto rounded-xl border border-slate-200 bg-slate-50 p-3 text-xs text-slate-600">
              {editing.schedules.map(item => (
                <p key={item.classId}><span className="font-semibold">{item.classCode}:</span> {formatDateTime(item.startDateUtc)} → {formatDateTime(item.endDateUtc)}</p>
              ))}
            </div>
          )}
          {confirmingBulk ? (
            <div className="space-y-2 text-sm text-slate-700">
              <p><span className="font-semibold">Start:</span> {formatDateTime(parsedStart)}</p>
              <p><span className="font-semibold">End:</span> {formatDateTime(parsedEnd)}</p>
              <button type="button" onClick={() => setConfirmingBulk(false)} className="text-xs font-semibold text-primary hover:underline">Edit dates</button>
            </div>
          ) : (
            <>
              <CheckpointDateTimeField
                label={editing?.allClosed ? 'New Start Date & Time' : 'Start Date & Time'}
                value={startDate}
                defaultTime="08:00"
                onChange={setStartDate}
              />
              <CheckpointDateTimeField
                label={editing?.allClosed ? 'New End Date & Time' : 'End Date & Time'}
                value={endDate}
                defaultTime="23:59"
                minDate={parsedStart ? startDate.split('T')[0] : undefined}
                onChange={setEndDate}
                error={rangeError || reopenError || undefined}
              />
              {!rangeError && !reopenError && startDate && endDate && (!parsedStart || !parsedEnd) && (
                <p role="alert" className="text-xs font-medium text-red-600">Enter a valid date and 24-hour time (HH 00–23, mm 00–59).</p>
              )}
            </>
          )}
        </div>
      </Modal>
    </div>
  );
}
