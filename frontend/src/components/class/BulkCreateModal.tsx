import { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import {
  AlertTriangle,
  CheckCircle2,
  GraduationCap,
  Loader2,
  Search,
  X,
} from 'lucide-react';
import { classApi } from '../../api/classApi';
import { subjectApi } from '../../api/subjectApi';
import { parseApiError } from '../../utils/apiError';
import { unwrapApiData } from '../../utils/classMappers';
import { parseClassPositions } from '../../utils/bulkClassAssignments';
import type {
  BulkClassPreviewResponse,
  ClassDto,
  CreateBulkClassesRequest,
} from '../../types/classes';
import type { ClassCreationSemesterOption } from '../../types/subjects';

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

interface BulkCreateModalProps {
  onClose: () => void;
  onCreated: (options?: { keepOpen?: boolean; suppressToast?: boolean }) => void;
}

const MAXIMUM_BATCH_SIZE = 100;
const LECTURER_COLORS = [
  { chip: 'border-blue-200 bg-blue-50 text-blue-700', dot: 'bg-blue-500' },
  { chip: 'border-violet-200 bg-violet-50 text-violet-700', dot: 'bg-violet-500' },
  { chip: 'border-emerald-200 bg-emerald-50 text-emerald-700', dot: 'bg-emerald-500' },
  { chip: 'border-amber-200 bg-amber-50 text-amber-700', dot: 'bg-amber-500' },
  { chip: 'border-pink-200 bg-pink-50 text-pink-700', dot: 'bg-pink-500' },
  { chip: 'border-cyan-200 bg-cyan-50 text-cyan-700', dot: 'bg-cyan-500' },
] as const;

export default function BulkCreateModal({
  onClose,
  onCreated,
}: BulkCreateModalProps) {
  const [subjects, setSubjects] = useState<SubjectOption[]>([]);
  const [semesters, setSemesters] = useState<ClassCreationSemesterOption[]>([]);
  const [lecturers, setLecturers] = useState<LecturerOption[]>([]);
  const [loadingOptions, setLoadingOptions] = useState(true);
  const [loadingLecturers, setLoadingLecturers] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [lecturerSearch, setLecturerSearch] = useState('');
  const [assignmentMode, setAssignmentMode] = useState<AssignmentMode>('assign');
  const [lecturerPositionInputs, setLecturerPositionInputs] = useState<Record<string, string>>({});
  const [form, setForm] = useState({
    subjectCode: '',
    semesterId: '',
    startClassIndex: '1',
    quantity: '1',
  });
  const [serverPreview, setServerPreview] = useState<BulkClassPreviewResponse | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    const loadOptions = async () => {
      setLoadingOptions(true);
      try {
        const [subjectsResponse, semestersResponse] = await Promise.all([
          subjectApi.getActive(),
          subjectApi.getClassCreationSemesterOptions(),
        ]);

        if (!active) return;

        const subjectPayload = unwrapApiData<any>(subjectsResponse);
        const semesterPayload = unwrapApiData<any>(semestersResponse);
        const subjectList = (subjectPayload?.subjects || []) as SubjectOption[];
        const semesterList = (semesterPayload?.semesters || []) as ClassCreationSemesterOption[];

        setSubjects(subjectList);
        setSemesters(semesterList);
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
  }, []);

  const selectedSemester = semesters.find(semester => semester.id === form.semesterId);
  useEffect(() => {
    if (!selectedSemester) {
      setLecturers([]);
      return;
    }

    let active = true;
    setLoadingLecturers(true);
    subjectApi.getTeachingStaff({
      semester: selectedSemester.semester,
      year: selectedSemester.year,
    }).then(response => {
      if (!active) return;
      const payload = unwrapApiData<any>(response);
      const options = (payload?.staff || [])
        .filter((member: any) => member.role === 'LECTURER' && member.status === 'Active' && member.userStatus === 'Active')
        .map((member: any) => ({
          _id: member.userId,
          name: member.name,
          email: member.email,
        }));
      setLecturers(options);
      setLecturerPositionInputs({});
      setServerPreview(null);
    }).catch(error => {
      if (active) {
        setLecturers([]);
        toast.error(parseApiError(error, 'Failed to load semester lecturers.').message);
      }
    }).finally(() => {
      if (active) setLoadingLecturers(false);
    });

    return () => {
      active = false;
    };
  }, [selectedSemester?.semester, selectedSemester?.year]);
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

  const lecturerAssignments = useMemo(() => lecturers.map((lecturer, lecturerIndex) => ({
    lecturer,
    lecturerIndex,
    ...parseClassPositions(lecturerPositionInputs[lecturer._id] || '', quantity),
  })), [lecturerPositionInputs, lecturers, quantity]);

  const positionAssignments = useMemo(() => {
    const assignments = new Map<number, { lecturer: LecturerOption; lecturerIndex: number }>();
    const conflicts = new Set<number>();
    for (const assignment of lecturerAssignments) {
      if (assignment.error) continue;
      for (const position of assignment.positions) {
        if (assignments.has(position)) conflicts.add(position);
        else assignments.set(position, assignment);
      }
    }
    return { assignments, conflicts };
  }, [lecturerAssignments]);

  const updateForm = (field: keyof typeof form, value: string) => {
    setForm(current => ({ ...current, [field]: value }));
    if (field === 'semesterId') {
      setLecturerPositionInputs({});
    }
    setServerPreview(null);
  };

  const selectAssignmentMode = (mode: AssignmentMode) => {
    setAssignmentMode(mode);
    setServerPreview(null);
    if (mode === 'unassigned') {
      setLecturerPositionInputs({});
    }
  };

  const updateLecturerPositions = (lecturerId: string, value: string) => {
    setLecturerPositionInputs(current => ({ ...current, [lecturerId]: value }));
    setServerPreview(null);
  };

  const validate = (): string | null => {
    if (!form.subjectCode) return 'Select an active subject.';
    if (!form.semesterId || !selectedSemester) return 'Select the current or next semester.';
    if (!Number.isInteger(startIndex) || startIndex < 1 || startIndex > 999) {
      return 'Starting class index must be between 1 and 999.';
    }
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > MAXIMUM_BATCH_SIZE) {
      return `Number of classes must be between 1 and ${MAXIMUM_BATCH_SIZE}.`;
    }
    if (startIndex + quantity - 1 > 999) return 'Generated class indices must not exceed 999.';
    if (assignmentMode === 'assign') {
      if (loadingLecturers) return 'Wait for the semester lecturer list to finish loading.';
      if (lecturers.length === 0) return 'Add an active lecturer to this semester before assigning classes.';
      const assignmentError = lecturerAssignments.find(item => item.error);
      if (assignmentError?.error) return `${assignmentError.lecturer.name}: ${assignmentError.error}`;
      if (positionAssignments.conflicts.size > 0) {
        return `Class position(s) ${Array.from(positionAssignments.conflicts).sort((a, b) => a - b).join(', ')} are assigned more than once.`;
      }
      const missing = Array.from({ length: quantity }, (_, index) => index + 1)
        .filter(position => !positionAssignments.assignments.has(position));
      if (missing.length > 0) return `Assign every class. Missing position(s): ${missing.join(', ')}.`;
    }
    return null;
  };

  const buildRequest = (): CreateBulkClassesRequest => {
    const assignments = lecturerAssignments
      .filter(item => !item.error && item.positions.length > 0)
      .map(item => ({
        lecturerId: item.lecturer._id,
        classIndices: item.positions.map(position => startIndex + position - 1),
      }));
    return {
      subjectCode: form.subjectCode,
      semesterId: form.semesterId,
      startClassIndex: startIndex,
      quantity,
      lecturerAssignments: assignmentMode === 'assign' ? assignments : undefined,
    };
  };

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
      const assignedLecturerCount = lecturerAssignments.filter(item => item.positions.length > 0).length;
      toast.success(
        assignmentMode === 'assign'
          ? `${createdClasses.length} class(es) created and assigned across ${assignedLecturerCount} lecturer(s).`
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
      <div className="relative flex max-h-[94vh] w-full max-w-4xl flex-col overflow-hidden rounded-2xl bg-white shadow-float animate-scale-in" role="dialog" aria-modal="true" aria-labelledby="create-classes-title">
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
                      {semesters.map(semester => <option key={semester.id} value={semester.id}>{semester.semester} {semester.year} — {semester.availability} ({semester.status})</option>)}
                    </select>
                    {semesters.length === 0 && <p className="mt-1 text-[11px] text-amber-600">Complete expired semesters and configure the current or next semester first.</p>}
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
                    <div className="mb-2 flex items-center justify-between gap-3">
                      <p className="text-xs font-semibold text-slate-500">Generated class codes</p>
                      <p className="text-[11px] text-slate-400">The number before each code is its assignment position.</p>
                    </div>
                    <div className="max-h-44 overflow-y-auto pr-1">
                      <div className="flex flex-wrap gap-2">
                        {clientCodes.map((code, index) => {
                          const position = index + 1;
                          const conflict = positionAssignments.conflicts.has(position);
                          const assignment = positionAssignments.assignments.get(position);
                          const color = assignment ? LECTURER_COLORS[assignment.lecturerIndex % LECTURER_COLORS.length] : null;
                          return (
                            <div
                              key={code}
                              className={`rounded-lg border px-2.5 py-1.5 shadow-xs ${
                                conflict
                                  ? 'border-red-300 bg-red-50 text-red-700'
                                  : assignmentMode === 'assign' && color
                                    ? color.chip
                                    : 'border-slate-200 bg-white text-slate-600'
                              }`}
                              title={conflict ? 'Assigned to multiple lecturers' : assignment?.lecturer.name || 'Unassigned'}
                            >
                              <div className="flex items-center gap-1.5">
                                <span className="text-[10px] font-bold opacity-70">#{position}</span>
                                <span className="font-mono text-xs font-semibold">{code}</span>
                              </div>
                              {assignmentMode === 'assign' && assignment && !conflict && (
                                <p className="mt-0.5 max-w-28 truncate text-[10px] font-medium">{assignment.lecturer.name}</p>
                              )}
                            </div>
                          );
                        })}
                      </div>
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
                    <div className="mb-3 rounded-lg bg-blue-50 px-3 py-2 text-xs text-blue-700">
                      Enter generated positions for each lecturer. Use commas and ranges, for example <span className="font-semibold">2,4,8</span> or <span className="font-semibold">1-3,6</span>.
                    </div>
                    <div className="relative mb-2">
                      <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                      <input type="search" value={lecturerSearch} onChange={event => setLecturerSearch(event.target.value)} placeholder="Search lecturer by name or email..." className="w-full rounded-xl border border-slate-200 py-2 pl-9 pr-3 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" />
                    </div>
                    <div className="max-h-64 space-y-2 overflow-y-auto pr-1">
                      {filteredLecturers.length === 0 ? (
                        <p className="py-6 text-center text-xs text-slate-400">No active lecturers found</p>
                      ) : filteredLecturers.map(lecturer => {
                        const lecturerIndex = lecturers.findIndex(item => item._id === lecturer._id);
                        const color = LECTURER_COLORS[lecturerIndex % LECTURER_COLORS.length];
                        const parsed = parseClassPositions(lecturerPositionInputs[lecturer._id] || '', quantity);
                        return (
                          <div key={lecturer._id} className={`grid gap-2 rounded-xl border p-2.5 sm:grid-cols-[minmax(0,1fr)_220px] ${parsed.error ? 'border-red-200 bg-red-50/40' : 'border-slate-100 bg-slate-50/60'}`}>
                            <div className="flex min-w-0 items-center gap-2">
                              <div className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-bold text-white ${color.dot}`}>{lecturer.name.charAt(0).toUpperCase()}</div>
                              <div className="min-w-0"><p className="truncate text-xs font-semibold text-slate-800">{lecturer.name}</p><p className="truncate text-[11px] text-slate-400">{lecturer.email}</p></div>
                            </div>
                            <div>
                              <input
                                type="text"
                                inputMode="numeric"
                                value={lecturerPositionInputs[lecturer._id] || ''}
                                onChange={event => updateLecturerPositions(lecturer._id, event.target.value)}
                                placeholder="e.g. 2,4,8 or 1-3"
                                aria-label={`Class positions assigned to ${lecturer.name}`}
                                className={`w-full rounded-lg border bg-white px-3 py-2 text-xs outline-none focus:ring-2 ${parsed.error ? 'border-red-300 focus:border-red-400 focus:ring-red-100' : 'border-slate-200 focus:border-primary focus:ring-primary/20'}`}
                              />
                              {parsed.error && <p className="mt-1 text-[10px] text-red-600">{parsed.error}</p>}
                            </div>
                          </div>
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
