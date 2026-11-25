import { useContext, useEffect, useState } from 'react';
import { AuthContext } from '../../context/AuthContext';
import ShowcaseModerationRules from './ShowcaseModerationRules';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Trash2 } from 'lucide-react';
import toast from 'react-hot-toast';
import Badge from '../ui/Badge';
import Button from '../ui/Button';
import Card from '../ui/Card';
import ConfirmDialog from '../ui/ConfirmDialog';
import EmptyState from '../ui/EmptyState';
import ErrorState from '../ui/ErrorState';
import LoadingSkeleton from '../ui/LoadingSkeleton';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import type { ShowcaseReportDto, ShowcaseReportStatus } from '../../types/showcase';
import { useTranslation } from 'react-i18next';
import i18n from '../../i18n';
import { formatDateTime as fmtDateTime } from '../../i18n/format';

const STATUSES: ShowcaseReportStatus[] = ['Open', 'Dismissed', 'ActionTaken'];
const STATUS_COPY: Record<ShowcaseReportStatus, string> = {
  get Open() { return i18n.t('showcase:showcaseReports.status.Open'); },
  get Dismissed() { return i18n.t('showcase:showcaseReports.status.Dismissed'); },
  get ActionTaken() { return i18n.t('showcase:showcaseReports.status.ActionTaken'); },
};

export default function ShowcaseReports() {
  const { t } = useTranslation('showcase');
  const { user } = useContext(AuthContext);
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<ShowcaseReportStatus>('Open');
  const [page, setPage] = useState(1);
  const [target, setTarget] = useState<{ report: ShowcaseReportDto; action: 'Dismissed' | 'ActionTaken' } | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ShowcaseReportDto | null>(null);
  const [deleteAllOpen, setDeleteAllOpen] = useState(false);
  const [note, setNote] = useState('');
  const reports = useQuery({ queryKey: ['showcase', 'reports', status, page], queryFn: ({ signal }) => showcaseApi.getReports({ status, page, pageSize: 50 }, signal), staleTime: 0, refetchInterval: 30_000 });
  const lastPage = reports.data?.totalPages;
  useEffect(() => {
    if (lastPage !== undefined && page > Math.max(1, lastPage)) setPage(Math.max(1, lastPage));
  }, [lastPage, page]);
  const resolve = useMutation({
    mutationFn: ({ report, action }: NonNullable<typeof target>) => showcaseApi.resolveReport(report.id, { status: action, resolutionNote: note.trim() || null }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['showcase', 'reports'] });
      await queryClient.invalidateQueries({ queryKey: ['showcase', 'moderation'] });
      toast.success(target?.action === 'Dismissed' ? t('showcaseReports.reportDismissed') : t('showcaseReports.actionCompleted'));
      setTarget(null); setNote(''); setPage(1);
    },
    onError: (error) => toast.error(parseApiError(error, t('showcaseReports.couldNotResolveThisReportTry')).message),
  });
  const remove = useMutation({
    mutationFn: (report: ShowcaseReportDto) => showcaseApi.deleteReportHistory(report.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['showcase', 'reports'] });
      toast.success(t('showcaseReports.reportHistoryDeleted'));
      setDeleteTarget(null);
      setPage(1);
    },
    onError: (error) => toast.error(parseApiError(error, t('showcaseReports.couldNotDeleteReportHistoryTry')).message),
  });
  const removeAll = useMutation({
    mutationFn: (nextStatus: Extract<ShowcaseReportStatus, 'Dismissed' | 'ActionTaken'>) => showcaseApi.deleteAllReportHistory(nextStatus),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['showcase', 'reports'] });
      toast.success(t('showcaseReports.reportHistoryDeleted'));
      setDeleteAllOpen(false);
      setPage(1);
    },
    onError: (error) => toast.error(parseApiError(error, t('showcaseReports.couldNotDeleteReportHistoryTry')).message),
  });
  const isComment = target?.report.commentId !== null;
  const isPendingShowcase = target?.report.showcaseStatus === 'PendingReview' && !target.report.commentId;
  const isHistory = status !== 'Open';

  return (
    <section aria-label={t('showcaseReports.contentReports')}>
      {user?.role === 'ADMIN' && <ShowcaseModerationRules />}
      <Card hover={false} className="mb-5 overflow-hidden">
        <div className="flex flex-col gap-4 border-b border-slate-200 px-5 py-5 sm:px-6 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h2 className="text-subheading text-secondary">{t('showcaseReports.contentReports')}</h2>
            <p className="mt-1 text-body-sm text-slate-600">{t('showcaseReports.reviewReportedShowcasePagesAndComments')}</p>
          </div>
          <div className="flex w-full flex-col gap-2 sm:flex-row lg:w-auto">
            <div className="flex rounded-xl bg-slate-100 p-1" role="tablist" aria-label={t('showcaseReports.reportStatus')}>
              {STATUSES.map((item) => <button key={item} type="button" role="tab" aria-selected={status === item} onClick={() => { setStatus(item); setPage(1); }}
                className={`min-w-0 flex-1 rounded-lg px-3 py-2 text-left text-body-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 lg:min-w-28 ${status === item ? 'bg-white font-semibold text-primary-700 shadow-sm' : 'font-medium text-slate-600 hover:text-secondary'}`}>
                <span className="block truncate">{STATUS_COPY[item]}</span>
              </button>)}
            </div>
          </div>
        </div>
        <div className="p-5 sm:p-6">
          {reports.isPending && <div role="status" aria-label={t('showcaseReports.loadingReports')} className="space-y-3"><LoadingSkeleton variant="card" /><LoadingSkeleton variant="card" /></div>}
          {reports.isError && <ErrorState title={t('showcaseReports.couldNotLoadReports')} message={parseApiError(reports.error, t('showcaseReports.failedToLoadReports')).message} onRetry={() => { void reports.refetch(); }} />}
          {reports.isSuccess && reports.data.items.length === 0 && <EmptyState title={t('showcaseReports.noReports')} description={t('showcaseReports.thereAreNoReports', { v1: STATUS_COPY[status].toLowerCase() })} />}
          {reports.isSuccess && reports.data.items.length > 0 && <>
            <ul className="space-y-4">
        {reports.data.items.map((report) => (
          <li key={report.id}><Card hover={false}>
            <div className="flex flex-col gap-3 sm:flex-row sm:justify-between"><div>
              <div className="flex flex-wrap items-center gap-2"><Badge variant={report.commentId ? 'warning' : 'danger'} size="sm">{report.commentId ? t('showcaseReports.comment') : t('showcaseReports.showcase')}</Badge><Badge variant="brand" size="sm">{report.reason}</Badge><Badge variant="neutral" size="sm">{t('autoModeration.counts', { users: report.userReportCount ?? report.reportCount, system: report.systemReportCount ?? 0 })}</Badge></div>
              <p className="mt-2 text-caption text-slate-600">{t('autoModeration.source')}: {(report.sources ?? ['User']).map(source => t(source === 'System' ? 'autoModeration.system' : 'autoModeration.user')).join(' · ')}</p>
              {report.sources?.includes('System') && <p className="mt-2 text-body-sm font-semibold text-secondary">{t('autoModeration.reviewOnly')}</p>}
              <p className="mt-2 whitespace-pre-wrap break-words text-body-sm text-slate-700">{report.note || t('showcaseReports.noAdditionalNote')}</p>
              {report.systemNote && report.systemNote !== report.note && <p className="mt-2 whitespace-pre-wrap break-words text-body-sm text-slate-600">{report.systemNote}</p>}
              <p className="mt-2 text-caption text-slate-500">{t('showcaseReports.reported', { v1: fmtDateTime(report.createdAt) })}</p>
              <Link to={`/showcase/${report.showcaseProjectId}${report.commentId ? `#comment-${report.commentId}` : ''}`} className="mt-2 inline-block text-body-sm font-semibold text-primary-700 hover:underline">{t(report.commentId ? 'showcaseReports.viewReportedComment' : 'showcaseReports.viewReportedShowcase')}</Link>
            </div>
            <div className="flex shrink-0 flex-wrap gap-2">
              {report.status === 'Open' && <><Button size="sm" variant="outline" onClick={() => setTarget({ report, action: 'Dismissed' })}>{t('common:actions.dismiss')}</Button><Button size="sm" variant="danger-outline" onClick={() => setTarget({ report, action: 'ActionTaken' })}>{report.commentId ? t('showcaseReports.hideComment') : report.showcaseStatus === 'PendingReview' ? t('autoModeration.returnDraft') : t('showcaseReports.hideShowcase')}</Button></>}
              {report.status !== 'Open' && <Button size="sm" variant="ghost" icon={Trash2} className="h-8 w-8 px-0 text-slate-500 hover:bg-danger-50 hover:text-danger-dark" aria-label={t('showcaseReports.deleteReportHistory')} title={t('showcaseReports.deleteReportHistory')} onClick={() => setDeleteTarget(report)} />}
            </div>
            </div>
          </Card></li>
        ))}
            </ul>
            {reports.data.totalPages > 1 && <nav className="mt-4 flex items-center justify-between gap-3" aria-label={t('autoModeration.pagination')}>
              <Button size="sm" variant="outline" disabled={page <= 1 || reports.isFetching} onClick={() => setPage(page - 1)}>{t('autoModeration.previous')}</Button>
              <span className="text-body-sm text-slate-600">{t('autoModeration.page', { page, total: reports.data.totalPages })}</span>
              <Button size="sm" variant="outline" disabled={page >= reports.data.totalPages || reports.isFetching} onClick={() => setPage(page + 1)}>{t('autoModeration.next')}</Button>
            </nav>}
            {isHistory && <div className="mt-5 flex flex-col gap-3 border-t border-slate-200 pt-4 sm:flex-row sm:items-center sm:justify-between">
              <p className="text-body-sm text-slate-600">{t('showcaseReports.historySummary', { count: reports.data.totalItems, status: STATUS_COPY[status].toLowerCase() })}</p>
              <Button size="sm" variant="danger-outline" icon={Trash2} className="self-start sm:self-auto" onClick={() => setDeleteAllOpen(true)}>{t('showcaseReports.deleteAllReports', { v1: STATUS_COPY[status].toLowerCase() })}</Button>
            </div>}
          </>}
        </div>
      </Card>
      <ConfirmDialog isOpen={target !== null} onClose={() => { if (!resolve.isPending) { setTarget(null); setNote(''); } }} onConfirm={() => { if (target) resolve.mutate(target); }}
        title={target?.action === 'Dismissed' ? t('showcaseReports.dismissThisReport') : isComment ? t('showcaseReports.hideThisComment') : isPendingShowcase ? t('autoModeration.returnDraft') : t('showcaseReports.hideThisShowcase')}
        description={target?.action === 'Dismissed' ? t('showcaseReports.thisClosesTheReportWithoutChanging') : isComment ? t('showcaseReports.theReportedCommentWillNoLonger') : isPendingShowcase ? t('autoModeration.returnDraftHelp') : t('showcaseReports.theShowcaseWillNoLongerBe')}
        confirmText={target?.action === 'Dismissed' ? t('showcaseReports.dismissReport') : isComment ? t('showcaseReports.hideComment') : isPendingShowcase ? t('autoModeration.returnDraft') : t('showcaseReports.hideShowcase')} confirmVariant={target?.action === 'Dismissed' ? 'primary' : 'danger'}
        reason={note} onReasonChange={setNote} reasonLabel={t('showcaseReports.resolutionNoteOptional')} isSubmitting={resolve.isPending} />
      <ConfirmDialog isOpen={deleteTarget !== null} onClose={() => { if (!remove.isPending) setDeleteTarget(null); }} onConfirm={() => { if (deleteTarget) remove.mutate(deleteTarget); }}
        title={t('showcaseReports.deleteReportHistory2')} description={t('showcaseReports.thisPermanentlyRemovesAllClosedReports')}
        confirmText={t('showcaseReports.deleteHistory')} confirmVariant="danger" isSubmitting={remove.isPending} />
      <ConfirmDialog isOpen={deleteAllOpen} onClose={() => { if (!removeAll.isPending) setDeleteAllOpen(false); }} onConfirm={() => { if (isHistory) removeAll.mutate(status); }}
        title={t('showcaseReports.deleteAllReports2', { v1: STATUS_COPY[status].toLowerCase() })} description={t('showcaseReports.thisPermanentlyRemovesTheHistoryCurrently')}
        confirmText={t('showcaseReports.deleteAll')} confirmVariant="danger" isSubmitting={removeAll.isPending} />
    </section>
  );
}
