// "Needs a look today" for lecturers and mentors: new versions, missed deadlines and deadlines coming up, across their classes.
// Renders nothing while AI summaries are off on the server, so production shows no trace of it.
import { Link } from 'react-router-dom';
import { CheckCircle2, ChevronRight, Inbox, Loader2, RefreshCw } from 'lucide-react';
import Badge from '../ui/Badge';
import Button from '../ui/Button';
import Card from '../ui/Card';
import { useReviewInbox } from '../../hooks/useReviewInbox';
import { inboxLine, inboxReasonView, teamCheckpointPath } from '../../utils/reviewDesk';
import { useTranslation } from 'react-i18next';

const VISIBLE_ITEMS = 8;

export default function ReviewInboxCard() {
  const { t } = useTranslation('workspace');
  const { enabled, items, loading, failed, reload } = useReviewInbox();
  if (!enabled) return null;
  const shown = items.slice(0, VISIBLE_ITEMS);

  return (
    <Card hover={false} padding="p-5" className="space-y-3" aria-label={t('reviewInboxCard.needsALookToday')}>
      <div className="flex items-center gap-2">
        <Inbox className="h-4 w-4 text-primary-700" aria-hidden="true" />
        <h2 className="text-subheading text-secondary">{t('reviewInboxCard.needsALookToday')}</h2>
        {items.length > 0 && <Badge variant="brand">{items.length}</Badge>}
        <button
          type="button"
          onClick={reload}
          aria-label={t('common:actions.refresh')}
          className="ml-auto inline-flex h-9 w-9 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
        >
          <RefreshCw className={loading ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} aria-hidden="true" />
        </button>
      </div>

      {failed && items.length === 0 ? (
        <div className="flex flex-wrap items-center gap-3" role="alert">
          <p className="text-body-sm text-danger-dark">{t('reviewInboxCard.couldNotLoadThisList')}</p>
          <Button variant="outline" size="sm" icon={RefreshCw} onClick={reload}>{t('teamLineageHistory.retry')}</Button>
        </div>
      ) : loading && items.length === 0 ? (
        <p className="flex items-center gap-2 text-body-sm text-slate-500" role="status">
          <Loader2 className="h-4 w-4 animate-spin text-primary" aria-hidden="true" /> {t('common:states.loading')}
        </p>
      ) : shown.length === 0 ? (
        <p className="flex items-center gap-2 text-body-sm text-slate-600">
          <CheckCircle2 className="h-4 w-4 text-success" aria-hidden="true" />
          {t('reviewInboxCard.nothingNeedsALookToday')}
        </p>
      ) : (
        <ul className="divide-y divide-slate-100">
          {shown.map((item) => {
            const reason = inboxReasonView[item.reason];
            return (
              <li key={`${item.teamId}-${item.checkpointNumber}-${item.reason}`} className="relative flex flex-wrap items-center gap-3 py-3 first:pt-0 last:pb-0">
                <Badge variant={reason.tone}>{reason.label}</Badge>
                <div className="min-w-0 flex-1">
                  <Link
                    to={teamCheckpointPath(item.teamId, item.checkpointNumber)}
                    className="block truncate text-body-sm font-semibold text-slate-900 after:absolute after:inset-0 hover:text-primary-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
                  >
                    {item.teamCode} · {item.teamName}
                  </Link>
                  <p className="text-caption text-slate-500">{t('reviewInboxCard.checkpoint', { classCode: item.classCode, checkpointNumber: item.checkpointNumber, v3: inboxLine(item) })}</p>
                </div>
                <ChevronRight className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />
              </li>
            );
          })}
        </ul>
      )}
      {items.length > VISIBLE_ITEMS && (
        <p className="text-caption text-slate-500">{t('reviewInboxCard.andMoreOpenAClassCheckpoint', { v1: items.length - VISIBLE_ITEMS })}</p>
      )}
    </Card>
  );
}
