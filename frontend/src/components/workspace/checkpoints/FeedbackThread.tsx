// @ts-nocheck
// src/components/workspace/checkpoints/FeedbackThread.jsx
import { useState } from 'react';
import { CornerDownRight, Calendar, Send } from 'lucide-react';
import toast from 'react-hot-toast';
import { checkpointApi } from '../../../api/checkpointApi';
import { useAuth } from '../../../hooks/useAuth';

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

// ─── Avatar initial ───────────────────────────────────────────────────────────
const Avatar = ({ name, size = 'md' }) => {
  const sz = size === 'sm' ? 'w-7 h-7 text-[10px]' : 'w-8 h-8 text-xs';
  return (
    <div className={`${sz} rounded-full bg-orange-100 text-orange-700 font-bold flex items-center justify-center shrink-0`}>
      {name?.charAt(0)?.toUpperCase() || '?'}
    </div>
  );
};

// ─── Single comment with optional reply thread ────────────────────────────────
function FeedbackItem({ comment, replies, teamId, checkpointNumber, onPosted }) {
  const { user } = useAuth();
  const [open,    setOpen]    = useState(false);
  const [text,    setText]    = useState('');
  const [sending, setSending] = useState(false);

  const handleReply = async (e) => {
    e.preventDefault();
    if (!text.trim()) return;
    setSending(true);
    try {
      await checkpointApi.addFeedback(teamId, checkpointNumber, {
        comment: text.trim(),
        parentFeedbackId: comment._id,
      });
      toast.success('Reply posted!');
      setText('');
      setOpen(false);
      onPosted?.();
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
        <Avatar name={comment.user?.name} />
        <div className="flex-1 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-xs font-bold text-slate-800">{comment.user?.name}</span>
            <span className={`text-[9px] font-bold px-1.5 py-px border rounded-md ${roleBadge(comment.user?.role)}`}>
              {roleLabel(comment.user?.role)}
            </span>
            <span className="text-[10px] text-slate-400 flex items-center gap-1">
              <Calendar className="w-3 h-3" />
              {new Date(comment.createdAt).toLocaleString()}
            </span>
          </div>
          <p className="text-xs text-slate-600 leading-relaxed whitespace-pre-wrap">
            {comment.comment}
          </p>
          <button
            onClick={() => { setOpen((o) => !o); setText(''); }}
            className="text-[10px] font-bold text-orange-500 hover:text-orange-600 transition-colors"
          >
            {open ? 'Cancel' : 'Reply'}
          </button>
        </div>
      </div>

      {/* Nested replies */}
      {replies.length > 0 && (
        <div className="pl-11 space-y-3">
          {replies.map((r) => (
            <div key={r._id} className="flex gap-2.5">
              <CornerDownRight className="w-3.5 h-3.5 text-slate-300 mt-1 shrink-0" />
              <Avatar name={r.user?.name} size="sm" />
              <div className="flex-1 bg-slate-50/60 border border-slate-100 rounded-xl px-3 py-2 space-y-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-[11px] font-bold text-slate-800">{r.user?.name}</span>
                  <span className={`text-[8px] font-bold px-1.5 py-px border rounded-md ${roleBadge(r.user?.role)}`}>
                    {roleLabel(r.user?.role)}
                  </span>
                  <span className="text-[9px] text-slate-400">
                    {new Date(r.createdAt).toLocaleString()}
                  </span>
                </div>
                <p className="text-[11px] text-slate-600 leading-relaxed whitespace-pre-wrap">
                  {r.comment}
                </p>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Reply input */}
      {open && (
        <form onSubmit={handleReply} className="pl-11 flex gap-2 items-center">
          <Avatar name={user?.name} size="sm" />
          <input
            autoFocus
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder="Write a reply…"
            className="flex-1 text-xs border border-slate-200 rounded-xl px-3 py-1.5 focus:outline-none focus:border-orange-400 bg-white"
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
export default function FeedbackThread({ feedbacks, teamId, checkpointNumber, onPosted, fullHeight = false }) {
  const { user } = useAuth();
  const [text,    setText]    = useState('');
  const [sending, setSending] = useState(false);

  const roots   = feedbacks.filter((f) => !f.parentFeedbackId);
  const replies = (parentId) => feedbacks.filter((f) => f.parentFeedbackId === parentId);

  const handlePost = async (e) => {
    e.preventDefault();
    if (!text.trim()) return;
    setSending(true);
    try {
      await checkpointApi.addFeedback(teamId, checkpointNumber, { comment: text.trim() });
      toast.success('Feedback posted!');
      setText('');
      onPosted?.();
    } catch (err) {
      toast.error(err?.response?.data?.error || 'Failed to post feedback.');
    } finally {
      setSending(false);
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
              />
            </div>
          ))}
        </div>
      )}

      {/* New top-level comment */}
      <form onSubmit={handlePost} className="flex gap-3 items-start pt-1">
        <Avatar name={user?.name} />
        <div className="flex-1 space-y-2">
          <textarea
            value={text}
            onChange={(e) => setText(e.target.value)}
            rows={2}
            placeholder="Share feedback or ask a question about this checkpoint…"
            className="w-full text-sm border border-slate-200 rounded-xl px-4 py-3 focus:outline-none focus:ring-2 focus:ring-orange-400/30 focus:border-orange-400 bg-slate-50/50 resize-none leading-relaxed"
          />
          <div className="flex justify-end">
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
      </form>
    </div>
  );
}
