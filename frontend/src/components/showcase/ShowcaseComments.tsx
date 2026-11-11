import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useInfiniteQuery, useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { useLocation } from 'react-router-dom';
import { MessageSquare } from 'lucide-react';
import toast from 'react-hot-toast';
import Badge from '../ui/Badge';
import Button from '../ui/Button';
import Card from '../ui/Card';
import AutoSizeTextarea from '../ui/AutoSizeTextarea';
import CharacterCounter from '../ui/CharacterCounter';
import ConfirmDialog from '../ui/ConfirmDialog';
import EmptyState from '../ui/EmptyState';
import ErrorState from '../ui/ErrorState';
import LoadingSkeleton from '../ui/LoadingSkeleton';
import { showcaseApi } from '../../api/showcaseApi';
import { useAuthContext } from '../../context/AuthContext';
import type { ShowcaseCommentDto } from '../../types/showcase';
import { cn } from '../../utils/cn';
import { parseApiError } from '../../utils/apiError';
import { SHOWCASE_LIMITS, timeAgo } from '../../utils/showcase';
import ShowcaseReportButton from './ShowcaseReportButton';

const PAGE_SIZE = 10;
const REPLY_PAGE_SIZE = 20;

const commentsKey = (showcaseId: string) => ['showcase', 'comments', showcaseId] as const;

/** A comment changes the thread, the counters on this page and the counters on the feed cards. */
const refreshComments = (queryClient: QueryClient, showcaseId: string) => Promise.all([
  queryClient.invalidateQueries({ queryKey: commentsKey(showcaseId) }),
  queryClient.invalidateQueries({ queryKey: ['showcase', 'detail', showcaseId] }),
  queryClient.invalidateQueries({ queryKey: ['showcase', 'discovery'] }),
]);

const failureMessage = (error: unknown, fallback: string): string =>
  (error as { response?: { status?: number } }).response?.status === 429
    ? 'You are commenting too fast. Please wait a minute and try again.'
    : parseApiError(error, fallback).message;

function Avatar({ name, url, small = false }: { name: string; url: string | null; small?: boolean }) {
  const size = small ? 'h-8 w-8 text-caption' : 'h-9 w-9 text-body-sm';
  return url
    ? <img src={url} alt="" className={`${size} shrink-0 rounded-full object-cover`} />
    : <span className={`grid ${size} shrink-0 place-items-center rounded-full bg-secondary-50 font-semibold text-secondary`} aria-hidden="true">{name.charAt(0).toUpperCase()}</span>;
}

interface ComposerProps {
  showcaseId: string;
  parentCommentId?: string;
  initialText?: string;
  placeholder: string;
  submitLabel: string;
  autoFocus?: boolean;
  small?: boolean;
  onPosted?: () => void;
  onCancel?: () => void;
}

function Composer({ showcaseId, parentCommentId, initialText = '', placeholder, submitLabel, autoFocus, small, onPosted, onCancel }: ComposerProps) {
  const queryClient = useQueryClient();
  const { user } = useAuthContext();
  const [text, setText] = useState(initialText);
  const post = useMutation({
    mutationFn: () => showcaseApi.createComment(showcaseId, { content: text.trim(), parentCommentId: parentCommentId ?? null }),
    onSuccess: async () => {
      setText('');
      await refreshComments(queryClient, showcaseId);
      toast.success(parentCommentId ? 'Reply posted.' : 'Comment posted.');
      onPosted?.();
    },
    // The text stays in the box so nothing typed is lost.
    onError: (error) => toast.error(failureMessage(error, 'Could not post your comment. Try again.')),
  });
  const tooLong = text.length > SHOWCASE_LIMITS.comment;
  const canPost = text.trim().length > 0 && !tooLong && !post.isPending;

  return (
    <form className="flex gap-3" onSubmit={(event) => { event.preventDefault(); if (canPost) post.mutate(); }}>
      <Avatar name={user?.fullName ?? '?'} url={user?.avatarUrl ?? null} small={small} />
      <div className="min-w-0 flex-1 rounded-xl border border-slate-300 bg-white px-3 py-2 focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/30">
        <AutoSizeTextarea
          aria-label={placeholder}
          placeholder={placeholder}
          autoFocus={autoFocus}
          minRows={small ? 1 : 2}
          maxRows={8}
          value={text}
          onChange={(event) => setText(event.target.value)}
          className="w-full resize-none bg-transparent text-body text-slate-700 outline-none placeholder:text-slate-400"
        />
        <div className="mt-1 flex items-center justify-between gap-3">
          <CharacterCounter value={text} max={SHOWCASE_LIMITS.comment} />
          <div className="flex items-center gap-2">
            {onCancel && <Button type="button" variant="ghost" size="sm" onClick={onCancel} disabled={post.isPending}>Cancel</Button>}
            <Button type="submit" size="sm" isLoading={post.isPending} disabled={!canPost}>{submitLabel}</Button>
          </div>
        </div>
      </div>
    </form>
  );
}

const CLAMPED_LINES = 'line-clamp-7';

/**
 * The last lines of a folded comment fade out, so it is plain that there is more to read. A mask fades the text itself,
 * which works on any background (light or dark) without painting a colour over it.
 */
const FADE_OUT = 'linear-gradient(to bottom, black 65%, transparent 100%)';
const foldedFade = { maskImage: FADE_OUT, WebkitMaskImage: FADE_OUT } as const;

/** Comment text that folds to a few lines when it is long, with a toggle to read it all. */
function ExpandableText({ text }: { text: string }) {
  const ref = useRef<HTMLParagraphElement>(null);
  const [expanded, setExpanded] = useState(false);
  const [overflows, setOverflows] = useState(false);

  useLayoutEffect(() => {
    const element = ref.current;
    if (!element) return undefined;
    // Measured while folded; once expanded the toggle must stay so the text can be folded again.
    const measure = () => { if (!expanded) setOverflows(element.scrollHeight > element.clientHeight + 1); };
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, [text, expanded]);

  return (
    <>
      <p
        ref={ref}
        className={cn('mt-0.5 whitespace-pre-line break-words text-body text-slate-700', !expanded && CLAMPED_LINES)}
        style={!expanded && overflows ? foldedFade : undefined}
      >
        {text}
      </p>
      {overflows && (
        <button
          type="button"
          aria-expanded={expanded}
          onClick={() => setExpanded((value) => !value)}
          className="mt-0.5 rounded text-body-sm font-semibold text-primary-700 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
        >
          {expanded ? 'Show less' : 'Show more'}
        </button>
      )}
    </>
  );
}

function CommentBody({ showcaseId, comment, small = false, onReply, onDelete }: {
  showcaseId: string;
  comment: ShowcaseCommentDto;
  small?: boolean;
  onReply: () => void;
  onDelete: () => void;
}) {
  return (
    <div id={`comment-${comment.id}`} className="flex scroll-mt-6 gap-3">
      <Avatar name={comment.authorName} url={comment.authorAvatarUrl} small={small} />
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
          <span className="text-body font-semibold text-secondary">{comment.authorName}</span>
          {comment.isTeamReply && <Badge variant="brand" size="sm">Team</Badge>}
          <time dateTime={comment.createdAt} title={new Date(comment.createdAt).toLocaleString()} className="text-caption text-slate-500">{timeAgo(comment.createdAt)}</time>
        </div>
        <ExpandableText text={comment.content} />
        <div className="mt-1 flex gap-4 text-body-sm font-semibold text-slate-500">
          <button type="button" onClick={onReply} className="rounded hover:text-secondary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600">Reply</button>
          {comment.canDelete && (
            <button type="button" onClick={onDelete} className="rounded hover:text-danger-dark focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600">Delete</button>
          )}
          {!comment.canDelete && <ShowcaseReportButton showcaseId={showcaseId} commentId={comment.id} />}
        </div>
      </div>
    </div>
  );
}

function Thread({ showcaseId, comment, onDelete }: { showcaseId: string; comment: ShowcaseCommentDto; onDelete: (comment: ShowcaseCommentDto) => void }) {
  const [expanded, setExpanded] = useState(false);
  const [replyTo, setReplyTo] = useState<ShowcaseCommentDto | null>(null);
  const hasHiddenReplies = comment.replyCount > comment.replies.length;
  const replies = useInfiniteQuery({
    queryKey: [...commentsKey(showcaseId), 'replies', comment.id],
    queryFn: ({ pageParam, signal }) => showcaseApi.getCommentReplies(showcaseId, comment.id, pageParam, REPLY_PAGE_SIZE, signal),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.pageIndex < last.totalPages ? last.pageIndex + 1 : undefined),
    enabled: expanded,
    staleTime: 0,
  });
  const loaded = replies.data?.pages.flatMap((page) => page.items);
  const shown = expanded && loaded ? loaded : comment.replies;

  return (
    <li>
      <CommentBody showcaseId={showcaseId} comment={comment} onReply={() => setReplyTo(comment)} onDelete={() => onDelete(comment)} />

      {(shown.length > 0 || replyTo) && (
        <div className="ml-12 mt-3 space-y-3.5 border-l-2 border-slate-200 pl-4">
          <ul className="space-y-3.5">
            {shown.map((reply) => (
              <li key={reply.id}>
                <CommentBody showcaseId={showcaseId} comment={reply} small onReply={() => setReplyTo(reply)} onDelete={() => onDelete(reply)} />
              </li>
            ))}
          </ul>

          {replies.isError && (
            <p role="alert" className="text-body-sm text-slate-600">
              Could not load the replies.{' '}
              <button type="button" className="font-semibold text-primary-700 hover:underline" onClick={() => { void replies.refetch(); }}>Retry</button>
            </p>
          )}

          {!expanded && hasHiddenReplies && (
            <button type="button" onClick={() => setExpanded(true)} className="rounded text-body-sm font-semibold text-primary-700 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600">
              View {comment.replyCount - comment.replies.length} more {comment.replyCount - comment.replies.length === 1 ? 'reply' : 'replies'}
            </button>
          )}
          {expanded && replies.isFetching && !replies.isFetchingNextPage && !loaded && <p className="text-body-sm text-slate-500" role="status">Loading replies…</p>}
          {expanded && replies.hasNextPage && (
            <Button variant="ghost-primary" size="sm" isLoading={replies.isFetchingNextPage} onClick={() => { void replies.fetchNextPage(); }}>Load more replies</Button>
          )}

          {replyTo && (
            <Composer
              key={replyTo.id}
              showcaseId={showcaseId}
              parentCommentId={replyTo.id}
              initialText={`@${replyTo.authorName} `}
              placeholder={`Reply to ${replyTo.authorName}`}
              submitLabel="Reply"
              autoFocus
              small
              onPosted={() => { setReplyTo(null); setExpanded(true); }}
              onCancel={() => setReplyTo(null)}
            />
          )}
        </div>
      )}
    </li>
  );
}

interface ShowcaseCommentsProps {
  showcaseId: string;
  commentCount: number;
}

/** Community comments of a published page: newest first, replies one level deep, loaded in small pages. */
export default function ShowcaseComments({ showcaseId, commentCount }: ShowcaseCommentsProps) {
  const location = useLocation();
  const queryClient = useQueryClient();
  const [target, setTarget] = useState<ShowcaseCommentDto | null>(null);
  const comments = useInfiniteQuery({
    queryKey: [...commentsKey(showcaseId), 'list'],
    queryFn: ({ pageParam, signal }) => showcaseApi.getComments(showcaseId, pageParam, PAGE_SIZE, signal),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.pageIndex < last.totalPages ? last.pageIndex + 1 : undefined),
    staleTime: 0,
  });
  const remove = useMutation({
    mutationFn: (comment: ShowcaseCommentDto) => showcaseApi.deleteComment(showcaseId, comment.id),
    onSuccess: async () => {
      await refreshComments(queryClient, showcaseId);
      toast.success('Comment removed.');
      setTarget(null);
    },
    onError: (error) => toast.error(failureMessage(error, 'Could not remove the comment. Try again.')),
  });
  const items = comments.data?.pages.flatMap((page) => page.items) ?? [];

  // A moderator can deep-link from the report queue to a loaded comment without adding another API endpoint.
  useEffect(() => {
    const target = location.hash.startsWith('#comment-') ? document.getElementById(location.hash.slice(1)) : null;
    if (target) target.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }, [items, location.hash]);

  return (
    <Card hover={false} padding="p-5 sm:p-6" aria-labelledby="showcase-comments-heading">
      <Card.Header className="mb-4">
        <Card.Title icon={MessageSquare}>
          <span id="showcase-comments-heading">Comments</span>
          <span className="ml-2 rounded-full bg-slate-100 px-2 py-0.5 text-caption font-semibold text-slate-700">{commentCount.toLocaleString()}</span>
        </Card.Title>
      </Card.Header>

      <Composer showcaseId={showcaseId} placeholder="Share feedback or ask the team a question…" submitLabel="Post comment" />

      <div className="mt-6">
        {comments.isPending && <div role="status" aria-label="Loading comments" className="space-y-3"><LoadingSkeleton lines={3} /></div>}
        {comments.isError && (
          <ErrorState title="Could not load comments" message={parseApiError(comments.error, 'Failed to load comments.').message} onRetry={() => { void comments.refetch(); }} />
        )}
        {comments.isSuccess && items.length === 0 && (
          <EmptyState icon={MessageSquare} title="No comments yet" description="Be the first to share what you think about this project." size="sm" />
        )}
        {items.length > 0 && (
          <ul className="space-y-6">
            {items.map((comment) => <Thread key={comment.id} showcaseId={showcaseId} comment={comment} onDelete={setTarget} />)}
          </ul>
        )}
        {comments.hasNextPage && (
          <div className="mt-6 text-center">
            <Button variant="outline" isLoading={comments.isFetchingNextPage} onClick={() => { void comments.fetchNextPage(); }}>Load more comments</Button>
          </div>
        )}
      </div>

      <ConfirmDialog
        isOpen={target !== null}
        onClose={() => { if (!remove.isPending) setTarget(null); }}
        onConfirm={() => { if (target) remove.mutate(target); }}
        title="Delete this comment?"
        description={target && target.replyCount > 0
          ? `Its ${target.replyCount} ${target.replyCount === 1 ? 'reply' : 'replies'} will be removed with it.`
          : 'This cannot be undone.'}
        confirmText="Delete comment"
        isSubmitting={remove.isPending}
      />
    </Card>
  );
}
