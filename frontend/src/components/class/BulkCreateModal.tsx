import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { X, Loader2, AlertTriangle } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { userApi } from '../../api/userApi';
import { subjectApi } from '../../api/subjectApi';

const CURRENT_YR  = new Date().getFullYear();

export default function BulkCreateModal({ lecturers: initialLecturers = [], isLecturer = false, onClose, onCreated }) {
  const [form, setForm] = useState({
    subjectCode: '',
    semester:    'SP',
    year:        String(CURRENT_YR),
    count:       '5',
    classIndicesText: '',
    lecturerIds: [],
    mentorIds:   [],
  });
  const [submitting, setSubmitting] = useState(false);
  const [preview,    setPreview]    = useState([]);
  const [classConflict, setClassConflict] = useState(null);

  const [allLecturers, setAllLecturers] = useState(initialLecturers);
  const [allMentors, setAllMentors] = useState([]);
  const [loadingUsers, setLoadingUsers] = useState(true);
  const [subjects, setSubjects] = useState([]);

  useEffect(() => {
    const fetchUsersAndSubjects = async () => {
      try {
        const [lectRes, mentorRes, subjRes, semRes] = await Promise.all([
          allLecturers.length === 0 ? userApi.getAll({ role: 'LECTURER', limit: 200 }) : Promise.resolve(null),
          userApi.getAll({ role: 'MENTOR', limit: 200 }),
          subjectApi.getActive(),
          subjectApi.getCurrentSemester()
        ]);
        if (lectRes) {
          setAllLecturers(lectRes?.data?.users || lectRes?.users || []);
        }
        setAllMentors(mentorRes?.data?.users || mentorRes?.users || []);
        
        const list = subjRes?.data?.subjects || subjRes?.subjects || [];
        setSubjects(list);
        
        const activeSem = semRes?.data?.currentSemester || semRes?.currentSemester || { semester: 'SP', year: new Date().getFullYear() };

        let defaultSubj = '';
        if (list.length > 0) {
          defaultSubj = list[0].subjectCode;
        }

        setForm(prev => ({
          ...prev,
          subjectCode: defaultSubj,
          semester: activeSem.semester,
          year: String(activeSem.year)
        }));

        if (defaultSubj && !isLecturer) {
          setPreview(Array.from({ length: Math.min(parseInt(form.count, 10), 5) }, (_, i) => `${defaultSubj}_${i + 1}`));
        }
      } catch {
        toast.error('Failed to load active subjects, active semester or users list');
      } finally {
        setLoadingUsers(false);
      }
    };
    fetchUsersAndSubjects();
  }, []);

  const parseClassIndices = (value) => {
    const numbers = String(value || '')
      .split(/[,\s]+/)
      .map(item => parseInt(item.trim(), 10))
      .filter(num => Number.isInteger(num));
    return [...new Set(numbers)];
  };

  const buildPreview = (f) => {
    if (isLecturer) {
      return parseClassIndices(f.classIndicesText)
        .slice(0, 8)
        .map(idx => `${f.subjectCode}_${idx}`);
    }

    const n = parseInt(f.count, 10);
    if (!n || n < 1) return [];
    return Array.from({ length: Math.min(n, 5) }, (_, i) => `${f.subjectCode}_${i + 1}`);
  };

  const handleChange = (k, v) => {
    const next = { ...form, [k]: v };
    setForm(next);
    if (k === 'subjectCode' || k === 'count' || k === 'classIndicesText') {
      setPreview(buildPreview(next));
    }
  };

  const validate = () => {
    if (!form.subjectCode) return 'Subject code is required';
    if (!['SP','SU','FA'].includes(form.semester)) return 'Invalid semester';
    if (isLecturer) {
      const indices = parseClassIndices(form.classIndicesText);
      if (indices.length === 0) return 'Assign Class is required';
      if (indices.some(idx => idx < 1 || idx > 999)) return 'Class numbers must be between 1 and 999';
    } else {
      const n = parseInt(form.count, 10);
      if (!n || n < 1 || n > 100) return 'Count must be between 1 and 100';
    }
    const y = parseInt(form.year, 10);
    if (!y || y < 2020) return 'Invalid year';
    return null;
  };

  const handleSubmit = async () => {
    const err = validate();
    if (err) { toast.error(err); return; }

    setSubmitting(true);
    setClassConflict(null);
    try {
      const res = await classApi.bulkCreate({
        subjectCode: form.subjectCode,
        semester:    form.semester,
        year:        parseInt(form.year, 10),
        count:       isLecturer ? undefined : parseInt(form.count, 10),
        classIndices: isLecturer ? parseClassIndices(form.classIndicesText) : undefined,
        lecturerIds: !isLecturer && form.lecturerIds.length > 0 ? form.lecturerIds : undefined,
        mentorIds:   !isLecturer && form.mentorIds.length > 0 ? form.mentorIds : undefined,
      });
      const count = res?.data?.count || res?.count || (isLecturer ? parseClassIndices(form.classIndicesText).length : parseInt(form.count, 10));
      toast.success(`${count} classes created successfully!`);
      onCreated();
    } catch (e) {
      const conflict = e?.data?.conflict;
      if (isLecturer && e?.status === 409 && conflict) {
        setClassConflict(conflict);
        return;
      }
      toast.error(e?.message || 'Failed to create classes');
    } finally {
      setSubmitting(false);
    }
  };

  const handleConflictMine = () => {
    setClassConflict(null);
    toast('Please check and enter your assigned class number again.');
  };

  const handleConflictOtherLecturer = async () => {
    if (!classConflict) return;

    setSubmitting(true);
    try {
      await classApi.reportCodeConflict({
        classCode: classConflict.classCode,
        semester: classConflict.semester,
        year: classConflict.year,
        reason: 'other_lecturer_may_have_created_wrong_class',
      });
      toast.success('The lecturer has been notified to verify this class code.');
      setClassConflict(null);
    } catch (e) {
      toast.error(e?.message || 'Failed to send conflict report');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-float w-full max-w-md animate-scale-in">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-slate-100">
          <div>
            <h2 className="text-xl font-bold text-slate-900">
              {isLecturer ? 'Tạo lớp học' : 'Bulk Create Classes'}
            </h2>
            <p className="text-sm text-slate-400 mt-0.5">
              {isLecturer
                ? 'Tạo nhiều lớp cùng lúc — tự động gán cho bạn'
                : 'Generate multiple classes at once'}
            </p>
          </div>
          <button onClick={onClose} className="p-2 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-all">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="p-6 space-y-4">
          {/* Locked semester info banner */}
          {form.semester && form.year && (
            <div className="flex items-center gap-3 p-3 bg-primary-50 border border-primary-100 rounded-xl">
              <div className="w-8 h-8 rounded-lg bg-primary-100 flex items-center justify-center shrink-0">
                <span className="text-xs font-bold text-primary">{form.semester}</span>
              </div>
              <div>
                <p className="text-xs font-semibold text-primary">Active Semester Locked</p>
                <p className="text-[11px] text-primary/70">
                  Classes will be created for <strong>{form.semester} {form.year}</strong>. Change via Subject &amp; Semester settings.
                </p>
              </div>
            </div>
          )}

          {/* Subject + Semester */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Subject Code *</label>
              <select
                value={form.subjectCode}
                onChange={(e) => handleChange('subjectCode', e.target.value)}
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-white focus:ring-2 focus:ring-primary/20 focus:border-primary"
              >
                {subjects.map(s => (
                  <option key={s.subjectCode} value={s.subjectCode}>
                    {s.subjectCode}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Semester *</label>
              <input
                type="text"
                disabled
                value={`${form.semester} (Active Semester)`}
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-slate-50 text-slate-500 font-medium"
              />
            </div>
          </div>

          {/* Year + Count / Assign Class */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Year *</label>
              <input
                type="text"
                disabled
                value={form.year}
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none bg-slate-50 text-slate-500 font-medium"
              />
            </div>
            {isLecturer ? (
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Assign Class *</label>
                <input
                  type="text"
                  value={form.classIndicesText}
                  onChange={(e) => handleChange('classIndicesText', e.target.value)}
                  placeholder="e.g. 4, 6, 7"
                  className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                />
              </div>
            ) : (
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Number of Classes *</label>
                <input
                  type="number"
                  min="1" max="100"
                  value={form.count}
                  onChange={(e) => handleChange('count', e.target.value)}
                  className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                />
              </div>
            )}
          </div>

          {loadingUsers ? (
            <div className="flex items-center justify-center py-4">
              <Loader2 className="w-5 h-5 text-primary animate-spin" />
            </div>
          ) : (
            <>
              {/* Lecturers — ẩn khi LECTURER tự tạo (tự động gán) */}
              {!isLecturer && (
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Assign Lecturers (optional)</label>
                  <div className="max-h-28 overflow-y-auto border border-slate-200 rounded-xl p-3 bg-slate-50 space-y-1.5">
                    {allLecturers.length === 0 ? (
                      <p className="text-xs text-slate-400 text-center py-2">No lecturers found</p>
                    ) : (
                      allLecturers.map(l => {
                        const checked = form.lecturerIds.includes(l._id);
                        return (
                          <label key={l._id} className="flex items-center gap-2 text-xs text-slate-700 cursor-pointer">
                            <input
                              type="checkbox"
                              checked={checked}
                              onChange={() => {
                                const nextIds = checked
                                  ? form.lecturerIds.filter(id => id !== l._id)
                                  : [...form.lecturerIds, l._id];
                                handleChange('lecturerIds', nextIds);
                              }}
                              className="rounded border-slate-300 text-primary focus:ring-primary h-3.5 w-3.5"
                            />
                            <span>{l.name} ({l.email})</span>
                          </label>
                        );
                      })
                    )}
                  </div>
                </div>
              )}

              {/* Lecturers info khi là LECTURER */}
              {isLecturer && (
                <div className="flex items-center gap-2 p-3 bg-primary-50 border border-primary-100 rounded-xl">
                  <span className="text-lg">👤</span>
                  <p className="text-xs text-primary font-medium">Lớp sẽ được tự động gán cho bạn.</p>
                </div>
              )}

              {/* Mentors */}
              {!isLecturer && (
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Assign Mentors (optional)</label>
                <div className="max-h-28 overflow-y-auto border border-slate-200 rounded-xl p-3 bg-slate-50 space-y-1.5">
                  {allMentors.length === 0 ? (
                    <p className="text-xs text-slate-400 text-center py-2">No mentors found</p>
                  ) : (
                    allMentors.map(m => {
                      const checked = form.mentorIds.includes(m._id);
                      return (
                        <label key={m._id} className="flex items-center gap-2 text-xs text-slate-700 cursor-pointer">
                          <input
                            type="checkbox"
                            checked={checked}
                            onChange={() => {
                              const nextMentorIds = checked
                                ? form.mentorIds.filter(id => id !== m._id)
                                : [...form.mentorIds, m._id];
                              handleChange('mentorIds', nextMentorIds);
                            }}
                            className="rounded border-slate-300 text-primary focus:ring-primary h-3.5 w-3.5"
                          />
                          <span>{m.name} ({m.email})</span>
                        </label>
                      );
                    })
                  )}
                </div>
              </div>
              )}
            </>
          )}

          {/* Preview */}
          {preview.length > 0 && (
            <div className="bg-slate-50 rounded-xl p-3 border border-slate-100">
              <p className="text-xs font-semibold text-slate-500 mb-2">
                {isLecturer ? 'Preview' : `Preview (first ${preview.length} of ${form.count})`}
              </p>
              <div className="flex flex-wrap gap-2">
                {preview.map(code => (
                  <span key={code} className="px-2 py-0.5 bg-primary-50 text-primary text-xs font-mono rounded-lg">{code}</span>
                ))}
                {!isLecturer && parseInt(form.count, 10) > 5 && (
                  <span className="px-2 py-0.5 text-slate-400 text-xs">+{parseInt(form.count, 10) - 5} more</span>
                )}
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex gap-3 p-6 pt-0">
          <button onClick={onClose} className="flex-1 px-4 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-600 hover:bg-slate-50 transition-all">
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={submitting}
            className="flex-1 px-4 py-2.5 bg-primary text-white rounded-xl text-sm font-medium hover:bg-primary-700 disabled:opacity-50 transition-all flex items-center justify-center gap-2"
          >
            {submitting ? <><Loader2 className="w-4 h-4 animate-spin" /> Creating...</> : 'Create Classes'}
          </button>
        </div>

        {classConflict && (
          <div className="absolute inset-0 z-10 flex items-center justify-center p-4 bg-white/80 backdrop-blur-sm rounded-2xl">
            <div className="w-full max-w-sm bg-white border border-amber-200 rounded-2xl shadow-float p-5">
              <div className="flex items-start gap-3">
                <div className="w-10 h-10 rounded-xl bg-amber-100 flex items-center justify-center shrink-0">
                  <AlertTriangle className="w-5 h-5 text-amber-600" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900">Class code already exists</h3>
                  <p className="text-sm text-slate-600 mt-1">
                    {classConflict.classCode} has already been created by {classConflict.lecturer?.name || 'another lecturer'}.
                  </p>
                  {classConflict.lecturer?.email && (
                    <p className="text-xs text-slate-400 mt-1">{classConflict.lecturer.email}</p>
                  )}
                </div>
              </div>

              <p className="text-xs text-slate-500 mt-4">
                Please decide whether the issue is on your side or whether the other lecturer should verify their assigned class code.
              </p>

              <div className="grid grid-cols-1 gap-2 mt-4">
                <button
                  type="button"
                  onClick={handleConflictMine}
                  className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-700 hover:bg-slate-50 transition-all"
                >
                  Issue is on my side
                </button>
                <button
                  type="button"
                  onClick={handleConflictOtherLecturer}
                  disabled={submitting}
                  className="px-4 py-2.5 bg-amber-500 text-white rounded-xl text-sm font-medium hover:bg-amber-600 disabled:opacity-50 transition-all"
                >
                  Issue is on the other lecturer side
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
