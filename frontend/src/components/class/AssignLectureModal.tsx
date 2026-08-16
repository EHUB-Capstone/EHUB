import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { X, Loader2, Search, Check } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { userApi } from '../../api/userApi';
import { parseApiError } from '../../utils/apiError';
import { buildApprovedLecturerQuery, normalizeLecturerOptions } from '../../utils/lecturerDirectory';

const EMPTY_LECTURERS = [];

export default function AssignLectureModal({ classId, currentLecture, rowVersion, allowUnassign = false, initialLecturers = EMPTY_LECTURERS, onClose, onAssigned }) {
  const [lecturers, setLecturers] = useState(() => normalizeLecturerOptions(initialLecturers));
  const [selectedId, setSelectedId] = useState(currentLecture?._id || currentLecture?.id || '');
  const [searchTerm, setSearchTerm] = useState('');
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (initialLecturers.length > 0) {
      setLecturers(normalizeLecturerOptions(initialLecturers));
      setLoading(false);
      return undefined;
    }

    const controller = new AbortController();
    let active = true;
    const fetchLecturers = async () => {
      try {
        const res = await userApi.getAll(buildApprovedLecturerQuery(), { signal: controller.signal });
        const list = res?.data?.users || res?.users || [];
        if (active) setLecturers(normalizeLecturerOptions(list));
      } catch (error) {
        if (active && !controller.signal.aborted) {
          toast.error(parseApiError(error, 'Failed to load lecturers.').message);
        }
      } finally {
        if (active) setLoading(false);
      }
    };
    void fetchLecturers();
    return () => {
      active = false;
      controller.abort();
    };
  }, [initialLecturers]);

  const handleSubmit = async () => {
    if (!selectedId && !currentLecture) {
      toast.error('Select an active lecturer before saving.');
      return;
    }
    if (!rowVersion) {
      toast.error('Class data is stale. Reload the page and try again.');
      return;
    }
    setSubmitting(true);
    try {
      await classApi.updateTeachingAssignment(classId, {
        primaryLecturerId: selectedId || null,
        rowVersion,
      });
      toast.success(selectedId ? 'Lecturer assigned successfully!' : 'Lecturer unassigned successfully!');
      await onAssigned();
    } catch (e) {
      toast.error(parseApiError(e, 'Failed to assign lecturer').message);
    } finally {
      setSubmitting(false);
    }
  };

  const filtered = lecturers.filter(l =>
    l.name?.toLowerCase().includes(searchTerm.toLowerCase()) ||
    l.email?.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-xs animate-fade-in" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-float w-full max-w-md overflow-hidden animate-scale-in">
        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b border-slate-100">
          <div>
            <h2 className="text-lg font-bold text-slate-900">Assign Lecturer</h2>
            <p className="text-xs text-slate-400 mt-0.5 font-medium">Select a lecturer for this class</p>
          </div>
          <button onClick={onClose} className="p-2 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-all">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="p-5 space-y-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <input
              type="text"
              placeholder="Search lecturers..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full pl-9 pr-4 py-2 border border-slate-200 rounded-xl text-xs outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-10">
              <Loader2 className="w-6 h-6 text-primary animate-spin" />
            </div>
          ) : (
            <div className="max-h-60 overflow-y-auto space-y-1.5 border border-slate-100 rounded-xl p-2.5 bg-slate-50/50">
              {allowUnassign && (
                <button
                  type="button"
                  onClick={() => setSelectedId('')}
                  className={`w-full p-2.5 rounded-xl border text-left text-xs font-semibold transition-all ${
                    selectedId === '' ? 'bg-primary-50/40 border-primary/20 text-primary' : 'bg-white border-slate-200/60 text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  Unassigned (Draft classes only)
                </button>
              )}
              {filtered.length === 0 ? (
                <p className="text-xs text-slate-400 text-center py-6">No lecturers found</p>
              ) : (
                filtered.map(l => {
                  const isSelected = selectedId === l._id;
                  return (
                    <div
                      key={l._id}
                      onClick={() => setSelectedId(l._id)}
                      className={`flex items-center justify-between p-2.5 rounded-xl border cursor-pointer transition-all ${
                        isSelected
                          ? 'bg-primary-50/40 border-primary/20 shadow-xs'
                          : 'bg-white border-slate-200/60 hover:bg-slate-50'
                      }`}
                    >
                      <div className="flex items-center gap-2.5 min-w-0">
                        <div className="w-8 h-8 rounded-full bg-primary-100 flex items-center justify-center text-primary font-bold text-xs shrink-0">
                          {l.name?.charAt(0)?.toUpperCase() || 'L'}
                        </div>
                        <div className="min-w-0">
                          <p className="text-xs font-semibold text-slate-800 truncate">{l.name}</p>
                          <p className="text-[10px] text-slate-400 truncate">{l.email}</p>
                        </div>
                      </div>
                      {isSelected && (
                        <div className="w-5 h-5 rounded-full bg-primary flex items-center justify-center shrink-0">
                          <Check className="w-3 h-3 text-white" />
                        </div>
                      )}
                    </div>
                  );
                })
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex gap-3 p-5 pt-0">
          <button onClick={onClose} className="flex-1 px-4 py-2 border border-slate-200 rounded-xl text-xs font-semibold text-slate-500 hover:bg-slate-50 transition-all">
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={submitting || loading}
            className="flex-1 px-4 py-2 bg-primary text-white rounded-xl text-xs font-semibold hover:bg-primary-700 disabled:opacity-50 transition-all flex items-center justify-center gap-1.5 shadow-sm"
          >
            {submitting ? <><Loader2 className="w-3.5 h-3.5 animate-spin" /> Saving...</> : 'Save Assignment'}
          </button>
        </div>
      </div>
    </div>
  );
}
