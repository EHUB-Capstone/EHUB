import { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import {
  AlertTriangle,
  Check,
  CheckCircle2,
  GraduationCap,
  Loader2,
  Search,
  X,
} from 'lucide-react';
import { classApi } from '../../api/classApi';
import { subjectApi } from '../../api/subjectApi';
import { userApi } from '../../api/userApi';
import { parseApiError } from '../../utils/apiError';
import { unwrapApiData } from '../../utils/classMappers';
import { buildApprovedLecturerQuery, normalizeLecturerOptions } from '../../utils/lecturerDirectory';
import type {
  BulkClassPreviewResponse,
  ClassDto,
  CreateBulkClassesRequest,
} from '../../types/classes';

type AssignmentMode = 'assign' | 'unassigned';

interface LecturerOption {
  _id: string;
  name: string;
  email?: string | null;
}

interface SubjectOption {
  subjectCode: string;
  subjectName?: string;
}

interface SemesterOption {
  id: string;
  semester: 'SP' | 'SU' | 'FA';
  year: number;
  status: 'Planned' | 'Active' | 'Completed' | 'Archived';
}

interface BulkCreateModalProps {
  lecturers?: LecturerOption[];
  onClose: () => void;
  onCreated: (options?: { keepOpen?: boolean; suppressToast?: boolean }) => void;
}

const MAXIMUM_BATCH_SIZE = 100;

export default function BulkCreateModal({
  lecturers: initialLecturers = [],
  onClose,
  onCreated,
}: BulkCreateModalProps) {
  const [subjects, setSubjects] = useState<SubjectOption[]>([]);
  const [semesters, setSemesters] = useState<SemesterOption[]>([]);
  const [lecturers, setLecturers] = useState<LecturerOption[]>(() => normalizeLecturerOptions(initialLecturers));
  const [loadingOptions, setLoadingOptions] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [lecturerSearch, setLecturerSearch] = useState('');
  const [assignmentMode, setAssignmentMode] = useState<AssignmentMode>('assign');
  const [form, setForm] = useState({
    subjectCode: '',
    semesterId: '',
    startClassIndex: '1',
    quantity: '1',
    primaryLecturerId: '',
  });
  const [serverPreview, setServerPreview] = useState<BulkClassPreviewResponse | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    const loadOptions = async () => {
      setLoadingOptions(true);
      try {
        const [subjectsResponse, semestersResponse, lecturersResponse] = await Promise.all([
          subjectApi.getActive(),
          subjectApi.getSemesters(),
          initialLecturers.length === 0
            ? userApi.getAll(buildApprovedLecturerQuery(), { signal: controller.signal })
            : Promise.resolve(null),
        ]);

        if (!active) return;

        const subjectPayload = unwrapApiData<any>(subjectsResponse);
        const semesterPayload = unwrapApiData<any>(semestersResponse);
        const subjectList = (subjectPayload?.subjects || []) as SubjectOption[];
        const semesterList = ((semesterPayload?.semesters || []) as SemesterOption[])
          .filter(semester => semester.status === 'Active' || semester.status === 'Planned');

        setSubjects(subjectList);
        setSemesters(semesterList);
        if (lecturersResponse) {
          const lecturerPayload = unwrapApiData<any>(lecturersResponse);
          setLecturers(normalizeLecturerOptions(lecturerPayload?.users || []));
        }

        const defaultSemester = semesterList.find(semester => semester.status === 'Active') || semesterList[0];
        setForm(current => ({
          ...current,
          subjectCode: current.subjectCode || subjectList[0]?.subjectCode || '',
          semesterId: current.semesterId || defaultSemester?.id || '',
        }));
      } catch (error) {
        if (active && !controller.signal.aborted) {
          toast.error(parseApiError(error, 'Failed to load subjects, semesters, or lecturers.').message);
        }
      } finally {
        if (active) setLoadingOptions(false);
      }
    };

    void loadOptions();
    return () => {
      active = false;
      controller.abort();
    };
  }, [initialLecturers.length]);

  const selectedSemester = semesters.find(semester => semester.id === form.semesterId);
  const selectedLecturer = lecturers.find(lecturer => lecturer._id === form.primaryLecturerId);
  const filteredLecturers = useMemo(() => {
    const search = lecturerSearch.trim().toLowerCase();
    if (!search) return lecturers;
    return lecturers.filter(lecturer =>
      lecturer.name.toLowerCase().includes(search) || lecturer.email?.toLowerCase().includes(search));
  }, [lecturerSearch, lecturers]);

  const startIndex = Number(form.startClassIndex);
  const quantity = Number(form.quantity);
  const clientCodes = useMemo(() => {
    if (!form.subjectCode || !Number.isInteger(startIndex) || !Number.isInteger(quantity) || quantity < 1) return [];
    return Array.from(
      { length: Math.min(quantity, MAXIMUM_BATCH_SIZE) },
      (_, index) => `${form.subjectCode}_${startIndex + index}`,
    );
  }, [form.subjectCode, quantity, startIndex]);

  const updateForm = (field: keyof typeof form, value: string) => {
    setForm(current => ({ ...current, [field]: value }));
    setServerPreview(null);
  };

  const selectAssignmentMode = (mode: AssignmentMode) => {
    setAssignmentMode(mode);
    setServerPreview(null);
    if (mode === 'unassigned') {
      setForm(current => ({ ...current, primaryLecturerId: '' }));
    }
  };

  const validate = (): string | null => {
    if (!form.subjectCode) return 'Select an active subject.';
    if (!form.semesterId || !selectedSemester) return 'Select a planned or active semester.';
    if (!Number.isInteger(startIndex) || startIndex < 1 || startIndex > 999) {
      return 'Starting class index must be between 1 and 999.';
    }
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > MAXIMUM_BATCH_SIZE) {
      return `Number of classes must be between 1 and ${MAXIMUM_BATCH_SIZE}.`;
    }
    if (startIndex + quantity - 1 > 999) return 'Generated class indices must not exceed 999.';
    if (assignmentMode === 'assign' && !form.primaryLecturerId) return 'Select a lecturer or choose Create unassigned.';
    return null;
  };

  const buildRequest = (): CreateBulkClassesRequest => ({
    subjectCode: form.subjectCode,
    semesterId: form.semesterId,
    startClassIndex: startIndex,
    quantity,
    primaryLecturerId: assignmentMode === 'assign' ? form.primaryLecturerId : undefined,
  });

  const handleSubmit = async () => {
    const validationError = validate();
    if (validationError) {
      toast.error(validationError);
      return;
    }

    setSubmitting(true);
    try {
      const request = buildRequest();
      if (!serverPreview) {
        const preview = unwrapApiData<BulkClassPreviewResponse>(await classApi.previewBulkCreate(request));
        setServerPreview(preview);
        if (preview.invalidCount > 0) {
          toast.error(`${preview.invalidCount} class(es) cannot be created. Review the conflicts below.`);
        } else {
          toast.success('Preview is valid. Review and confirm the creation.');
        }
        return;
      }

      if (serverPreview.invalidCount > 0) {
        toast.error('Change the class indices before creating classes.');
        return;
      }

      const createdClasses = unwrapApiData<ClassDto[]>(await classApi.commitBulkCreate(request));
      toast.success(
        assignmentMode === 'assign'
          ? `${createdClasses.length} class(es) created and assigned to ${selectedLecturer?.name}.`
          : `${createdClasses.length} unassigned Draft class(es) created.`,
      );
      onCreated({ suppressToast: true });
    } catch (error) {
      setServerPreview(null);
      toast.error(parseApiError(error, 'Failed to create classes.').message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <button type="button" className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} aria-label="Close create classes dialog" />
      <div className="relative flex max-h-[94vh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl bg-white shadow-float animate-scale-in" role="dialog" aria-modal="true" aria-labelledby="create-classes-title">
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-5">
          <div>
            <h2 id="create-classes-title" className="text-xl font-bold text-slate-900">Create Classes</h2>
            <p className="mt-0.5 text-sm text-slate-400">Create one or more classes and optionally assign their lecturer now</p>
          </div>
          <button type="button" onClick={onClose} className="rounded-xl p-2 text-slate-400 transition hover:bg-slate-100 hover:text-slate-600" aria-label="Close">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="min-h-0 flex-1 space-y-5 overflow-y-auto p-6">
          {loadingOptions ? (
            <div className="flex items-center justify-center py-20">
              <Loader2 className="h-7 w-7 animate-spin text-primary" />
            </div>
          ) : (
            <>
              <section>
                <div className="mb-3">
                  <h3 className="text-sm font-bold text-slate-800">1. Academic information</h3>
                  <p className="text-xs text-slate-400">Class codes are generated by the server from subject and class index.</p>
                </div>
                <div className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <label htmlFor="create-class-subject" className="mb-1 block text-xs font-semibold text-slate-600">Subject *</label>
                    <select id="create-class-subject" value={form.subjectCode} onChange={event => updateForm('subjectCode', event.target.value)} className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20">
                      {subjects.length === 0 && <option value="">No active subjects</option>}
                      {subjects.map(subject => <option key={subject.subjectCode} value={subject.subjectCode}>{subject.subjectCode}{subject.subjectName ? ` — ${subject.subjectName}` : ''}</option>)}
                    </select>
                  </div>
                  <div>
                    <label htmlFor="create-class-semester" className="mb-1 block text-xs font-semibold text-slate-600">Semester *</label>
                    <select id="create-class-semester" value={form.semesterId} onChange={event => updateForm('semesterId', event.target.value)} className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20">
                      {semesters.length === 0 && <option value="">No open semesters</option>}
                      {semesters.map(semester => <option key={semester.id} value={semester.id}>{semester.semester} {semester.year} — {semester.status}</option>)}
                    </select>
                  </div>
                  <div>
                    <label htmlFor="create-class-index" className="mb-1 block text-xs font-semibold text-slate-600">Starting class index *</label>
                    <input id="create-class-index" type="number" min="1" max="999" value={form.startClassIndex} onChange={event => updateForm('startClassIndex', event.target.value)} className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" />
                  </div>
                  <div>
                    <label htmlFor="create-class-quantity" className="mb-1 block text-xs font-semibold text-slate-600">Number of classes *</label>
                    <input id="create-class-quantity" type="number" min="1" max={MAXIMUM_BATCH_SIZE} value={form.quantity} onChange={event => updateForm('quantity', event.target.value)} className="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" />
                  </div>
                </div>

                {clientCodes.length > 0 && (
                  <div className="mt-3 rounded-xl border border-slate-100 bg-slate-50 p-3">
                    <p className="mb-2 text-xs font-semibold text-slate-500">Generated class codes</p>
                    <div className="flex flex-wrap gap-1.5">
                      {clientCodes.slice(0, 8).map(code => <span key={code} className="rounded-lg bg-white px-2 py-1 font-mono text-xs text-primary shadow-xs">{code}</span>)}
                      {clientCodes.length > 8 && <span className="px-2 py-1 text-xs font-medium text-slate-400">+{clientCodes.length - 8} more</span>}
                    </div>
                  </div>
                )}
              </section>

              <section className="border-t border-slate-100 pt-5">
                <h3 className="text-sm font-bold text-slate-800">2. Lecturer assignment</h3>
                <p className="mb-3 text-xs text-slate-400">Assign now is recommended. Unassigned classes remain Draft and are invisible to lecturers.</p>
                <div className="grid gap-2 sm:grid-cols-2">
                  <button type="button" onClick={() => selectAssignmentMode('assign')} className={`rounded-xl border p-3 text-left transition ${assignmentMode === 'assign' ? 'border-primary bg-primary-50/60 ring-2 ring-primary/10' : 'border-slate-200 hover:bg-slate-50'}`}>
                    <div className="flex items-center gap-2">
                      <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary-100"><GraduationCap className="h-4 w-4 text-primary" /></div>
                      <div><p className="text-sm font-semibold text-slate-800">Assign now</p><p className="text-xs text-slate-400">Create and assign in one operation</p></div>
                      {assignmentMode === 'assign' && <CheckCircle2 className="ml-auto h-4 w-4 text-primary" />}
                    </div>
                  </button>
                  <button type="button" onClick={() => selectAssignmentMode('unassigned')} className={`rounded-xl border p-3 text-left transition ${assignmentMode === 'unassigned' ? 'border-amber-400 bg-amber-50 ring-2 ring-amber-100' : 'border-slate-200 hover:bg-slate-50'}`}>
                    <div className="flex items-center gap-2">
                      <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-amber-100"><AlertTriangle className="h-4 w-4 text-amber-600" /></div>
                      <div><p className="text-sm font-semibold text-slate-800">Create unassigned</p><p className="text-xs text-slate-400">Assign a lecturer later</p></div>
                      {assignmentMode === 'unassigned' && <CheckCircle2 className="ml-auto h-4 w-4 text-amber-600" />}
                    </div>
                  </button>
                </div>

                {assignmentMode === 'assign' && (
                  <div className="mt-3 rounded-xl border border-slate-200 p-3">
                    <div className="relative mb-2">
                      <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                      <input type="search" value={lecturerSearch} onChange={event => setLecturerSearch(event.target.value)} placeholder="Search lecturer by name or email..." className="w-full rounded-xl border border-slate-200 py-2 pl-9 pr-3 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" />
                    </div>
                    <div className="max-h-44 space-y-1 overflow-y-auto">
                      {filteredLecturers.length === 0 ? (
                        <p className="py-6 text-center text-xs text-slate-400">No active lecturers found</p>
                      ) : filteredLecturers.map(lecturer => {
                        const selected = lecturer._id === form.primaryLecturerId;
                        return (
                          <button key={lecturer._id} type="button" onClick={() => updateForm('primaryLecturerId', lecturer._id)} className={`flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left transition ${selected ? 'bg-primary-50 text-primary' : 'hover:bg-slate-50'}`}>
                            <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary-100 text-xs font-bold text-primary">{lecturer.name.charAt(0).toUpperCase()}</div>
                            <div className="min-w-0"><p className="truncate text-xs font-semibold text-slate-800">{lecturer.name}</p><p className="truncate text-[11px] text-slate-400">{lecturer.email}</p></div>
                            {selected && <Check className="ml-auto h-4 w-4 shrink-0" />}
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}
              </section>

              {serverPreview && (
                <section className={`rounded-xl border p-4 ${serverPreview.invalidCount > 0 ? 'border-red-200 bg-red-50' : 'border-green-200 bg-green-50'}`}>
                  <div className="flex items-center justify-between gap-3">
                    <div><p className="text-sm font-bold text-slate-800">Server validation</p><p className="text-xs text-slate-500">{serverPreview.validCount} of {serverPreview.totalCount} classes are ready</p></div>
                    {serverPreview.invalidCount === 0 ? <CheckCircle2 className="h-5 w-5 text-green-600" /> : <AlertTriangle className="h-5 w-5 text-red-600" />}
                  </div>
                  <div className="mt-3 max-h-36 space-y-1 overflow-y-auto">
                    {serverPreview.items.map(item => (
                      <div key={`${item.classCode}-${item.classIndex}`} className="flex items-start justify-between gap-3 rounded-lg bg-white/70 px-2.5 py-2 text-xs">
                        <span className="font-mono font-semibold text-slate-700">{item.classCode}</span>
                        <span className={item.isValid ? 'text-green-700' : 'text-red-700'}>{item.isValid ? (item.primaryLecturerName ? `Assign to ${item.primaryLecturerName}` : 'Create as unassigned Draft') : item.errorMessage}</span>
                      </div>
                    ))}
                  </div>
                </section>
              )}
            </>
          )}
        </div>

        <div className="flex gap-3 border-t border-slate-100 bg-white px-6 py-4">
          <button type="button" onClick={onClose} className="flex-1 rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-medium text-slate-600 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={handleSubmit} disabled={loadingOptions || submitting} className="flex flex-1 items-center justify-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-primary-700 disabled:opacity-50">
            {submitting ? <><Loader2 className="h-4 w-4 animate-spin" /> Processing...</> : serverPreview ? 'Confirm & Create' : 'Preview Classes'}
          </button>
        </div>
      </div>
    </div>
  );
}
