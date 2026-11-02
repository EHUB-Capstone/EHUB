// @ts-nocheck
// src/components/workspace/checkpoints/FeedbackThread.jsx
import { useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { ChevronDown, ChevronUp, CornerDownRight, Calendar, ClipboardCheck, Download, Pencil, Send, Trash2, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { checkpointApi } from '../../../api/checkpointApi';
import { useAuth } from '../../../hooks/useAuth';
import ConfirmDialog from '../../ui/ConfirmDialog';
import AutoSizeTextarea from '../../ui/AutoSizeTextarea';

// Enter sends through the form's own submit handler, so the empty/duplicate checks stay in one place.
const submitForm = (event) => event.currentTarget.form?.requestSubmit();

const TECHNICAL_REVIEW_CRITERIA = [
  { key: 'mainFunction', label: 'Core functionality', evidence: 'Live demo', description: 'The core user flow works and meets the selected requirements.' },
  { key: 'interface', label: 'Interface and user experience', evidence: 'Desktop or mobile testing', description: 'Easy to understand and use, with an appropriate display across devices.' },
  { key: 'technology', label: 'Technology and architecture', evidence: 'Architecture diagram and source code', description: 'The chosen technology is suitable and the team can explain how the system works.' },
  { key: 'security', label: 'Data and basic security', evidence: 'Data handling and access-control demo', description: 'Input validation, appropriate permissions, and protection for passwords and API keys.' },
  { key: 'testing', label: 'Testing and improvement', evidence: 'Test checklist and issue list', description: 'Core functions are tested; issues are logged and verified after fixes.' },
];

function FeedbackModeSwitch({ value, onChange }) {
  return <div className="inline-flex items-center gap-1 rounded-full bg-slate-100 p-1" role="group" aria-label="Feedback type">
    <button type="button" onClick={() => onChange('regular')} aria-pressed={value === 'regular'} className={`rounded-full px-3 py-1.5 text-caption font-bold transition ${value === 'regular' ? 'bg-secondary text-white shadow-sm' : 'text-slate-600 hover:text-slate-800'}`}>Regular feedback</button>
    <button type="button" onClick={() => onChange('technical')} aria-pressed={value === 'technical'} className={`rounded-full px-3 py-1.5 text-caption font-bold transition ${value === 'technical' ? 'bg-secondary text-white shadow-sm' : 'text-slate-600 hover:text-slate-800'}`}>Technical review</button>
  </div>;
}

function TechnicalReviewForm({ teamId, checkpointNumber, onPosted, initiallyCollapsed = false }) {
  const { user } = useAuth();
  const [reviews, setReviews] = useState(() => Object.fromEntries(TECHNICAL_REVIEW_CRITERIA.map(({ key }) => [key, ''])));
  const [sending, setSending] = useState(false);
  const [expanded, setExpanded] = useState(!initiallyCollapsed);
  const hasValidReviews = Object.values(reviews).every((value) => value.trim().length >= 3);

  const submitTechnicalReview = async (event) => {
    event.preventDefault();
    if (!hasValidReviews || sending) return;
    const reviewedAt = new Intl.DateTimeFormat('en-US', { dateStyle: 'long' }).format(new Date());
    const rows = TECHNICAL_REVIEW_CRITERIA
      .map(({ key, label, evidence }) => `• ${label} (${evidence}): ${reviews[key].trim()}`)
      .join('\n');
    const comment = `TECHNICAL REVIEW\nReview date: ${reviewedAt}\nReviewer: ${user?.name || 'IT Mentor'}\n\n${rows}`;

    setSending(true);
    try {
      const res = await checkpointApi.addFeedback(teamId, checkpointNumber, { comment });
      toast.success('Technical review submitted.');
      setReviews(Object.fromEntries(TECHNICAL_REVIEW_CRITERIA.map(({ key }) => [key, ''])));
      onPosted?.(res?.data);
      setExpanded(false);
    } catch (err) {
      toast.error(err?.response?.data?.error || 'Unable to submit the technical review.');
    } finally {
      setSending(false);
    }
  };

  return (
    <form onSubmit={submitTechnicalReview} className="overflow-hidden rounded-xl border border-primary-200 bg-white">
      <button type="button" onClick={() => setExpanded((current) => !current)} aria-expanded={expanded} className="flex w-full flex-wrap items-start justify-between gap-3 bg-primary-50 px-4 py-3 text-left transition hover:bg-primary-100">
        <div className="flex items-start gap-2.5">
          <span className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-white text-primary-700 shadow-sm">
            <ClipboardCheck className="h-4 w-4" />
          </span>
          <div>
            <h4 className="text-body-sm font-bold text-slate-900">Technical review form</h4>
            <p className="mt-0.5 text-caption text-slate-600">Use this when this checkpoint needs a structured technical review.</p>
          </div>
        </div>
        <span className="inline-flex items-center gap-1 text-caption font-medium text-slate-600"><Calendar className="h-3.5 w-3.5" /> {new Intl.DateTimeFormat('en-US').format(new Date())} {expanded ? <ChevronUp className="ml-1 h-4 w-4" /> : <ChevronDown className="ml-1 h-4 w-4" />}</span>
      </button>
      {expanded && <><div className="divide-y divide-slate-100">
        {TECHNICAL_REVIEW_CRITERIA.map(({ key, label, evidence, description }) => (
          <div key={key} className="grid gap-2 px-4 py-3 md:grid-cols-[minmax(0,1fr)_minmax(220px,1.1fr)] md:gap-5">
            <div>
              <p className="text-body-sm font-semibold text-slate-800">{label}</p>
              <p className="mt-0.5 text-caption leading-5 text-slate-500">{description}</p>
              <p className="mt-1 text-caption font-medium text-primary-700">Evidence: {evidence}</p>
            </div>
            <label className="block">
              <span className="sr-only">Feedback or issues to fix for {label}</span>
              <textarea
                value={reviews[key]}
                onChange={(event) => setReviews((current) => ({ ...current, [key]: event.target.value }))}
                required
                minLength={3}
                rows={3}
                placeholder="Feedback / issues to fix…"
                className="w-full resize-y rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-body-sm leading-5 text-slate-700 outline-none transition placeholder:text-slate-400 focus:border-primary focus:bg-white focus:ring-2 focus:ring-primary-100"
              />
              <span className="mt-1 block text-right text-caption text-slate-400">{reviews[key].length} characters</span>
            </label>
          </div>
        ))}
      </div>
      <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 bg-slate-50 px-4 py-3">
        <p className="text-caption text-slate-500">Each item requires at least 3 characters.</p>
        <button type="submit" disabled={!hasValidReviews || sending} className="inline-flex items-center gap-1.5 rounded-lg bg-primary px-4 py-2 text-body-sm font-semibold text-white shadow-sm transition hover:bg-primary-600 disabled:cursor-not-allowed disabled:opacity-50">
          <Send className="h-3.5 w-3.5" /> {sending ? 'Submitting…' : 'Submit review'}
        </button>
      </div>
      </>}
    </form>
  );
}

function FeedbackEditor({ feedback, teamId, checkpointNumber, onSaved, onCancel, framed = true }) {
  const [value, setValue] = useState(feedback.comment);
  const [saving, setSaving] = useState(false);
  const save = async (event) => {
    event.preventDefault();
    if (!value.trim() || saving) return;
    setSaving(true);
    try {
      const response = await checkpointApi.updateFeedback(teamId, checkpointNumber, feedback._id, { comment: value.trim() });
      toast.success('Feedback updated.');
      onSaved?.(response?.data);
    } catch (error) {
      toast.error(error?.response?.data?.message || error?.response?.data?.error || 'Unable to update feedback.');
    } finally {
      setSaving(false);
    }
  };

  return <form onSubmit={save} className={framed ? 'mt-2 rounded-xl border border-primary-200 bg-primary-50/40 p-3' : 'mt-2'}>
    <label className="block text-caption font-semibold text-slate-700" htmlFor={`feedback-edit-${feedback._id}`}>Edit feedback</label>
    <AutoSizeTextarea id={`feedback-edit-${feedback._id}`} autoFocus minRows={3} maxRows={18} value={value} onChange={(event) => setValue(event.target.value)} className="mt-2 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-body-sm leading-5 text-slate-700 outline-none focus:border-primary focus:ring-2 focus:ring-primary-100" />
    <div className="mt-2 flex justify-end gap-2">
      <button type="button" onClick={onCancel} className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-caption font-semibold text-slate-700 hover:bg-slate-50">Cancel</button>
      <button type="submit" disabled={!value.trim() || saving} className="rounded-lg bg-primary px-3 py-1.5 text-caption font-semibold text-white hover:bg-primary-600 disabled:cursor-not-allowed disabled:opacity-50">{saving ? 'Saving…' : 'Save changes'}</button>
    </div>
  </form>;
}

// ─── Helper badges ────────────────────────────────────────────────────────────
const ROLE_COLORS = {
  ADMIN:    'bg-red-50    text-red-700    border-red-200',
  LECTURER: 'bg-blue-50   text-blue-700   border-blue-200',
  MENTOR:   'bg-amber-50  text-amber-700  border-amber-200',
  STUDENT:  'bg-emerald-50 text-emerald-700 border-emerald-200',
};
const roleBadge = (role) =>
  ROLE_COLORS[(role || '').toUpperCase()] || 'bg-slate-50 text-slate-600 border-slate-200';
const roleLabel = (role) =>
  ({ ADMIN: 'Admin', LECTURER: 'Lecturer', MENTOR: 'Mentor', STUDENT: 'Student' }[
    (role || '').toUpperCase()
  ] || role);

function TechnicalReviewDisplay({ content }) {
  if (!content?.startsWith('TECHNICAL REVIEW\n')) return <p className="text-xs text-slate-600 leading-relaxed whitespace-pre-wrap break-words">{content}</p>;
  const lines = content.split('\n');
  const metadata = lines.slice(1, 3);
  const overallNote = lines[3]?.replace(/^Overall note:\s*/, '').trim();
  const rows = lines.slice(4).reduce((items, line) => {
    const match = line.match(/^• (.+?) \((.+?)\): ([\s\S]*)$/);
    if (match) items.push({ label: match[1], evidence: match[2], review: match[3] });
    else if (items.length) items.at(-1).review += `\n${line}`;
    return items;
  }, []);
  return <div className="overflow-hidden rounded-lg border border-primary-100 bg-white">
    <div className="border-b border-primary-100 bg-primary-50 px-3 py-2"><p className="text-caption font-bold text-primary-700">Technical review</p><p className="text-caption text-slate-600">{metadata.join(' · ')}</p>{overallNote && <p className="mt-1 text-caption text-slate-700"><span className="font-semibold">Overall note:</span> {overallNote}</p>}</div>
    <div className="divide-y divide-slate-100">{rows.map((row) => { const criterion = TECHNICAL_REVIEW_CRITERIA.find((item) => item.label === row.label); return <div key={row.label} className="grid gap-2 px-3 py-2 sm:grid-cols-[minmax(0,1fr)_minmax(0,1.3fr)] sm:gap-4"><div><p className="text-caption font-semibold text-slate-800">{row.label}</p><p className="mt-0.5 text-caption leading-5 text-slate-500"><span className="font-semibold text-slate-600">Assessment:</span> {criterion?.description || '—'}</p><p className="mt-1 text-caption text-primary-700">Evidence: {row.evidence}</p></div><p className="text-caption whitespace-pre-wrap break-words text-slate-600">{row.review}</p></div>; })}</div>
  </div>;
}

function TechnicalReviewEditor({ feedback, teamId, checkpointNumber, onSaved, onCancel }) {
  const lines = feedback.comment.split('\n');
  const contentStart = lines.findIndex((line, index) => index >= 3 && line === '');
  const headerLines = lines.slice(0, contentStart === -1 ? 3 : contentStart);
  const metadata = headerLines.slice(1);
  const parsedRows = lines.slice(contentStart === -1 ? 4 : contentStart + 1).reduce((items, line) => {
    const match = line.match(/^• (.+?) \((.+?)\): ([\s\S]*)$/);
    if (match) items.push({ label: match[1], evidence: match[2], review: match[3] });
    else if (items.length) items.at(-1).review += `\n${line}`;
    return items;
  }, []);
  const [reviews, setReviews] = useState(() => Object.fromEntries(parsedRows.map((row) => [row.label, row.review])));
  const [saving, setSaving] = useState(false);
  const save = async (event) => {
    event.preventDefault();
    if (saving || parsedRows.some((row) => !reviews[row.label]?.trim())) return;
    const rows = parsedRows.map((row) => `• ${row.label} (${row.evidence}): ${reviews[row.label].trim()}`).join('\n');
    const comment = `${headerLines.join('\n')}\n\n${rows}`;
    setSaving(true);
    try {
      const response = await checkpointApi.updateFeedback(teamId, checkpointNumber, feedback._id, { comment });
      toast.success('Technical review updated.');
      onSaved?.(response?.data);
    } catch (error) {
      toast.error(error?.response?.data?.message || error?.response?.data?.error || 'Unable to update technical review.');
    } finally {
      setSaving(false);
    }
  };

  return <form onSubmit={save} className="overflow-hidden rounded-lg border border-primary-100 bg-white">
    <div className="border-b border-primary-100 bg-primary-50 px-3 py-2"><p className="text-caption font-bold text-primary-700">Technical review</p><p className="text-caption text-slate-600">{metadata.join(' · ')}</p></div>
    <div className="divide-y divide-slate-100">{parsedRows.map((row) => { const criterion = TECHNICAL_REVIEW_CRITERIA.find((item) => item.label === row.label); return <div key={row.label} className="grid gap-2 px-3 py-2 sm:grid-cols-[minmax(0,1fr)_minmax(0,1.3fr)] sm:gap-4"><div><p className="text-caption font-semibold text-slate-800">{row.label}</p><p className="mt-0.5 text-caption leading-5 text-slate-500"><span className="font-semibold text-slate-600">Assessment:</span> {criterion?.description || '—'}</p><p className="mt-1 text-caption text-primary-700">Evidence: {row.evidence}</p></div><textarea value={reviews[row.label] || ''} onChange={(event) => setReviews((current) => ({ ...current, [row.label]: event.target.value }))} minLength={3} required rows={3} aria-label={`Feedback for ${row.label}`} className="w-full resize-y rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-caption leading-5 text-slate-700 outline-none transition focus:border-primary focus:bg-white focus:ring-2 focus:ring-primary-100" /></div>; })}</div>
    <div className="flex justify-end gap-2 border-t border-slate-100 bg-slate-50 px-3 py-2"><button type="button" onClick={onCancel} className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-caption font-semibold text-slate-700 hover:bg-slate-50">Cancel</button><button type="submit" disabled={saving} className="rounded-lg bg-primary px-3 py-1.5 text-caption font-semibold text-white hover:bg-primary-600 disabled:cursor-not-allowed disabled:opacity-50">{saving ? 'Saving…' : 'Save changes'}</button></div>
  </form>;
}

// ─── Avatar initial ───────────────────────────────────────────────────────────
const Avatar = ({ name, avatarUrl, size = 'md' }) => {
  const [imageFailed, setImageFailed] = useState(false);
  const sz = size === 'sm' ? 'w-7 h-7 text-overline' : 'w-8 h-8 text-xs';
  return (
    <div className={`${sz} overflow-hidden rounded-full bg-orange-100 text-orange-700 font-bold flex items-center justify-center shrink-0`}>
      {avatarUrl && !imageFailed ? (
        <img src={avatarUrl} alt="" onError={() => setImageFailed(true)} className="h-full w-full object-cover" />
      ) : (name?.charAt(0)?.toUpperCase() || '?')}
    </div>
  );
};

function ReplyItem({ reply, teamId, checkpointNumber, onPosted, onRequestDelete }) {
  const { user } = useAuth();
  const [editing, setEditing] = useState(false);
  const canEdit = ['MENTOR', 'LECTURER', 'STUDENT'].includes(user?.role?.toUpperCase()) && reply.user?._id === user?._id;
  const canDelete = user?.role?.toUpperCase() === 'ADMIN' || reply.user?._id === user?._id;
  const isTechnicalReview = reply.comment?.startsWith('TECHNICAL REVIEW\n');
  return <div className="flex gap-2.5">
    <CornerDownRight className="mt-1 h-3.5 w-3.5 shrink-0 text-slate-300" />
    <Avatar name={reply.user?.name} avatarUrl={reply.user?.avatarUrl} size="sm" />
    <div className="min-w-0 flex-1 space-y-1 rounded-xl border border-slate-100 bg-slate-50/60 px-3 py-2">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-caption font-bold text-slate-800">{reply.user?.name}</span>
        <span className={`text-[8px] font-bold px-1.5 py-px border rounded-md ${roleBadge(reply.user?.role)}`}>{roleLabel(reply.user?.role)}</span>
        <span className="text-[9px] text-slate-400">{new Date(reply.createdAt).toLocaleString()}</span>
        {canEdit && !editing && <button type="button" onClick={() => setEditing(true)} className="inline-flex h-6 w-6 items-center justify-center rounded-md text-slate-400 transition hover:bg-white hover:text-primary-700" aria-label="Edit feedback" title="Edit feedback"><Pencil className="h-3 w-3" /></button>}
      </div>
      {editing ? (isTechnicalReview ? <TechnicalReviewEditor feedback={reply} teamId={teamId} checkpointNumber={checkpointNumber} onSaved={(updated) => { onPosted?.(updated); setEditing(false); }} onCancel={() => setEditing(false)} /> : <FeedbackEditor feedback={reply} teamId={teamId} checkpointNumber={checkpointNumber} onSaved={(updated) => { onPosted?.(updated); setEditing(false); }} onCancel={() => setEditing(false)} framed={false} />) : <TechnicalReviewDisplay content={reply.comment} />}
      <div className="flex items-center gap-2">
        {canDelete && <button onClick={() => onRequestDelete(reply)} className="inline-flex items-center gap-1 text-[9px] font-bold text-slate-400 hover:text-red-600"><Trash2 className="h-3 w-3" />Delete</button>}
      </div>
    </div>
  </div>;
}

// ─── Single comment with optional reply thread ────────────────────────────────
function FeedbackItem({ comment, replies, teamId, checkpointNumber, onPosted, onRequestDelete }) {
  const { user } = useAuth();
  const [open,    setOpen]    = useState(false);
  const [text,    setText]    = useState('');
  const [sending, setSending] = useState(false);
  const [editing, setEditing] = useState(false);
  const canDelete = user?.role?.toUpperCase() === 'ADMIN' || comment.user?._id === user?._id;
  const canEdit = ['MENTOR', 'LECTURER', 'STUDENT'].includes(user?.role?.toUpperCase()) && comment.user?._id === user?._id;
  const isTechnicalReview = comment.comment?.startsWith('TECHNICAL REVIEW\n');
  const canExport = (['ADMIN', 'LECTURER'].includes(user?.role?.toUpperCase()) ||
    (user?.role?.toUpperCase() === 'MENTOR' && comment.user?._id === user?._id)) &&
    comment.comment?.startsWith('TECHNICAL REVIEW\n');
  const exportReview = async () => {
    try {
      const { blob, fileName } = await checkpointApi.exportTechnicalReview(teamId, checkpointNumber, comment._id);
      const url = URL.createObjectURL(blob); const link = document.createElement('a'); link.href = url; link.download = fileName; link.click(); URL.revokeObjectURL(url);
    } catch { toast.error('Unable to export the technical review.'); }
  };

  const handleReply = async (e) => {
    e.preventDefault();
    if (!text.trim() || sending) return;
    setSending(true);
    try {
      const res = await checkpointApi.addFeedback(teamId, checkpointNumber, {
        comment: text.trim(),
        parentFeedbackId: comment._id,
      });
      toast.success('Reply posted!');
      setText('');
      setOpen(false);
      onPosted?.(res?.data);
    } catch (err) {
      toast.error(err?.response?.data?.error || 'Failed to post reply.');
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="space-y-3">
      {/* Root comment */}
      <div className="flex gap-3">
        <Avatar name={comment.user?.name} avatarUrl={comment.user?.avatarUrl} />
        <div className="min-w-0 flex-1 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-xs font-bold text-slate-800">{comment.user?.name}</span>
            <span className={`text-[9px] font-bold px-1.5 py-px border rounded-md ${roleBadge(comment.user?.role)}`}>
              {roleLabel(comment.user?.role)}
            </span>
            <span className="text-overline text-slate-400 flex items-center gap-1">
              <Calendar className="w-3 h-3" />
              {new Date(comment.createdAt).toLocaleString()}
            </span>
            {canEdit && !editing && <button type="button" onClick={() => setEditing(true)} className="inline-flex h-7 w-7 items-center justify-center rounded-md text-slate-400 transition hover:bg-slate-100 hover:text-primary-700" aria-label="Edit feedback" title="Edit feedback"><Pencil className="h-3.5 w-3.5" /></button>}
          </div>
          {editing ? (isTechnicalReview ? <TechnicalReviewEditor feedback={comment} teamId={teamId} checkpointNumber={checkpointNumber} onSaved={(updated) => { onPosted?.(updated); setEditing(false); }} onCancel={() => setEditing(false)} /> : <FeedbackEditor feedback={comment} teamId={teamId} checkpointNumber={checkpointNumber} onSaved={(updated) => { onPosted?.(updated); setEditing(false); }} onCancel={() => setEditing(false)} framed={false} />) : <TechnicalReviewDisplay content={comment.comment} />}
          <div className="mt-2 flex flex-wrap items-center gap-2">
            {canExport && <button type="button" onClick={exportReview} className="inline-flex items-center gap-1 rounded-md bg-primary px-2.5 py-1 text-overline font-bold text-white shadow-sm transition hover:bg-primary-600"><Download className="h-3 w-3" />Export Excel</button>}
            <button onClick={() => { setOpen((o) => !o); setText(''); }} className="inline-flex rounded-md bg-secondary px-2.5 py-1 text-overline font-bold text-white shadow-sm transition hover:bg-secondary-700">{open ? 'Cancel' : 'Reply'}</button>
            {canDelete && <button onClick={() => onRequestDelete(comment)} className="inline-flex rounded-md border border-red-300 bg-white px-2.5 py-1 text-overline font-bold text-danger-dark transition hover:bg-red-50">Delete</button>}
          </div>
        </div>
      </div>

      {/* Nested replies */}
      {replies.length > 0 && (
        <div className="pl-11 space-y-3">
          {replies.map((reply) => <ReplyItem key={reply._id} reply={reply} teamId={teamId} checkpointNumber={checkpointNumber} onPosted={onPosted} onRequestDelete={onRequestDelete} />)}
        </div>
      )}

      {/* Reply input */}
      {open && (
        <form onSubmit={handleReply} className="pl-11 flex gap-2 items-end">
          <Avatar name={user?.name} avatarUrl={user?.avatarUrl || user?.avatar} size="sm" />
          <AutoSizeTextarea
            autoFocus
            minRows={1}
            maxRows={6}
            value={text}
            onChange={(e) => setText(e.target.value)}
            submitOnEnter={submitForm}
            aria-label="Write a reply"
            placeholder="Write a reply…"
            className="flex-1 text-xs leading-5 border border-slate-200 rounded-xl px-3 py-1.5 focus:outline-none focus:border-orange-400 bg-white"
          />
          <button
            type="submit"
            disabled={!text.trim() || sending}
            className="p-1.5 rounded-xl bg-orange-500 text-white hover:bg-orange-600 disabled:opacity-40 transition-colors"
          >
            <Send className="w-3.5 h-3.5" />
          </button>
        </form>
      )}
    </div>
  );
}

// ─── Main FeedbackThread component ───────────────────────────────────────────
export default function FeedbackThread({ feedbacks, teamId, checkpointNumber, onPosted, onDeleted, fullHeight = false }) {
  const { user } = useAuth();
  const [text,    setText]    = useState('');
  const [sending, setSending] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);
  const [feedbackType, setFeedbackType] = useState('regular');
  const feedbackListRef = useRef(null);
  const isMentor = user?.role?.toUpperCase() === 'MENTOR';

  const roots   = feedbacks.filter((f) => !f.parentFeedbackId || !feedbacks.some((parent) => parent._id === f.parentFeedbackId));
  const replies = (parentId) => feedbacks.filter((f) => f.parentFeedbackId === parentId);

  const handlePosted = (feedback) => {
    onPosted?.(feedback);
    window.setTimeout(() => {
      if (feedbackListRef.current) feedbackListRef.current.scrollTop = feedbackListRef.current.scrollHeight;
    }, 0);
  };

  const handlePost = async (e) => {
    e.preventDefault();
    if (!text.trim() || sending) return;
    setSending(true);
    try {
      const res = await checkpointApi.addFeedback(teamId, checkpointNumber, { comment: text.trim() });
      toast.success('Feedback posted!');
      setText('');
      handlePosted(res?.data);
    } catch (err) {
      toast.error(err?.response?.data?.error || 'Failed to post feedback.');
    } finally {
      setSending(false);
    }
  };

  const deleteFeedback = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      const res = await checkpointApi.deleteFeedback(teamId, checkpointNumber, deleteTarget._id);
      if (res.success) {
        onDeleted?.(deleteTarget._id);
        setDeleteTarget(null);
        toast.success('Feedback deleted.');
      }
    } catch (err) {
      toast.error(err?.response?.data?.error || 'Failed to delete feedback.');
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="space-y-5">
      {/* Comment list */}
      {roots.length === 0 ? (
        <div className="text-center py-10 text-slate-400 text-sm bg-slate-50/50 border border-dashed border-slate-200 rounded-xl">
          No feedback yet. Be the first to comment!
        </div>
      ) : (
        <div ref={feedbackListRef} className={`space-y-5 overflow-y-auto pr-1 scrollbar-thin ${fullHeight ? 'max-h-[min(50vh,480px)]' : 'max-h-[340px]'}`}>
          {roots.map((root) => (
            <div key={root._id} className="pb-4 border-b border-slate-100 last:border-b-0">
              <FeedbackItem
                comment={root}
                replies={replies(root._id)}
                teamId={teamId}
                checkpointNumber={checkpointNumber}
                onPosted={handlePosted}
                onRequestDelete={setDeleteTarget}
              />
            </div>
          ))}
        </div>
      )}

      {isMentor && feedbackType === 'technical' ? (
        createPortal(
          <div className="fixed inset-0 z-[110] flex items-center justify-center bg-slate-950/40 p-4" role="dialog" aria-modal="true" aria-label="Technical review form">
            <button type="button" className="absolute inset-0 cursor-default" aria-label="Close technical review form" onClick={() => setFeedbackType('regular')} />
            <div className="relative z-10 flex max-h-[calc(100dvh-2rem)] w-full max-w-5xl flex-col overflow-hidden rounded-2xl bg-white shadow-2xl">
              <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
                <div><h3 className="text-subheading font-bold text-slate-900">Technical review form</h3><p className="mt-0.5 text-caption text-slate-500">Complete the review for this checkpoint.</p></div>
                <button type="button" onClick={() => setFeedbackType('regular')} className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-slate-500 transition hover:bg-slate-100 hover:text-slate-700" aria-label="Close technical review form"><X className="h-5 w-5" /></button>
              </div>
              <div className="overflow-y-auto p-5"><TechnicalReviewForm teamId={teamId} checkpointNumber={checkpointNumber} onPosted={(feedback) => { handlePosted(feedback); setFeedbackType('regular'); }} /></div>
            </div>
          </div>,
          document.body,
        )
      ) : <form onSubmit={handlePost} className="flex gap-3 items-start pt-1">
        <Avatar name={user?.name} avatarUrl={user?.avatarUrl || user?.avatar} />
        <div className="flex-1 rounded-2xl border border-slate-200 bg-white p-3 shadow-sm">
          <label className="block text-caption font-semibold text-slate-700" htmlFor="regular-feedback">Your feedback</label>
          <AutoSizeTextarea
            id="regular-feedback"
            minRows={2}
            maxRows={10}
            value={text}
            onChange={(e) => setText(e.target.value)}
            submitOnEnter={submitForm}
            aria-label="Your feedback"
            placeholder="Share feedback or ask a question about this checkpoint…"
            className="mt-1 w-full resize-none border-0 bg-transparent px-0 py-1 text-body-sm leading-relaxed outline-none placeholder:text-slate-400 focus:ring-0"
          />
          <div className="mt-2 flex items-center justify-between gap-3 border-t border-slate-100 pt-2">
            {isMentor ? <FeedbackModeSwitch value="regular" onChange={setFeedbackType} /> : <span className="text-caption text-slate-400"></span>}
            <button type="submit" disabled={!text.trim() || sending} aria-label="Post feedback" title="Post feedback" className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary text-white shadow-sm transition hover:bg-primary-600 disabled:opacity-40">
              <Send className="h-4 w-4" />
            </button>
          </div>
        </div>
      </form>}

      <ConfirmDialog
        isOpen={Boolean(deleteTarget)}
        onClose={() => { if (!deleting) setDeleteTarget(null); }}
        onConfirm={deleteFeedback}
        title="Delete feedback?"
        description="This comment will be permanently removed from the checkpoint."
        confirmText="Delete feedback"
        isSubmitting={deleting}
      />
    </div>
  );
}
