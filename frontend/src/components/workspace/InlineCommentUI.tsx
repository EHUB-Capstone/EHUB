// @ts-nocheck
import { useEffect, useMemo, useState } from 'react';
import { CornerDownRight, MessageSquareText, RefreshCcw, Send, CheckCircle2 } from 'lucide-react';
import { getProposalComments, createProposalComment, resolveComment } from '../../api/commentApi';
import { useAuth } from '../../hooks/useAuth';
import toast from 'react-hot-toast';

const roleBadge = (role) => {
  const normalized = (role || '').toUpperCase();
  const map = {
    ADMIN: 'bg-red-50 text-red-700 border-red-200',
    LECTURER: 'bg-blue-50 text-blue-700 border-blue-200',
    MENTOR: 'bg-amber-50 text-amber-700 border-amber-200',
    STUDENT: 'bg-emerald-50 text-emerald-700 border-emerald-200',
    USER: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  };
  return map[normalized] || 'bg-slate-50 text-slate-600 border-slate-200';
};

const roleLabel = (role) => ({ ADMIN: 'Admin', LECTURER: 'Lecturer', MENTOR: 'Mentor', STUDENT: 'Student', USER: 'Student' }[(role || '').toUpperCase()] || role || 'User');

const Avatar = ({ name }) => (
  <div className="w-8 h-8 rounded-full bg-orange-100 text-orange-700 font-bold flex items-center justify-center shrink-0 text-xs">
    {name?.charAt(0)?.toUpperCase() || '?'}
  </div>
);

const buildTree = (comments = []) => {
  const byId = new Map();
  const roots = [];

  comments.forEach((comment) => {
    byId.set(comment._id, { ...comment, replies: [] });
  });

  byId.forEach((comment) => {
    if (comment.parentCommentId && byId.has(comment.parentCommentId)) {
      byId.get(comment.parentCommentId).replies.push(comment);
    } else {
      roots.push(comment);
    }
  });

  const sortReplies = (list) => {
    list.sort((a, b) => new Date(a.createdAt) - new Date(b.createdAt));
    list.forEach((item) => sortReplies(item.replies));
  };

  sortReplies(roots);
  return roots;
};

function CommentNode({ comment, onReply, onResolve, canResolve, canReply }) {
  const [showReply, setShowReply] = useState(false);
  const [replyText, setReplyText] = useState('');
  const [posting, setPosting] = useState(false);

  const handleReply = async (e) => {
    e.preventDefault();
    if (!replyText.trim()) return;
    setPosting(true);
    try {
      await onReply(comment._id, replyText.trim());
      setReplyText('');
      setShowReply(false);
    } finally {
      setPosting(false);
    }
  };

  return (
    <div className={`rounded-2xl border ${comment.resolved ? 'border-emerald-200 bg-emerald-50/40' : 'border-slate-200 bg-white'} p-4` }>
      <div className="flex gap-3">
        <Avatar name={comment.createdBy?.name} />
        <div className="flex-1 min-w-0 space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-sm font-bold text-slate-900">{comment.createdBy?.name}</span>
            <span className={`text-[10px] font-bold px-1.5 py-px border rounded-md ${roleBadge(comment.createdBy?.role)}`}>{roleLabel(comment.createdBy?.role)}</span>
            {comment.sectionLabel && <span className="text-[10px] font-semibold text-slate-500 bg-slate-100 px-2 py-1 rounded-full">{comment.sectionLabel}</span>}
            {comment.resolved && (
              <span className="inline-flex items-center gap-1 text-[10px] font-bold text-emerald-700 bg-emerald-100 px-2 py-1 rounded-full">
                <CheckCircle2 className="w-3 h-3" /> Resolved
              </span>
            )}
          </div>
          <p className="text-xs text-slate-400 flex items-center gap-1.5">
            {new Date(comment.createdAt).toLocaleString()}
            {comment.resolvedAt && <span>· resolved {new Date(comment.resolvedAt).toLocaleString()}</span>}
          </p>
          <p className="text-sm text-slate-700 whitespace-pre-wrap leading-relaxed">{comment.content}</p>

          <div className="flex flex-wrap items-center gap-2 pt-1">
            {canReply && (
              <button
                onClick={() => setShowReply((v) => !v)}
                className="text-xs font-semibold text-primary hover:underline"
              >
                {showReply ? 'Cancel' : 'Reply'}
              </button>
            )}
            {canResolve && !comment.resolved && (
              <button
                onClick={() => onResolve(comment._id)}
                className="text-xs font-semibold text-emerald-600 hover:underline"
              >
                Resolve
              </button>
            )}
          </div>

          {showReply && (
            <form onSubmit={handleReply} className="flex items-start gap-2 pt-2">
              <Avatar name={comment.createdBy?.name} />
              <div className="flex-1">
                <textarea
                  value={replyText}
                  onChange={(e) => setReplyText(e.target.value)}
                  rows={2}
                  placeholder="Write a reply..."
                  className="w-full rounded-xl border border-slate-200 px-3 py-2 text-sm focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 resize-none"
                />
                <div className="mt-2 flex justify-end">
                  <button
                    type="submit"
                    disabled={!replyText.trim() || posting}
                    className="inline-flex items-center gap-2 rounded-xl bg-primary px-4 py-2 text-xs font-semibold text-white hover:bg-primary/90 disabled:opacity-50"
                  >
                    <Send className="w-3.5 h-3.5" /> Reply
                  </button>
                </div>
              </div>
            </form>
          )}
        </div>
      </div>

      {comment.replies?.length > 0 && (
        <div className="mt-4 pl-4 space-y-3 border-l border-slate-200">
          {comment.replies.map((reply) => (
            <CommentNode
              key={reply._id}
              comment={reply}
              onReply={onReply}
              onResolve={onResolve}
              canResolve={canResolve || reply.createdBy?._id === reply.createdBy}
              canReply={canReply}
            />
          ))}
        </div>
      )}
    </div>
  );
}

export default function InlineCommentUI({ proposalId, sectionKey, sectionLabel }) {
  const { user } = useAuth();
  const [comments, setComments] = useState([]);
  const [loading, setLoading] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
  const [newComment, setNewComment] = useState('');
  const [posting, setPosting] = useState(false);

  const isStudent = user?.role === 'STUDENT' || user?.role === 'USER';
  const canStartThread = !isStudent;
  const canResolve = ['ADMIN', 'LECTURER', 'MENTOR'].includes((user?.role || '').toUpperCase());

  useEffect(() => {
    if (proposalId && isOpen) {
      fetchComments();
    }
  }, [proposalId, isOpen, sectionKey]);

  const fetchComments = async () => {
    try {
      setLoading(true);
      const res = await getProposalComments(proposalId);
      if (res.success) {
        const list = res.data?.comments || res.data || [];
        const filtered = sectionKey ? list.filter((c) => c.sectionKey === sectionKey) : list;
        setComments(filtered);
      }
    } catch (err) {
      toast.error(err.response?.data?.message || err.response?.data?.error || 'Failed to load comments');
    } finally {
      setLoading(false);
    }
  };

  const handleAddComment = async (e) => {
    e.preventDefault();
    if (!newComment.trim()) return;

    try {
      setPosting(true);
      const res = await createProposalComment(proposalId, {
        sectionKey,
        sectionLabel,
        content: newComment,
      });
      if (res.success) {
        toast.success('Comment added');
        setNewComment('');
        fetchComments();
      }
    } catch (err) {
      toast.error(err.response?.data?.message || err.response?.data?.error || 'Failed to add comment');
    } finally {
      setPosting(false);
    }
  };

  const handleResolve = async (commentId) => {
    try {
      const res = await resolveComment(commentId);
      if (res.success) {
        toast.success('Comment resolved');
        fetchComments();
      }
    } catch (err) {
      toast.error(err.response?.data?.message || err.response?.data?.error || 'Failed to resolve comment');
    }
  };

  const handleReply = async (parentCommentId, content) => {
    const res = await createProposalComment(proposalId, {
      sectionKey,
      sectionLabel,
      content,
      parentCommentId,
    });
    if (!res.success) throw new Error('Failed to reply');
    toast.success('Reply posted');
    fetchComments();
  };

  const activeComments = comments.filter((c) => !c.resolved);
  const resolvedComments = comments.filter((c) => c.resolved);
  const threads = useMemo(() => buildTree(comments), [comments]);

  return (
    <div className="mt-2">
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="inline-flex items-center gap-2 text-sm font-medium text-slate-500 hover:text-slate-700 bg-slate-50 px-3 py-1.5 rounded-xl border border-slate-200"
      >
        <MessageSquareText className="w-4 h-4" />
        {sectionLabel || sectionKey}
        <span className="text-xs text-slate-400">{activeComments.length + resolvedComments.length}</span>
      </button>

      {isOpen && (
        <div className="mt-3 p-4 sm:p-5 bg-slate-50 rounded-2xl border border-slate-200 shadow-inner space-y-4">
          <div className="flex items-center justify-between gap-3">
            <div>
              <p className="text-sm font-bold text-slate-900">Section comments</p>
              <p className="text-xs text-slate-500">Threaded feedback attached to this proposal section.</p>
            </div>
            <button
              onClick={fetchComments}
              className="inline-flex items-center gap-2 text-xs font-semibold text-slate-500 hover:text-slate-700"
            >
              <RefreshCcw className="w-3.5 h-3.5" /> Refresh
            </button>
          </div>

          {loading ? (
            <div className="text-sm text-slate-500">Loading comments...</div>
          ) : threads.length > 0 ? (
            <div className="space-y-4">
              {threads.map((root) => (
                <CommentNode
                  key={root._id}
                  comment={root}
                  onReply={handleReply}
                  onResolve={handleResolve}
                  canResolve={canResolve || root.createdBy?._id === user?._id}
                  canReply={true}
                />
              ))}
            </div>
          ) : (
            <p className="text-sm text-slate-500 italic">No comments yet for this section.</p>
          )}

          {canStartThread ? (
            <form onSubmit={handleAddComment} className="pt-2 border-t border-slate-200">
              <div className="flex items-start gap-3">
                <Avatar name={user?.name} />
                <div className="flex-1">
                  <textarea
                    rows={2}
                    value={newComment}
                    onChange={(e) => setNewComment(e.target.value)}
                    placeholder="Add a comment..."
                    className="w-full text-sm rounded-xl border border-slate-200 bg-white px-3 py-2 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 resize-none"
                  />
                  <div className="mt-2 flex justify-end">
                    <button
                      type="submit"
                      disabled={!newComment.trim() || posting}
                      className="inline-flex items-center gap-2 rounded-xl bg-primary px-4 py-2 text-xs font-semibold text-white hover:bg-primary/90 disabled:opacity-50"
                    >
                      <Send className="w-3.5 h-3.5" /> Post Comment
                    </button>
                  </div>
                </div>
              </div>
            </form>
          ) : (
            <div className="rounded-2xl border border-dashed border-slate-200 bg-white p-4 text-sm text-slate-500">
              Students can reply to comments if enabled, but cannot create new official threads.
            </div>
          )}
        </div>
      )}
    </div>
  );
}
