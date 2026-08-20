import { useEffect, useMemo, useState } from 'react';
import { Check, GraduationCap, Search } from 'lucide-react';
import Modal from '../ui/Modal';
import Button from '../ui/Button';
import type { ClassViewModel } from '../../types/classes';
import { normalizeLecturerOptions } from '../../utils/lecturerDirectory';

interface LecturerOption {
  _id: string;
  name: string;
  email?: string | null;
}

interface BulkAssignLecturerModalProps {
  isOpen: boolean;
  classes: ClassViewModel[];
  lecturers: LecturerOption[];
  isSubmitting: boolean;
  onClose: () => void;
  onAssign: (lecturerId: string) => void | Promise<void>;
}

const BulkAssignLecturerModal = ({
  isOpen,
  classes,
  lecturers,
  isSubmitting,
  onClose,
  onAssign,
}: BulkAssignLecturerModalProps) => {
  const [selectedId, setSelectedId] = useState('');
  const [search, setSearch] = useState('');
  useEffect(() => {
    if (isOpen) {
      setSelectedId('');
      setSearch('');
    }
  }, [isOpen]);
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
          {options.length === 0 ? (
            <p className="py-8 text-center text-sm text-slate-400">No active lecturers found</p>
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
            disabled={!selectedId || isSubmitting}
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
