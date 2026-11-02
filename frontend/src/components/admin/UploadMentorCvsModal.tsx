import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { FileText, Loader2, RefreshCw, Upload, X } from 'lucide-react';
import { mentorSupportApi } from '../../api/mentorSupportApi';
import type { MentorProfile } from '../../types/mentoring';
import { parseApiError } from '../../utils/apiError';
import {
  addMentorCvFiles, applyCvEmailMatch, describeCvEmailMatch, failCvEmailRead, mentorCvRowIssue,
  pendingMentorCvs, validateMentorCvFile, type MentorCvUploadRow,
} from '../../utils/mentorCvUpload';
import { filterSearchOptions } from '../../utils/mentorSearchSelect';
import Badge from '../ui/Badge';
import Button from '../ui/Button';
import Modal from '../ui/Modal';

interface Props { onClose: () => void; onUploaded: () => void }

export default function UploadMentorCvsModal({ onClose, onUploaded }: Props) {
  const inputId = useId();
  const fileInput = useRef<HTMLInputElement>(null);
  const directoryRequest = useRef<AbortController | null>(null);
  const uploadRequest = useRef<AbortController | null>(null);
  const emailRequest = useRef<AbortController | null>(null);
  const mounted = useRef(true);
  const busy = useRef(false);
  const readingEmails = useRef(false);
  const [mentors, setMentors] = useState<MentorProfile[]>([]);
  const [rows, setRows] = useState<MentorCvUploadRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [matching, setMatching] = useState(false);
  const [emailProgress, setEmailProgress] = useState({ current: 0, total: 0 });
  const [directoryError, setDirectoryError] = useState('');
  const [selectionError, setSelectionError] = useState('');
  const [result, setResult] = useState('');
  const [progress, setProgress] = useState({ current: 0, total: 0 });
  const [dragging, setDragging] = useState(false);

  const loadMentors = useCallback(async () => {
    directoryRequest.current?.abort();
    const controller = new AbortController();
    directoryRequest.current = controller;
    setLoading(true);
    setDirectoryError('');
    try {
      const values = await mentorSupportApi.getDirectory(controller.signal);
      if (!controller.signal.aborted && mounted.current) {
        setMentors(values);
        setRows(current => current.map(row => row.status === 'success' ? row : { ...row, replaceExisting: false }));
      }
    } catch (cause) {
      if (!controller.signal.aborted && mounted.current)
        setDirectoryError(parseApiError(cause, 'Could not load mentor profiles. Please try again.').message);
    } finally {
      if (!controller.signal.aborted && mounted.current) setLoading(false);
    }
  }, []);

  useEffect(() => {
    mounted.current = true;
    void loadMentors();
    return () => {
      mounted.current = false;
      directoryRequest.current?.abort();
      uploadRequest.current?.abort();
      emailRequest.current?.abort();
    };
  }, [loadMentors]);

  const close = () => {
    if (busy.current) return;
    emailRequest.current?.abort();
    onClose();
  };
  const readEmails = async (selected: readonly MentorCvUploadRow[]) => {
    if (busy.current || readingEmails.current || selected.length === 0) return;
    readingEmails.current = true;
    setMatching(true);
    const controller = new AbortController();
    emailRequest.current = controller;
    const ids = new Set(selected.map(row => row.id));
    setRows(current => current.map(row => ids.has(row.id) && row.status !== 'success' ? {
      ...row, mentorId: '', suggested: false, replaceExisting: false, status: 'pending', error: '',
      emailReadStatus: 'pending', emailMatch: null, emailReadError: '',
    } : row));
    try {
      for (const [index, row] of selected.entries()) {
        if (controller.signal.aborted || !mounted.current) break;
        setEmailProgress({ current: index + 1, total: selected.length });
        setRows(current => current.map(item => item.id === row.id ? { ...item, emailReadStatus: 'reading' } : item));
        try {
          const response = await mentorSupportApi.matchCvMentor(row.file, controller.signal);
          if (controller.signal.aborted || !mounted.current) break;
          setRows(current => current.map(item => item.id === row.id ? applyCvEmailMatch(item, response, mentors) : item));
        } catch (cause) {
          if (controller.signal.aborted || !mounted.current) break;
          const message = parseApiError(cause, 'Could not read the CV email. Try again or select the mentor manually.').message;
          setRows(current => current.map(item => item.id === row.id ? failCvEmailRead(item, message) : item));
        }
      }
    } finally {
      readingEmails.current = false;
      if (!controller.signal.aborted && mounted.current) setMatching(false);
    }
  };
  const addFiles = (files: readonly File[]) => {
    if (busy.current || readingEmails.current || loading || directoryError || mentors.length === 0) return;
    const added = addMentorCvFiles(rows, files);
    setRows(added.rows);
    setSelectionError(added.error);
    setResult('');
    const existing = new Set(rows.map(row => row.id));
    void readEmails(added.rows.filter(row => !existing.has(row.id) && !validateMentorCvFile(row.file)));
  };
  const changeMentor = (rowId: string, mentorId: string) => {
    if (busy.current || readingEmails.current) return;
    setRows(current => current.map(row => row.id === rowId ? {
      ...row, mentorId, replaceExisting: false, suggested: false, status: 'pending', error: '',
    } : row));
    setResult('');
  };
  const pending = pendingMentorCvs(rows);
  const completed = rows.length - pending.length;
  const issues = new Map(pending.map(row => [row.id, mentorCvRowIssue(row, rows, mentors)]));
  const canUpload = pending.length > 0 && !loading && !directoryError && !uploading && !matching &&
    pending.every(row => !issues.get(row.id));

  const upload = async () => {
    if (busy.current || readingEmails.current || !canUpload) return;
    busy.current = true;
    setUploading(true);
    setSelectionError('');
    setResult('');
    const controller = new AbortController();
    uploadRequest.current = controller;
    let saved = 0;
    let failed = 0;
    try {
      for (const [index, row] of pending.entries()) {
        if (controller.signal.aborted) break;
        setProgress({ current: index + 1, total: pending.length });
        setRows(current => current.map(item => item.id === row.id ? { ...item, status: 'uploading', error: '' } : item));
        try {
          const updated = await mentorSupportApi.uploadManagedCv(row.mentorId, row.file, row.replaceExisting, controller.signal);
          if (controller.signal.aborted || !mounted.current) break;
          saved++;
          setMentors(current => current.map(mentor => mentor.id === updated.id ? updated : mentor));
          setRows(current => current.map(item => item.id === row.id ? { ...item, status: 'success', error: '' } : item));
        } catch (cause) {
          if (controller.signal.aborted || !mounted.current) break;
          failed++;
          const message = parseApiError(cause, 'Upload could not be confirmed. Check the current CV before trying again.').message;
          setRows(current => current.map(item => item.id === row.id ? { ...item, status: 'error', error: message } : item));
        }
      }
      if (!controller.signal.aborted && mounted.current) {
        // A timed-out response may have saved a file. Refresh ownership/current CVs before a retry.
        await loadMentors();
        if (controller.signal.aborted || !mounted.current) return;
        setRows(current => current.map(row => row.status === 'success' ? row : { ...row, replaceExisting: false }));
        setResult(`${saved} CV${saved === 1 ? '' : 's'} uploaded successfully.${failed ? ` ${failed} could not be confirmed; review the affected rows and retry.` : ''}`);
        if (saved > 0) onUploaded();
      }
    } finally {
      busy.current = false;
      if (!controller.signal.aborted && mounted.current) setUploading(false);
    }
  };

  return (
    <Modal isOpen onClose={close} title="Upload mentor CVs" size="2xl">
      <div className="space-y-4 text-body text-slate-700">
        <p>Import mentor accounts from Excel first, then select one CV or a list of CVs. The email inside each CV is compared with the imported mentor emails. A unique match selects the mentor automatically.</p>
        <p className="text-body-sm text-slate-500">Reading the CV email does not save the file or change a profile. Review the selected mentors, or choose manually when the email is missing or ambiguous, before pressing Upload.</p>
        <p className="text-body-sm text-slate-500">Uploading stores the full document and extracts education, professional background, work experience and expertise into the mentor profile. Mentor type and manually entered technology skills stay unchanged.</p>

        {loading && !uploading && <p role="status" className="flex items-center gap-2"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading mentors…</p>}
        {directoryError && <div role="alert" className="space-y-2 rounded-lg border border-danger-light bg-danger-50 p-3 text-danger-dark">
          <p>{directoryError}</p>
          <Button variant="outline" size="sm" icon={RefreshCw} onClick={() => void loadMentors()} disabled={uploading || matching}>Retry loading mentors</Button>
        </div>}
        {!loading && !directoryError && mentors.length === 0 && <p role="status" className="rounded-lg border border-slate-200 p-4">No registered mentors are available. Use Import Mentors to create their accounts, then add their CVs here.</p>}

        <div
          onDragOver={event => { event.preventDefault(); if (!uploading && !matching) setDragging(true); }}
          onDragLeave={() => setDragging(false)}
          onDrop={event => { event.preventDefault(); setDragging(false); addFiles(Array.from(event.dataTransfer.files)); }}
          className={`rounded-xl border-2 border-dashed p-5 text-center ${dragging ? 'border-primary' : 'border-slate-300'}`}
        >
          <FileText className="mx-auto mb-2 h-7 w-7 text-slate-400" aria-hidden="true" />
          <p className="font-semibold text-secondary">Drop one or more CV files here</p>
          <p className="mt-1 text-body-sm text-slate-500">PDF, DOC or DOCX · up to 10 MB each · 50 files / 100 MB per list</p>
          <input ref={fileInput} id={inputId} type="file" multiple accept=".pdf,.doc,.docx" className="sr-only" aria-label="Mentor CV files"
            disabled={uploading || matching || loading || Boolean(directoryError) || mentors.length === 0}
            onChange={event => { addFiles(Array.from(event.target.files ?? [])); event.target.value = ''; }} />
          <Button variant="outline" size="sm" icon={Upload} className="mt-3" disabled={uploading || matching || loading || Boolean(directoryError) || mentors.length === 0}
            onClick={() => fileInput.current?.click()}>Select CV files</Button>
        </div>

        {selectionError && <p role="alert" className="text-body-sm text-danger-dark">{selectionError}</p>}
        {rows.length > 0 && <>
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-body-sm">{rows.length} file{rows.length === 1 ? '' : 's'} selected · {completed} uploaded · {pending.length} remaining</p>
            <Button variant="ghost" size="sm" icon={RefreshCw} disabled={uploading || matching || loading} onClick={() => void loadMentors()}>Refresh mentor list</Button>
          </div>
          <p className="text-body-sm text-slate-500">Check every file and mentor before uploading. Automatic selection uses the email found inside the CV.</p>
          {matching && <p role="status" aria-live="polite" className="flex items-center gap-2"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Reading CV email {emailProgress.current} of {emailProgress.total}…</p>}
          <div className="overflow-x-auto rounded-lg border border-slate-200">
            <table className="w-full min-w-[42rem] table-fixed text-left text-body-sm">
              <caption className="sr-only">CV files and their selected mentor profiles</caption>
              <thead className="bg-slate-50 text-slate-600"><tr><th scope="col" className="w-1/4 p-3">CV file</th><th scope="col" className="w-1/2 p-3">Mentor</th><th scope="col" className="p-3">Status</th><th scope="col" className="w-14 p-3"><span className="sr-only">Actions</span></th></tr></thead>
              <tbody className="divide-y divide-slate-200">{rows.map((row, index) => {
                const mentor = mentors.find(item => item.id === row.mentorId);
                const issue = issues.get(row.id);
                const locked = uploading || matching || row.status === 'success';
                const labelId = `${inputId}-mentor-${index}`;
                return <tr key={row.id}>
                  <td className="w-1/4 p-3 align-top"><p className="break-all font-medium">{row.file.name}</p><p className="mt-1 text-caption text-slate-500">{(row.file.size / 1024).toLocaleString(undefined, { maximumFractionDigits: 1 })} KB</p></td>
                  <td className="w-1/2 space-y-2 p-3 align-top">
                    <label htmlFor={labelId} className="sr-only">Mentor for {row.file.name}</label>
                    <CvMentorPicker id={labelId} fileName={row.file.name} value={row.mentorId} disabled={locked}
                      mentors={mentors} onChange={id => changeMentor(row.id, id)} />
                    {row.status !== 'success' && <p className={`break-words text-caption ${row.emailReadError ? 'text-danger-dark' : 'text-slate-500'}`}>{describeCvEmailMatch(row)}</p>}
                    {row.emailMatch && row.emailMatch.detectedEmails.length > 0 && <p className="break-all text-caption text-slate-500">Email in CV: {row.emailMatch.detectedEmails.join(', ')}</p>}
                    {row.status !== 'success' && !validateMentorCvFile(row.file) && <Button variant="ghost" size="sm" icon={RefreshCw} disabled={locked || loading || Boolean(directoryError)}
                      aria-label={`Read emails again for ${row.file.name}`} onClick={() => { setResult(''); void readEmails([row]); }}>Read emails again</Button>}
                    {mentor?.cvFileName && row.status !== 'success' && <>
                      <p className="break-all text-caption text-slate-500">Current CV: {mentor.cvFileName}</p>
                      <label className="flex items-start gap-2 text-body-sm"><input type="checkbox" className="mt-1 accent-primary" checked={row.replaceExisting} disabled={locked}
                        onChange={event => setRows(current => current.map(item => item.id === row.id ? { ...item, replaceExisting: event.target.checked } : item))} />
                        <span>Replace this CV and update the extracted profile information.</span></label>
                    </>}
                    {issue && <p className="text-caption text-danger-dark">{issue}</p>}
                  </td>
                  <td className="p-3 align-top">
                    <Badge variant={row.status === 'success' ? 'success' : row.status === 'error' ? 'danger' : row.status === 'uploading' || row.emailReadStatus === 'pending' || row.emailReadStatus === 'reading' ? 'info' : issue ? 'warning' : 'neutral'}>
                      {row.status === 'success' ? 'Uploaded' : row.status === 'error' ? 'Failed' : row.status === 'uploading' ? 'Uploading…' : row.emailReadStatus === 'reading' ? 'Reading email…' : row.emailReadStatus === 'pending' ? 'Waiting to read…' : issue ? 'Needs attention' : 'Ready'}
                    </Badge>
                    {row.error && <p role="alert" className="mt-2 break-words text-caption text-danger-dark">{row.error}</p>}
                  </td>
                  <td className="p-2 align-top"><Button variant="ghost" className="h-9 w-9 p-0" aria-label={`Remove ${row.file.name}`} disabled={locked}
                    onClick={() => { setRows(current => current.filter(item => item.id !== row.id)); setSelectionError(''); }}><X className="h-4 w-4" aria-hidden="true" /></Button></td>
                </tr>;
              })}</tbody>
            </table>
          </div>
        </>}

        {uploading && <p role="status" aria-live="polite" className="flex items-center gap-2"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Uploading CV {progress.current} of {progress.total}…</p>}
        {result && <p role="status" aria-live="polite" className="rounded-lg border border-slate-200 p-3">{result}</p>}
        {rows.length > 0 && !uploading && !matching && pending.length > 0 && !canUpload && !loading && !directoryError && <p className="text-body-sm text-slate-500">Resolve the highlighted rows or remove them before uploading. Each request saves its own CV; successful uploads are kept if another file fails.</p>}
        <div className="flex justify-end gap-3 border-t border-slate-200 pt-4">
          <Button variant={completed === rows.length && rows.length > 0 ? 'primary' : 'outline'} disabled={uploading} onClick={close}>{completed === rows.length && rows.length > 0 ? 'Done' : 'Close'}</Button>
          {(pending.length > 0 || rows.length === 0) && <Button icon={Upload} isLoading={uploading} disabled={!canUpload} onClick={() => void upload()}>{pending.some(row => row.status === 'error') ? 'Retry' : 'Upload'} {pending.length} CV{pending.length === 1 ? '' : 's'}</Button>}
        </div>
      </div>
    </Modal>
  );
}

/** The native select opens outside scrolling table boundaries, including the final row. */
function CvMentorPicker({ id, fileName, value, disabled, mentors, onChange }: {
  id: string; fileName: string; value: string; disabled: boolean; mentors: MentorProfile[]; onChange: (id: string) => void;
}) {
  const [query, setQuery] = useState('');
  const options = mentors.map(mentor => ({ id: mentor.id, label: mentor.fullName, detail: `${mentor.email} · ${mentor.mentorType}` }));
  const matches = filterSearchOptions(options, query);
  const selected = mentors.find(mentor => mentor.id === value);
  const shown = selected && !matches.some(option => option.id === selected.id)
    ? [options.find(option => option.id === selected.id)!, ...matches] : matches;
  return <div className="min-w-0 space-y-1.5">
    {!disabled && <input type="search" value={query} onChange={event => setQuery(event.target.value)}
      aria-label={`Search mentors for ${fileName}`} placeholder="Search name or email…"
      className="w-full min-w-0 rounded-lg border border-slate-300 bg-white px-2 py-1.5 text-body-sm text-slate-700 outline-none focus:border-primary" />}
    <select id={id} value={value} disabled={disabled} title={selected ? `${selected.fullName} · ${selected.email}` : 'Choose a mentor'}
      onChange={event => { onChange(event.target.value); setQuery(''); }}
      className="w-full min-w-0 max-w-full rounded-lg border border-slate-300 bg-white px-2 py-1.5 text-body-sm text-slate-700 outline-none focus:border-primary disabled:opacity-60">
      <option value="">Choose a mentor</option>
      {shown.map(option => <option key={option.id} value={option.id}>{option.label} · {option.detail}</option>)}
    </select>
    {selected && <p className="break-all text-caption text-slate-500">{selected.email} · {selected.mentorType}</p>}
    {query.trim() && matches.length === 0 && <p role="status" className="text-caption text-slate-500">No mentor matches this name or email.</p>}
  </div>;
}
