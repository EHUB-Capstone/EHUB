import { useMutation, useMutationState, useQueryClient, type InfiniteData } from '@tanstack/react-query';
import { Flame } from 'lucide-react';
import toast from 'react-hot-toast';
import { showcaseApi } from '../../api/showcaseApi';
import { useAuthContext } from '../../context/AuthContext';
import type { ShowcaseDetailDto, ShowcaseDiscoveryPageDto, ShowcaseUpvoteDto } from '../../types/showcase';
import { cn } from '../../utils/cn';
import { optimisticShowcaseUpvote } from '../../utils/showcase';
import { updateShowcaseDiscoveryUpvote } from '../../utils/showcaseDiscovery';
import Button from '../ui/Button';

interface ShowcaseUpvoteButtonProps extends ShowcaseUpvoteDto {
  showcaseId: string;
  isOwner: boolean;
  isPublished?: boolean;
  showLabel?: boolean;
  /** Counter-tile look (number above its label), to sit in a row of counters. */
  tile?: boolean;
  className?: string;
}

export default function ShowcaseUpvoteButton({
  showcaseId,
  likeCount,
  hasUpvoted = false,
  isOwner,
  isPublished = true,
  showLabel = false,
  tile = false,
  className,
}: ShowcaseUpvoteButtonProps) {
  const queryClient = useQueryClient();
  const { user } = useAuthContext();
  const mutationKey = ['showcase', 'upvote', user?.id, user?.role, showcaseId];
  const discovery = { queryKey: ['showcase', 'discovery', user?.id, user?.role] };
  const detailKey = ['showcase', 'detail', showcaseId, user?.id, user?.role];
  // Share the pending vote across card and detail. An error removes the overlay and reveals the last confirmed data.
  const [pendingVote] = useMutationState<ShowcaseUpvoteDto | undefined>({
    filters: { mutationKey, exact: true, status: 'pending' },
    select: mutation => mutation.state.variables as ShowcaseUpvoteDto | undefined,
  });
  const vote = useMutation({
    mutationKey,
    mutationFn: (next: ShowcaseUpvoteDto) => showcaseApi.setUpvote(showcaseId, next.hasUpvoted),
    onSuccess: saved => {
      queryClient.setQueriesData<ShowcaseDiscoveryPageDto | InfiniteData<ShowcaseDiscoveryPageDto>>(
        discovery, data => updateShowcaseDiscoveryUpvote(data, showcaseId, saved),
      );
      queryClient.setQueryData<ShowcaseDetailDto>(detailKey, detail => detail && ({
        ...detail,
        hasUpvoted: saved.hasUpvoted,
        showcase: { ...detail.showcase, likeCount: saved.likeCount },
      }));
    },
    onError: () => toast.error('Could not update your upvote. Try again.'),
    onSettled: () => {
      // A confirmed vote can change the rank, so refresh the feed after updating its counters.
      void queryClient.invalidateQueries(discovery);
      void queryClient.invalidateQueries({ queryKey: detailKey, exact: true });
    },
  });
  const current = pendingVote ?? { likeCount, hasUpvoted };
  const action = current.hasUpvoted ? 'Remove upvote' : 'Upvote';
  const disabledReason = !isPublished
    ? 'Upvotes are available after publication.'
    : user?.role === 'STUDENT' && isOwner && !current.hasUpvoted
      ? 'Team members cannot upvote their own showcase.'
      : undefined;

  return (
    <Button
      variant="ghost"
      size="md"
      isLoading={Boolean(pendingVote)}
      aria-busy={Boolean(pendingVote)}
      aria-disabled={Boolean(disabledReason || pendingVote)}
      aria-pressed={current.hasUpvoted}
      aria-label={`${disabledReason ?? 'Upvote showcase'} ${current.likeCount} interested`}
      title={disabledReason ?? action}
      className={cn(
        'min-h-9 min-w-9 text-body-sm focus-visible:ring-inset focus-visible:ring-offset-0 disabled:pointer-events-auto dark:text-slate-300 dark:hover:bg-slate-800',
        current.hasUpvoted && 'bg-primary-50 text-primary-700 hover:bg-primary-100 hover:text-primary-800 dark:bg-primary-900/30 dark:text-primary-300 dark:hover:bg-primary-800',
        disabledReason && 'cursor-not-allowed hover:bg-transparent hover:text-slate-600 dark:hover:bg-transparent dark:hover:text-slate-300',
        className,
      )}
      onClick={() => {
        if (!user || disabledReason || queryClient.isMutating({ mutationKey, exact: true }) > 0) return;
        vote.mutate(optimisticShowcaseUpvote(current));
      }}
    >
      {tile ? (
        <>
          <span className="text-heading text-secondary">{current.likeCount.toLocaleString()}</span>
          <span className="inline-flex items-center gap-1 text-caption font-medium text-primary-700">
            {!pendingVote && <Flame className={cn('h-3.5 w-3.5 text-primary', current.hasUpvoted && 'fill-current')} aria-hidden="true" />}
            Interested
          </span>
        </>
      ) : (
        <>
          {!pendingVote && <Flame className={cn('h-4 w-4 text-primary', current.hasUpvoted && 'fill-current')} aria-hidden="true" />}
          {current.likeCount.toLocaleString()}
          {showLabel && <span>interested</span>}
        </>
      )}
    </Button>
  );
}
