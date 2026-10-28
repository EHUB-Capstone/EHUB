// @ts-nocheck
// src/components/workspace/checkpoints/FeedbackThread.jsx
import { useState } from 'react';
import { ChevronDown, ChevronUp, CornerDownRight, Calendar, ClipboardCheck, Download, Send, Trash2 } from 'lucide-react';
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
            <p className="mt-0.5 text-caption text-slate-600">For IT mentors.</p>
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
  const rows = lines.slice(4).reduce((items, line) => {
    const match = line.match(/^• (.+?) \((.+?)\): ([\s\S]*)$/);
    if (match) items.push({ label: match[1], evidence: match[2], review: match[3] });
    else if (items.length) items.at(-1).review += `\n${line}`;
    return items;
  }, []);
  return <div className="overflow-hidden rounded-lg border border-primary-100 bg-white">
    <div className="border-b border-primary-100 bg-primary-50 px-3 py-2"><p className="text-caption font-bold text-primary-700">Technical review</p><p className="text-caption text-slate-600">{metadata.join(' · ')}</p></div>
    <div className="divide-y divide-slate-100">{rows.map((row) => { const criterion = TECHNICAL_REVIEW_CRITERIA.find((item) => item.label === row.label); return <div key={row.label} className="grid gap-2 px-3 py-2 sm:grid-cols-[minmax(0,1fr)_minmax(0,1.3fr)] sm:gap-4"><div><p className="text-caption font-semibold text-slate-800">{row.label}</p><p className="mt-0.5 text-caption leading-5 text-slate-500"><span className="font-semibold text-slate-600">Assessment:</span> {criterion?.description || '—'}</p><p className="mt-1 text-caption text-primary-700">Evidence: {row.evidence}</p></div><p className="text-caption whitespace-pre-wrap break-words text-slate-600">{row.review}</p></div>; })}</div>
  </div>;
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

// ─── Single comment with optional reply thread ────────────────────────────────
function FeedbackItem({ comment, replies, teamId, checkpointNumber, onPosted, onRequestDelete }) {
  const { user } = useAuth();
  const [open,    setOpen]    = useState(false);
  const [text,    setText]    = useState('');
  const [sending, setSending] = useState(false);
  const canDelete = user?.role?.toUpperCase() === 'ADMIN' || comment.user?._id === user?._id;
  const canExport = (['ADMIN', 'LECTURER'].includes(user?.role?.toUpperCase()) ||
    (user?.role?.toUpperCase() === 'MENTOR' && ['ACADEMIC', 'IT'].includes(String(user?.mentorType || '').toUpperCase()) && comment.user?._id === user?._id)) &&
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
          </div>
          <TechnicalReviewDisplay content={comment.comment} />
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
          {replies.map((r) => (
            <div key={r._id} className="flex gap-2.5">
              <CornerDownRight className="w-3.5 h-3.5 text-slate-300 mt-1 shrink-0" />
              <Avatar name={r.user?.name} avatarUrl={r.user?.avatarUrl} size="sm" />
              <div className="min-w-0 flex-1 bg-slate-50/60 border border-slate-100 rounded-xl px-3 py-2 space-y-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-caption font-bold text-slate-800">{r.user?.name}</span>
                  <span className={`text-[8px] font-bold px-1.5 py-px border rounded-md ${roleBadge(r.user?.role)}`}>
                    {roleLabel(r.user?.role)}
                  </span>
                  <span className="text-[9px] text-slate-400">
                    {new Date(r.createdAt).toLocaleString()}
                  </span>
                </div>
                <TechnicalReviewDisplay content={r.comment} />
                {(user?.role?.toUpperCase() === 'ADMIN' || r.user?._id === user?._id) && <button onClick={() => onRequestDelete(r)} className="inline-flex items-center gap-1 text-[9px] font-bold text-slate-400 hover:text-red-600"><Trash2 className="w-3 h-3" />Delete</button>}
              </div>
            </div>
          ))}
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
  const isTechnicalMentor = user?.role?.toUpperCase() === 'MENTOR' &&
    ['ACADEMIC', 'IT'].includes(String(user?.mentorType || '').toUpperCase());

  const roots   = feedbacks.filter((f) => !f.parentFeedbackId || !feedbacks.some((parent) => parent._id === f.parentFeedbackId));
  const replies = (parentId) => feedbacks.filter((f) => f.parentFeedbackId === parentId);

  const handlePost = async (e) => {
    e.preventDefault();
    if (!text.trim() || sending) return;
    setSending(true);
    try {
      const res = await checkpointApi.addFeedback(teamId, checkpointNumber, { comment: text.trim() });
      toast.success('Feedback posted!');
      setText('');
      onPosted?.(res?.data);
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
        <div className={`space-y-5 overflow-y-auto pr-1 scrollbar-thin ${fullHeight ? 'max-h-[min(50vh,480px)]' : 'max-h-[340px]'}`}>
          {roots.map((root) => (
            <div key={root._id} className="pb-4 border-b border-slate-100 last:border-b-0">
              <FeedbackItem
                comment={root}
                replies={replies(root._id)}
                teamId={teamId}
                checkpointNumber={checkpointNumber}
                onPosted={onPosted}
                onRequestDelete={setDeleteTarget}
              />
            </div>
          ))}
        </div>
      )}

      {/* IT mentors use the structured technical review; enterprise mentors keep the existing discussion flow. */}
      {isTechnicalMentor ? (
        <TechnicalReviewForm teamId={teamId} checkpointNumber={checkpointNumber} onPosted={onPosted} initiallyCollapsed={feedbacks.some((item) => item.user?._id === user?._id && item.comment?.startsWith('TECHNICAL REVIEW\n'))} />
      ) : <form onSubmit={handlePost} className="flex gap-3 items-start pt-1">
        <Avatar name={user?.name} avatarUrl={user?.avatarUrl || user?.avatar} />
        <div className="flex-1 space-y-2">
          <AutoSizeTextarea
            minRows={2}
            maxRows={10}
            value={text}
            onChange={(e) => setText(e.target.value)}
            submitOnEnter={submitForm}
            aria-label="Share feedback"
            placeholder="Share feedback or ask a question about this checkpoint…"
            className="w-full text-sm border border-slate-200 rounded-xl px-4 py-3 focus:outline-none focus:ring-2 focus:ring-orange-400/30 focus:border-orange-400 bg-slate-50/50 leading-relaxed"
          />
          <div className="flex items-center justify-between gap-3">
            <span className="text-caption text-slate-400">Enter to send · Shift+Enter for a new line</span>
            <button
              type="submit"
              disabled={!text.trim() || sending}
              className="inline-flex items-center gap-1.5 px-4 py-1.5 rounded-xl bg-orange-500 text-white text-xs font-bold hover:bg-orange-600 disabled:opacity-40 transition-colors shadow-sm"
            >
              <Send className="w-3 h-3" />
              Post Feedback
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
