import { useEffect, useMemo, useState } from 'react';
import { Check, GraduationCap, Loader2, Search } from 'lucide-react';
import toast from 'react-hot-toast';
import Modal from '../ui/Modal';
import Button from '../ui/Button';
import type { ClassViewModel } from '../../types/classes';
import { normalizeLecturerOptions } from '../../utils/lecturerDirectory';
import { subjectApi } from '../../api/subjectApi';
import { unwrapApiData } from '../../utils/classMappers';
import { parseApiError } from '../../utils/apiError';

interface LecturerOption {
  _id: string;
  name: string;
  email?: string | null;
}

interface BulkAssignLecturerModalProps {
  isOpen: boolean;
  classes: ClassViewModel[];
  isSubmitting: boolean;
  onClose: () => void;
  onAssign: (lecturerId: string) => void | Promise<void>;
}

const BulkAssignLecturerModal = ({
  isOpen,
  classes,
  isSubmitting,
  onClose,
  onAssign,
}: BulkAssignLecturerModalProps) => {
  const [selectedId, setSelectedId] = useState('');
  const [search, setSearch] = useState('');
  const [lecturers, setLecturers] = useState<LecturerOption[]>([]);
  const [loadingLecturers, setLoadingLecturers] = useState(false);
  useEffect(() => {
    if (isOpen) {
      setSelectedId('');
      setSearch('');
    }
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen || classes.length === 0) return;

    let active = true;
    const semesters = Array.from(new Map(classes.map(item => [
      `${item.semester}:${item.year}`,
      {
        semester: item.semester as 'SP' | 'SU' | 'FA',
        year: item.year,
      },
    ])).values());

    setLoadingLecturers(true);
    Promise.all(semesters.map(item => subjectApi.getTeachingStaff(item)))
      .then(responses => {
        if (!active) return;
        const semesterOptions = responses.map(response => {
          const payload = unwrapApiData<any>(response);
          return (payload?.staff || [])
            .filter((member: any) => member.role === 'LECTURER' && member.status === 'Active' && member.userStatus === 'Active')
            .map((member: any) => ({
              _id: member.userId,
              name: member.name,
              email: member.email,
            }));
        });
        const firstSemester = semesterOptions[0] || [];
        const sharedLecturers = firstSemester.filter(lecturer =>
          semesterOptions.every(optionsForSemester =>
            optionsForSemester.some(candidate => candidate._id === lecturer._id)));
        setLecturers(sharedLecturers);
      })
      .catch(error => {
        if (active) {
          setLecturers([]);
          toast.error(parseApiError(error, 'Failed to load semester lecturers.').message);
        }
      })
      .finally(() => {
        if (active) setLoadingLecturers(false);
      });

    return () => {
      active = false;
    };
  }, [classes, isOpen]);
  const options = useMemo(
    () => normalizeLecturerOptions(lecturers).filter((lecturer) => {
      const query = search.trim().toLowerCase();
      return !query || lecturer.name?.toLowerCase().includes(query) || lecturer.email?.toLowerCase().includes(query);
    }),
    [lecturers, search],
  );

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="Assign Lecturer to Selected Classes" size="lg">
      <div className="space-y-5">
        <div className="rounded-xl border border-primary-100 bg-primary-50/50 p-3">
          <p className="text-sm font-semibold text-primary">{classes.length} classes will use the same lecturer</p>
          <div className="mt-2 flex max-h-20 flex-wrap gap-1.5 overflow-y-auto">
            {classes.map((item) => (
              <span key={item._id} className="rounded-md bg-white px-2 py-1 font-mono text-xs font-semibold text-slate-600 shadow-xs">
                {item.classCode}
              </span>
            ))}
          </div>
          <p className="mt-2 text-xs leading-5 text-slate-500">
            Each class is validated independently. Schedule conflicts or stale class versions will be reported without reverting successful assignments.
          </p>
        </div>

        <div className="relative">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search lecturers by name or email..."
            className="w-full rounded-xl border border-slate-200 py-2.5 pl-9 pr-3 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/15"
          />
        </div>

        <div className="max-h-72 space-y-2 overflow-y-auto rounded-xl border border-slate-100 bg-slate-50/60 p-2.5">
          {loadingLecturers ? (
            <div className="flex items-center justify-center py-8"><Loader2 className="h-5 w-5 animate-spin text-primary" /></div>
          ) : options.length === 0 ? (
            <p className="py-8 text-center text-sm text-slate-400">No active lecturer is listed for every selected semester</p>
          ) : options.map((lecturer) => {
            const selected = lecturer._id === selectedId;
            return (
              <button
                key={lecturer._id}
                type="button"
                onClick={() => setSelectedId(lecturer._id)}
                className={`flex w-full items-center gap-3 rounded-xl border p-3 text-left transition ${selected ? 'border-primary bg-primary-50 shadow-xs' : 'border-slate-200 bg-white hover:border-primary-200'}`}
              >
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary-100 text-primary">
                  <GraduationCap className="h-4 w-4" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-semibold text-slate-800">{lecturer.name}</span>
                  <span className="block truncate text-xs text-slate-400">{lecturer.email}</span>
                </span>
                {selected && <span className="flex h-6 w-6 items-center justify-center rounded-full bg-primary text-white"><Check className="h-3.5 w-3.5" /></span>}
              </button>
            );
          })}
        </div>

        <div className="flex gap-3 border-t border-slate-100 pt-4">
          <Button variant="outline" className="flex-1" onClick={onClose} disabled={isSubmitting}>Cancel</Button>
          <Button
            variant="gradient"
            className="flex-1"
            onClick={() => void onAssign(selectedId)}
            disabled={!selectedId || isSubmitting || loadingLecturers}
            isLoading={isSubmitting}
          >
            Assign to {classes.length} classes
          </Button>
        </div>
      </div>
    </Modal>
  );
};

export default BulkAssignLecturerModal;
