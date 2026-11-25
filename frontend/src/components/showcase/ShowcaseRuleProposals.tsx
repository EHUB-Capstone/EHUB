import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import toast from 'react-hot-toast';
import Button from '../ui/Button';
import Card from '../ui/Card';
import ConfirmDialog from '../ui/ConfirmDialog';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import { formatDateTime as fmtDateTime } from '../../i18n/format';
import type { ShowcaseRuleProposalDto, ShowcaseRuleProposalStatus } from '../../types/showcase';

export default function ShowcaseRuleProposals() {
  const { t } = useTranslation('showcase');
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<ShowcaseRuleProposalStatus>('Pending');
  const [reviewNote, setReviewNote] = useState('');
  const [target, setTarget] = useState<{ proposal: ShowcaseRuleProposalDto; status: 'Approved' | 'Rejected' } | null>(null);
  const proposals = useQuery({ queryKey: ['showcase', 'rule-proposals', status, page], queryFn: ({ signal }) => showcaseApi.getRuleProposals({ status, page, pageSize: 20 }, signal), refetchInterval: 30_000 });
  const totalPages = proposals.data?.totalPages;
  useEffect(() => { if (totalPages !== undefined && page > Math.max(1, totalPages)) setPage(Math.max(1, totalPages)); }, [totalPages, page]);
  const review = useMutation({
    mutationFn: ({ proposal, status, note }: NonNullable<typeof target> & { note: string }) => showcaseApi.reviewRuleProposal(proposal.id, { status, rowVersion: proposal.rowVersion, reviewNote: note.trim() || null }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['showcase', 'rule-proposals'] });
      await queryClient.invalidateQueries({ queryKey: ['showcase', 'moderation-rules'] });
      toast.success(t('ruleProposals.reviewed')); setTarget(null); setReviewNote('');
    },
    onError: error => toast.error(parseApiError(error, t('ruleProposals.reviewFailed')).message),
  });
  return <Card hover={false} className="mb-5">
    <h2 className="text-subheading text-secondary">{t('ruleProposals.queueTitle')}</h2>
    <p className="mt-2 text-body-sm text-slate-600">{t('ruleProposals.reviewHelp')}</p>
    <label className="mt-4 block text-body-sm font-semibold text-secondary">{t('autoModeration.status')}
      <select value={status} onChange={event => { setStatus(event.target.value as ShowcaseRuleProposalStatus); setPage(1); }} className="mt-2 rounded-lg border border-slate-300 bg-white p-2 text-body font-normal text-secondary">
        {(['Pending', 'Approved', 'Rejected'] as const).map(item => <option key={item} value={item}>{t(`reportAudit.proposalStatuses.${item}`)}</option>)}
      </select>
    </label>
    {proposals.isPending && <p role="status" className="mt-4">{t('autoModeration.loading')}</p>}
    {proposals.isError && <div role="alert" className="mt-4"><p>{t('ruleProposals.loadFailed')}</p><Button size="sm" variant="outline" onClick={() => { void proposals.refetch(); }}>{t('showcaseComments.retry')}</Button></div>}
    {proposals.isSuccess && <>
      {proposals.data.items.length === 0 && <p className="mt-4 text-body-sm text-slate-600">{t('reportAudit.emptyProposals')}</p>}
      <ul className="mt-4 divide-y divide-slate-200">{proposals.data.items.map(proposal => <li key={proposal.id} className="space-y-3 py-4">
        <p className="break-words font-semibold text-secondary">{proposal.phrase} · {t(`autoModeration.categories.${proposal.category}`)}</p>
        <p className="text-body-sm font-medium text-secondary">{t('ruleProposals.comment')}</p>
        <blockquote className="whitespace-pre-wrap break-words border-l-4 border-slate-300 pl-3 text-body-sm text-slate-700">{proposal.commentContent ?? t('ruleProposals.sourceUnavailable')}</blockquote>
        {proposal.reviewedAtUtc && <p className="text-caption text-slate-600">{t('reportAudit.reviewedAt', { date: fmtDateTime(proposal.reviewedAtUtc) })}</p>}
        {proposal.reviewNote && <p className="whitespace-pre-wrap break-words text-body-sm text-slate-700"><strong>{t('reportAudit.reviewNote')}: </strong>{proposal.reviewNote}</p>}
        {proposal.status === 'Pending' && <div className="flex flex-wrap gap-2">
          <Button size="sm" variant="outline" disabled={!proposal.commentContent || review.isPending} onClick={() => { setReviewNote(''); setTarget({ proposal, status: 'Approved' }); }}>{t('ruleProposals.approve')}</Button>
          <Button size="sm" variant="ghost" disabled={review.isPending} onClick={() => { setReviewNote(''); setTarget({ proposal, status: 'Rejected' }); }}>{t('ruleProposals.reject')}</Button>
        </div>}
      </li>)}</ul>
      {proposals.data.totalPages > 1 && <nav aria-label={t('ruleProposals.pages')} className="mt-4 flex items-center justify-between gap-3">
        <Button size="sm" variant="outline" disabled={page <= 1 || proposals.isFetching} onClick={() => setPage(page - 1)}>{t('autoModeration.previous')}</Button>
        <span className="text-body-sm text-slate-600">{t('autoModeration.page', { page, total: proposals.data.totalPages })}</span>
        <Button size="sm" variant="outline" disabled={page >= proposals.data.totalPages || proposals.isFetching} onClick={() => setPage(page + 1)}>{t('autoModeration.next')}</Button>
      </nav>}
    </>}
    <ConfirmDialog isOpen={!!target} onClose={() => { if (!review.isPending) { setTarget(null); setReviewNote(''); } }}
      title={t(target?.status === 'Approved' ? 'ruleProposals.approve' : 'ruleProposals.reject')}
      description={t(target?.status === 'Approved' ? 'ruleProposals.approveHelp' : 'ruleProposals.rejectHelp')}
      details={<div className="w-full space-y-3 text-left"><p className="break-words font-semibold text-secondary">{target?.proposal.phrase}</p>
        <label className="block text-body-sm text-secondary">{t('reportAudit.reviewNoteOptional')}
          <textarea rows={4} maxLength={1000} disabled={review.isPending} value={reviewNote} onChange={event => setReviewNote(event.target.value)} className="mt-2 w-full rounded-lg border border-slate-300 bg-white p-3 text-body" />
        </label><p className="text-caption text-slate-600">{t('reportAudit.noteLimit', { count: reviewNote.length })}</p>
      </div>}
      confirmText={t('ruleProposals.confirm')} confirmVariant={target?.status === 'Approved' ? 'primary' : 'danger'} isSubmitting={review.isPending}
      onConfirm={() => { if (target && !review.isPending) review.mutate({ ...target, note: reviewNote }); }} />
  </Card>;
}
