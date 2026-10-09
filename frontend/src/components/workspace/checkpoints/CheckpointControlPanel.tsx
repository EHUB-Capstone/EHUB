// @ts-nocheck
import { Archive, CalendarClock, Info, Settings2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { classApi } from '../../../api/classApi';

const toLocalDateTime = (value) => value ? new Date(value).toISOString().slice(0, 16) : '';

export default function CheckpointControlPanel({ classId, checkpoints = [], submissionStatsByCheckpoint = {} }) {
  const [availableClasses, setAvailableClasses] = useState([]);
  const [selectedClassId, setSelectedClassId] = useState(classId || '');
  const [applyToAllClasses, setApplyToAllClasses] = useState(false);
  const [loadedCheckpoints, setLoadedCheckpoints] = useState([]);
  const [selectedCheckpointNumber, setSelectedCheckpointNumber] = useState('');
  const [archived, setArchived] = useState(false);
  const [deadline, setDeadline] = useState('');
  const [schedules, setSchedules] = useState({});
  const [loadingSchedules, setLoadingSchedules] = useState(false);
  const [saving, setSaving] = useState(false);

  const availableCheckpoints = checkpoints.length > 0 ? checkpoints : loadedCheckpoints;
  const checkpoint = availableCheckpoints.find((item) => String(item.number) === selectedCheckpointNumber) ?? null;
  const submissionStats = checkpoint ? submissionStatsByCheckpoint[checkpoint.number] : null;

  useEffect(() => {
    if (!selectedCheckpointNumber && availableCheckpoints.length > 0) {
      setSelectedCheckpointNumber(String(availableCheckpoints[0].number));
    }
  }, [availableCheckpoints, selectedCheckpointNumber]);

  useEffect(() => {
    const checkpointNumber = checkpoint?.number;
    const configured = checkpointNumber ? schedules[checkpointNumber] : null;
    setArchived(String(configured?.status || '').toUpperCase() === 'ARCHIVED');
    setDeadline(toLocalDateTime(configured?.dueDate));
  }, [checkpoint?.number, schedules]);

  useEffect(() => {
    let active = true;
    classApi.getCheckpointDeadlineClasses()
      .then((response) => {
        if (!active || !response?.success) return;
        const classes = response.data || [];
        setAvailableClasses(classes);
        setSelectedClassId((current) => current && classes.some((item) => String(item.classId) === String(current))
          ? current
          : String(classes[0]?.classId || ''));
      })
      .catch(() => active && toast.error('Unable to load classes for checkpoint deadlines.'));
    return () => { active = false; };
  }, [classId]);

  useEffect(() => {
    if (!selectedClassId) return;
    let active = true;
    setLoadingSchedules(true);
    classApi.getCheckpointDeadlines(selectedClassId)
      .then((response) => {
        if (!active || !response?.success) return;
        const items = response.data || [];
        setLoadedCheckpoints(items.map((item) => ({ number: item.checkpointNumber, title: item.title, requirements: [] })));
        setSchedules(Object.fromEntries(items.map((item) => [item.checkpointNumber, item])));
        setSelectedCheckpointNumber((current) => items.some((item) => String(item.checkpointNumber) === current)
          ? current
          : String(items[0]?.checkpointNumber || ''));
      })
      .catch(() => active && toast.error('Unable to load checkpoint deadlines.'))
      .finally(() => active && setLoadingSchedules(false));
    return () => { active = false; };
  }, [selectedClassId]);

  const save = async () => {
    if (!selectedClassId || !checkpoint) return;
    if (!deadline) return toast.error('Please choose a deadline.');
    setSaving(true);
    try {
      const payload = {
        dueDate: new Date(deadline).toISOString(),
        status: archived ? 'ARCHIVED' : 'OPEN',
      };
      const response = applyToAllClasses
        ? await classApi.saveCheckpointDeadlineForClasses(checkpoint.number, {
          ...payload,
          applyToAllAccessibleClasses: true,
          classIds: [],
        })
        : await classApi.saveCheckpointDeadline(selectedClassId, checkpoint.number, payload);
      if (!response?.success) throw new Error(response?.message || 'Unable to save checkpoint deadline.');
      if (!applyToAllClasses) setSchedules((current) => ({ ...current, [checkpoint.number]: response.data }));
      toast.success(applyToAllClasses ? `Checkpoint deadline saved for ${response.data?.updatedCount || 0} classes.` : 'Checkpoint deadline saved.');
    } catch (error) {
      toast.error(error?.response?.data?.message || error?.response?.data?.error || error?.message || 'Unable to save checkpoint deadline.');
    } finally {
      setSaving(false);
    }
  };

  if (!checkpoint) {
    return (
      <section className="rounded-2xl border border-dashed border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex items-center gap-2 text-slate-700">
          <Settings2 className="h-4 w-4 text-primary" />
          <h3 className="font-bold">Checkpoint control</h3>
        </div>
        <p className="mt-3 text-sm leading-6 text-slate-500">
          Checkpoints will appear here when the workspace configuration is available.
        </p>
      </section>
    );
  }

  const totalRequirements = checkpoint.requirements?.length || 0;

  return (
    <section className="rounded-2xl border border-slate-200/60 bg-white p-5 shadow-sm" aria-busy={loadingSchedules}>
      <div className="flex items-start justify-between gap-3 border-b border-slate-100 pb-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2 text-primary">
            <Settings2 className="h-4 w-4" />
            <h3 className="font-bold text-slate-800">Checkpoint control</h3>
          </div>
          <p className="mt-1 text-xs text-slate-400">Class-wide configuration</p>
          {loadingSchedules && <p className="mt-1 text-[11px] text-slate-400">Updating class data…</p>}
        </div>
        <span className="rounded-md bg-primary-50 px-2 py-1 text-[10px] font-bold uppercase tracking-wide text-primary">
          CP {checkpoint.number}
        </span>
      </div>

      <fieldset className="mt-4">
        <legend className="text-xs font-bold text-slate-600">Apply to</legend>
        <div className="mt-2 grid grid-cols-2 gap-2">
          <button type="button" onClick={() => setApplyToAllClasses(false)} aria-pressed={!applyToAllClasses}
            className={`rounded-xl border px-3 py-2 text-xs font-semibold ${!applyToAllClasses ? 'border-primary bg-primary-50 text-primary' : 'border-slate-200 text-slate-500 hover:bg-slate-50'}`}>
            One class
          </button>
          <button type="button" onClick={() => setApplyToAllClasses(true)} aria-pressed={applyToAllClasses}
            className={`rounded-xl border px-3 py-2 text-xs font-semibold ${applyToAllClasses ? 'border-primary bg-primary-50 text-primary' : 'border-slate-200 text-slate-500 hover:bg-slate-50'}`}>
            All accessible classes
          </button>
        </div>
      </fieldset>

      {!applyToAllClasses && (
        <label className="mt-4 block text-xs font-bold text-slate-600" htmlFor="checkpoint-class-selector">
          Class
          <select id="checkpoint-class-selector" value={selectedClassId} onChange={(event) => setSelectedClassId(event.target.value)}
            className="mt-2 w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm font-semibold text-slate-700 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15">
            {availableClasses.map((item) => (
              <option key={item.classId} value={item.classId}>{item.classCode} · {item.subjectName} · {item.semesterCode}</option>
            ))}
          </select>
        </label>
      )}
      {applyToAllClasses && (
        <p className="mt-3 rounded-xl bg-slate-50 px-3 py-2 text-xs leading-5 text-slate-500">
          The same deadline will be applied to all {availableClasses.length} classes you are allowed to manage. Every class must pass its semester and checkpoint-order validation.
        </p>
      )}

      <label className="mt-4 block text-xs font-bold text-slate-600" htmlFor="checkpoint-selector">
        Checkpoint
      </label>
      <select
        id="checkpoint-selector"
        value={selectedCheckpointNumber}
        onChange={(event) => setSelectedCheckpointNumber(event.target.value)}
        className="mt-2 w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm font-semibold text-slate-700 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
      >
        {availableCheckpoints.map((item) => (
          <option key={item.number} value={item.number}>Checkpoint {item.number} · {item.title}</option>
        ))}
      </select>

      <div className="mt-4">
        <p className="text-sm font-semibold text-slate-800">{checkpoint.title}</p>
        <p className="mt-1 text-xs leading-5 text-slate-500">
          These settings apply to every team in this class, not only the team currently open.
        </p>
      </div>

      <label className="mt-5 block text-xs font-bold text-slate-600" htmlFor="checkpoint-deadline">
        Deadline
      </label>
      <div className="relative mt-2">
        <CalendarClock className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-slate-400" />
        <input
          id="checkpoint-deadline"
          type="datetime-local"
          value={deadline}
          onChange={(event) => setDeadline(event.target.value)}
          className="w-full rounded-xl border border-slate-200 bg-white py-2.5 pl-10 pr-3 text-sm text-slate-700 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
        />
      </div>
      <p className="mt-2 text-[11px] leading-5 text-slate-400">
        A valid deadline enables this checkpoint automatically. Student access still follows checkpoint order and submission status.
      </p>

      <button
        type="button"
        onClick={() => setArchived((current) => !current)}
        className={`mt-4 flex w-full items-center justify-between rounded-xl border px-3 py-3 text-left text-xs font-semibold transition ${
          archived ? 'border-slate-300 bg-slate-100 text-slate-700' : 'border-slate-200 bg-white text-slate-500 hover:bg-slate-50'
        }`}
        aria-pressed={archived}
      >
        <span className="flex items-center gap-2"><Archive className="h-4 w-4" /> Archive checkpoint</span>
        <span>{archived ? 'Archived' : 'Active'}</span>
      </button>
      <p className="mt-2 text-[11px] leading-5 text-slate-400">
        Archive is the only manual override. Remove it and save to let the deadline rules apply again.
      </p>

      <div className="mt-5 rounded-xl bg-slate-50 p-3">
        <div className="flex items-center justify-between gap-3 text-xs">
          <span className="text-slate-500">Submission progress</span>
          <span className="font-bold text-slate-700">{submissionStats?.submittedTeams ?? '—'} / {submissionStats?.eligibleTeams ?? '—'} teams</span>
        </div>
        <div className="mt-2 flex items-center justify-between gap-3 text-xs text-slate-500">
          <span>{submissionStats?.reqFilled || 0} / {totalRequirements} requirements in this team</span>
          <span>{submissionStats?.count || 0} files</span>
        </div>
      </div>

      <div className="mt-4 flex gap-2 rounded-xl border border-blue-100 bg-blue-50 p-3 text-xs leading-5 text-blue-700">
        <Info className="mt-0.5 h-3.5 w-3.5 shrink-0" />
        <p>Open and Closed are calculated on the backend from deadline, checkpoint order, and the team’s previous submission.</p>
      </div>
      <button type="button" onClick={save} disabled={saving || loadingSchedules} className="mt-4 w-full rounded-xl bg-primary px-4 py-2.5 text-sm font-bold text-white transition hover:bg-primary-700 disabled:cursor-not-allowed disabled:opacity-60">
        {saving ? 'Saving…' : loadingSchedules ? 'Loading…' : 'Save deadline'}
      </button>
    </section>
  );
}
