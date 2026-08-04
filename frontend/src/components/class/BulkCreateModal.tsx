import { useState, useEffect, useRef } from 'react';
import toast from 'react-hot-toast';
import { X, Loader2, AlertTriangle, FileSpreadsheet, Keyboard, Upload, CheckCircle2 } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { userApi } from '../../api/userApi';
import { subjectApi } from '../../api/subjectApi';
import { readStudentImportFile } from '../../utils/studentImport';

const CURRENT_YR = new Date().getFullYear();

export default function BulkCreateModal({ lecturers: initialLecturers = [], isLecturer = false, onClose, onCreated }) {
  const [tabMode, setTabMode] = useState('numbers'); // 'numbers' | 'excel'
  const [form, setForm] = useState({
    subjectCode: '',
    semester: 'SP',
    year: String(CURRENT_YR),
    count: '5',
    classIndicesText: '',
    lecturerIds: [],
    mentorIds: [],
  });

  const [excelFile, setExcelFile] = useState<File | null>(null);
  const [excelParsedRows, setExcelParsedRows] = useState<any[]>([]);
  const [excelError, setExcelError] = useState('');
  const [parsingExcel, setParsingExcel] = useState(false);

  const [submitting, setSubmitting] = useState(false);
  const [preview, setPreview] = useState([]);
  const [classConflict, setClassConflict] = useState(null);

  const [allLecturers, setAllLecturers] = useState(initialLecturers);
  const [allMentors, setAllMentors] = useState([]);
  const [loadingUsers, setLoadingUsers] = useState(true);
  const [subjects, setSubjects] = useState([]);

  const fileInputRef = useRef<HTMLInputElement>(null);

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

  // ── Excel Parsing for Lecturer Import ─────────────────────────────────────
  const handleExcelFileSelect = async (file?: File) => {
    if (!file) return;
    const ext = file.name.split('.').pop()?.toLowerCase();
    if (ext !== 'xlsx' && ext !== 'xls' && ext !== 'csv') {
      setExcelError('Unsupported file type. Please choose an .xlsx, .xls or .csv file.');
      setExcelFile(null);
      setExcelParsedRows([]);
      return;
    }

    setExcelFile(file);
    setExcelError('');
    setParsingExcel(true);

    try {
      const rawRows = await readStudentImportFile(file);
      if (!rawRows || rawRows.length <= 1) {
        setExcelError('File contains no data rows.');
        setExcelParsedRows([]);
        return;
      }

      const normalizeStr = (v: any) => String(v || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[^a-z0-9]/gi, '').toLowerCase();

      // Dynamic header row detection
      let headerRowIndex = 0;
      for (let i = 0; i < Math.min(rawRows.length, 10); i++) {
        const normalizedRow = (rawRows[i] || []).map(normalizeStr);
        const hasClass = normalizedRow.some(h => h.includes('class') || h.includes('lop'));
        const hasRoll = normalizedRow.some(h => h.includes('rollnumber') || h.includes('studentcode') || h.includes('masv') || h.includes('mssv'));
        if (hasClass || hasRoll) {
          headerRowIndex = i;
          break;
        }
      }

      // Map columns
      const headers = (rawRows[headerRowIndex] || []).map(normalizeStr);
      let classCol = headers.findIndex(h => h.includes('class') || h.includes('lop'));
      let rollCol = headers.findIndex(h => h.includes('rollnumber') || h.includes('studentcode') || h.includes('masv') || h.includes('mssv'));
      let emailCol = headers.findIndex(h => h.includes('email'));
      let nameCol = headers.findIndex(h => h.includes('fullname') || h.includes('hovaten') || h.includes('hoten') || h === 'name');
      let majorCol = headers.findIndex(h => h.includes('major') || h.includes('nganh') || h.includes('specialization'));

      if (classCol === -1 || rollCol === -1 || emailCol === -1 || nameCol === -1) {
        setExcelError('Required header columns missing: Class, RollNumber, Email, FullName');
        setExcelParsedRows([]);
        return;
      }

      const parsed = [];
      for (let i = headerRowIndex + 1; i < rawRows.length; i++) {
        const row = rawRows[i];
        if (!row || row.every(c => !c)) continue;

        const classVal = String(row[classCol] || '').trim();
        const rollVal = String(row[rollCol] || '').trim().toUpperCase();
        let emailVal = String(row[emailCol] || '').trim().toLowerCase();
        if (emailVal && !emailVal.includes('@')) {
          emailVal = `${emailVal}@fpt.edu.vn`;
        }
        const nameVal = String(row[nameCol] || '').trim();
        const majorVal = majorCol !== -1 ? String(row[majorCol] || '').trim().toUpperCase() : 'BIT_SE';

        if (classVal && rollVal && emailVal && nameVal) {
          parsed.push({
            classVal,
            studentCode: rollVal,
            email: emailVal,
            fullName: nameVal,
            majorCode: majorVal
          });
        }
      }

      if (parsed.length === 0) {
        setExcelError('No valid student rows found in the file.');
      } else {
        setExcelParsedRows(parsed);
      }
    } catch (err: any) {
      setExcelError(err?.message || 'Error reading Excel file');
      setExcelParsedRows([]);
    } finally {
      setParsingExcel(false);
    }
  };

  const validate = () => {
    if (!form.subjectCode) return 'Subject code is required';
    if (!['SP', 'SU', 'FA'].includes(form.semester)) return 'Invalid semester';

    if (isLecturer) {
      if (tabMode === 'numbers') {
        const indices = parseClassIndices(form.classIndicesText);
        if (indices.length === 0) return 'Assign Class is required';
        if (indices.some(idx => idx < 1 || idx > 999)) return 'Class numbers must be between 1 and 999';
      } else {
        if (!excelFile) return 'Please select an Excel file to import';
        if (excelParsedRows.length === 0) return 'No valid data in Excel file';
      }
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
      if (isLecturer && tabMode === 'excel') {
        // Luồng Import Excel cho Giảng viên
        // 1. Lấy tất cả classIndex duy nhất từ file Excel (ví dụ "EXE101_4" -> index 4, hoặc "4" -> index 4)
        const detectedIndicesSet = new Set<number>();
        excelParsedRows.forEach(r => {
          const match = r.classVal.match(/\d+/);
          if (match) {
            const idx = parseInt(match[0], 10);
            if (idx > 0) detectedIndicesSet.add(idx);
          }
        });

        const classIndices = Array.from(detectedIndicesSet);
        if (classIndices.length === 0) {
          toast.error('Could not detect class numbers from Class column in Excel file');
          setSubmitting(false);
          return;
        }

        // 2. Gọi API bulkCreate để khởi tạo các Lớp học
        const bulkRes = await classApi.bulkCreate({
          subjectCode: form.subjectCode,
          semester: form.semester,
          year: parseInt(form.year, 10),
          classIndices: classIndices
        });

        const createdClasses = bulkRes?.data || bulkRes?.items || [];
        
        // 3. Import sinh viên vào từng lớp tương ứng
        let totalImportedStudents = 0;
        for (const clsItem of createdClasses) {
          const clsCode = clsItem.classCode || `${form.subjectCode}_${clsItem.classIndex}`;
          const clsId = clsItem.id || clsItem._id;

          // Lọc sinh viên thuộc lớp này
          const matchingStudents = excelParsedRows.filter(r => {
            const match = r.classVal.match(/\d+/);
            const idx = match ? parseInt(match[0], 10) : 0;
            return idx === clsItem.classIndex || r.classVal.toUpperCase().includes(clsCode.toUpperCase());
          });

          for (const st of matchingStudents) {
            try {
              await classApi.addStudent(clsId, {
                studentCode: st.studentCode,
                fullName: st.fullName,
                email: st.email,
                majorCode: st.majorCode
              });
              totalImportedStudents++;
            } catch {
              // Bỏ qua nếu SV đã trùng
            }
          }
        }

        toast.success(`Tạo thành công ${createdClasses.length || classIndices.length} lớp và import ${totalImportedStudents} sinh viên!`);
        onCreated();
      } else {
        // Luồng nhập số lớp thủ công
        const res = await classApi.bulkCreate({
          subjectCode: form.subjectCode,
          semester: form.semester,
          year: parseInt(form.year, 10),
          count: isLecturer ? undefined : parseInt(form.count, 10),
          classIndices: isLecturer ? parseClassIndices(form.classIndicesText) : undefined,
          lecturerIds: !isLecturer && form.lecturerIds.length > 0 ? form.lecturerIds : undefined,
          mentorIds: !isLecturer && form.mentorIds.length > 0 ? form.mentorIds : undefined,
        });
        const count = res?.data?.count || res?.count || (isLecturer ? parseClassIndices(form.classIndicesText).length : parseInt(form.count, 10));
        toast.success(`${count} classes created successfully!`);
        onCreated();
      }
    } catch (e: any) {
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
    } catch (e: any) {
      toast.error(e?.message || 'Failed to send conflict report');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-float w-full max-w-md animate-scale-in overflow-hidden">
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
          {/* Tabs for Lecturer */}
          {isLecturer && (
            <div className="p-1 bg-slate-100 rounded-xl flex gap-1 mb-2">
              <button
                type="button"
                onClick={() => setTabMode('numbers')}
                className={`flex-1 flex items-center justify-center gap-2 py-2 text-xs font-semibold rounded-lg transition-all ${
                  tabMode === 'numbers' ? 'bg-white text-slate-900 shadow-xs' : 'text-slate-500 hover:text-slate-700'
                }`}
              >
                <Keyboard className="w-4 h-4" /> Enter class numbers
              </button>
              <button
                type="button"
                onClick={() => setTabMode('excel')}
                className={`flex-1 flex items-center justify-center gap-2 py-2 text-xs font-semibold rounded-lg transition-all ${
                  tabMode === 'excel' ? 'bg-white text-slate-900 shadow-xs' : 'text-slate-500 hover:text-slate-700'
                }`}
              >
                <FileSpreadsheet className="w-4 h-4" /> Import Excel
              </button>
            </div>
          )}

          {/* Locked semester info banner */}
          {form.semester && form.year && (
            <div className="flex items-center gap-3 p-3 bg-orange-50 border border-orange-100 rounded-xl">
              <div className="w-8 h-8 rounded-lg bg-orange-100 flex items-center justify-center shrink-0">
                <span className="text-xs font-bold text-orange-600">{form.semester}</span>
              </div>
              <div>
                <p className="text-xs font-semibold text-orange-600">Active Semester Locked</p>
                <p className="text-[11px] text-orange-600/80">
                  Classes will be created for <strong>{form.semester} {form.year}</strong>. Change via Subject &amp; Semester settings.
                </p>
              </div>
            </div>
          )}

          {/* TAB 1: ENTER CLASS NUMBERS */}
          {(!isLecturer || tabMode === 'numbers') && (
            <>
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

                  {isLecturer && (
                    <div className="flex items-center gap-2 p-3 bg-primary-50 border border-primary-100 rounded-xl">
                      <span className="text-lg">👤</span>
                      <p className="text-xs text-primary font-medium">Lớp sẽ được tự động gán cho bạn.</p>
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
                  </div>
                </div>
              )}
            </>
          )}

          {/* TAB 2: IMPORT EXCEL (FOR LECTURER) */}
          {isLecturer && tabMode === 'excel' && (
            <div className="space-y-4">
              {/* Helper info banner */}
              <div className="p-3.5 bg-blue-50/70 border border-blue-100 rounded-xl text-xs text-blue-800 leading-relaxed">
                The <strong className="font-semibold text-blue-900">Class</strong> column determines the classes to create, for example <strong className="font-mono text-blue-900">EXE101_4</strong> or <strong className="font-mono text-blue-900">EXE201_2</strong>. Each student will be imported into the matching class, and all new classes will be assigned to you.
              </div>

              {/* File Dropzone */}
              <div
                onClick={() => fileInputRef.current?.click()}
                className={`border-2 border-dashed rounded-2xl p-8 text-center cursor-pointer transition-all ${
                  excelError
                    ? 'border-red-300 bg-red-50/50'
                    : excelFile
                    ? 'border-green-300 bg-green-50/50'
                    : 'border-slate-200 bg-slate-50/50 hover:border-primary/50 hover:bg-primary-50/30'
                }`}
              >
                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".xlsx,.xls,.csv"
                  className="hidden"
                  onChange={(e) => handleExcelFileSelect(e.target.files?.[0])}
                />
                <div className="mx-auto w-12 h-12 rounded-2xl bg-white border border-slate-200/60 shadow-xs flex items-center justify-center text-slate-400 mb-3">
                  {parsingExcel ? (
                    <Loader2 className="w-6 h-6 animate-spin text-primary" />
                  ) : excelFile ? (
                    <CheckCircle2 className="w-6 h-6 text-green-600" />
                  ) : (
                    <Upload className="w-6 h-6" />
                  )}
                </div>

                {parsingExcel ? (
                  <p className="text-sm font-medium text-slate-600">Reading Excel file...</p>
                ) : excelFile ? (
                  <div>
                    <p className="text-sm font-semibold text-slate-900">{excelFile.name}</p>
                    <p className="text-xs text-green-600 font-medium mt-1">
                      {excelParsedRows.length} valid student rows detected
                    </p>
                  </div>
                ) : (
                  <div>
                    <p className="text-sm font-semibold text-slate-800">Drop an Excel file here or click to browse</p>
                    <p className="text-xs text-slate-400 mt-1">Required: Class, RollNumber, Email, FullName</p>
                  </div>
                )}
              </div>

              {excelError && (
                <div className="p-3 bg-red-50 border border-red-100 rounded-xl text-xs text-red-600 flex items-center gap-2">
                  <AlertTriangle className="w-4 h-4 shrink-0" />
                  <span>{excelError}</span>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex gap-3 p-6 pt-0">
          <button onClick={onClose} className="flex-1 px-4 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-600 hover:bg-slate-50 transition-all font-medium">
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={submitting || (isLecturer && tabMode === 'excel' && (!excelFile || excelParsedRows.length === 0))}
            className="flex-1 px-4 py-2.5 bg-primary text-white rounded-xl text-sm font-medium hover:bg-primary-700 disabled:opacity-50 transition-all flex items-center justify-center gap-2"
          >
            {submitting ? (
              <><Loader2 className="w-4 h-4 animate-spin" /> Processing...</>
            ) : isLecturer && tabMode === 'excel' ? (
              <><Upload className="w-4 h-4" /> Create &amp; Import</>
            ) : (
              'Create Classes'
            )}
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
                </div>
              </div>

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
