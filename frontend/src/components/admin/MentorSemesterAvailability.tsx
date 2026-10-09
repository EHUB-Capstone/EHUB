import { useEffect, useRef, useState } from 'react';
import { subjectApi } from '../../api/subjectApi';
import type { MentorProfile } from '../../types/mentoring';
import type { SemesterDto, TeachingStaffDto } from '../../types/subjects';
import { parseApiError } from '../../utils/apiError';
import Modal from '../ui/Modal';
import Button from '../ui/Button';
import ConfirmDialog from '../ui/ConfirmDialog';

interface Row { semester: SemesterDto; staff?: TeachingStaffDto }
export default function MentorSemesterAvailability({ profile, onClose }: { profile: MentorProfile; onClose: () => void }) {
  const [rows, setRows] = useState<Row[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const busy = useRef(false);
  const mutationController = useRef<AbortController | null>(null);
  useEffect(() => () => mutationController.current?.abort(), []);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [reload, setReload] = useState(0);
  const [confirm, setConfirm] = useState<Row | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    async function load() {
      try {
        const response = await subjectApi.getSemesters(controller.signal);
        const semesters: SemesterDto[] = response.data.semesters;
        const loaded = await Promise.all(semesters.map(async semester => {
          const response = await subjectApi.getTeachingStaff({ semester: semester.semester, year: semester.year }, controller.signal);
          const staff: TeachingStaffDto[] = response.data.staff;
          return { semester, staff: staff.find(x => x.userId === profile.userId && x.role === 'MENTOR') };
        }));
        if (!controller.signal.aborted) setRows(loaded);
      } catch (cause) { if (!controller.signal.aborted) setError(parseApiError(cause, 'Could not load semester availability.').message); }
      finally { if (!controller.signal.aborted) setLoading(false); }
    }
    void load();
    return () => controller.abort();
  }, [profile.userId, reload]);
  const change = async (row: Row) => {
    if (busy.current) return;
    busy.current = true; setSaving(true); setError(''); setNotice('');
    const controller = new AbortController(); mutationController.current = controller;
    try {
      if (row.staff) await subjectApi.updateTeachingStaff(row.staff._id, {
        status: row.staff.status === 'Active' ? 'Inactive' : 'Active', rowVersion: row.staff.rowVersion,
      }, controller.signal);
      else await subjectApi.addTeachingStaff({ semester: row.semester.semester, year: row.semester.year, userId: profile.userId, role: 'MENTOR' }, controller.signal);
      if (!controller.signal.aborted) { setNotice('Semester availability saved.'); setReload(x => x + 1); }
    } catch (cause) { if (!controller.signal.aborted) setError(parseApiError(cause, 'Could not update availability. Reload and try again.').message); }
    finally { busy.current = false; if (!controller.signal.aborted) { setSaving(false); setConfirm(null); } }
  };
  return <Modal isOpen onClose={() => { if (!busy.current) onClose(); }} title={`Semester availability · ${profile.fullName}`} size="lg">
    <p className="mb-4 text-sm text-slate-600">Available mentors can be assigned and recommended for that semester. Availability cannot be removed while the mentor has active assignments.</p>
    {error && <p role="alert" className="mb-3 rounded-lg bg-red-50 p-3 text-red-700">{error} <Button variant="ghost" disabled={saving} onClick={() => { setError(''); setReload(x => x + 1); }}>Retry</Button></p>}
    {notice && <p role="status" className="mb-3 text-green-700">{notice}</p>}
    {loading ? <p role="status">Loading semesters…</p> : rows.length === 0 ? <p>No semesters have been created.</p> :
      <ul className="divide-y divide-slate-200">{rows.map(row => <li key={row.semester.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
        <div><p className="font-medium">{row.semester.semester} {row.semester.year}</p><p className="text-sm text-slate-500">{row.semester.status} · {row.staff?.status === 'Active' ? 'Available' : 'Unavailable'}</p></div>
        <Button variant="outline" size="sm" disabled={saving || !['Planned', 'Active'].includes(row.semester.status)} onClick={() => {
          if (row.staff?.status === 'Active') setConfirm(row); else void change(row);
        }}>{row.staff?.status === 'Active' ? 'Make unavailable' : 'Make available'}</Button>
      </li>)}</ul>}
    <ConfirmDialog isOpen={confirm !== null} onClose={() => { if (!busy.current) setConfirm(null); }} onConfirm={() => { if (confirm) return change(confirm); }}
      title="Make mentor unavailable?" description={`Remove availability for ${confirm?.semester.semester ?? ''} ${confirm?.semester.year ?? ''}. Existing assignments must be reassigned first.`}
      confirmText="Make unavailable" isSubmitting={saving} />
  </Modal>;
}
